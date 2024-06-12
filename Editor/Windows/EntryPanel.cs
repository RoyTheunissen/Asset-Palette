using System;
using System.Collections.Generic;
using System.Linq;
using RoyTheunissen.AssetPalette.CustomEditors;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using RectExtensions = RoyTheunissen.AssetPalette.Extensions.RectExtensions;
using SerializedPropertyExtensions = RoyTheunissen.AssetPalette.Extensions.SerializedPropertyExtensions;

namespace RoyTheunissen.AssetPalette.Windows
{
    public sealed class EntryPanel
    {
        // Editor prefs
        private static string EntriesSortModeEditorPref => AssetPaletteWindow.EditorPrefPrefix + "EntriesSortMode";
        
        // Measurements
        public static float EntryPanelHeightMin => 50;
        private const float Padding = 2;
        private const float EntrySpacing = 4;
        private const int EntrySizeMax = 128;
        private const float EntrySizeMin = EntrySizeMax * 0.45f;
        private const float ScrollBarWidth = 13;
        
        public const string EntryDragGenericDataType = "AssetPaletteEntryDrag";
        
        [NonSerialized] private readonly List<PaletteEntry> entriesSelected = new List<PaletteEntry>();
        public List<PaletteEntry> EntriesSelected => entriesSelected;

        [NonSerialized] private readonly List<PaletteEntry> entriesIndividuallySelected = new List<PaletteEntry>();
        public List<PaletteEntry> EntriesIndividuallySelected => entriesIndividuallySelected;

        private Vector2 entriesPanelScrollPosition;

