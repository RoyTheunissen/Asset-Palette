using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Editor.CustomEditors
{
    /// <summary>
    /// Draws an Asset entry in the palette.
    /// </summary>
    [CustomPropertyDrawer(typeof(PaletteAsset))]
    public class PaletteAssetPropertyDrawer : PaletteEntryPropertyDrawer<PaletteAsset>
    {
        protected override void DrawContents(Rect position, SerializedProperty property, PaletteAsset entry)
        {
            // OPTIMIZATION: Don't bother with any of this if we're not currently drawing.
            if (Event.current.type != EventType.Repaint)
                return;

            // Draw the texture.
            if (entry.PreviewTexture != null)
            {
                EditorGUI.DrawPreviewTexture(
                    position, entry.PreviewTexture, null, ScaleMode.ScaleToFit, 0.0f);
            }
            else
            {
                // If we don't have a nice rendered preview, draw an icon instead.
                Texture2D iconTexture = AssetPreview.GetMiniThumbnail(entry.Asset);
                float width = Mathf.Min(iconTexture.width, position.width * 0.75f);
                Vector2 size = new Vector2(width, width);
                Rect iconRect = new Rect(position.center - size / 2, size);
                
                if (iconTexture != null)
                    GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit);
            }
        }

        protected override void OnContextMenu(GenericMenu menu, PaletteAsset entry, SerializedProperty property)
        {
            base.OnContextMenu(menu, entry, property);
            
            menu.AddItem(new GUIContent("Show In Project Window"), false, ShowInProjectWindow, entry);
            menu.AddItem(new GUIContent("Show In Explorer"), false, ShowInExplorer, entry);
        }

        private void ShowInProjectWindow(object userData)
        {
            PaletteAsset entry = (PaletteAsset)userData;
            
            EditorGUIUtility.PingObject(entry.Asset);
        }

        private void ShowInExplorer(object userData)
        {
            PaletteAsset entry = (PaletteAsset)userData;

            ShowAssetInExplorer(entry.Asset);
        }
    }
}
