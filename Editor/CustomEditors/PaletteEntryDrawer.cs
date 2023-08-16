using System;
using System.IO;
using RoyTheunissen.AssetPalette.Extensions;
using RoyTheunissen.AssetPalette.Windows;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette.CustomEditors
{
    public abstract class PaletteEntryDrawerBase
    {
        [NonSerialized] private static GUIStyle cachedLabelStyle;
        [NonSerialized] private static bool didCacheLabelStyle;
        private static GUIStyle LabelStyle
        {
            get
            {
                if (!didCacheLabelStyle)
                {
                    didCacheLabelStyle = true;
                    cachedLabelStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                    {
                        alignment = TextAnchor.LowerCenter,
                        normal = {textColor = Color.white}
                    };
                }
                return cachedLabelStyle;
            }
        }
        
        [NonSerialized] private static GUIStyle cachedCustomLabelStyle;
        [NonSerialized] private static bool didCustomLabelStyle;
        private static GUIStyle CustomLabelStyle
        {
            get
            {
                if (!didCustomLabelStyle)
                {
                    didCustomLabelStyle = true;
                    cachedCustomLabelStyle = new GUIStyle(LabelStyle)
                    {
                        fontStyle = FontStyle.Bold,
                    };
                }
                return cachedCustomLabelStyle;
            }
        }

        protected GUIStyle GetLabelStyle(PaletteEntry entry)
        {
            return entry.HasCustomName ? CustomLabelStyle : LabelStyle;
        }
        
        public static Rect GetLabelRect(Rect position, PaletteEntry entry)
        {
            GUIContent label = new GUIContent(entry.Name);
            float height = LabelStyle.CalcHeight(label, position.width);
            return RectExtensions.GetSubRectFromBottom(position, height);
        }
        
        public static Rect GetRenameRect(Rect position, string name)
        {
            GUIContent label = new GUIContent(name);
            GUIStyle style = AssetPaletteWindowEntryPanel.GridEntryRenameTextStyle;
            float height = style.CalcHeight(label, position.width);
            return RectExtensions.GetSubRectFromBottom(position, height);
        }

        public abstract void OnGUI(Rect position, SerializedProperty property, PaletteEntry entry);
        public abstract void OnListGUI(Rect position, SerializedProperty property, PaletteEntry entry, bool isSelected);
    }
    
    /// <summary>
    /// Base class for drawing entries in the palette.
    /// </summary>
    public abstract class PaletteEntryDrawer<EntryType> : PaletteEntryDrawerBase
        where EntryType : PaletteEntry
    {
        protected virtual string OpenText => "Open";
        
        public override void OnGUI(Rect position, SerializedProperty property, PaletteEntry baseEntry)
        {
            EntryType entry = (EntryType)baseEntry;
            
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                position.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(entry, property);
                return;
            }

            // Draw a nice background.
            const float brightness = 0.325f;
            EditorGUI.DrawRect(position, new Color(brightness, brightness, brightness));

            // Draw the contents for this particular entry.
            DrawContents(position, property, entry);

            // Draw a label with a nice semi-transparent backdrop.
            // NOTE: For some reason, if we don't filter out non-repaint events here then drawing the label will cause
            // the Zoom Level control to lose focus when switching between list view and grid view.
            // Doesn't make sense to me. How can a label steal focus mid-drag?
            // NOTE: If an entry is being renamed in the grid view and we ONLY handle the repaint events here, then
            // for some reason the rename text field stops working correctly. Another weird bug. How can the label
            // rect even steal focus? It is not a named control...
            if ((Event.current.type == EventType.Repaint || PaletteEntry.IsEntryBeingRenamed) && !entry.IsRenaming)
            {
                GUIContent label = new GUIContent(entry.Name);
                Rect labelRect = GetLabelRect(position, entry);
                DrawLabel(position, labelRect, property, label, entry);
            }
        }

        /// <inheritdoc />
        public override void OnListGUI(Rect position, SerializedProperty property, PaletteEntry baseEntry, bool isSelected)
        {
            EntryType entry = (EntryType)baseEntry;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                position.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(entry, property);
                return;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            // This draws the backdrop of a Reorderable List Element to show that an entry has been selected.
            if (isSelected)
            {
                GUIStyle selectionStyle = "RL Element";
                selectionStyle.Draw(position, false, true, true, true);
            }

            Texture2D icon = GetListIcon(entry);
            if (icon == null)
            {
                DrawListEntry(position, property, entry);
            }
            else
            {
                bool isSelectedAndHasFocus =
                    isSelected && EditorWindow.focusedWindow == PaletteDrawing.ActivePaletteWindow;
                GUIContent content = new GUIContent(entry.Name, icon);
                EditorStyles.label.Draw(position, content, false, false, isSelectedAndHasFocus, isSelectedAndHasFocus);
            }
        }

        protected virtual Texture2D GetListIcon(EntryType entry)
        {
            return null;
        }

        protected abstract void DrawContents(Rect position, SerializedProperty property, EntryType entry);

        protected virtual void DrawListEntry(Rect position, SerializedProperty property, EntryType entry)
        {
            EditorGUI.LabelField(position, "No GUI implemented");
        }

        protected virtual void DrawLabel(
            Rect position, Rect labelPosition, SerializedProperty property, GUIContent label, EntryType entry)
        {
            EditorGUI.DrawRect(labelPosition, new Color(0, 0, 0, 0.3f));
            EditorGUI.LabelField(position, label, GetLabelStyle(entry));
        }

        private void ShowContextMenu(EntryType entry, SerializedProperty property)
        {
            GenericMenu menu = new GenericMenu();
            OnContextMenu(menu, entry, property);
            menu.ShowAsContext();
        }

        protected virtual void OnContextMenu(GenericMenu menu, EntryType entry, SerializedProperty property)
        {
            menu.AddItem(new GUIContent(OpenText), false, entry.Open);
            if (entry.CanRename)
                menu.AddItem(new GUIContent("Rename"), false, StartRename, entry);
            
            // Calling it "Delete" instead of "Remove" here to make it more distinct from "Rename" to avoid mistakes.
            menu.AddItem(new GUIContent("Delete"), false, DeleteSelectedEntries, entry);
        }

        private void StartRename(object userData)
        {
            EntryType entry = (EntryType)userData;
            AssetPaletteWindow window = EditorWindow.GetWindow<AssetPaletteWindow>();
            if (window == null)
                return;
            
            window.EntryPanel.StartEntryRename(entry);
        }

        private void DeleteSelectedEntries(object userData)
        {
            AssetPaletteWindow window = EditorWindow.GetWindow<AssetPaletteWindow>();
            if (window == null)
                return;
            
            window.RemoveSelectedEntries();
        }

        protected void ShowAssetInExplorer(Object asset)
        {
            string pathRelativeToProject = AssetDatabase.GetAssetPath(asset);
            string pathAbsolute = Path.GetFullPath(pathRelativeToProject);
            EditorUtility.RevealInFinder(pathAbsolute);
        }
    }
}
