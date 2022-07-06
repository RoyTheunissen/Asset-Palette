using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoyTheunissen.AssetPalette.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Helps organize collections of prefabs and drag them into scenes quickly.
    /// </summary>
    public partial class AssetPaletteWindow : EditorWindow
    {
        private const string EditorPrefPrefix = "RoyTheunissen/PrefabPalette/";
        private const string CurrentCollectionGUIDEditorPref = EditorPrefPrefix + "CurrentCollectionGUID";
        private const string FolderPanelWidthEditorPref = EditorPrefPrefix + "FolderPanelWidth";
        private const string ZoomLevelEditorPref = EditorPrefPrefix + "ZoomLevel";
        private const string SelectedFolderIndexEditorPref = EditorPrefPrefix + "SelectedFolderIndex";

        private const string FolderDragGenericDataType = "AssetPaletteFolderDrag";
        private const string EntryDragGenericDataType = "AssetPaletteEntryDrag";

        private static float FolderPanelWidthMin => CollectionButtonWidth + NewFolderButtonWidth;
        private static float EntriesPanelWidthMin => 200;
        private static float PrefabPanelHeightMin => 50;
        private static float WindowWidthMin => FolderPanelWidthMin + EntriesPanelWidthMin;
        private static float CollectionButtonWidth => 130;
        private static bool HasMultipleFolderTypes => FolderTypes.Length > 1;
        private static int NewFolderButtonWidth => 76 + (HasMultipleFolderTypes ? 9 : 0);
        
        private static float HeaderHeight => EditorGUIUtility.singleLineHeight + 3;
        private static float FooterHeight => EditorGUIUtility.singleLineHeight + 6;
        private static readonly float WindowHeightMin = FooterHeight + HeaderHeight + PrefabPanelHeightMin;

        [NonSerialized] private bool isMouseInHeader;
        [NonSerialized] private bool isMouseInFooter;
        [NonSerialized] private bool isMouseInFolderPanel;
        [NonSerialized] private bool isMouseInEntriesPanel;
        
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

        private static EditorAssetReference<Texture2D> lightModeIcon = new EditorAssetReference<Texture2D>(
                "Assets/Asset-Palette/Textures/Editor/Resources/AssetPaletteWindow Icon.psd");
        private static EditorAssetReference<Texture2D> darkModeIcon = new EditorAssetReference<Texture2D>(
            "Assets/Asset-Palette/Textures/Editor/Resources/d_AssetPaletteWindow Icon.psd");

        [MenuItem ("Window/General/Asset Palette")]
        public static void Init() 
        {
            AssetPaletteWindow window = GetWindow<AssetPaletteWindow>(false);
            window.titleContent = new GUIContent(
                "Asset Palette", EditorGUIUtility.isProSkin ? darkModeIcon : lightModeIcon);
            window.minSize = new Vector2(WindowWidthMin, WindowHeightMin);
            window.wantsMouseMove = true;
        }

        private void OnGUI()
        {
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
                    HandleAssetDropping();

                    DrawEntriesPanel();

                    DrawFooter();
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
        }
        
        private void PerformKeyboardShortcuts()
        {
            if (Event.current.type != EventType.KeyDown)
                return;
            
            if (Event.current.keyCode == KeyCode.Return && PaletteFolder.IsFolderBeingRenamed)
            {
                StopFolderRename();
                return;
            }
            
            // Allow all currently visible entries to be selected if CTRL+A is pressed. 
            if (Event.current.control && Event.current.keyCode == KeyCode.A)
            {
                SelectEntries(GetEntries(), true);
                if (GetEntryCount() > 0)
                    entriesIndividuallySelected.Add(GetEntry(GetEntryCount() - 1));
                Repaint();
                return;
            }

            if (Event.current.keyCode == KeyCode.Delete)
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
                         !isDraggingFolder && !PaletteFolder.IsFolderBeingRenamed)
                {
                    CurrentCollectionSerializedObject.Update();
                    SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                    foldersProperty.DeleteArrayElementAtIndex(SelectedFolderIndex);
                    CurrentCollectionSerializedObject.ApplyModifiedProperties();

                    ClearCachedFolderSerializedProperties();
                    
                    Repaint();
                }
            }
        }
    }
}
