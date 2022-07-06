using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
    public partial class AssetPaletteWindow
    {
        private const float EntrySpacing = 4;
        private const int EntrySizeMax = 128;
        private const float EntrySizeMin = EntrySizeMax * 0.45f;
        
        [NonSerialized] private readonly List<PaletteEntry> entriesSelected = new List<PaletteEntry>();
        [NonSerialized] private readonly List<PaletteEntry> entriesIndividuallySelected = new List<PaletteEntry>();

        private Vector2 entriesPanelScrollPosition;
        
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
        
        private void AddEntry(PaletteEntry entry, bool partOfMultipleAdditions)
        {
            if (!partOfMultipleAdditions)
                CurrentCollectionSerializedObject.Update();
            
            SerializedProperty newEntryProperty = SelectedFolderEntriesSerializedProperty.AddArrayElement();
            newEntryProperty.managedReferenceValue = entry;
            
            if (!partOfMultipleAdditions)
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
            
            SelectEntry(entry, false);
        }

        private void RemoveEntry(PaletteEntry entry)
        {
            int index = GetEntries().IndexOf(entry);
            if (index != -1)
                RemoveEntryAt(index);
        }
        
        private void RemoveEntryAt(int index)
        {
            CurrentCollectionSerializedObject.Update();
            SelectedFolderEntriesSerializedProperty.DeleteArrayElementAtIndex(index);
            CurrentCollectionSerializedObject.ApplyModifiedProperties();
        }
        
        private void DrawEntriesPanel()
        {
            entriesPanelScrollPosition = GUILayout.BeginScrollView(
                entriesPanelScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

            // Draw a dark background for the entries panel.
            Rect entriesPanelRect = new Rect(0, 0, position.width - FolderPanelWidth, 90000000);
            EditorGUI.DrawRect(entriesPanelRect, new Color(0, 0, 0, 0.1f));

            // If the current state is invalid, draw a message instead.
            AssetPaletteCollection currentCollection = CurrentCollection;
            bool hasCollection = currentCollection != null;
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
            if (Event.current.type == EventType.MouseDown && !didClickASpecificEntry && !Event.current.shift)
            {
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
            if (Event.current.type == EventType.MouseDown && isMouseOnEntry)
            {
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
                        entry.Open();
                }

                didClickASpecificEntry = true;
                Repaint();
            }
            else if (Event.current.type == EventType.MouseUp && isMouseOnEntry && !Event.current.control &&
                     !Event.current.shift)
            {
                // Regular click to select only this entry.
                SelectEntry(entry, true);
                Repaint();
            }
            else if (Event.current.type == EventType.MouseDrag && isMouseOnEntry && !isResizingFolderPanel &&
                     isMouseInEntriesPanel && !IsZoomLevelFocused)
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
                DragAndDrop.SetGenericData(EntryDragGenericDataType, true);
                DragAndDrop.StartDrag("Drag from Asset Palette");
            }

            bool isSelected = entriesSelected.Contains(entry);

            Color borderColor = isSelected ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            float borderWidth = isSelected ? 2 : 1;
            Rect borderRect = rect.Expand(borderWidth);
            GUI.DrawTexture(
                borderRect, EditorGUIUtility.whiteTexture, ScaleMode.ScaleToFit, true, 0.0f, borderColor, borderWidth,
                borderWidth);

            // Actually draw the contents of the entry.
            Rect entryContentsRect = rect;
            SerializedProperty entryProperty = SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(entryContentsRect, entryProperty, GUIContent.none);
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
    }
}
