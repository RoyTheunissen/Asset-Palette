using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Editor;
using RoyTheunissen.AssetPalette.Editor.CustomEditors;
using RoyTheunissen.AssetPalette.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using RectExtensions = RoyTheunissen.AssetPalette.Extensions.RectExtensions;
using SerializedPropertyExtensions = RoyTheunissen.AssetPalette.Extensions.SerializedPropertyExtensions;

namespace RoyTheunissen.AssetPalette.Editor.Windows
{
    public partial class AssetPaletteWindow
    {
        private const float EntrySpacing = 4;
        private const int EntrySizeMax = 128;
        private const float EntrySizeMin = EntrySizeMax * 0.45f;
        
        [NonSerialized] private readonly List<PaletteEntry> entriesSelected = new List<PaletteEntry>();
        [NonSerialized] private readonly List<PaletteEntry> entriesIndividuallySelected = new List<PaletteEntry>();

        private Vector2 entriesPanelScrollPosition;
        
        private SortModes SortMode
        {
            get => (SortModes)EditorPrefs.GetInt(EntriesSortModeEditorPref);
            set => EditorPrefs.SetInt(EntriesSortModeEditorPref, (int)value);
        }
        
        [NonSerialized] private bool didCacheSelectedFolderEntriesSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderEntriesSerializedProperty;
        private SerializedProperty SelectedFolderEntriesSerializedProperty
        {
            get
            {
                if (!didCacheSelectedFolderEntriesSerializedProperty)
                {
                    didCacheSelectedFolderEntriesSerializedProperty = true;
                    cachedSelectedFolderEntriesSerializedProperty =
                        SelectedFolderSerializedProperty.FindPropertyRelative("entries");
                }
                return cachedSelectedFolderEntriesSerializedProperty;
            }
        }
        
        [NonSerialized] private GUIStyle cachedMessageTextStyle;
        [NonSerialized] private bool didCacheMessageTextStyle;
        private GUIStyle MessageTextStyle
        {
            get
            {
                if (!didCacheMessageTextStyle)
                {
                    didCacheMessageTextStyle = true;
                    cachedMessageTextStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return cachedMessageTextStyle;
            }
        }
        
        [NonSerialized] private static GUIStyle cachedEntryRenameTextStyle;
        [NonSerialized] private static bool didCacheEntryRenameTextStyle;
        protected static GUIStyle EntryRenameTextStyle
        {
            get
            {
                if (!didCacheEntryRenameTextStyle)
                {
                    didCacheEntryRenameTextStyle = true;
                    cachedEntryRenameTextStyle = new GUIStyle(EditorStyles.textField)
                    {
                        wordWrap = true,
                        alignment = TextAnchor.LowerCenter,
                    };
                }
                return cachedEntryRenameTextStyle;
            }
        }
        
        [NonSerialized] private PaletteEntry entryBelowCursorOnMouseDown;
        
        private int GetEntryCount()
        {
            return SelectedFolder.Entries.Count;
        }
        
        private List<PaletteEntry> GetEntries()
        {
            return SelectedFolder.Entries;
        }
        
        private PaletteEntry GetEntry(int index)
        {
            return SelectedFolder.Entries[index];
        }
        
        private void SortEntriesInSerializedObject()
        {
            if (SortMode == SortModes.Unsorted)
                return;
            
            // Create a list of all the Palette Entries currently in the serialized object. Doing it this way means
            // that you can sort the list while adding new entries, without the sorting operation being a separate Undo
            List<PaletteEntry> entries = new List<PaletteEntry>();
            for (int i = 0; i < SelectedFolderEntriesSerializedProperty.arraySize; i++)
            {
                SerializedProperty entryProperty = SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(i);
                PaletteEntry entry = (PaletteEntry)entryProperty.managedReferenceValue;
                entries.Add(entry);
            }
            
            // Now sort that list of Palette Entries.
            entries.Sort();
            if (SortMode == SortModes.ReverseAlphabetical)
                entries.Reverse();
            
            // Now make sure that the actual list as it exists in the serialized object has all its values in the same
            // position as that sorted list.
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty entryProperty = SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(i);
                entryProperty.managedReferenceValue = entries[i];
            }
        }
        
        private void AddEntry(PaletteEntry entry, bool apply)
        {
            if (apply)
                CurrentCollectionSerializedObject.Update();

            SerializedProperty newEntryProperty =
                SerializedPropertyExtensions.AddArrayElement(SelectedFolderEntriesSerializedProperty);
            newEntryProperty.managedReferenceValue = entry;
            
            if (apply)
            {
                SortEntriesInSerializedObject();
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
            }
            
            SelectEntry(entry, false);
        }

