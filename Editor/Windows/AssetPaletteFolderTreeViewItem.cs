using System;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;

#if UNITY_6000_5_OR_NEWER
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
    using TreeView = UnityEditor.IMGUI.Controls.TreeView;
    using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem;
    using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
#endif

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// One entry in the palette folders tree view.
    /// </summary>
    public class AssetPaletteFolderTreeViewItem : TreeViewItem
    {
        private SerializedProperty property;
        public SerializedProperty Property => property;

        [NonSerialized] private PaletteFolder folder;
        public PaletteFolder Folder => folder;

        public string Path => property.GetIdPath("name", "children");

        public AssetPaletteFolderTreeViewItem(
            int id, int depth, string displayName, SerializedProperty property, PaletteFolder folder)
            : base(id, depth, displayName)
        {
            this.property = property;
            this.folder = folder;
        }
    }
}
