using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette.Windows
{
    public partial class AssetPaletteWindow
    {
        private const string InitialFolderName = "Default";
        private const string NewFolderName = "New Folder";
        private const int MaxUniqueFolderNameAttempts = 100;
        
        private const string RootFoldersPropertyName = "folders";
        internal const string ChildFoldersPropertyName = "children";
        internal const string IdPropertyName = "id";
        
        private const float DividerBrightness = 0.13f;
        private static readonly Color DividerColor = new Color(DividerBrightness, DividerBrightness, DividerBrightness);

        private bool IsDraggingFolder => foldersTreeView != null && foldersTreeView.IsDragging;

        [NonSerialized] private bool isResizingFolderPanel;
        
        [SerializeField] private TreeViewState foldersTreeViewState;
        [NonSerialized] private AssetPaletteFolderTreeView foldersTreeView;

        private const int DividerRectThickness = 1;
        private Rect DividerRect => new Rect(
            FolderPanelWidth - DividerRectThickness, HeaderHeight, DividerRectThickness, position.height);

        private Rect DividerResizeRect
        {
            get
            {
                // NOTE: We make the resize rect very big while resizing otherwise quick cursor movements cause the
                // cursor to flicker back to the normal one. I'd rather change the cursor not based on a screen rect
                // but on the isResizingFolderPanel state, but there's no functionality for that apparently.
                int expansion = isResizingFolderPanel ? 100000 : 1;
                Rect rect = DividerRect;
                rect.xMin -= expansion;
                rect.xMax += expansion;
                return rect;
            }
        }

        private float FolderPanelWidth
        {
            get
            {
                if (!EditorPrefs.HasKey(FolderPanelWidthEditorPref))
                    FolderPanelWidth = FolderPanelWidthMin;
                return Mathf.Max(EditorPrefs.GetFloat(FolderPanelWidthEditorPref), FolderPanelWidthMin);
            }
            set => EditorPrefs.SetFloat(FolderPanelWidthEditorPref, value);
        }

        [NonSerialized] private bool didCacheFoldersSerializedProperty;
        [NonSerialized] private SerializedProperty cachedFoldersSerializedProperty;
        private SerializedProperty FoldersSerializedProperty
        {
            get
            {
                if (!didCacheFoldersSerializedProperty)
                {
                    didCacheFoldersSerializedProperty = true;
                    cachedFoldersSerializedProperty = CurrentCollectionSerializedObject
                        .FindProperty(RootFoldersPropertyName);
                    UpdateFoldersTreeView(true);
                }

                return cachedFoldersSerializedProperty;
            }
        }
        
        private string SelectedFolderReferenceIdPath
        {
            get => EditorPrefs.GetString(SelectedFolderReferenceIdPathEditorPref);
            set => EditorPrefs.SetString(SelectedFolderReferenceIdPathEditorPref, value);
        }
        
        private int SelectedFolderId
        {
            get => EditorPrefs.GetInt(SelectedFolderIdEditorPref);
            set => EditorPrefs.SetInt(SelectedFolderIdEditorPref, value);
        }
        
        private int FolderCount => FoldersSerializedProperty.arraySize;

        [NonSerialized] private bool didCacheSelectedFolderSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderSerializedPropertyParent;
        [NonSerialized] private string cachedSelectedFolderSerializedPropertyPath;
        private SerializedProperty SelectedFolderSerializedProperty
        {
            get
            {
                // Sanity check: if the folder selection is no longer valid, reset the selected folder.
                // This is something that can happen with undo...
                if (didCacheSelectedFolderSerializedProperty && (cachedSelectedFolderSerializedProperty == null
                                    || !cachedSelectedFolderSerializedProperty.ExistsInParentArray(
                                        cachedSelectedFolderSerializedPropertyParent,
                                        cachedSelectedFolderSerializedPropertyPath)
                                    || cachedSelectedFolderSerializedProperty.GetValue<PaletteFolder>() == null))
                {
                    ClearCachedSelectedFolderSerializedProperties();
                    SelectedFolderReferenceIdPath = null;
                }
                
                if (!didCacheSelectedFolderSerializedProperty)
                {
                    EnsureFolderExists();
                    didCacheSelectedFolderSerializedProperty = true;
                    
                    cachedSelectedFolderSerializedProperty = CurrentCollectionSerializedObject
                        .FindPropertyFromId(SelectedFolderId, RootFoldersPropertyName);
                    
                    // Did not exist. Just select the first folder.
                    if (cachedSelectedFolderSerializedProperty == null)
                        cachedSelectedFolderSerializedProperty = FoldersSerializedProperty.GetArrayElementAtIndex(0);

                    cachedSelectedFolderSerializedPropertyParent = cachedSelectedFolderSerializedProperty.GetParent();
                    cachedSelectedFolderSerializedPropertyPath = cachedSelectedFolderSerializedProperty.propertyPath;
                }

                return cachedSelectedFolderSerializedProperty;
            }
            set
            {
                string referenceIdPath = value.GetReferenceIdPath(ChildFoldersPropertyName);
                if (string.Equals(SelectedFolderReferenceIdPath, referenceIdPath, StringComparison.Ordinal))
                    return;

                SelectedFolderReferenceIdPath = referenceIdPath;
                SelectedFolderId = value.FindPropertyRelative("id").intValue;
                
                ClearCachedSelectedFolderSerializedProperties();
                
                // If you change the folder that's selected, we need to clear the selection.
                ClearEntrySelection();
                
                // Now is actually also a good time to make sure it's sorted correctly, because sorting modes configured
                // while on another folder are meant to apply to newly selected folders too.
                CurrentCollectionSerializedObject.Update();
                SortEntriesInSerializedObject();
                CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        
        private PaletteFolder SelectedFolder => SelectedFolderSerializedProperty.GetValue<PaletteFolder>();

        private bool IsFolderBeingRenamed => foldersTreeView != null && foldersTreeView.IsRenaming;

        private void InitializeFoldersTreeView()
        {
            if (foldersTreeViewState == null)
                foldersTreeViewState = new TreeViewState();
            if (foldersTreeView == null)
            {
                foldersTreeView = new AssetPaletteFolderTreeView(
                    foldersTreeViewState, FoldersSerializedProperty, SelectedFolder);
                foldersTreeView.SelectedFolderEvent += HandleTreeViewSelectedFolderEvent;
                foldersTreeView.RenamedFolderEvent += HandleTreeViewRenamedFolderEvent;
                foldersTreeView.MovedFolderEvent += HandleTreeViewMovedFolderEvent;
                foldersTreeView.DeleteFolderRequestedEvent += HandleTreeViewDeleteFolderRequestedEvent;
                foldersTreeView.CreateFolderRequestedEvent += HandleTreeViewCreateFolderRequestedEvent;
                foldersTreeView.DroppedAssetsIntoFolderEvent += HandleTreeViewDroppedAssetsIntoFolderEvent;
            }

            EnsureFoldersIds();
        }

        private void EnsureFoldersIds()
        {
            int id = 1;
            for (int startIndex = 0; startIndex < FoldersSerializedProperty.arraySize; startIndex++)
            {
                SerializedProperty folderProperty = FoldersSerializedProperty.GetArrayElementAtIndex(startIndex);
                EnsureFolderIDRecursively(folderProperty, ref id);
            }
            
            ApplyModifiedProperties();
        }

        private void EnsureFolderIDRecursively(SerializedProperty folderProperty, ref int id)
        {
            if (folderProperty.FindPropertyRelative(IdPropertyName).intValue != -1)
                return;

            folderProperty.FindPropertyRelative(IdPropertyName).intValue = id;
            id++;

            SerializedProperty childrenProperty = folderProperty.FindPropertyRelative(ChildFoldersPropertyName);
            for (int i = 0; i < childrenProperty.arraySize; i++)
            {
                SerializedProperty childrenFolder = childrenProperty.GetArrayElementAtIndex(i);
                EnsureFolderIDRecursively(childrenFolder, ref id);
            }
        }

        private void UpdateFoldersTreeView(bool clearState)
        {
            ClearFoldersTreeView(clearState);
            InitializeFoldersTreeView();
        }

        private void ClearFoldersTreeView(bool clearState)
        {
            if (clearState)
                foldersTreeViewState = null;
            if (foldersTreeView != null)
            {
                foldersTreeView.SelectedFolderEvent -= HandleTreeViewSelectedFolderEvent;
                foldersTreeView.RenamedFolderEvent -= HandleTreeViewRenamedFolderEvent;
                foldersTreeView.MovedFolderEvent -= HandleTreeViewMovedFolderEvent;
                foldersTreeView.DeleteFolderRequestedEvent -= HandleTreeViewDeleteFolderRequestedEvent;
                foldersTreeView.CreateFolderRequestedEvent -= HandleTreeViewCreateFolderRequestedEvent;
                foldersTreeView.DroppedAssetsIntoFolderEvent -= HandleTreeViewDroppedAssetsIntoFolderEvent;
                foldersTreeView = null;
            }
        }

        private void HandleTreeViewSelectedFolderEvent(
            AssetPaletteFolderTreeView treeView, SerializedProperty folderProperty)
        {
            SelectedFolderSerializedProperty = folderProperty;
        }

        private void EnsureFolderExists()
        {
            if (FolderCount > 0)
                return;
            
            // Make sure there is at least one folder.
            CurrentCollectionSerializedObject.Update();
            CreateNewFolder<PaletteFolder>(null, InitialFolderName);
            CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
        
        private void ClearCachedSelectedFolderSerializedProperties()
        {
            didCacheSelectedFolderSerializedProperty = false;
            didCacheSelectedFolderEntriesSerializedProperty = false;
            
            ClearEntrySelection();
        }

        private void ClearCachedFoldersSerializedProperties()
        {
            ClearCachedSelectedFolderSerializedProperties();
            
            didCacheFoldersSerializedProperty = false;
            ClearFoldersTreeView(true);
        }

        private string GetUniqueFolderName(
            SerializedProperty parentFolderProperty, string desiredName, int previousAttempts = 0)
        {
            if (previousAttempts > MaxUniqueFolderNameAttempts)
            {
                throw new Exception(
                    $"Tried to find a unique version of folder name '{desiredName}' but failed " +
                    $"after {previousAttempts} attempts.");
            }

            bool alreadyTaken = false;

            SerializedProperty listProperty = parentFolderProperty == null
                ? FoldersSerializedProperty
                : parentFolderProperty.FindPropertyRelative(ChildFoldersPropertyName);
            for (int i = 0; i < listProperty.arraySize; i++)
            {
                PaletteFolder folder = listProperty.GetArrayElementAtIndex(i).GetValue<PaletteFolder>();
                if (folder != null && folder.Name == desiredName)
                {
                    alreadyTaken = true;
                    break;
                }
            }

            if (!alreadyTaken)
                return desiredName;

            bool hadNumberPrefix = desiredName.TryGetNumberSuffix(out int number);
            if (!hadNumberPrefix)
                desiredName = desiredName.SetNumberSuffix(1);
            else
                desiredName = desiredName.SetNumberSuffix(number + 1);

            return GetUniqueFolderName(parentFolderProperty, desiredName, previousAttempts + 1);
        }
        
        private void TryCreateNewFolderDropDown(Rect buttonRect)
        {
            if (!HasMultipleFolderTypes)
            {
                CreateNewFolder<PaletteFolder>();
                return;
            }

            GenericMenu dropdownMenu = GetCreateNewFolderDropdown();
            dropdownMenu.DropDown(buttonRect);
        }
        
        private void TryCreateNewFolderContext(SerializedProperty parentFolderProperty)
        {
            if (!HasMultipleFolderTypes)
            {
                CreateNewFolder<PaletteFolder>(parentFolderProperty);
                return;
            }

            GenericMenu dropdownMenu = GetCreateNewFolderDropdown(parentFolderProperty);
            dropdownMenu.ShowAsContext();
        }
        
        private GenericMenu GetCreateNewFolderDropdown(SerializedProperty parentFolderProperty = null)
        {
            GenericMenu dropdownMenu = new GenericMenu();

            foreach (Type type in FolderTypes)
            {
                string name = type.Name.RemoveSuffix("Folder").ToHumanReadable();
                dropdownMenu.AddItem(new GUIContent(name), false, () => CreateNewFolder(type, parentFolderProperty));
            }

            return dropdownMenu;
        }

        private PaletteFolder CreateNewFolder(
            Type type, SerializedProperty parentFolderProperty = null, string name = null)
        {
            // Add it to the current collection's list of folders.
            CurrentCollectionSerializedObject.Update();

            // If we're adding it to an existing folder, make sure that folder is now expanded so we can see what
            // we're doing. The Tree View will be updated shortly and it will then represent the current value.
            if (parentFolderProperty != null)
                parentFolderProperty.isExpanded = true;
            
            // Add it to the list.
            SerializedProperty collectionProperty = parentFolderProperty == null
                ? FoldersSerializedProperty : parentFolderProperty.FindPropertyRelative(ChildFoldersPropertyName);

            int id = collectionProperty.arraySize;
            SerializedProperty newFolderProperty = collectionProperty.AddArrayElement();
            
            bool nameWasExplicitlySpecified = !string.IsNullOrEmpty(name);
            if (string.IsNullOrEmpty(name))
                name = GetUniqueFolderName(parentFolderProperty, NewFolderName);

            PaletteFolder newFolder = (PaletteFolder)Activator.CreateInstance(type);
            newFolder.Initialize(name, id);
            
            newFolderProperty.managedReferenceValue = newFolder;

            ApplyModifiedProperties();

            SelectedFolderSerializedProperty = newFolderProperty;

            UpdateFoldersTreeView(false);
            
            if (!nameWasExplicitlySpecified)
                StartFolderRename(newFolder);

            return newFolder;
        }

        private FolderType CreateNewFolder<FolderType>(
            SerializedProperty parentFolderProperty = null, string name = null)
            where FolderType : PaletteFolder
        {
            return (FolderType)CreateNewFolder(typeof(FolderType), parentFolderProperty, name);
        }
        
        private void DrawFolderPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(FolderPanelWidth));

            // Cancel out if there's no collection available.
            if (HasCollection)
            {
                EnsureFolderExists();

                if (HasCollection)
                {
                    CurrentCollectionSerializedObject.Update();
                    DrawFolders();
                    CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            
            EditorGUILayout.EndVertical();

            DrawResizableFolderPanelDivider();
        }

        private void DrawFolders()
        {
            InitializeFoldersTreeView();
            
            Rect position = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            position.xMax -= 2;
            position.yMin += 2;
            
            foldersTreeView.OnGUI(position);
        }

        private void DrawResizableFolderPanelDivider()
        {
            EditorGUI.DrawRect(DividerRect, DividerColor);
            
            EditorGUIUtility.AddCursorRect(DividerResizeRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                isMouseOverFolderPanelResizeBorder)
            {
                isResizingFolderPanel = true;
            }

            if (isResizingFolderPanel &&
                (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag))
            {
                FolderPanelWidth = Mathf.Clamp(
                    Event.current.mousePosition.x, FolderPanelWidthMin, position.width - EntriesPanelWidthMin);
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
            {
                isResizingFolderPanel = false;
                
                // NOTE: Need to repaint now so that the resize rect becomes the normal size again.
                Repaint();
            }
        }

        private void StartFolderRename(PaletteFolder folder)
        {
            foldersTreeView.BeginRename(folder);
        }

        private string GetRenameControlId(SerializedProperty serializedProperty)
        {
            return serializedProperty.propertyPath;
        }

        private string GetRenameControlId(PaletteEntry entry)
        {
            SerializedProperty serializedProperty = GetSerializedPropertyForEntry(entry);
            return GetRenameControlId(serializedProperty);
        }

        private void HandleTreeViewRenamedFolderEvent(
            AssetPaletteFolderTreeView treeView, SerializedProperty folderProperty, string oldName, string newName)
        {
            PerformFolderRename(folderProperty, newName);
        }
        
        private void CancelFolderRename()
        {
            foldersTreeView?.EndRename();
        }

        private void PerformFolderRename(SerializedProperty folderProperty, string newName)
        {
            CurrentCollectionSerializedObject.Update();
            SerializedProperty folderBeingRenamedProperty = folderProperty;
            SerializedProperty nameProperty = folderBeingRenamedProperty.FindPropertyRelative("name");
            nameProperty.stringValue = newName;
            ApplyModifiedProperties();
            Repaint();
        }

        private SerializedProperty GetParentFolderProperty(SerializedProperty folderProperty)
        {
            SerializedProperty listProperty = folderProperty.GetParent();
            
            // Actually this was a root folder.
            if (listProperty.name != ChildFoldersPropertyName)
                return null;

            return listProperty.GetParent();
        }

        private void HandleTreeViewMovedFolderEvent(
            AssetPaletteFolderTreeView treeView, SerializedProperty folderProperty,
            SerializedProperty targetFolderProperty, int toIndex)
        {
            PaletteFolder folder = folderProperty.GetValue<PaletteFolder>();
            
            string targetFolderReferenceIdPath = targetFolderProperty.GetReferenceIdPath(ChildFoldersPropertyName);
            
            SerializedProperty fromListProperty = folderProperty.GetParent();
            int folderToDragIndex = fromListProperty.GetIndexOfArrayElement(folderProperty);

            SerializedProperty originalParentProperty = GetParentFolderProperty(folderProperty);

            bool isMovedToDifferentParent = !originalParentProperty.PathEquals(targetFolderProperty);
            
            SerializedProperty toListProperty = targetFolderProperty == null
                ? FoldersSerializedProperty : targetFolderProperty.FindPropertyRelative(ChildFoldersPropertyName);

            // Remove the folder from the original list and add it back to the target list at the specified position.
            CurrentCollectionSerializedObject.Update();
            
            fromListProperty.DeleteArrayElementAtIndex(folderToDragIndex);

            // If you want to drag a folder downwards within the same parent, keep in mind that the indices will shift
            // as a result from the dragged folder being removed and therefore not being where it used to be any more.
            if (!isMovedToDifferentParent && folderToDragIndex < toIndex)
                toIndex--;

            // Having removed the folder that we want to drag, that changed the order of all the properties. Figure out
            // the correct property for the folder that we wanted to drag to
            targetFolderProperty = CurrentCollectionSerializedObject.FindPropertyFromReferenceIdPath(
                targetFolderReferenceIdPath, RootFoldersPropertyName, ChildFoldersPropertyName);
            toListProperty = targetFolderProperty == null
                ? FoldersSerializedProperty : targetFolderProperty.FindPropertyRelative(ChildFoldersPropertyName);
            toListProperty.InsertArrayElementAtIndex(toIndex);
            
            SerializedProperty movedFolderProperty = toListProperty.GetArrayElementAtIndex(toIndex);
            movedFolderProperty.managedReferenceValue = folder;

            ApplyModifiedProperties();

            SelectedFolderSerializedProperty = movedFolderProperty;

            UpdateAndRepaint();
        }
        
        private void HandleTreeViewDeleteFolderRequestedEvent(
            AssetPaletteFolderTreeView treeView, SerializedProperty folderProperty)
        {
            RemoveFolder(folderProperty);
        }
        
        private void HandleTreeViewCreateFolderRequestedEvent(
            AssetPaletteFolderTreeView treeView, SerializedProperty parentFolderProperty)
        {
            TryCreateNewFolderContext(parentFolderProperty);
        }
        
        private void HandleTreeViewDroppedAssetsIntoFolderEvent(
            AssetPaletteFolderTreeView treeView, Object[] assets,
            SerializedProperty folderProperty, bool isDraggedFromFolder)
        {
            if (isDraggedFromFolder)
            {
                // First remove all of the selected entries from the current folder.
                List<PaletteEntry> entriesToMove = new List<PaletteEntry>(entriesSelected);
                RemoveEntries(entriesToMove);

                // Make the recipient folder the current folder.
                SelectedFolderSerializedProperty = folderProperty;

                // Now add all of the entries to the recipient folder.
                AddEntries(entriesToMove);
            }
            else
            {
                // Make the recipient folder the current folder.
                SelectedFolderSerializedProperty = folderProperty;

                // Just act as if these assets were dropped into the entries panel.
                HandleAssetDropping(assets);
            }
            
            UpdateAndRepaint();
        }
        
        private void RemoveFolder(SerializedProperty folderProperty)
        {
            if (folderProperty == null || !HasCollection || FolderCount <= 1)
                return;

            SerializedProperty listProperty = folderProperty.GetParent();
            int folderIndex = listProperty.GetIndexOfArrayElement(folderProperty);
            
            CurrentCollectionSerializedObject.Update();
            listProperty.DeleteArrayElementAtIndex(folderIndex);
            ApplyModifiedProperties();

            // If we deleted the last child of a folder, select that folder instead.
            if (listProperty.arraySize == 0)
            {
                SelectedFolderSerializedProperty = listProperty.GetParent();
            }
            else if (folderIndex >= listProperty.arraySize)
            {
                // If we deleted the last folder, select what is now the new last folder.
                SelectedFolderSerializedProperty = listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1);
            }
            else
            {
                // Otherwise select the folder that took the place of the folder we deleted.
                SelectedFolderSerializedProperty = listProperty.GetArrayElementAtIndex(folderIndex);
            }
            
            UpdateFoldersTreeView(false);

            Repaint();
        }

        private void RemoveSelectedFolder()
        {
            RemoveFolder(SelectedFolderSerializedProperty);
        }
    }
}
