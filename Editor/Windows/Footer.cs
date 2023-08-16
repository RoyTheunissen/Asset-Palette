using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Windows
{
    public sealed class Footer
    {
        // Editor prefs
        private static string ZoomLevelEditorPref => AssetPaletteWindow.EditorPrefPrefix + "ZoomLevel";
        
        // Measurements
        public static float FooterHeight => EditorGUIUtility.singleLineHeight + 6;
        
        private const string ZoomLevelControlName = "AssetPaletteEntriesZoomLevelControl";

        public float ZoomLevel
        {
            get
            {
                if (!EditorPrefs.HasKey(ZoomLevelEditorPref))
                    ZoomLevel = 0.25f;
                return EditorPrefs.GetFloat(ZoomLevelEditorPref);
            }
            set => EditorPrefs.SetFloat(ZoomLevelEditorPref, value);
        }
        
        private List<Object> entryAssetsWhosePathToShow = new List<Object>();

        public bool IsZoomLevelFocused => GUI.GetNameOfFocusedControl() == ZoomLevelControlName;
        
        private AssetPaletteWindow window;

        public Footer(AssetPaletteWindow window)
        {
            this.window = window;
        }

        public void DrawFooter()
        {
            Rect separatorRect = new Rect(
                window.FolderPanel.FolderPanelWidth,
                window.position.height - FooterHeight, window.position.width - window.FolderPanel.FolderPanelWidth, 1);

            EditorGUI.DrawRect(separatorRect, FolderPanel.DividerColor);

            EditorGUILayout.BeginVertical(GUILayout.Height(FooterHeight));
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                {
                    // Draw the asset path of the first asset that is selected. That's how the Project View does it.
                    foreach (PaletteEntry paletteEntry in window.EntryPanel.EntriesSelected)
                    {
                        entryAssetsWhosePathToShow.Clear();
                        paletteEntry.GetAssetsToSelect(ref entryAssetsWhosePathToShow);
                        if (entryAssetsWhosePathToShow.Count > 0)
                        {
                            Object objectToShow = entryAssetsWhosePathToShow[0];
                            string path = AssetDatabase.GetAssetPath(objectToShow);
                            Texture icon = 
                                //EditorGUIUtility.GetIconForObject(objectToShow)
                                AssetDatabase.GetCachedIcon(path)
                                ;
                            GUIContent guiContent = new GUIContent(path, icon);
                            //EditorGUILayout.LabelField(guiContent);
                            EditorGUIUtility.SetIconSize(Vector2.one * 14);
                            Rect pathRect = GUILayoutUtility.GetRect(guiContent, EditorStyles.label);
                            EditorGUI.LabelField(pathRect, guiContent);
                            EditorGUIUtility.SetIconSize(Vector2.zero);
                            break;
                        }
                    }
                    
                    GUILayout.FlexibleSpace();
                    Rect zoomLevelRect = GUILayoutUtility.GetRect(80, EditorGUIUtility.singleLineHeight);

                    GUI.SetNextControlName(ZoomLevelControlName);
                    ZoomLevel = GUI.HorizontalSlider(zoomLevelRect, ZoomLevel, 0.0f, 1.0f);

                    GUILayout.Space(16);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndVertical();
        }
    }
}