        private void AddEntries(List<PaletteEntry> entries, bool apply = true)
        {
            if (apply)
                CurrentCollectionSerializedObject.Update();

            for (int i = 0; i < entries.Count; i++)
            {
                AddEntry(entries[i], false);
            }
            
            if (apply)
            {
                SortEntriesInSerializedObject();
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
            }
        }

        private int IndexOfEntry(PaletteEntry entry)
        {
            for (int i = 0; i < SelectedFolderEntriesSerializedProperty.arraySize; i++)
            {
                PaletteEntry entryAtIndex = (PaletteEntry)SelectedFolderEntriesSerializedProperty
                    .GetArrayElementAtIndex(i).managedReferenceValue;
                if (entryAtIndex == entry)
                    return i;
            }
            return -1;
        }

        private void RemoveEntry(PaletteEntry entry, bool apply = true)
        {
            int index = IndexOfEntry(entry);
            
            if (index != -1)
                RemoveEntryAt(index, apply);
        }
        
        private void RemoveEntries(List<PaletteEntry> entries, bool apply = true)
        {
            if (apply)
                CurrentCollectionSerializedObject.Update();

            for (int i = 0; i < entries.Count; i++)
            {
                RemoveEntry(entries[i], false);
            }
            
            if (apply)
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
        }
        
        private void RemoveEntryAt(int index, bool apply = true)
        {
            if (apply)
                CurrentCollectionSerializedObject.Update();
            SelectedFolderEntriesSerializedProperty.DeleteArrayElementAtIndex(index);
            
            if (apply)
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
        }
        
        private void DrawEntriesPanel()
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                entryBelowCursorOnMouseDown = null;
            
            entriesPanelScrollPosition = GUILayout.BeginScrollView(
                entriesPanelScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

            // Draw a dark background for the entries panel.
            Rect entriesPanelRect = new Rect(0, 0, position.width - FolderPanelWidth, 90000000);
            EditorGUI.DrawRect(entriesPanelRect, new Color(0, 0, 0, 0.1f));

            // If the current state is invalid, draw a message instead.
            bool hasCollection = HasCollection;
            bool hasEntries = hasCollection && GetEntryCount() > 0;
            if (!hasCollection || !hasEntries)
            {
                DrawEntryPanelMessage(hasCollection);
                GUILayout.EndScrollView();
                return;
            }

            float containerWidth = Mathf.Floor(EditorGUIUtility.currentViewWidth) - FolderPanelWidth;

            const float padding = 2;
            int entrySize = Mathf.RoundToInt(Mathf.Lerp(EntrySizeMin, EntrySizeMax, ZoomLevel));

            int columnCount = Mathf.FloorToInt(containerWidth / (entrySize + EntrySpacing));
            int rowCount = Mathf.CeilToInt((float)GetEntryCount() / columnCount);

            GUILayout.Space(padding);

            bool didClickASpecificEntry = false;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(padding);
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    int index = rowIndex * columnCount + columnIndex;

                    // Purge invalid entries.
                    while (index < GetEntryCount() && !GetEntry(index).IsValid)
                    {
                        RemoveEntryAt(index);
                    }

                    if (index >= GetEntryCount())
                    {
                        GUILayout.FlexibleSpace();
                        break;
                    }

                    DrawEntry(index, entrySize, ref didClickASpecificEntry);

                    if (columnIndex < columnCount - 1)
                        EditorGUILayout.Space(EntrySpacing);
                    else
                        GUILayout.FlexibleSpace();
                }

                GUILayout.Space(padding);
                EditorGUILayout.EndHorizontal();

                if (rowIndex < rowCount - 1)
                    EditorGUILayout.Space(EntrySpacing);
            }

            GUILayout.Space(padding);

