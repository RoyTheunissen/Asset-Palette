using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Editor.Windows
{
    public partial class AssetPaletteWindow
    {
        private const string ZoomLevelControlName = "AssetPaletteEntriesZoomLevelControl";
        
        private float ZoomLevel
        {
            get
            {
                if (!EditorPrefs.HasKey(ZoomLevelEditorPref))
                    ZoomLevel = 0.25f;
                return EditorPrefs.GetFloat(ZoomLevelEditorPref);
            }
            set => EditorPrefs.SetFloat(ZoomLevelEditorPref, value);
        }

        private bool IsZoomLevelFocused => GUI.GetNameOfFocusedControl() == ZoomLevelControlName;
        
        private void DrawFooter()
        {
            Rect separatorRect = new Rect(
                FolderPanelWidth,
                position.height - FooterHeight, position.width - FolderPanelWidth, 1);

            EditorGUI.DrawRect(separatorRect, DividerColor);

            EditorGUILayout.BeginVertical(GUILayout.Height(FooterHeight));
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                {
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