        public SortModes SortMode
        {
            get
            {
                if (!EditorPrefs.HasKey(EntriesSortModeEditorPref))
                    SortMode = SortModes.Alphabetical;
                return (SortModes)EditorPrefs.GetInt(EntriesSortModeEditorPref);
            }
            set => EditorPrefs.SetInt(EntriesSortModeEditorPref, (int)value);
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
        
        [NonSerialized] private static GUIStyle cachedGridEntryRenameTextStyle;
        [NonSerialized] private static bool didCacheGridEntryRenameTextStyle;
        public static GUIStyle GridEntryRenameTextStyle
        {
            get
            {
                if (!didCacheGridEntryRenameTextStyle)
                {
                    didCacheGridEntryRenameTextStyle = true;
                    cachedGridEntryRenameTextStyle = new GUIStyle(EditorStyles.textField)
                    {
                        wordWrap = true,
                        alignment = TextAnchor.LowerCenter,
                    };
                }
                return cachedGridEntryRenameTextStyle;
            }
        }
        
        [NonSerialized] private static GUIStyle cachedListEntryRenameTextStyle;
        [NonSerialized] private static bool didCacheListEntryRenameTextStyle;
        private GUIStyle ListEntryRenameTextStyle
        {
            get
            {
                if (!didCacheListEntryRenameTextStyle)
                {
                    didCacheListEntryRenameTextStyle = true;
                    cachedListEntryRenameTextStyle = new GUIStyle(EditorStyles.textField)
                    {
                        wordWrap = true,
                        alignment = TextAnchor.MiddleLeft
                    };
                }
                return cachedListEntryRenameTextStyle;
            }
        }

        private GUIStyle EntryRenameTextStyle =>
            ShouldDrawListView ? ListEntryRenameTextStyle : GridEntryRenameTextStyle;
        
        [NonSerialized] private PaletteEntry entryBelowCursorOnMouseDown;

        private bool ShouldDrawListView => Mathf.Approximately(window.Footer.ZoomLevel, 0.0f);
        
        [NonSerialized] private string renameText;
        
        private AssetPaletteWindow window;

        public EntryPanel(AssetPaletteWindow window)
        {
            this.window = window;
        }

        public int GetEntryCount()
        {
            return window.FolderPanel.SelectedFolder.Entries.Count;
        }

        public List<PaletteEntry> GetEntries()
        {
            return window.FolderPanel.SelectedFolder.Entries;
        }

        public PaletteEntry GetEntry(int index)
        {
            return window.FolderPanel.SelectedFolder.Entries[index];
        }

        public void SetSortModeAndSortCurrentEntries(SortModes sortMode)
        {
            SortMode = sortMode;
            
            // TODO: Maybe we should consider sorting the entries in ALL the folders at this time.. ?
            window.CurrentCollectionSerializedObject.Update();
            SortEntriesInSerializedObject();
            window.CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public void SortEntriesInSerializedObject()
        {
            if (SortMode == SortModes.Unsorted)
                return;
            
            // Create a list of all the Palette Entries currently in the serialized object. Doing it this way means
            // that you can sort the list while adding new entries, without the sorting operation being a separate Undo
            List<PaletteEntry> entries = new List<PaletteEntry>();
            for (int i = 0; i < window.SelectedFolderEntriesSerializedProperty.arraySize; i++)
            {
                SerializedProperty entryProperty = window
                    .SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(i);
                PaletteEntry entry = entryProperty.GetValue<PaletteEntry>();
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
                SerializedProperty entryProperty = window
                    .SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(i);
                entryProperty.managedReferenceValue = entries[i];
            }
        }
        
        private void AddEntry(PaletteEntry entry, bool apply)
        {
            if (apply)
                window.CurrentCollectionSerializedObject.Update();

            SerializedProperty newEntryProperty =
                SerializedPropertyExtensions.AddArrayElement(window.SelectedFolderEntriesSerializedProperty);
            newEntryProperty.managedReferenceValue = entry;
            
            if (apply)
            {
                // Applying it before the sorting because otherwise the sorting will be unable to find the items
                window.ApplyModifiedProperties();

                SortEntriesInSerializedObject();
                window.ApplyModifiedProperties();
            }
            
            SelectEntry(entry, false);
        }

        public void AddEntries(List<PaletteEntry> entries, bool apply = true)
        {
            if (apply)
                window.CurrentCollectionSerializedObject.Update();

            for (int i = 0; i < entries.Count; i++)
            {
                AddEntry(entries[i], false);
            }
            
            if (apply)
            {
                // Applying it before the sorting because otherwise the sorting will be unable to find the items
                window.ApplyModifiedProperties();

                SortEntriesInSerializedObject();
                window.ApplyModifiedProperties();
            }
        }

        private int IndexOfEntry(PaletteEntry entry)
        {
            for (int i = 0; i < window.SelectedFolderEntriesSerializedProperty.arraySize; i++)
            {
                PaletteEntry entryAtIndex = window.SelectedFolderEntriesSerializedProperty
                    .GetArrayElementAtIndex(i).GetValue<PaletteEntry>();
                if (entryAtIndex == entry)
                    return i;
            }
            return -1;
        }

        private void RemoveEntry(PaletteEntry entry)
        {
            int index = IndexOfEntry(entry);
            
            if (index != -1)
                RemoveEntryAt(index);
        }

        public void RemoveEntries(List<PaletteEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                RemoveEntry(entries[i]);
            }
        }
        
        private void RemoveEntryAt(int index)
        {
            window.CurrentCollectionSerializedObject.Update();
            window.SelectedFolderEntriesSerializedProperty.DeleteArrayElementAtIndex(index);
            
            // NOTE: *Need* to apply this after every individual change because otherwise GetValue<> will not return
            // correct values, and we need to do it that way to have 2020 support because Unity 2020 has a setter for
            // managedReferenceValue but not a getter >_>
            window.ApplyModifiedProperties();
        }

        public void DrawEntriesPanel()
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                entryBelowCursorOnMouseDown = null;

            entriesPanelScrollPosition = GUILayout.BeginScrollView(
                entriesPanelScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

            // Draw a dark background for the entries panel.
            Rect entriesPanelRect = new Rect(
                0, 0, window.position.width - window.FolderPanel.FolderPanelWidth, 90000000);
            EditorGUI.DrawRect(entriesPanelRect, new Color(0, 0, 0, 0.1f));

            // If the current state is invalid, draw a message instead.
            bool hasEntries = GetEntryCount() > 0;
            if (!hasEntries)
            {
                DrawEntryPanelMessage();
                GUILayout.EndScrollView();
                return;
            }
            
            float containerWidth = Mathf.Floor(EditorGUIUtility.currentViewWidth)
                                   - window.FolderPanel.FolderPanelWidth - ScrollBarWidth;

            GUILayout.Space(Padding);

            bool didClickASpecificEntry;
            if (ShouldDrawListView)
                DrawListEntries(containerWidth, out didClickASpecificEntry);
            else
                DrawGridEntries(containerWidth, out didClickASpecificEntry);

            GUILayout.Space(Padding);

            // If you didn't click an entry and weren't pressing SHIFT, clear the selection.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !didClickASpecificEntry &&
                !Event.current.shift)
            {
                window.StopAllRenames(false);

                ClearEntrySelection();
                window.Repaint();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawListEntries(float containerWidth, out bool didClickASpecificEntry)
        {
            didClickASpecificEntry = false;

            for (int i = 0; i < GetEntryCount(); i++)
            {
                PurgeInvalidEntries(i);
                if (i >= GetEntryCount())
                    break;

                DrawListEntry(i, containerWidth, ref didClickASpecificEntry);
            }
        }

        private void DrawListEntry(int index, float containerWidth, ref bool didClickASpecificEntry)
        {
            PaletteEntry entry = GetEntry(index);
            Rect rect = GUILayoutUtility.GetRect(0, 0,
                GUILayout.Width(containerWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            
            // Add an indent. Looks a bit more spacious this way. Unity does it for their project view list too.
            rect = RectExtensions.Indent(rect, 1);

            HandleEntrySelection(index, rect, entry, ref didClickASpecificEntry);

            Rect entryContentsRect = rect;
            SerializedProperty entryProperty = window
                .SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);

            PaletteDrawing.DrawListEntry(entryContentsRect, entryProperty, entry, entriesSelected.Contains(entry));

            if (entry.IsRenaming)
            {
                // This is done purely by eye
                entryContentsRect.xMin += 17;
                DrawRenameEntry(entryProperty, entryContentsRect);
            }
        }

        private void DrawGridEntries(float containerWidth, out bool didClickASpecificEntry)
        {
            int entrySize = Mathf.RoundToInt(Mathf.Lerp(EntrySizeMin, EntrySizeMax, window.Footer.ZoomLevel));
            int columnCount = Mathf.FloorToInt(containerWidth / (entrySize + EntrySpacing));
            int rowCount = Mathf.CeilToInt((float)GetEntryCount() / columnCount);

            didClickASpecificEntry = false;

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Padding);
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    int index = rowIndex * columnCount + columnIndex;

                    PurgeInvalidEntries(index);

                    if (index >= GetEntryCount())
                    {
                        GUILayout.FlexibleSpace();
                        break;
                    }

                    DrawGridEntry(index, entrySize, ref didClickASpecificEntry);

                    if (columnIndex < columnCount - 1)
                        EditorGUILayout.Space(EntrySpacing);
                    else
                        GUILayout.FlexibleSpace();
                }

                GUILayout.Space(Padding);
                EditorGUILayout.EndHorizontal();

                if (rowIndex < rowCount - 1)
                    EditorGUILayout.Space(EntrySpacing);
            }
        }

