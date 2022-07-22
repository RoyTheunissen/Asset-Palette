using System;
using RoyTheunissen.AssetPalette.Runtime;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.CustomEditors
{
    /// <summary>
    /// Draws an Asset entry in the palette.
    /// </summary>
    public class PaletteMacroDrawer : PaletteEntryDrawer<PaletteMacro>
    {
        private Texture2D cachedMacroIcon;
        private bool didCacheShortcutIcon;
        private Texture2D MacroIcon
        {
            get
            {
                if (!didCacheShortcutIcon)
                {
                    didCacheShortcutIcon = true;
                    cachedMacroIcon = Resources.Load<Texture2D>("Palette Macro Icon");
                }
                return cachedMacroIcon;
            }
        }

        protected override string OpenText => "Run";

        protected override void DrawContents(Rect position, SerializedProperty property, PaletteMacro entry)
        {
            // OPTIMIZATION: Don't bother with any of this if we're not currently drawing.
            if (Event.current.type != EventType.Repaint)
                return;

            float width = Mathf.Min(MacroIcon.width, position.width * 0.75f);
            Vector2 size = new Vector2(width, width);
            Rect iconRect = new Rect(position.center - size / 2, size);
                
            // Draw either a default icon or some custom one that the user dragged onto it.
            GUI.DrawTexture(iconRect, entry.HasCustomIcon ? entry.CustomIcon : MacroIcon, ScaleMode.ScaleToFit);
        }

        /// <inheritdoc />
        protected override void DrawListEntry(Rect position, SerializedProperty property, PaletteMacro entry)
        {
            // TODO: Implement
        }

        protected override void OnContextMenu(GenericMenu menu, PaletteMacro entry, SerializedProperty property)
        {
            base.OnContextMenu(menu, entry, property);
            
            menu.AddItem(new GUIContent("Edit Script"), false, EditScript, entry);
            menu.AddItem(new GUIContent("Show Script In Project Window"), false, ShowInProjectWindow, entry);
            menu.AddItem(new GUIContent("Show Script In Explorer"), false, ShowInExplorer, entry);
            if (entry.HasCustomIcon)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Remove Custom Icon"), false, RemoveCustomIcon, property);
            }
        }

        private void EditScript(object userData)
        {
            PaletteMacro entry = (PaletteMacro)userData;
            
            AssetDatabase.OpenAsset(entry.Script);
        }

        private void RemoveCustomIcon(object userData)
        {
            SerializedProperty entrySerializedProperty = (SerializedProperty)userData;
            
            entrySerializedProperty.serializedObject.Update();
            SerializedProperty customIconProperty = entrySerializedProperty.FindPropertyRelative("customIcon");
            customIconProperty.objectReferenceValue = null;
            entrySerializedProperty.serializedObject.ApplyModifiedProperties();
        }

        private void ShowInProjectWindow(object userData)
        {
            PaletteMacro entry = (PaletteMacro)userData;
            
            EditorGUIUtility.PingObject(entry.Script);
        }

        private void ShowInExplorer(object userData)
        {
            PaletteMacro entry = (PaletteMacro)userData;

            ShowAssetInExplorer(entry.Script);
        }
    }
}
