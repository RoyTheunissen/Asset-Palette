using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// One entry in the palette folders tree view.
    /// </summary>
    public class AssetPaletteFolderTreeViewItem : TreeViewItem
    {
        [NonSerialized] private SerializedProperty property;
        public SerializedProperty Property => property;

        [NonSerialized] private PaletteFolder folder;
        public PaletteFolder Folder => folder;

        public AssetPaletteFolderTreeViewItem(
            int id, int depth, string displayName, SerializedProperty property, PaletteFolder folder)
            : base(id, depth, displayName)
        {
            this.property = property;
            this.folder = folder;
        }
    }
}
