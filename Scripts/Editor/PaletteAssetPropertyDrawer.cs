using System;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.PrefabPalette.Editor
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
            // OPTIMIZATION: Don't bother with any of this if we're not currently drawing.
            if (Event.current.type != EventType.Repaint)
                return;
            
            PaletteAsset entry = property.GetValue<PaletteAsset>();

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
            GUIContent title = new GUIContent(entry.Asset.name);
            float height = EntryNameTextStyle.CalcHeight(title, position.width);
            Rect labelRect = position.GetSubRectFromBottom(height);
            EditorGUI.DrawRect(labelRect, new Color(0, 0, 0, 0.15f));
            EditorGUI.LabelField(position, title, EntryNameTextStyle);
        }
    }
}
