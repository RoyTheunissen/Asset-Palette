using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.CustomEditors;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEngine;
using RectExtensions = RoyTheunissen.AssetPalette.Extensions.RectExtensions;
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

        private bool IsDraggingFolder => DragAndDrop.GetGenericData(FolderDragGenericDataType) != null;
        
        [NonSerialized] private int currentFolderDragIndex;
        [NonSerialized] private int folderToDragIndex;

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

                ClearCachedFolderSerializedProperties();
                
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
        
        [NonSerialized] private PaletteFolder folderBelowCursorOnMouseDown;
        
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
            SerializedProperty newElement = SerializedPropertyExtensions.AddArrayElement(foldersProperty);
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
            bool didClickAnywhereInWindow = Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (didClickAnywhereInWindow)
                folderBelowCursorOnMouseDown = null;

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

            if (IsDraggingFolder && Event.current.type == EventType.DragUpdated ||
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

            Color highlightColor = new Color(1, 1, 1, 0.1f);

            bool isDraggingAssets = DragAndDrop.objectReferences.Length > 0;
            string folderDraggedFrom = (string)DragAndDrop.GetGenericData(EntryDragGenericDataType);
            bool isDraggingEntriesFromAFolder = folderDraggedFrom != null;

            for (int i = 0; i < foldersProperty.arraySize; i++)
            {
                SerializedProperty folderProperty = foldersProperty.GetArrayElementAtIndex(i);
                PaletteFolder folder = SerializedPropertyExtensions.GetValue<PaletteFolder>(folderProperty);
                
                float folderHeight = PaletteDrawing.GetFolderHeight(folderProperty, folder);
                float folderWidth = FolderPanelWidth;
                Rect folderRect = GUILayoutUtility.GetRect(folderWidth, folderHeight);
                folderRect = RectExtensions.Indent(folderRect, 1);
                bool isSelected = SelectedFolderIndex == i;

                bool isMouseOver = isMouseInFolderPanel && folderRect.Contains(Event.current.mousePosition);
                
                // Dragging and dropping assets into folders. Need to handle this early because it affects the way we
                // draw the folder (might have to be highlighted).
                bool isHighlighted = false;
                if (isDraggingAssets && isMouseOver)
                {
                    bool isValidDrag = !isDraggingEntriesFromAFolder || folderDraggedFrom != folder.Name;
                    if (isValidDrag)
                    {
                        isHighlighted = true;

                        if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            DragAndDrop.visualMode = isDraggingEntriesFromAFolder
                                ? DragAndDropVisualMode.Move
                                : DragAndDropVisualMode.Copy;
                            
                            // Need to repaint now to draw a highlight underneath the hovered folder.
                            Repaint();

                            if (Event.current.type == EventType.DragPerform)
                            {
                                if (isDraggingEntriesFromAFolder)
                                {
                                    // First remove all of the selected entries from the current folder.
                                    List<PaletteEntry> entriesToMove = new List<PaletteEntry>(entriesSelected);
                                    RemoveEntries(entriesToMove);

                                    // Make the recipient folder the current folder.
                                    SelectedFolder = folder;
                                    
                                    // Now add all of the entries to the recipient folder.
                                    AddEntries(entriesToMove);
                                }
                                else
                                {
                                    // Make the recipient folder the current folder.
                                    SelectedFolder = folder;
                                    
                                    // Just act as if these assets were dropped into the entries panel.
                                    HandleAssetDropping(DragAndDrop.objectReferences);
                                }
                            }
                        }
                    }
                }
                
                // Draw a background for the folder. Can be both selected and highlighted.
                Color bgColor = Color.clear;
                if (isSelected)
                    bgColor = selectionColor;
                if (isHighlighted)
                {
                    if (!isSelected)
                        bgColor = highlightColor;
                    else
                    {
                        // Blend the selection color with the highlight color, and respect the selection alpha.
                        Color highlightColorWithCurrentAlpha = new Color(
                            highlightColor.r, highlightColor.g, highlightColor.b, bgColor.a);
                        bgColor = Color.Lerp(bgColor, highlightColorWithCurrentAlpha, 0.25f);
                    }
                }
                if (bgColor.a > 0)
                    EditorGUI.DrawRect(folderRect, bgColor);

                // Draw the actual folder itself.
                if (folder.IsRenaming)
                {
                    string renameControlId = GetRenameControlId(folderProperty);
                    GUI.SetNextControlName(renameControlId);
                    renameText = EditorGUI.TextField(folderRect, renameText);
                    GUI.FocusControl(renameControlId);
                }
                else
                {
                    PaletteDrawing.DrawFolder(folderRect, folderProperty, folder);
                }

                // Dragging and dropping folders.
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 0 && isMouseOver &&
                    !isResizingFolderPanel && !isDraggingAssets && folderBelowCursorOnMouseDown == folder)
                {
                    StartFolderDrag(folder);
                }

                if (IsDraggingFolder)
                {
                    bool didFindDragIndex = false;
                    Rect dragMarkerRect = Rect.zero;
                    if (Event.current.mousePosition.y <= folderRect.center.y &&
                        (currentFolderDragIndex == -1 || i < currentFolderDragIndex))
                    {
                        currentFolderDragIndex = i;
                        didFindDragIndex = true;
                        dragMarkerRect = RectExtensions.GetSubRectFromTop(folderRect, 2);
                        Repaint();
                    }
                    else if (currentFolderDragIndex == -1 && i == foldersProperty.arraySize - 1)
                    {
                        currentFolderDragIndex = i + 1;
                        didFindDragIndex = true;
                        dragMarkerRect = RectExtensions.GetSubRectFromBottom(folderRect, 2);
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
                        StopAllRenames();
                    }
                    else if (!IsRenaming && isMouseOver)
                    {
                        SelectedFolderIndex = i;

                        folderBelowCursorOnMouseDown = folder;

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

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                resizeRect.Contains(Event.current.mousePosition))
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
            folderToDragIndex = CurrentCollection.Folders.IndexOf(folder);
        }

        private void StopFolderDrag()
        {
            if (!IsDraggingFolder)
                return;

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
            renameText = folder.Name;
            folder.StartRename();
            
            EditorGUI.FocusTextInControl(GetRenameControlId(folder));
        }

        private string GetRenameControlId(SerializedProperty serializedProperty)
        {
            return serializedProperty.propertyPath;
        }
        
        private string GetRenameControlId(PaletteFolder folder)
        {
            SerializedProperty serializedProperty = GetSerializedPropertyForFolder(folder);
            return GetRenameControlId(serializedProperty);
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
                    (PaletteFolder)FoldersSerializedProperty.GetArrayElementAtIndex(i).managedReferenceValue;
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

        private void StopFolderRename(bool isCancel)
        {
            if (!PaletteFolder.IsFolderBeingRenamed)
                return;

            bool isValidRename = !string.IsNullOrEmpty(renameText) &&
                                 !string.IsNullOrWhiteSpace(renameText) &&
                                 PaletteFolder.FolderCurrentlyRenaming.Name != renameText;
            if (isValidRename && !isCancel)
            {
                renameText = GetUniqueFolderName(renameText);
                CurrentCollectionSerializedObject.Update();
                SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                int index = CurrentCollection.Folders.IndexOf(PaletteFolder.FolderCurrentlyRenaming);
                SerializedProperty folderBeingRenamedProperty = foldersProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProperty = folderBeingRenamedProperty.FindPropertyRelative("name");
                nameProperty.stringValue = renameText;
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
            }

            PaletteFolder.CancelRename();
            Repaint();
        }
        
        private void OnLostFocus()
        {
            StopAllRenames();
        }

        private void OnSelectionChange()
        {
            StopAllRenames();
        }

        private void OnFocus()
        {
            StopAllRenames();
        }

        private void OnProjectChange()
        {
            StopAllRenames();
        }
    }
}
