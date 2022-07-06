using System;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette
{
    public partial class AssetPaletteWindow
    {
        private const string InitialFolderName = "Default";
        private const string NewFolderName = "New Folder";
        private const int MaxUniqueFolderNameAttempts = 100;
        
        private const float DividerBrightness = 0.13f;
        private static readonly Color DividerColor = new Color(DividerBrightness, DividerBrightness, DividerBrightness);
        
        [NonSerialized] private bool isDraggingFolder;
        [NonSerialized] private int currentFolderDragIndex;
        [NonSerialized] private int folderToDragIndex;
        
        [NonSerialized] private string folderRenameText;
        
        [NonSerialized] private bool isResizingFolderPanel;
        
        private Vector2 folderPanelScrollPosition;

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
        
        private int SelectedFolderIndex
        {
            get
            {
                int index = EditorPrefs.GetInt(SelectedFolderIndexEditorPref);
                index = Mathf.Clamp(index, 0, CurrentCollection.Folders.Count - 1);
                return index;
            }
            set
            {
                int clampedValue = Mathf.Clamp(value, 0, CurrentCollection.Folders.Count - 1);
                
                if (SelectedFolderIndex == clampedValue)
                    return;
                
                EditorPrefs.SetInt(SelectedFolderIndexEditorPref, clampedValue);

                ClearCachedFolderSerializedProperties();
                
                // If you change the folder that's selected, we need to clear the selection.
                ClearEntrySelection();
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
                    cachedSelectedFolderSerializedProperty = CurrentCollectionSerializedObject.FindProperty("folders")
                        .GetArrayElementAtIndex(SelectedFolderIndex);
                }

                return cachedSelectedFolderSerializedProperty;
            }
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
        
        private void ClearCachedFolderSerializedProperties()
        {
            didCacheSelectedFolderSerializedProperty = false;
            didCacheSelectedFolderEntriesSerializedProperty = false;
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
            SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");

            // Add it to the list.
            SerializedProperty newElement = foldersProperty.AddArrayElement();
            newElement.managedReferenceValue = newFolder;

            CurrentCollectionSerializedObject.ApplyModifiedProperties();

            SelectedFolder = newFolder;

            return newFolder;
        }

        private FolderType CreateNewFolder<FolderType>(string name) where FolderType : PaletteFolder
        {
            return (FolderType)CreateNewFolderOfType(typeof(FolderType), name);
        }
        
        private void DrawFolderPanel()
        {
            // It seems like mouse events are relative to scroll views.
            bool didClickAnywhereInWindow = Event.current.type == EventType.MouseDown;

            folderPanelScrollPosition = EditorGUILayout.BeginScrollView(
                folderPanelScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Width(FolderPanelWidth));

            // Cancel out if there's no collection available.
            if (HasCollection)
            {
                EnsureFolderExists();

                currentFolderDragIndex = -1;
                
                if (HasCollection)
                {
                    CurrentCollectionSerializedObject.Update();
                    using (SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders"))
                    {
                        DrawFolders(foldersProperty, didClickAnywhereInWindow);
                    }
                    CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            
            EditorGUILayout.EndScrollView();

            if (isDraggingFolder && Event.current.type == EventType.DragUpdated ||
                Event.current.type == EventType.DragPerform)
            {
                if (!isMouseInFolderPanel)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                else
                {
                    DragAndDrop.visualMode = currentFolderDragIndex < folderToDragIndex || currentFolderDragIndex > folderToDragIndex + 1
                        ? DragAndDropVisualMode.Move
                        : DragAndDropVisualMode.Rejected;
                }

                if (Event.current.type == EventType.DragPerform)
                    StopFolderDrag();
            }

            DrawResizableFolderPanelDivider();
        }

        private void DrawFolders(SerializedProperty foldersProperty, bool didClickAnywhereInWindow)
        {
            Color selectionColor = SelectionColor;
            selectionColor.a = 0.25f;

            for (int i = 0; i < foldersProperty.arraySize; i++)
            {
                SerializedProperty folderProperty = foldersProperty.GetArrayElementAtIndex(i);
                float folderHeight = EditorGUI.GetPropertyHeight(folderProperty, GUIContent.none, true);
                float folderWidth = FolderPanelWidth;
                Rect folderRect = GUILayoutUtility.GetRect(folderWidth, folderHeight);
                bool isSelected = SelectedFolderIndex == i;
                
                // Draw the actual folder itself.
                if (isSelected)
                    EditorGUI.DrawRect(folderRect, selectionColor);
                PaletteFolder folder = folderProperty.GetValue<PaletteFolder>();
                folderRect = folderRect.Indent(1);
                if (folder.IsRenaming)
                {
                    GUI.SetNextControlName(folder.RenameControlId);
                    folderRenameText = EditorGUI.TextField(folderRect, folderRenameText);
                    GUI.FocusControl(folder.RenameControlId);
                }
                else
                {
                    EditorGUI.PropertyField(folderRect, folderProperty, GUIContent.none);
                }

                bool isMouseOver = folderRect.Contains(Event.current.mousePosition);

                // Dragging and dropping folders.
                if (Event.current.type == EventType.MouseDrag && isMouseOver && !isResizingFolderPanel)
                    StartFolderDrag(folder);
                if (isDraggingFolder)
                {
                    bool didFindDragIndex = false;
                    Rect dragMarkerRect = Rect.zero;
                    if (Event.current.mousePosition.y <= folderRect.center.y &&
                        (currentFolderDragIndex == -1 || i < currentFolderDragIndex))
                    {
                        currentFolderDragIndex = i;
                        didFindDragIndex = true;
                        dragMarkerRect = folderRect.GetSubRectFromTop(2);
                        Repaint();
                    }
                    else if (currentFolderDragIndex == -1 && i == foldersProperty.arraySize - 1)
                    {
                        currentFolderDragIndex = i + 1;
                        didFindDragIndex = true;
                        dragMarkerRect = folderRect.GetSubRectFromBottom(2);
                        Repaint();
                    }

                    if (didFindDragIndex && currentFolderDragIndex != folderToDragIndex &&
                        currentFolderDragIndex != folderToDragIndex + 1 &&
                        DragAndDrop.visualMode == DragAndDropVisualMode.Move)
                    {
                        EditorGUI.DrawRect(dragMarkerRect, Color.blue);
                    }
                }

                // Allow users to select a folder by clicking with LMB.
                if (didClickAnywhereInWindow)
                {
                    if (folder.IsRenaming && !isMouseOver)
                    {
                        StopFolderRename();
                    }
                    else if (!PaletteFolder.IsFolderBeingRenamed && isMouseOver)
                    {
                        SelectedFolderIndex = i;

                        // Allow starting a rename by clicking on it twice.
                        if (Event.current.clickCount == 2)
                            StartFolderRename(folder);

                        Repaint();
                    }
                }
            }
        }
        
        private void DrawResizableFolderPanelDivider()
        {
            const int thickness = 1;
            Rect dividerRect = new Rect(
                FolderPanelWidth - thickness, HeaderHeight, thickness, position.height);
            EditorGUI.DrawRect(dividerRect, DividerColor);

            const int expansion = 1;
            Rect resizeRect = dividerRect;
            resizeRect.xMin -= expansion;
            resizeRect.xMax += expansion;
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
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
                isResizingFolderPanel = false;
        }

        private void StartFolderDrag(PaletteFolder folder)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(FolderDragGenericDataType, folder);
            DragAndDrop.StartDrag("Drag Palette Folder");
            isDraggingFolder = true;
            folderToDragIndex = CurrentCollection.Folders.IndexOf(folder);
        }

        private void StopFolderDrag()
        {
            isDraggingFolder = false;

            bool isValidDrop = DragAndDrop.visualMode == DragAndDropVisualMode.Move;
            if (!isValidDrop)
                return;

            PaletteFolder folderBeingDragged = (PaletteFolder)DragAndDrop.GetGenericData(FolderDragGenericDataType);
            DragAndDrop.AcceptDrag();

            // If you want to drag a folder downwards, keep in mind that the indices will shift as a result from the
            // dragged folder not being where it used to be any more.
            if (currentFolderDragIndex > folderToDragIndex)
                currentFolderDragIndex--;

            // Remove the folder from the list and add it back at the specified position.
            CurrentCollectionSerializedObject.Update();
            SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
            foldersProperty.DeleteArrayElementAtIndex(folderToDragIndex);
            foldersProperty.InsertArrayElementAtIndex(currentFolderDragIndex);
            SerializedProperty movedFolderProperty = foldersProperty.GetArrayElementAtIndex(currentFolderDragIndex);
            movedFolderProperty.managedReferenceValue = folderBeingDragged;
            CurrentCollectionSerializedObject.ApplyModifiedProperties();

            SelectedFolderIndex = currentFolderDragIndex;
            
            Repaint();
        }
        
        private void StartFolderRename(PaletteFolder folder)
        {
            folderRenameText = folder.Name;
            folder.StartRename();
        }

        private void StopFolderRename()
        {
            if (!PaletteFolder.IsFolderBeingRenamed)
                return;

            bool isValidRename = !string.IsNullOrEmpty(folderRenameText) &&
                                 !string.IsNullOrWhiteSpace(folderRenameText) &&
                                 PaletteFolder.FolderCurrentlyRenaming.Name != folderRenameText;
            if (isValidRename)
            {
                folderRenameText = GetUniqueFolderName(folderRenameText);
                CurrentCollectionSerializedObject.Update();
                SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                int index = CurrentCollection.Folders.IndexOf(PaletteFolder.FolderCurrentlyRenaming);
                SerializedProperty folderBeingRenamedProperty = foldersProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProperty = folderBeingRenamedProperty.FindPropertyRelative("name");
                nameProperty.stringValue = folderRenameText;
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
            }

            PaletteFolder.CancelRename();
            Repaint();
        }
        
        private void OnLostFocus()
        {
            StopFolderRename();
        }

        private void OnSelectionChange()
        {
            StopFolderRename();
        }

        private void OnFocus()
        {
            StopFolderRename();
        }

        private void OnProjectChange()
        {
            StopFolderRename();
        }
    }
}
