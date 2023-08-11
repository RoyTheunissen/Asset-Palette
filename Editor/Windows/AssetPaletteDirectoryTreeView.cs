using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// Visualizes the palette's directories using a treeview.
    /// </summary>
    public class AssetPaletteDirectoryTreeView : TreeView
    {
        [NonSerialized] private SerializedProperty foldersProperty;

        [NonSerialized] private int lastItemIndex;

        [NonSerialized] private bool didInitialSelection;
        
        private Dictionary<int, PaletteFolder> itemIndexToFolder = new Dictionary<int, PaletteFolder>();
        
        public delegate void SelectedFolderHandler(AssetPaletteDirectoryTreeView treeView, PaletteFolder folder);
        public event SelectedFolderHandler SelectedFolderEvent;
        
        public AssetPaletteDirectoryTreeView(TreeViewState state, SerializedProperty foldersProperty)
            : base(state)
        {
            this.foldersProperty = foldersProperty;
            
            Reload();
            
            // Make sure the first item is always selected by default.
            SelectionClick(rootItem.children[0], false);
        }

        protected override TreeViewItem BuildRoot()
        {
            // BuildRoot is called every time Reload is called to ensure that TreeViewItems 
            // are created from data. Here we create a fixed set of items. In a real world example,
            // a data model should be passed into the TreeView and the items created from the model.

            // This section illustrates that IDs should be unique. The root item is required to 
            // have a depth of -1, and the rest of the items increment from that.
            TreeViewItem root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            itemIndexToFolder.Clear();
            for (int i = 0; i < foldersProperty.arraySize; i++)
            {
                SerializedProperty folderProperty = foldersProperty.GetArrayElementAtIndex(i);
                PaletteFolder folder = SerializedPropertyExtensions.GetValue<PaletteFolder>(folderProperty);
                TreeViewItem folderItem = new TreeViewItem(lastItemIndex++, 0, folder.Name);
                itemIndexToFolder.Add(folderItem.id, folder);
                root.AddChild(folderItem);
            }

            // Return root of the tree
            return root;
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);

            // The initial selection is not a selection "change" so we don't need to inform the window.
            if (!didInitialSelection)
            {
                didInitialSelection = true;
                return;
            }
            
            if (selectedIds.Count != 1)
                return;

            if (!itemIndexToFolder.TryGetValue(selectedIds[0], out PaletteFolder folder))
                return;
            
            SelectedFolderEvent?.Invoke(this, folder);
        }
    }
}
