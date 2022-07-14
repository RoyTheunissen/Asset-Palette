using RoyTheunissen.AssetPalette.Runtime;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Editor
{
    /// <summary>
    /// Draws an entry in the palette that represents a shortcut to a Project Window selection.
    /// </summary>
    [CustomPropertyDrawer(typeof(PaletteSelectionShortcut))]
    public class PaletteSelectionShortcutPropertyDrawer : PaletteEntryPropertyDrawer<PaletteSelectionShortcut>
    {
        private const int IconsToShowMax = 10;
        
        private Texture2D cachedShortcutIcon;
        private bool didCacheShortcutIcon;
        private Texture2D ShortcutIcon
        {
            get
            {
                if (!didCacheShortcutIcon)
                {
                    didCacheShortcutIcon = true;
                    cachedShortcutIcon = Resources.Load<Texture2D>("Palette Shortcut Icon");
                }
                return cachedShortcutIcon;
            }
        }
        
        protected override void DrawContents(Rect position, SerializedProperty property, PaletteSelectionShortcut entry)
        {
            // OPTIMIZATION: Don't bother with any of this if we're not currently drawing.
            if (Event.current.type != EventType.Repaint)
                return;

            int itemsToShowCount = Mathf.Min(IconsToShowMax, entry.Selection.Length);
            float density = (float)itemsToShowCount / IconsToShowMax;
            
            float iconSize = position.width * 0.55f * Mathf.Lerp(1.0f, 0.75f, density);
            float offsetDistance = position.width * Mathf.Lerp(0.1f, 0.025f, density);
            Vector2 offset = new Vector2(offsetDistance, offsetDistance);
            Vector2 offsetMax = itemsToShowCount == 1 ? Vector2.zero : offset * itemsToShowCount;
            
            for (int i = 0; i < itemsToShowCount; i++)
            {
                Object asset = entry.Selection[i];
                
                if (asset == null)
                    continue;
                
                Vector2 iconOffset = -offsetMax * 0.5f + offset * i;

                Texture2D iconTexture = AssetPreview.GetMiniThumbnail(asset);
                float width = Mathf.Min(iconTexture.width, iconSize);
                Vector2 size = new Vector2(width, width);
                Rect iconRect = new Rect(position.center + iconOffset - size / 2, size);
                
                if (iconTexture != null)
                    GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit);
            }

            // Also draw a shortcut icon in the corner, for clarity.
            float shortcutIconSize = Mathf.RoundToInt(position.width * 0.25f);
            Rect shortcutIconRect = new Rect(
                position.xMin, position.yMin, shortcutIconSize, shortcutIconSize);
            GUI.DrawTexture(shortcutIconRect, ShortcutIcon, ScaleMode.ScaleToFit);
        }
    }
}
