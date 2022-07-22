using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// Helps organize collections of prefabs and drag them into scenes quickly.
    /// </summary>
    public partial class AssetPaletteWindow : EditorWindow, IHasCustomMenu
    {
        private const string EditorPrefPrefix = "RoyTheunissen/PrefabPalette/";
        private const string CurrentCollectionGUIDEditorPref = EditorPrefPrefix + "CurrentCollectionGUID";
        private const string FolderPanelWidthEditorPref = EditorPrefPrefix + "FolderPanelWidth";
        private const string ZoomLevelEditorPref = EditorPrefPrefix + "ZoomLevel";
        private const string SelectedFolderIndexEditorPref = EditorPrefPrefix + "SelectedFolderIndex";
        private const string EntriesSortModeEditorPref = EditorPrefPrefix + "EntriesSortMode";

        private const string FolderDragGenericDataType = "AssetPaletteFolderDrag";
        private const string EntryDragGenericDataType = "AssetPaletteEntryDrag";

        private static float FolderPanelWidthMin => CollectionButtonWidth + NewFolderButtonWidth;
        private static float EntriesPanelWidthMin => 200;
        private static float PrefabPanelHeightMin => 50;
        private static float WindowWidthMin => FolderPanelWidthMin + EntriesPanelWidthMin;
        private static float CollectionButtonWidth => 130;
        private static bool HasMultipleFolderTypes => FolderTypes.Length > 1;
        private static int NewFolderButtonWidth => 76 + (HasMultipleFolderTypes ? 9 : 0);
        private static int SortModeButtonWidth => 140;
        private static int AddEntryForProjectWindowSelectionButtonWidth => 90;
        
        private static float HeaderHeight => EditorGUIUtility.singleLineHeight + 3;
        private static float FooterHeight => EditorGUIUtility.singleLineHeight + 6;
        private static readonly float WindowHeightMin = FooterHeight + HeaderHeight + PrefabPanelHeightMin;

        [NonSerialized] private bool isMouseInHeader;
        [NonSerialized] private bool isMouseInFooter;
        [NonSerialized] private bool isMouseInFolderPanel;
        [NonSerialized] private bool isMouseInEntriesPanel;
        
        [NonSerialized] private string renameText;

        private bool IsRenaming => PaletteFolder.IsFolderBeingRenamed || PaletteEntry.IsEntryBeingRenamed;
        
        private Color cachedSelectionColor;
        private bool didCacheSelectionColor;
        private Color SelectionColor
        {
            get
            {
                if (!didCacheSelectionColor)
                {
                    didCacheSelectionColor = true;
                    cachedSelectionColor = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).settings.selectionColor;
                }
                return cachedSelectionColor;
            }
        }

        private static Texture2D lightModeIcon;
        private static Texture2D darkModeIcon;

        [MenuItem ("Window/General/Asset Palette")]
        public static void Init()
        {
            if (lightModeIcon == null)
                lightModeIcon = Resources.Load<Texture2D>("AssetPaletteWindow Icon");

            if (darkModeIcon == null)
                darkModeIcon = Resources.Load<Texture2D>("d_AssetPaletteWindow Icon");

            AssetPaletteWindow window = GetWindow<AssetPaletteWindow>(false);
            window.titleContent = new GUIContent(
                "Asset Palette", EditorGUIUtility.isProSkin ? darkModeIcon : lightModeIcon);
            window.minSize = new Vector2(WindowWidthMin, WindowHeightMin);
            window.wantsMouseMove = true;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }
        
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnUndoRedoPerformed()
        {
            // Repaint immediately otherwise you don't see the result of your undo!
            Repaint();
        }

        private void OnGUI()
        {
            // Clear the currently saved GUID when the collection has been destroyed.
            if (!string.IsNullOrEmpty(CurrentCollectionGuid) && CurrentCollection == null)
                CurrentCollectionGuid = null;
            
            UpdateMouseOverStates();
            
            PerformKeyboardShortcuts();

            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical();
                {
                    DrawFolderPanel();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                {
                    DrawEntriesPanel();

                    DrawFooter();
                    
                    // Do this last! Specific entries can now handle a dragged asset in which case this needn't happen. 
                    HandleAssetDroppingInEntryPanel();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void UpdateMouseOverStates()
        {
            // Need to do this here, because Event.current.mousePosition is relative to any scrollviews, so doing these
            // same checks within say the entries panel will yield different results.
            isMouseInHeader = Event.current.mousePosition.y <= HeaderHeight;
            isMouseInFooter = Event.current.mousePosition.y >= position.height - FooterHeight;
            isMouseInFolderPanel = !isMouseInHeader && Event.current.mousePosition.x < FolderPanelWidth;

            isMouseInEntriesPanel = !isMouseInHeader && !isMouseInFooter && !isMouseInFolderPanel;
            
            // Store this once so that specific entries can consider if they should be handling an asset drop, or 
            // if the entry panel as a whole should be handling.
            isDraggingAssetIntoEntryPanel = (Event.current.type == EventType.DragUpdated ||
                                             Event.current.type == EventType.DragPerform) && isMouseInEntriesPanel &&
                                            DragAndDrop.objectReferences.Length > 0 &&
                                            DragAndDrop.GetGenericData(EntryDragGenericDataType) == null;
        }
        
        private void PerformKeyboardShortcuts()
        {
            if (Event.current.type != EventType.KeyDown)
                return;
            
            if (Event.current.keyCode == KeyCode.Return && IsRenaming)
            {
                StopAllRenames();
                return;
            }
            
            // Allow all currently visible entries to be selected if CTRL+A is pressed. 
            if (Event.current.control && Event.current.keyCode == KeyCode.A && !IsRenaming)
            {
                SelectEntries(GetEntries(), true);
                if (GetEntryCount() > 0)
                    entriesIndividuallySelected.Add(GetEntry(GetEntryCount() - 1));
                Repaint();
                return;
            }

            if (Event.current.keyCode == KeyCode.Delete && !IsRenaming)
            {
                if (isMouseInEntriesPanel)
                {
                    // Pressing Delete will remove all selected entries from the palette.
                    foreach (PaletteEntry entry in entriesSelected)
                    {
                        RemoveEntry(entry);
                    }

                    ClearEntrySelection();
                    Repaint();
                }
                else if (isMouseInFolderPanel && HasCollection && CurrentCollection.Folders.Count > 1 &&
                         !IsDraggingFolder)
                {
                    CurrentCollectionSerializedObject.Update();
                    SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                    foldersProperty.DeleteArrayElementAtIndex(SelectedFolderIndex);
                    CurrentCollectionSerializedObject.ApplyModifiedProperties();

                    // Select the last folder.
                    SelectedFolderIndex = CurrentCollection.Folders.Count - 1;
                    
                    Repaint();
                }
            }
        }

        private void StopAllRenames()
        {
            StopFolderRename();
            StopEntryRename();
        }
    }
}