        private void DrawGridEntry(int index, int entrySize, ref bool didClickASpecificEntry)
        {
            PaletteEntry entry = GetEntry(index);

            Rect rect = GUILayoutUtility.GetRect(
                0, 0, GUILayout.Width(entrySize), GUILayout.Height(entrySize));

            HandleEntrySelection(index, rect, entry, ref didClickASpecificEntry);

            bool isSelected = entriesSelected.Contains(entry);

            Color borderColor = isSelected ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            float borderWidth = isSelected ? 2 : 1;
            Rect borderRect = RectExtensions.Expand(rect, borderWidth);
            GUI.DrawTexture(
                borderRect, EditorGUIUtility.whiteTexture, ScaleMode.ScaleToFit, true, 0.0f, borderColor, borderWidth,
                borderWidth);

            // Actually draw the entry itself.
            Rect entryContentsRect = rect;
            SerializedProperty entryProperty = window
                .SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);

            PaletteDrawing.DrawGridEntry(entryContentsRect, entryProperty, entry);
            
            if (entry.IsRenaming)
            {
                Rect labelRect = PaletteEntryDrawerBase.GetRenameRect(entryContentsRect, renameText);
                DrawRenameEntry(entryProperty, labelRect);
            }
        }

        private void PurgeInvalidEntries(int index)
        {
            // Purge invalid entries.
            while (index < GetEntryCount() && !GetEntry(index).IsValid)
            {
                RemoveEntryAt(index);
            }
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

        private void DrawRenameEntry(SerializedProperty entryProperty, Rect labelRect)
        {
            string renameControlId = GetRenameControlId(entryProperty);
            GUI.SetNextControlName(renameControlId);
            renameText = EditorGUI.TextField(labelRect, renameText, EntryRenameTextStyle);
            EditorGUI.FocusTextInControl(renameControlId);
        }

        private void HandleEntrySelection(int index, Rect rect, PaletteEntry entry, ref bool didClickASpecificEntry)
        {
            if (window.IsRenaming)
                return;
            
            // Allow this entry to be selected by clicking it.
            bool isMouseOnEntry = rect.Contains(Event.current.mousePosition) && window.IsMouseInEntriesPanel;
            bool wasAlreadySelected = entriesSelected.Contains(entry);
            if (window.IsDraggingAssetIntoEntryPanel && isMouseOnEntry &&
                entry.CanAcceptDraggedAssets(DragAndDrop.objectReferences))
            {
                DragAndDrop.AcceptDrag();
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                if (Event.current.type == EventType.DragPerform)
                {
                    window.CurrentCollectionSerializedObject.Update();
                    SerializedProperty serializedProperty = GetSerializedPropertyForEntry(entry);
                    entry.AcceptDraggedAssets(DragAndDrop.objectReferences, serializedProperty);
                    window.ApplyModifiedProperties();
                }

                // Make sure nothing else handles this, like the entry panel itself.
                window.IsDraggingAssetIntoEntryPanel = false;
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
                            PaletteEntry lastEntryIndividuallySelected =
                                entriesIndividuallySelected[entriesIndividuallySelected.Count - 1];
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
                        entry.Open();
                }

                didClickASpecificEntry = true;
                window.Repaint();
            }
            else if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && isMouseOnEntry
                     && !Event.current.control && !Event.current.shift)
            {
                // Regular click to select only this entry.
                SelectEntry(entry, true);
                window.Repaint();
            }
            else if (Event.current.type == EventType.MouseDrag && Event.current.button == 0 && isMouseOnEntry
                     && !window.FolderPanel.IsResizingFolderPanel && window.IsMouseInEntriesPanel
                     && !window.Footer.IsZoomLevelFocused
                     && entriesSelected.Contains(entryBelowCursorOnMouseDown))
            {
                StartEntriesDrag();
            }
            else if (Event.current.type == EventType.DragExited)
            {
                CancelEntriesDrag();
            }
        }

        private void StartEntriesDrag()
        {
            DragAndDrop.PrepareStartDrag();
            
            // Tell the drag and drop system that certain assets are being dragged. This allows you to drag entries
            // straight into the scene view, the inspector, things like that.
            List<Object> selectedAssets = new List<Object>();
            List<SerializedProperty> selectedEntryProperties = new List<SerializedProperty>();
            foreach (PaletteEntry selectedEntry in entriesSelected)
            {
                if (selectedEntry is PaletteAsset paletteAsset)
                    selectedAssets.Add(paletteAsset.Asset);

                SerializedProperty serializedProperty = GetSerializedPropertyForEntry(selectedEntry);
                selectedEntryProperties.Add(serializedProperty);
            }
            DragAndDrop.objectReferences = selectedAssets.ToArray();
            
            // Mark the drag as being an asset palette entry drag with the entire context. This means that we can
            // differentiate between someone dragging in assets from outside of the palette, or whether this was dragged
            // from a different folder. This distinction is important because if we dragged entries then we want to move
            // the *entries* and not just re-add the corresponding assets to make brand new entries.
            EntryDragData entryDragData = new EntryDragData(
                selectedEntryProperties, window.SelectedFolderSerializedProperty);
            DragAndDrop.SetGenericData(EntryDragGenericDataType, entryDragData);
            string dragName = selectedAssets.Count == 1 ? selectedAssets[0].ToString() : "<multiple>";
            DragAndDrop.StartDrag(dragName);
        }

        private static void CancelEntriesDrag()
        {
            DragAndDrop.SetGenericData(EntryDragGenericDataType, null);
        }

        private void DrawEntryPanelMessage()
        {
            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Drag assets here!", MessageTextStyle);

            GUILayout.FlexibleSpace();
        }

        private void DeselectEntry(PaletteEntry entry)
        {
            entriesSelected.Remove(entry);
            entriesIndividuallySelected.Remove(entry);
            
            if (AssetPaletteWindow.SelectAssetsWhenSelectingEntries)
            {
                List<Object> assetsToSelect = new List<Object>();
                entry.GetAssetsToSelect(ref assetsToSelect);

                List<Object> selection = Selection.objects.ToList();
                for (int i = assetsToSelect.Count - 1; i >= 0; i--)
                {
                    selection.Remove(assetsToSelect[i]);
                }

                Selection.objects = selection.ToArray();
            }
        }

        private void SelectEntryInternal(PaletteEntry entry, bool exclusively)
        {
            if (PaletteEntry.IsEntryBeingRenamed && PaletteEntry.EntryCurrentlyRenaming != entry)
                StopEntryRename(true);

            List<Object> assetsToSelect = new List<Object>();
            entry.GetAssetsToSelect(ref assetsToSelect);
            
            if (exclusively)
            {
                ClearEntrySelection();
                
                if (AssetPaletteWindow.SelectAssetsWhenSelectingEntries)
                    Selection.objects = assetsToSelect.ToArray();
            }
            else if (AssetPaletteWindow.SelectAssetsWhenSelectingEntries)
            {
                List<Object> selection = Selection.objects.ToList();
                selection.AddRange(assetsToSelect);
                Selection.objects = selection.ToArray();
            }

            entriesSelected.Add(entry);
        }

        private void SelectEntry(PaletteEntry entry, bool exclusively)
        {
            SelectEntryInternal(entry, exclusively);
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
                    SelectEntryInternal(GetEntry(i), false);
            }

            if (!entriesSelected.Contains(GetEntry(to)))
                SelectEntryInternal(GetEntry(to), false);
        }

        public void SelectEntries(List<PaletteEntry> entries, bool exclusively)
        {
            if (PaletteEntry.IsEntryBeingRenamed)
                StopEntryRename(true);

            if (exclusively)
                ClearEntrySelection();

            foreach (PaletteEntry entry in entries)
            {
                SelectEntryInternal(entry, false);
            }
        }

        public void ClearEntrySelection()
        {
            bool didHaveEntriesSelected = entriesSelected.Count > 0;
            
            entriesSelected.Clear();
            entriesIndividuallySelected.Clear();

            if (AssetPaletteWindow.SelectAssetsWhenSelectingEntries && didHaveEntriesSelected)
                Selection.activeObject = null;
        }

        public void AddEntryForProjectWindowSelection()
        {
            PaletteSelectionShortcut paletteSelectionShortcut = new PaletteSelectionShortcut(Selection.objects);
            AddEntry(paletteSelectionShortcut, true);
            
            if (Selection.objects.Length > PaletteSelectionShortcut.ItemNamesToDisplayMax)
                StartEntryRename(paletteSelectionShortcut);
            
            window.Repaint();
        }

        public SerializedProperty GetSerializedPropertyForEntry(PaletteEntry entry)
        {
            int index = IndexOfEntry(entry);

            if (index == -1)
                return null;
            
            return window.SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);
        }
        
        public void StartEntryRename(PaletteEntry entry)
        {
            renameText = entry.Name;
            entry.StartRename();
            
            EditorGUI.FocusTextInControl(GetRenameControlId(entry));
        }

        public void StopEntryRename(bool isCancel)
        {
            if (!PaletteEntry.IsEntryBeingRenamed)
                return;

            bool isValidRename = PaletteEntry.EntryCurrentlyRenaming.Name != renameText;
            if (isValidRename && !isCancel)
            {
                window.CurrentCollectionSerializedObject.Update();
                int index = window.FolderPanel.SelectedFolder.Entries.IndexOf(PaletteEntry.EntryCurrentlyRenaming);
                SerializedProperty entryBeingRenamedProperty =
                    window.SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty customNameProperty = entryBeingRenamedProperty.FindPropertyRelative("customName");
                customNameProperty.stringValue = renameText;
                window.ApplyModifiedProperties();
                
                // Also sort the collection. Make sure to do this AFTER we apply the rename, otherwise the sort will
                // be based on the old name! Don't worry, doing this in a separate Apply doesn't cause a separate Undo
                window.CurrentCollectionSerializedObject.Update();
                SortEntriesInSerializedObject();
                window.ApplyModifiedProperties();
            }

            PaletteEntry.CancelRename();
            window.Repaint();
        }
    }
}
