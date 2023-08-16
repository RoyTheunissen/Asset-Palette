using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette.Windows
{
    public class FolderPanel
    {
        // Editor preferences
        private static string FolderPanelWidthEditorPref => AssetPaletteWindow.EditorPrefPrefix + "FolderPanelWidth";
        
        // Measurements
        public static float FolderPanelWidthMin => AssetPaletteWindowHeader.CollectionButtonWidth
                                                   + AssetPaletteWindowHeader.NewFolderButtonWidth;
        
        // Miscellaneous
        private const string InitialFolderName = "Default";
        private const string NewFolderName = "New Folder";
        private const int MaxUniqueFolderNameAttempts = 100;

        public const string RootFoldersPropertyName = "folders";
        public const string ChildFoldersPropertyName = "children";
        
        private const float DividerBrightness = 0.13f;
        public static readonly Color DividerColor = new Color(DividerBrightness, DividerBrightness, DividerBrightness);

        public bool IsDraggingFolder => foldersTreeView != null && foldersTreeView.IsDragging;

        [NonSerialized] private bool isResizingFolderPanel;
        public bool IsResizingFolderPanel => isResizingFolderPanel;

        private TreeViewState foldersTreeViewState;
        [NonSerialized] private AssetPaletteFolderTreeView foldersTreeView;

        private const int DividerRectThickness = 1;
        private Rect DividerRect => new Rect(
            FolderPanelWidth - DividerRectThickness, AssetPaletteWindowHeader.HeaderHeight,
            DividerRectThickness, window.position.height);

        public Rect DividerResizeRect
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

        public float FolderPanelWidth
        {
            get
            {
                if (!EditorPrefs.HasKey(FolderPanelWidthEditorPref))
                    FolderPanelWidth = FolderPanelWidthMin;
                return Mathf.Max(EditorPrefs.GetFloat(FolderPanelWidthEditorPref), FolderPanelWidthMin);
            }
            set => EditorPrefs.SetFloat(FolderPanelWidthEditorPref, value);
        }

        public PaletteFolder SelectedFolder => window.SelectedFolderSerializedProperty.GetValue<PaletteFolder>();

        public bool IsFolderBeingRenamed => foldersTreeView != null && foldersTreeView.IsRenaming;
        
        private int FolderCount => window.FoldersSerializedProperty.arraySize;
        
        [NonSerialized] private static Type[] cachedFolderTypes;
        [NonSerialized] private static bool didCacheFolderTypes;
        private static Type[] FolderTypes
        {
            get
            {
                if (!didCacheFolderTypes)
                {
                    didCacheFolderTypes = true;
                    cachedFolderTypes = TypeExtensions.GetAllAssignableClasses(typeof(PaletteFolder), false, true);
                }
                return cachedFolderTypes;
            }
        }

        public static bool HasMultipleFolderTypes => FolderTypes.Length > 1;
        
        private AssetPaletteWindow window;

        public FolderPanel(AssetPaletteWindow window)
        {
            this.window = window;
        }

        private void InitializeFoldersTreeView()
        {
            if (foldersTreeViewState == null)
                foldersTreeViewState = new TreeViewState();
            if (foldersTreeView == null)
            {
                foldersTreeView = new AssetPaletteFolderTreeView(
                    foldersTreeViewState, window.FoldersSerializedProperty, SelectedFolder);
                foldersTreeView.SelectedFolderEvent += HandleTreeViewSelectedFolderEvent;
                foldersTreeView.RenamedFolderEvent += HandleTreeViewRenamedFolderEvent;
                foldersTreeView.MovedFolderEvent += HandleTreeViewMovedFolderEvent;
                foldersTreeView.DeleteFolderRequestedEvent += HandleTreeViewDeleteFolderRequestedEvent;
                foldersTreeView.CreateFolderRequestedEvent += HandleTreeViewCreateFolderRequestedEvent;
                foldersTreeView.DroppedAssetsIntoFolderEvent += HandleTreeViewDroppedAssetsIntoFolderEvent;
            }
        }

        public void UpdateFoldersTreeView(bool clearState)
        {
            ClearFoldersTreeView(clearState);
            InitializeFoldersTreeView();
        }

        public void ClearFoldersTreeView(bool clearState)
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
            window.SelectedFolderSerializedProperty = folderProperty;
        }

        public void EnsureFolderExists()
        {
            if (FolderCount > 0)
                return;
            
            // Make sure there is at least one folder.
            window.CurrentCollectionSerializedObject.Update();
            CreateNewFolder<PaletteFolder>(null, InitialFolderName);
            window.CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
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
                ? window.FoldersSerializedProperty
                : parentFolderProperty.FindPropertyRelative(ChildFoldersPropertyName);
            for (int i = 0; i < listProperty.arraySize; i++)
            {
                PaletteFolder folder = listProperty.GetArrayElementAtIndex(i).GetValue<PaletteFolder>();
                if (folder.Name == desiredName)
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

        public void TryCreateNewFolderDropDown(Rect buttonRect)
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
            bool nameWasExplicitlySpecified = !string.IsNullOrEmpty(name);
            if (string.IsNullOrEmpty(name))
                name = GetUniqueFolderName(parentFolderProperty, NewFolderName);

            PaletteFolder newFolder = (PaletteFolder)Activator.CreateInstance(type);
            newFolder.Initialize(name);

            // Add it to the current collection's list of folders.
            window.CurrentCollectionSerializedObject.Update();

            // If we're adding it to an existing folder, make sure that folder is now expanded so we can see what
            // we're doing. The Tree View will be updated shortly and it will then represent the current value.
            if (parentFolderProperty != null)
                parentFolderProperty.isExpanded = true;
            
            // Add it to the list.
            SerializedProperty collectionProperty = parentFolderProperty == null
                ? window.FoldersSerializedProperty : parentFolderProperty.FindPropertyRelative(ChildFoldersPropertyName);
            SerializedProperty newFolderProperty = collectionProperty.AddArrayElement();
            newFolderProperty.managedReferenceValue = newFolder;

            window.ApplyModifiedProperties();

            window.SelectedFolderSerializedProperty = newFolderProperty;

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

        public void DrawFolderPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(FolderPanelWidth));

            // Cancel out if there's no collection available.
            if (window.HasCollection)
            {
                EnsureFolderExists();

                if (window.HasCollection)
                {
                    window.CurrentCollectionSerializedObject.Update();
                    DrawFolders();
                    window.CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
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
                window.IsMouseOverFolderPanelResizeBorder)
            {
                isResizingFolderPanel = true;
            }

            if (isResizingFolderPanel &&
                (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag))
            {
                FolderPanelWidth = Mathf.Clamp(
                    Event.current.mousePosition.x, FolderPanelWidthMin,
                    window.position.width - AssetPaletteWindow.EntriesPanelWidthMin);
                window.Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
            {
                isResizingFolderPanel = false;
                
                // NOTE: Need to repaint now so that the resize rect becomes the normal size again.
                window.Repaint();
            }
        }

        private void StartFolderRename(PaletteFolder folder)
        {
            foldersTreeView.BeginRename(folder);
        }

        private void HandleTreeViewRenamedFolderEvent(
            AssetPaletteFolderTreeView treeView, SerializedProperty folderProperty, string oldName, string newName)
        {
            PerformFolderRename(folderProperty, newName);
        }

        public void CancelFolderRename()
        {
            foldersTreeView?.EndRename();
        }

        private void PerformFolderRename(SerializedProperty folderProperty, string newName)
        {
            window.CurrentCollectionSerializedObject.Update();
            SerializedProperty folderBeingRenamedProperty = folderProperty;
            SerializedProperty nameProperty = folderBeingRenamedProperty.FindPropertyRelative("name");
            nameProperty.stringValue = newName;
            window.ApplyModifiedProperties();
            window.Repaint();
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
                ? window.FoldersSerializedProperty : targetFolderProperty.FindPropertyRelative(ChildFoldersPropertyName);

            // Remove the folder from the original list and add it back to the target list at the specified position.
            window.CurrentCollectionSerializedObject.Update();
            
            fromListProperty.DeleteArrayElementAtIndex(folderToDragIndex);

            // If you want to drag a folder downwards within the same parent, keep in mind that the indices will shift
            // as a result from the dragged folder being removed and therefore not being where it used to be any more.
            if (!isMovedToDifferentParent && folderToDragIndex < toIndex)
                toIndex--;

            // Having removed the folder that we want to drag, that changed the order of all the properties. Figure out
            // the correct property for the folder that we wanted to drag to
            targetFolderProperty = window.CurrentCollectionSerializedObject.FindPropertyFromReferenceIdPath(
                targetFolderReferenceIdPath, RootFoldersPropertyName, ChildFoldersPropertyName);
            toListProperty = targetFolderProperty == null
                ? window.FoldersSerializedProperty : targetFolderProperty.FindPropertyRelative(ChildFoldersPropertyName);
            toListProperty.InsertArrayElementAtIndex(toIndex);
            
            SerializedProperty movedFolderProperty = toListProperty.GetArrayElementAtIndex(toIndex);
            movedFolderProperty.managedReferenceValue = folder;

            window.ApplyModifiedProperties();

            window.SelectedFolderSerializedProperty = movedFolderProperty;

            window.UpdateAndRepaint();
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
                List<PaletteEntry> entriesToMove = new List<PaletteEntry>(window.EntryPanel.EntriesSelected);
                window.EntryPanel.RemoveEntries(entriesToMove);

                // Make the recipient folder the current folder.
                window.SelectedFolderSerializedProperty = folderProperty;

                // Now add all of the entries to the recipient folder.
                window.EntryPanel.AddEntries(entriesToMove);
            }
            else
            {
                // Make the recipient folder the current folder.
                window.SelectedFolderSerializedProperty = folderProperty;

                // Just act as if these assets were dropped into the entries panel.
                window.HandleAssetDropping(assets);
            }
            
            window.UpdateAndRepaint();
        }
        
        private void RemoveFolder(SerializedProperty folderProperty)
        {
            if (folderProperty == null || !window.HasCollection || FolderCount <= 1)
                return;

            SerializedProperty listProperty = folderProperty.GetParent();
            int folderIndex = listProperty.GetIndexOfArrayElement(folderProperty);
            
            window.CurrentCollectionSerializedObject.Update();
            listProperty.DeleteArrayElementAtIndex(folderIndex);
            window.ApplyModifiedProperties();

            // If we deleted the last child of a folder, select that folder instead.
            if (listProperty.arraySize == 0)
            {
                window.SelectedFolderSerializedProperty = listProperty.GetParent();
            }
            else if (folderIndex >= listProperty.arraySize)
            {
                // If we deleted the last folder, select what is now the new last folder.
                window.SelectedFolderSerializedProperty = listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1);
            }
            else
            {
                // Otherwise select the folder that took the place of the folder we deleted.
                window.SelectedFolderSerializedProperty = listProperty.GetArrayElementAtIndex(folderIndex);
            }
            
            UpdateFoldersTreeView(false);

            window.Repaint();
        }

        public void RemoveSelectedFolder()
        {
            RemoveFolder(window.SelectedFolderSerializedProperty);
        }
    }
}