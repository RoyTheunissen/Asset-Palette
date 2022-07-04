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
            // Draw the texture.
            PaletteAsset entry = property.GetValue<PaletteAsset>();
            if (entry.PreviewTexture != null)
            {
                EditorGUI.DrawPreviewTexture(
                    position, entry.PreviewTexture, null, ScaleMode.ScaleToFit, 0.0f);
            }
            else
            {
                const float brightness = 0.325f;
                EditorGUI.DrawRect(position, new Color(brightness, brightness, brightness));
            }
            
            // Draw a label with a nice semi-transparent backdrop.
            GUIContent title = new GUIContent(entry.Asset.name);
            float height = EntryNameTextStyle.CalcHeight(title, position.width);
                    
            EditorGUI.DrawRect(position.GetSubRectFromBottom(height), new Color(0, 0, 0, 0.15f));
            EditorGUI.LabelField(position, title, EntryNameTextStyle);
        }
    }
}
