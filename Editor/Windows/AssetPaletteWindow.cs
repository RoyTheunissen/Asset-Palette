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

        // Editor prefs
        public static string EditorPrefPrefix => $"RoyTheunissen/PrefabPalette/{ProjectName}/";
        private static string CurrentCollectionGUIDEditorPref => EditorPrefPrefix + "CurrentCollectionGUID";
        private static string SelectedFolderReferenceIdPathEditorPref => EditorPrefPrefix + "SelectedFolderReferenceIdPath";

        private static string PersonalPaletteStorageKeyEditorPref => EditorPrefPrefix + "PersonalPaletteStorageKey";


        // Measurements
        public static float EntriesPanelWidthMin => AssetPaletteWindowHeader.RefreshButtonWidth
                        + AssetPaletteWindowHeader.AddSpecialButtonWidth + AssetPaletteWindowHeader.SortModeButtonWidth;
        private static float WindowWidthMin => AssetPaletteWindowFolderPanel.FolderPanelWidthMin + EntriesPanelWidthMin;
        private static readonly float WindowHeightMin = AssetPaletteWindowFooter.FooterHeight
                                                        + AssetPaletteWindowHeader.HeaderHeight
                                                        + Windows.EntryPanel.EntryPanelHeightMin;

        [NonSerialized] private bool isMouseInHeader;
        [NonSerialized] private bool isMouseInFooter;
        [NonSerialized] private bool isMouseInFolderPanel;
        
        [NonSerialized] private bool isMouseOverFolderPanelResizeBorder;
        public bool IsMouseOverFolderPanelResizeBorder => isMouseOverFolderPanelResizeBorder;
        
        [NonSerialized] private bool isMouseInEntriesPanel;
        public bool IsMouseInEntriesPanel => isMouseInEntriesPanel;

        public bool IsRenaming => PaletteEntry.IsEntryBeingRenamed || folderPanel.IsFolderBeingRenamed;

        private static Texture2D lightModeIcon;
        private static Texture2D darkModeIcon;

        private readonly AssetPaletteWindowFolderPanel folderPanel;
        public AssetPaletteWindowFolderPanel FolderPanel => folderPanel;
        
        private readonly EntryPanel entryPanel;
        public EntryPanel EntryPanel => entryPanel;
        
        private readonly AssetPaletteWindowFooter footer;
        public AssetPaletteWindowFooter Footer => footer;
        
        private AssetPaletteWindowHeader header;
        public AssetPaletteWindowHeader Header => header;

        public AssetPaletteWindow()
        {
            folderPanel = new AssetPaletteWindowFolderPanel(this);
            entryPanel = new EntryPanel(this);
            footer = new AssetPaletteWindowFooter(this);
            header = new AssetPaletteWindowHeader(this);
        }

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
            
            AssetPaletteAssetImporter.AssetsImportedEvent -= HandleAssetsImportedEvent;
            AssetPaletteAssetImporter.AssetsImportedEvent += HandleAssetsImportedEvent;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            
            AssetPaletteAssetImporter.AssetsImportedEvent -= HandleAssetsImportedEvent;
        }

        private void OnUndoRedoPerformed()
        {
            // Repaint otherwise it may have done the undo but you won't see the result.
            EditorApplication.delayCall += UpdateAndRepaint;
        }
        
        private void HandleAssetsImportedEvent(string[] importedAssetPaths)
        {
            if (CurrentCollection == null)
                return;

            // Now that we know that assets were imported, figure out if the current collection has been imported.
            string currentCollectionPath = AssetDatabase.GetAssetPath(CurrentCollection);
            for (int i = 0; i < importedAssetPaths.Length; i++)
            {
                if (importedAssetPaths[i] == currentCollectionPath)
                {
                    OnCurrentPaletteAssetImported();
                    return;
                }
            }
        }

        public void UpdateAndRepaint()
        {
            folderPanel.UpdateFoldersTreeView(false);
            Repaint();
        }

        private void OnGUI()
        {
            // Fall back to the personal palette when no valid palette asset is available.
            if (string.IsNullOrEmpty(CurrentCollectionGuid) || CurrentCollection == null)
                CurrentCollectionGuid = PersonalPaletteGuid;
            PaletteDrawing.ActivePaletteWindow = this;

            UpdateMouseOverStates();
            
            PerformKeyboardShortcuts();

            header.DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical();
                {
                    folderPanel.DrawFolderPanel();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                {
                    entryPanel.DrawEntriesPanel();

                    footer.DrawFooter();
                    
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
            isMouseInHeader = Event.current.mousePosition.y <= AssetPaletteWindowHeader.HeaderHeight;
            isMouseInFooter = Event.current.mousePosition.y >= position.height - AssetPaletteWindowFooter.FooterHeight;
            isMouseInFolderPanel = !isMouseInHeader
                                   && Event.current.mousePosition.x < folderPanel.FolderPanelWidth;
            isMouseOverFolderPanelResizeBorder = folderPanel.DividerResizeRect.Contains(Event.current.mousePosition);

            isMouseInEntriesPanel = !isMouseInHeader && !isMouseInFooter && !isMouseInFolderPanel;
            
            // Store this once so that specific entries can consider if they should be handling an asset drop, or 
            // if the entry panel as a whole should be handling it.
            isDraggingAssetIntoEntryPanel = (Event.current.type == EventType.DragUpdated ||
                            Event.current.type == EventType.DragPerform) && isMouseInEntriesPanel &&
                            DragAndDrop.objectReferences.Length > 0 &&
                            DragAndDrop.GetGenericData(EntryPanel.EntryDragGenericDataType) == null;
        }

        private void PerformKeyboardShortcuts()
        {
            if (Event.current.type != EventType.KeyDown || folderPanel.IsFolderBeingRenamed)
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

                // Busy with a rename, don't do any special shortcuts.
                return;
            }

            if (Event.current.keyCode == KeyCode.F2 && entryPanel.EntriesSelected.Count == 1)
            {
                entryPanel.StartEntryRename(entryPanel.EntriesSelected[0]);
                Event.current.Use();
                return;
            }

            // Allow all currently visible entries to be selected if CTRL+A is pressed. 
            if (Event.current.control && Event.current.keyCode == KeyCode.A)
            {
                entryPanel.SelectEntries(entryPanel.GetEntries(), true);
                if (entryPanel.GetEntryCount() > 0)
                    entryPanel.EntriesIndividuallySelected.Add(entryPanel.GetEntry(entryPanel.GetEntryCount() - 1));
                Repaint();
                return;
            }

            if ((Event.current.keyCode == KeyCode.Delete ||
                 Event.current.command && Event.current.keyCode == KeyCode.Backspace))
            {
                if (isMouseInEntriesPanel)
                    RemoveSelectedEntries();
                else if (isMouseInFolderPanel && !folderPanel.IsDraggingFolder)
                    folderPanel.RemoveSelectedFolder();
            }
        }

        public void RemoveSelectedEntries()
        {
            entryPanel.RemoveEntries(entryPanel.EntriesSelected);

            entryPanel.ClearEntrySelection();
            Repaint();
        }

        public void StopAllRenames(bool isCancel)
        {
            entryPanel.StopEntryRename(isCancel);
            folderPanel.CancelFolderRename();
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

        public void OnCurrentPaletteAssetImported()
        {
            StopAllRenames(false);

            // Let's be really thorough and just ditch the entire cache when the palette is changed externally.
            ClearCachedCollection();
            
            UpdateAndRepaint();
        }

        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            header.AddItemsToMenu(menu);
        }
    }
}
