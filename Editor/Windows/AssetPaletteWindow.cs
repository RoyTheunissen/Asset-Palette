using System;
using System.IO;
using RoyTheunissen.AssetPalette.CustomEditors;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// Helps organize collections of prefabs and drag them into scenes quickly.
    /// </summary>
    public partial class AssetPaletteWindow : EditorWindow, IHasCustomMenu
    {
        private static string ProjectName
        {
            get
            {
                string[] sections = Application.dataPath.Split(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                
                // The name of the project is the dataPath i.e. Project/Assets and then go up a directory.
                return sections[sections.Length - 2];
            }
        }
        
        private static string EditorPrefPrefix => $"RoyTheunissen/PrefabPalette/{ProjectName}/";
        private static string CurrentCollectionGUIDEditorPref => EditorPrefPrefix + "CurrentCollectionGUID";
        private static string FolderPanelWidthEditorPref => EditorPrefPrefix + "FolderPanelWidth";
        private static string ZoomLevelEditorPref => EditorPrefPrefix + "ZoomLevel";
        private static string SelectedFolderIndexEditorPref => EditorPrefPrefix + "SelectedFolderIndex";
        private static string EntriesSortModeEditorPref => EditorPrefPrefix + "EntriesSortMode";
        
        private const string EntryDragGenericDataType = "AssetPaletteEntryDrag";
        
        private static string PersonalPaletteStorageKeyEditorPref => EditorPrefPrefix + "PersonalPaletteStorageKey";


        private static float FolderPanelWidthMin => CollectionButtonWidth + NewFolderButtonWidth;
        private static float EntriesPanelWidthMin => RefreshButtonWidth + AddSpecialButtonWidth + SortModeButtonWidth;
        private static float PrefabPanelHeightMin => 50;
        private static float WindowWidthMin => FolderPanelWidthMin + EntriesPanelWidthMin;
        private static float CollectionButtonWidth => 130;
        private static bool HasMultipleFolderTypes => FolderTypes.Length > 1;
        private static int NewFolderButtonWidth => 76 + (HasMultipleFolderTypes ? 9 : 0);
        private static int SortModeButtonWidth => 140;
        private static int AddSpecialButtonWidth => 90;
        private static int RefreshButtonWidth => 60;
        
        private static float HeaderHeight => EditorGUIUtility.singleLineHeight + 3;
        private static float FooterHeight => EditorGUIUtility.singleLineHeight + 6;
        private static readonly float WindowHeightMin = FooterHeight + HeaderHeight + PrefabPanelHeightMin;

        [NonSerialized] private bool isMouseInHeader;
        [NonSerialized] private bool isMouseInFooter;
        [NonSerialized] private bool isMouseInFolderPanel;
        [NonSerialized] private bool isMouseOverFolderPanelResizeBorder;
        [NonSerialized] private bool isMouseInEntriesPanel;
        
        [NonSerialized] private string renameText;

        private bool IsRenaming => PaletteEntry.IsEntryBeingRenamed;

        private static Texture2D lightModeIcon;
        private static Texture2D darkModeIcon;

        [MenuItem ("Window/General/Asset Palette")]
        private static void OpenViaMenu()
        {
            AssetPaletteWindow window = GetWindow<AssetPaletteWindow>(false);

            window.Initialize();
        }

        public void Initialize()
        {
            if (lightModeIcon == null)
                lightModeIcon = Resources.Load<Texture2D>("AssetPaletteWindow Icon");

            if (darkModeIcon == null)
                darkModeIcon = Resources.Load<Texture2D>("d_AssetPaletteWindow Icon");
            
            titleContent = new GUIContent(
                "Asset Palette", EditorGUIUtility.isProSkin ? darkModeIcon : lightModeIcon);
            minSize = new Vector2(WindowWidthMin, WindowHeightMin);
            wantsMouseMove = true;
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
            UpdateAndRepaint();
        }

        private void UpdateAndRepaint()
        {
            UpdateFoldersTreeView();
            Repaint();
        }

        private void OnGUI()
        {
            // Fall back to the personal palette when no valid palette asset is available.
            if (string.IsNullOrEmpty(CurrentCollectionGuid) || CurrentCollection == null)
                CurrentCollectionGuid = personalPaletteGuid;
            PaletteDrawing.ActivePaletteWindow = this;

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
            isMouseOverFolderPanelResizeBorder = DividerResizeRect.Contains(Event.current.mousePosition);

            isMouseInEntriesPanel = !isMouseInHeader && !isMouseInFooter && !isMouseInFolderPanel;
            
            // Store this once so that specific entries can consider if they should be handling an asset drop, or 
            // if the entry panel as a whole should be handling it.
            isDraggingAssetIntoEntryPanel = (Event.current.type == EventType.DragUpdated ||
                                             Event.current.type == EventType.DragPerform) && isMouseInEntriesPanel &&
                                            DragAndDrop.objectReferences.Length > 0 &&
                                            DragAndDrop.GetGenericData(EntryDragGenericDataType) == null;
        }

        private void PerformKeyboardShortcuts()
        {
            if (Event.current.type != EventType.KeyDown)
                return;

            if (IsRenaming)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.Escape:
                        StopAllRenames(true);
                        Event.current.Use();
                        return;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        StopAllRenames(false);
                        Event.current.Use();
                        return;
                }
            }

            if (Event.current.keyCode == KeyCode.F2 && entriesSelected.Count == 1)
            {
                StartEntryRename(entriesSelected[0]);
                Event.current.Use();
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

            if ((Event.current.keyCode == KeyCode.Delete ||
                 Event.current.command && Event.current.keyCode == KeyCode.Backspace) && !IsRenaming)
            {
                if (isMouseInEntriesPanel)
                    RemoveSelectedEntries();
                else if (isMouseInFolderPanel && !IsDraggingFolder)
                    RemoveSelectedFolder();
            }
        }

        private void RemoveSelectedFolder()
        {
            if (!HasCollection || CurrentCollection.Folders.Count <= 1)
                return;
            
            CurrentCollectionSerializedObject.Update();
            FoldersSerializedProperty.DeleteArrayElementAtIndex(SelectedFolderIndex);
            ApplyModifiedProperties();

            // Select the last folder.
            SelectedFolderIndex = CurrentCollection.Folders.Count - 1;
            
            UpdateFoldersTreeView();

            Repaint();
        }
        
        private void RenameSelectedFolder()
        {
            StartFolderRename(SelectedFolder);
        }

        public void RemoveSelectedEntries()
        {
            RemoveEntries(entriesSelected);

            ClearEntrySelection();
            Repaint();
        }

        private void StopAllRenames(bool isCancel)
        {
            StopEntryRename(isCancel);
            CancelFolderRename();
        }
        
        private void OnLostFocus()
        {
            StopAllRenames(false);
        }

        private void OnSelectionChange()
        {
            StopAllRenames(false);
        }

        private void OnFocus()
        {
            StopAllRenames(false);
        }

        private void OnProjectChange()
        {
            StopAllRenames(false);
            
            UpdateAndRepaint();
        }

        public void OnPaletteAssetImported()
        {
            StopAllRenames(false);
            
            UpdateAndRepaint();
        }
    }
}
