using System;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using SerializedPropertyExtensions = RoyTheunissen.AssetPalette.Extensions.SerializedPropertyExtensions;

namespace RoyTheunissen.AssetPalette.Windows
{
    public partial class AssetPaletteWindow
    {
        private const string InitialFolderName = "Default";
        private const string NewFolderName = "New Folder";
        private const int MaxUniqueFolderNameAttempts = 100;
        
        private const float DividerBrightness = 0.13f;
        private static readonly Color DividerColor = new Color(DividerBrightness, DividerBrightness, DividerBrightness);

        private bool IsDraggingFolder => foldersTreeView != null && foldersTreeView.IsDragging;

        [NonSerialized] private bool isResizingFolderPanel;
        
        [NonSerialized] private TreeViewState foldersTreeViewState;
        [NonSerialized] private AssetPaletteFolderTreeView foldersTreeView;
        
        private Vector2 folderPanelScrollPosition;
        
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

        private int SelectedFolderIndexRaw
        {
            get => EditorPrefs.GetInt(SelectedFolderIndexEditorPref);
            set => EditorPrefs.SetInt(SelectedFolderIndexEditorPref, value);
        }
        
        private int SelectedFolderIndex
        {
            get
            {
                int index = SelectedFolderIndexRaw;
                index = Mathf.Clamp(index, 0, CurrentCollection.Folders.Count - 1);
                return index;
            }
            set
            {
                int clampedValue = Mathf.Clamp(value, 0, CurrentCollection.Folders.Count - 1);

                if (SelectedFolderIndexRaw == clampedValue)
                    return;

                SelectedFolderIndexRaw = clampedValue;

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

        private PaletteFolder SelectedFolder
        {
            get
            {
                EnsureFolderExists();
                return CurrentCollection.Folders[SelectedFolderIndex];
            }
            set => SelectedFolderIndex = CurrentCollection.Folders.IndexOf(value);
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
                    cachedFoldersSerializedProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                    UpdateFoldersTreeView();
                }

                return cachedFoldersSerializedProperty;
            }
        }

