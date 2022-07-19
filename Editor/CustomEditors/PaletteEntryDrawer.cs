using System;
using System.IO;
using RoyTheunissen.AssetPalette.Windows;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using RectExtensions = RoyTheunissen.AssetPalette.Extensions.RectExtensions;
using SerializedPropertyExtensions = RoyTheunissen.AssetPalette.Extensions.SerializedPropertyExtensions;

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

        public abstract void OnGUI(Rect position, SerializedProperty property, PaletteEntry entry);
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
            GUIContent label = new GUIContent(entry.Name);
            Rect labelRect = GetLabelRect(position, entry);
            DrawLabel(position, labelRect, property, label, entry);
        }

        protected abstract void DrawContents(Rect position, SerializedProperty property, EntryType entry);

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
        }

        private void StartRename(object userData)
        {
            EntryType entry = (EntryType)userData;
            AssetPaletteWindow window = EditorWindow.GetWindow<AssetPaletteWindow>();
            if (window == null)
                return;
            
            window.StartEntryRename(entry);
        }

        protected void ShowAssetInExplorer(Object asset)
        {
            string pathRelativeToProject = AssetDatabase.GetAssetPath(asset);
            string pathRelativeToAssetsFolder = Path.GetRelativePath("Assets", pathRelativeToProject);
            string pathAbsolute = Path.GetFullPath(pathRelativeToAssetsFolder, Application.dataPath);
            EditorUtility.RevealInFinder(pathAbsolute);
        }
    }
}
