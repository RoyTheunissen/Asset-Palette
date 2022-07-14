using System;
using UnityEditor;
using UnityEngine;
using RectExtensions = RoyTheunissen.AssetPalette.Extensions.RectExtensions;
using SerializedPropertyExtensions = RoyTheunissen.AssetPalette.Extensions.SerializedPropertyExtensions;

namespace RoyTheunissen.AssetPalette.Editor
{
    /// <summary>
    /// Base class for drawing entries in the palette.
    /// </summary>
    public abstract class PaletteEntryPropertyDrawer<EntryType> : PropertyDrawer
        where EntryType : PaletteEntry
    {
        [NonSerialized] private GUIStyle cachedEntryNameTextStyle;
        [NonSerialized] private bool didCacheEntryNameTextStyle;
        protected GUIStyle EntryNameTextStyle
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
            EntryType entry;
            
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                position.Contains(Event.current.mousePosition))
            {
                entry = SerializedPropertyExtensions.GetValue<EntryType>(property);
                ShowContextMenu(entry);
                return;
            }
            
            entry = SerializedPropertyExtensions.GetValue<EntryType>(property);
            
            // Draw a nice background.
            const float brightness = 0.325f;
            EditorGUI.DrawRect(position, new Color(brightness, brightness, brightness));

            // Draw the contents for this particular entry.
            DrawContents(position, property, entry);

            // Draw a label with a nice semi-transparent backdrop.
            label = new GUIContent(entry.Name);
            float height = EntryNameTextStyle.CalcHeight(label, position.width);
            Rect labelRect = RectExtensions.GetSubRectFromBottom(position, height);
            DrawLabel(position, labelRect, property, label, entry);
        }

        protected abstract void DrawContents(Rect position, SerializedProperty property, EntryType entry);

        protected virtual void DrawLabel(
            Rect position, Rect labelPosition, SerializedProperty property, GUIContent label, EntryType entry)
        {
            EditorGUI.DrawRect(labelPosition, new Color(0, 0, 0, 0.15f));
            EditorGUI.LabelField(position, label, EntryNameTextStyle);
        }

        private void ShowContextMenu(EntryType entry)
        {
            GenericMenu menu = new GenericMenu();
            OnContextMenu(menu, entry);
            menu.ShowAsContext();
        }

        protected virtual void OnContextMenu(GenericMenu menu, EntryType entry)
        {
            menu.AddItem(new GUIContent("Open"), false, entry.Open);
        }
    }
}
