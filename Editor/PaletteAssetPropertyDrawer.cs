using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RectExtensions = RoyTheunissen.AssetPalette.Extensions.RectExtensions;
using SerializedPropertyExtensions = RoyTheunissen.AssetPalette.Extensions.SerializedPropertyExtensions;

namespace RoyTheunissen.AssetPalette.Editor
{
    /// <summary>
    /// Draws an Asset entry in the palette.
    /// </summary>
    [CustomPropertyDrawer(typeof(PaletteAsset))]
    public class PaletteAssetPropertyDrawer : PropertyDrawer
    {
        [NonSerialized] private GUIStyle cachedEntryNameTextStyle;
        [NonSerialized] private bool didCacheEntryNameTextStyle;
        private GUIStyle EntryNameTextStyle
        {
            get
            {
                if (!didCacheEntryNameTextStyle)
                {
                    didCacheEntryNameTextStyle = true;
                    cachedEntryNameTextStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                    {
                        alignment = TextAnchor.LowerCenter
                    };
                    cachedEntryNameTextStyle.normal.textColor = Color.white;
                }
                return cachedEntryNameTextStyle;
            }
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PaletteAsset entry;
            
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                position.Contains(Event.current.mousePosition))
            {
                entry = SerializedPropertyExtensions.GetValue<PaletteAsset>(property);
                ShowContextMenu(entry);
                return;
            }
            
            // OPTIMIZATION: Don't bother with any of this if we're not currently drawing.
            if (Event.current.type != EventType.Repaint)
                return;
            
            entry = SerializedPropertyExtensions.GetValue<PaletteAsset>(property);

            // Draw the texture.
            if (entry.PreviewTexture != null)
            {
                EditorGUI.DrawPreviewTexture(
                    position, entry.PreviewTexture, null, ScaleMode.ScaleToFit, 0.0f);
            }
            else
            {
                const float brightness = 0.325f;
                EditorGUI.DrawRect(position, new Color(brightness, brightness, brightness));
                
                // If we don't have a nice rendered preview, draw an icon instead.
                Texture2D iconTexture = AssetPreview.GetMiniThumbnail(entry.Asset);
                float width = Mathf.Min(iconTexture.width, position.width * 0.75f);
                Vector2 size = new Vector2(width, width);
                Rect iconRect = new Rect(position.center - size / 2, size);
                
                if (iconTexture != null)
                    GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit);
            }
            
            // Draw a label with a nice semi-transparent backdrop.
            GUIContent title = new GUIContent(entry.Name);
            float height = EntryNameTextStyle.CalcHeight(title, position.width);
            Rect labelRect = RectExtensions.GetSubRectFromBottom(position, height);
            EditorGUI.DrawRect(labelRect, new Color(0, 0, 0, 0.15f));
            EditorGUI.LabelField(position, title, EntryNameTextStyle);
        }

        private void ShowContextMenu(PaletteAsset entry)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open"), false, entry.Open);
            menu.AddItem(new GUIContent("Show In Project Window"), false, ShowInProjectWindow, entry);
            menu.AddItem(new GUIContent("Show In Explorer"), false, ShowInExplorer, entry);
            menu.ShowAsContext();
        }

        private void ShowInProjectWindow(object userData)
        {
            PaletteAsset entry = (PaletteAsset)userData;
            
            EditorGUIUtility.PingObject(entry.Asset);
        }

        private void ShowInExplorer(object userData)
        {
            PaletteAsset entry = (PaletteAsset)userData;

            string pathRelativeToProject = AssetDatabase.GetAssetPath(entry.Asset);
            string pathRelativeToAssetsFolder = Path.GetRelativePath("Assets", pathRelativeToProject);
            string pathAbsolute = Path.GetFullPath(pathRelativeToAssetsFolder, Application.dataPath);
            EditorUtility.RevealInFinder(pathAbsolute);
        }
    }
}