        [NonSerialized] private bool didCacheSelectedFolderSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderSerializedProperty;
        private SerializedProperty SelectedFolderSerializedProperty
        {
            get
            {
                if (!didCacheSelectedFolderSerializedProperty)
                {
                    EnsureFolderExists();
                    didCacheSelectedFolderSerializedProperty = true;
                    cachedSelectedFolderSerializedProperty =
                        FoldersSerializedProperty.GetArrayElementAtIndex(SelectedFolderIndex);
                }

                return cachedSelectedFolderSerializedProperty;
            }
        }

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
            }
        }

        private void UpdateFoldersTreeView()
        {
            ClearFoldersTreeView();
            InitializeFoldersTreeView();
        }

        private void ClearFoldersTreeView()
        {
            foldersTreeViewState = null;
            if (foldersTreeView != null)
            {
                foldersTreeView.SelectedFolderEvent -= HandleTreeViewSelectedFolderEvent;
                foldersTreeView.RenamedFolderEvent -= HandleTreeViewRenamedFolderEvent;
                foldersTreeView.MovedFolderEvent -= HandleTreeViewMovedFolderEvent;
                foldersTreeView.DeleteFolderRequestedEvent -= HandleTreeViewDeleteFolderRequestedEvent;
                foldersTreeView = null;
            }
        }

        private void HandleTreeViewSelectedFolderEvent(AssetPaletteFolderTreeView treeView, PaletteFolder folder)
        {
            SelectedFolder = folder;
        }

        private void EnsureFolderExists()
        {
            if (CurrentCollection.Folders.Count > 0)
                return;
            
            // Make sure there is at least one folder.
            CurrentCollectionSerializedObject.Update();
            CreateNewFolder<PaletteFolder>(InitialFolderName);
            CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
        
        private void ClearCachedSelectedFolderSerializedProperties()
        {
            didCacheSelectedFolderSerializedProperty = false;
            didCacheSelectedFolderEntriesSerializedProperty = false;
        }

        private void ClearCachedFoldersSerializedProperties()
        {
            ClearCachedSelectedFolderSerializedProperties();
            
            didCacheFoldersSerializedProperty = false;
            ClearFoldersTreeView();
        }

        private string GetUniqueFolderName(string desiredName, int previousAttempts = 0)
        {
            if (previousAttempts > MaxUniqueFolderNameAttempts)
            {
                throw new Exception(
                    $"Tried to find a unique version of folder name '{desiredName}' but failed " +
                    $"after {previousAttempts} attempts.");
            }

            bool alreadyTaken = false;

            foreach (PaletteFolder folder in CurrentCollection.Folders)
            {
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

            return GetUniqueFolderName(desiredName, previousAttempts + 1);
        }

        private PaletteFolder CreateNewFolderOfType(Type type, string name)
        {
            PaletteFolder newFolder = (PaletteFolder)Activator.CreateInstance(type);
            newFolder.Initialize(name);

            // Add it to the current collection's list of folders.
            CurrentCollectionSerializedObject.Update();

            // Add it to the list.
            SerializedProperty newElement = SerializedPropertyExtensions.AddArrayElement(FoldersSerializedProperty);
            newElement.managedReferenceValue = newFolder;

            ApplyModifiedProperties();

            SelectedFolder = newFolder;

            UpdateFoldersTreeView();

            return newFolder;
        }

        private FolderType CreateNewFolder<FolderType>(string name) where FolderType : PaletteFolder
        {
            return (FolderType)CreateNewFolderOfType(typeof(FolderType), name);
        }
        
        private void DrawFolderPanel()
        {
            folderPanelScrollPosition = EditorGUILayout.BeginScrollView(
                folderPanelScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Width(FolderPanelWidth));

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
            
            EditorGUILayout.EndScrollView();

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
        
        private int IndexOfFolder(PaletteFolder folder)
        {
            for (int i = 0; i < FoldersSerializedProperty.arraySize; i++)
            {
                PaletteFolder folderAtIndex =
                    FoldersSerializedProperty.GetArrayElementAtIndex(i).GetValue<PaletteFolder>();
                if (folderAtIndex == folder)
                    return i;
            }
            return -1;
        }
        
        private SerializedProperty GetSerializedPropertyForFolder(PaletteFolder folder)
        {
            int index = IndexOfFolder(folder);

            if (index == -1)
                return null;
            
            return FoldersSerializedProperty.GetArrayElementAtIndex(index);
        }
        
        private void HandleTreeViewRenamedFolderEvent(
            AssetPaletteFolderTreeView treeView, PaletteFolder folder, string oldName, string newName)
        {
            PerformFolderRename(folder, newName);
        }
        
        private void CancelFolderRename()
        {
            foldersTreeView?.EndRename();
        }

        private void PerformFolderRename(PaletteFolder folder, string newName)
        {
            CurrentCollectionSerializedObject.Update();
            SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
            int index = CurrentCollection.Folders.IndexOf(folder);
            SerializedProperty folderBeingRenamedProperty = foldersProperty.GetArrayElementAtIndex(index);
            SerializedProperty nameProperty = folderBeingRenamedProperty.FindPropertyRelative("name");
            nameProperty.stringValue = newName;
            ApplyModifiedProperties();
            Repaint();
        }

        private int GetFolderIndex(PaletteFolder folder) => CurrentCollection.Folders.IndexOf(folder);

        private void HandleTreeViewMovedFolderEvent(
            AssetPaletteFolderTreeView treeView, PaletteFolder folder, int toIndex)
        {
            int folderToDragIndex = GetFolderIndex(folder);
            
            // If you want to drag a folder downwards, keep in mind that the indices will shift as a result from the
            // dragged folder not being where it used to be any more.
            if (toIndex > folderToDragIndex)
                toIndex--;

            // Remove the folder from the list and add it back at the specified position.
            CurrentCollectionSerializedObject.Update();
            FoldersSerializedProperty.DeleteArrayElementAtIndex(folderToDragIndex);
            FoldersSerializedProperty.InsertArrayElementAtIndex(toIndex);
            SerializedProperty movedFolderProperty = FoldersSerializedProperty
                .GetArrayElementAtIndex(toIndex);
            movedFolderProperty.managedReferenceValue = folder;
            ApplyModifiedProperties();

            SelectedFolderIndex = toIndex;

            UpdateAndRepaint();
        }
        
        private void HandleTreeViewDeleteFolderRequestedEvent(AssetPaletteFolderTreeView treeView, PaletteFolder folder)
        {
            RemoveSelectedFolder();
        }
        
        private void RemoveFolder(PaletteFolder folder)
        {
            if (folder == null || !HasCollection || CurrentCollection.Folders.Count <= 1)
                return;

            int folderIndex = GetFolderIndex(folder);
            CurrentCollectionSerializedObject.Update();
            FoldersSerializedProperty.DeleteArrayElementAtIndex(folderIndex);
            ApplyModifiedProperties();

            // Select the last folder.
            SelectedFolderIndex = CurrentCollection.Folders.Count - 1;
            
            UpdateFoldersTreeView();

            Repaint();
        }

        private void RemoveSelectedFolder()
        {
            RemoveFolder(SelectedFolder);
        }
    }
}