            // If you didn't click an entry and weren't pressing SHIFT, clear the selection.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !didClickASpecificEntry &&
                !Event.current.shift)
            {
                StopAllRenames();
                
                ClearEntrySelection();
                Repaint();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(int index, int entrySize, ref bool didClickASpecificEntry)
        {
            PaletteEntry entry = GetEntry(index);

            Rect rect = GUILayoutUtility.GetRect(
                0, 0, GUILayout.Width(entrySize), GUILayout.Height(entrySize));

            // Allow this entry to be selected by clicking it.
            bool isMouseOnEntry = rect.Contains(Event.current.mousePosition) && isMouseInEntriesPanel;
            bool wasAlreadySelected = entriesSelected.Contains(entry);
            if (isDraggingAssetIntoEntryPanel && isMouseOnEntry &&
                entry.CanAcceptDraggedAssets(DragAndDrop.objectReferences))
            {
                DragAndDrop.AcceptDrag();
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                if (Event.current.type == EventType.DragPerform)
                {
                    CurrentCollectionSerializedObject.Update();
                    SerializedProperty serializedProperty = GetSerializedPropertyForEntry(entry);
                    entry.AcceptDraggedAssets(DragAndDrop.objectReferences, serializedProperty);
                    CurrentCollectionSerializedObject.ApplyModifiedProperties();
                }

                // Make sure nothing else handles this, like the entry panel itself.
                isDraggingAssetIntoEntryPanel = false;
            }
            else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && isMouseOnEntry)
            {
                entryBelowCursorOnMouseDown = entry;
                
                if (Event.current.shift)
                {
                    // Shift+click to grow selection until this point.
                    if (entriesSelected.Count == 0)
                    {
                        SelectEntry(entry, true);
                    }
                    else
                    {
                        if (wasAlreadySelected)
                        {
                            // Define a new extremity away from the first individually selected entry.
                            // Might seem convoluted and with weird edge cases, but this is how Unity does it...
                            PaletteEntry firstEntryIndividuallySelected = entriesIndividuallySelected[0];
                            int indexOfFirstIndividuallySelectedEntry =
                                GetEntries().IndexOf(firstEntryIndividuallySelected);
                            if (index > indexOfFirstIndividuallySelectedEntry)
                            {
                                PaletteEntry lowestSelectedEntry = null;
                                for (int i = 0; i < GetEntryCount(); i++)
                                {
                                    if (entriesSelected.Contains(GetEntry(i)))
                                    {
                                        lowestSelectedEntry = GetEntry(i);
                                        break;
                                    }
                                }

                                entriesIndividuallySelected.Clear();
                                entriesIndividuallySelected.Add(lowestSelectedEntry);
                                indexOfFirstIndividuallySelectedEntry = GetEntries().IndexOf(lowestSelectedEntry);
                            }
                            else if (index < indexOfFirstIndividuallySelectedEntry)
                            {
                                PaletteEntry highestSelectedEntry = null;
                                for (int i = GetEntryCount() - 1; i >= 0; i--)
                                {
                                    if (entriesSelected.Contains(GetEntry(i)))
                                    {
                                        highestSelectedEntry = GetEntry(i);
                                        break;
                                    }
                                }

                                entriesIndividuallySelected.Clear();
                                entriesIndividuallySelected.Add(highestSelectedEntry);
                                indexOfFirstIndividuallySelectedEntry = GetEntries().IndexOf(highestSelectedEntry);
                            }

                            SelectEntriesByRange(indexOfFirstIndividuallySelectedEntry, index, true);
                        }
                        else
                        {
                            // Grow the selection from the last individually selected entry.
                            PaletteEntry lastEntryIndividuallySelected = entriesIndividuallySelected[^1];
                            int indexOfLastIndividuallySelectedEntry =
                                GetEntries().IndexOf(lastEntryIndividuallySelected);
                            SelectEntriesByRange(indexOfLastIndividuallySelectedEntry, index, false);
                        }
                    }
                }
                else if (Event.current.control)
                {
                    // Control+click to add specific files to the selection.
                    if (!wasAlreadySelected)
                        SelectEntry(entry, false);
                    else
                        DeselectEntry(entry);
                }
                else
                {
                    // Regular click to select only this entry.
                    if (!wasAlreadySelected)
                        SelectEntry(entry, true);

                    // Allow assets to be opened by double clicking on them.
                    if (Event.current.clickCount == 2)
                    {
                        Rect labelRect = PaletteEntryDrawerBase.GetLabelRect(rect, entry);
                        if (entry.CanRename && labelRect.Contains(Event.current.mousePosition))
                            StartEntryRename(entry);
                        else
                            entry.Open();
                    }
                }

                didClickASpecificEntry = true;
                Repaint();
            }
            else if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && isMouseOnEntry
                     && !Event.current.control && !Event.current.shift)
            {
                // Regular click to select only this entry.
                SelectEntry(entry, true);
                Repaint();
            }
            else if (Event.current.type == EventType.MouseDrag && Event.current.button == 0 && isMouseOnEntry &&
                     !isResizingFolderPanel && isMouseInEntriesPanel && !IsZoomLevelFocused &&
                     entriesSelected.Contains(entryBelowCursorOnMouseDown))
            {
                DragAndDrop.PrepareStartDrag();
                List<Object> selectedAssets = new List<Object>();
                foreach (PaletteEntry selectedEntry in entriesSelected)
                {
                    if (selectedEntry is PaletteAsset paletteAsset)
                        selectedAssets.Add(paletteAsset.Asset);
                }

                DragAndDrop.objectReferences = selectedAssets.ToArray();
                // Mark the drag as being an asset palette entry drag, so we know not to accept it again ourselves.
                // Also pass along the name of the directory so we can handle stuff like dragging assets out into
                // another folder (but ignore the folder it was originally dragged from).
                DragAndDrop.SetGenericData(EntryDragGenericDataType, SelectedFolder.Name);
                DragAndDrop.StartDrag("Drag from Asset Palette");
            }

            bool isSelected = entriesSelected.Contains(entry);

            Color borderColor = isSelected ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            float borderWidth = isSelected ? 2 : 1;
            Rect borderRect = RectExtensions.Expand(rect, borderWidth);
            GUI.DrawTexture(
                borderRect, EditorGUIUtility.whiteTexture, ScaleMode.ScaleToFit, true, 0.0f, borderColor, borderWidth,
                borderWidth);

            // Actually draw the entry itself.
            Rect entryContentsRect = rect;
            SerializedProperty entryProperty = SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);

            if (entry.IsRenaming)
            {
                string renameControlId = GetRenameControlId(entryProperty);
                GUI.SetNextControlName(renameControlId);
                renameText = EditorGUI.TextField(entryContentsRect, renameText, EntryRenameTextStyle);
                GUI.FocusControl(renameControlId);
            }
            else
            {
                PaletteDrawing.DrawEntry(entryContentsRect, entryProperty, entry);
            }
        }

        private void DrawEntryPanelMessage(bool hasCollection)
        {
            GUILayout.FlexibleSpace();

            if (!hasCollection)
            {
                EditorGUILayout.LabelField(
                    "To begin organizing assets, create a collection.", MessageTextStyle);
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    bool shouldCreate = GUILayout.Button("Create", GUILayout.Width(100));
                    GUILayout.FlexibleSpace();

                    if (shouldCreate)
                        CreateNewCollection();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Drag assets here!", MessageTextStyle);
            }

            GUILayout.FlexibleSpace();
        }

        private void DeselectEntry(PaletteEntry entry)
        {
            entriesSelected.Remove(entry);
            entriesIndividuallySelected.Remove(entry);
        }

        private void SelectEntry(PaletteEntry entry, bool exclusively)
        {
            if (exclusively)
                ClearEntrySelection();
            entriesSelected.Add(entry);
            entriesIndividuallySelected.Add(entry);
        }

        private void SelectEntriesByRange(int from, int to, bool exclusively)
        {
            if (exclusively)
            {
                entriesSelected.Clear();
                // NOTE: Do NOT clear the individually selected entries. These are used to determine from what point we 
                // define the range. Used for SHIFT-selecting ranges of entries.
            }

            int direction = @from <= to ? 1 : -1;
            for (int i = @from; i != to; i += direction)
            {
                if (!entriesSelected.Contains(GetEntry(i)))
                    entriesSelected.Add(GetEntry(i));
            }

            if (!entriesSelected.Contains(GetEntry(to)))
                entriesSelected.Add(GetEntry(to));
        }

        private void SelectEntries(List<PaletteEntry> entries, bool exclusively)
        {
            if (exclusively)
                ClearEntrySelection();

            foreach (PaletteEntry entry in entries)
            {
                entriesSelected.Add(entry);
            }
        }

        private void ClearEntrySelection()
        {
            entriesSelected.Clear();
            entriesIndividuallySelected.Clear();
        }

        private void AddEntryForProjectWindowSelection()
        {
            PaletteSelectionShortcut paletteSelectionShortcut = new PaletteSelectionShortcut(Selection.objects);
            AddEntry(paletteSelectionShortcut, true);
            
            if (Selection.objects.Length > PaletteSelectionShortcut.ItemNamesToDisplayMax)
                StartEntryRename(paletteSelectionShortcut);
            
            Repaint();
        }

        private SerializedProperty GetSerializedPropertyForEntry(PaletteEntry entry)
        {
            int index = IndexOfEntry(entry);

            if (index == -1)
                return null;
            
            return SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);
        }
        
        public void StartEntryRename(PaletteEntry entry)
        {
            renameText = entry.Name;
            entry.StartRename();
            
            EditorGUI.FocusTextInControl(GetRenameControlId(entry));
        }

        private void StopEntryRename()
        {
            if (!PaletteEntry.IsEntryBeingRenamed)
                return;

            bool isValidRename = PaletteEntry.EntryCurrentlyRenaming.Name != renameText;
            if (isValidRename)
            {
                CurrentCollectionSerializedObject.Update();
                int index = SelectedFolder.Entries.IndexOf(PaletteEntry.EntryCurrentlyRenaming);
                SerializedProperty entryBeingRenamedProperty =
                    SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty customNameProperty = entryBeingRenamedProperty.FindPropertyRelative("customName");
                customNameProperty.stringValue = renameText;
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
            }

            PaletteEntry.CancelRename();
            Repaint();
        }
    }
}
