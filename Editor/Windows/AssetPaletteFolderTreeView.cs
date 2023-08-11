using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// Visualizes the palette's folders using a treeview.
    /// </summary>
    public class AssetPaletteFolderTreeView : TreeView
    {
        [NonSerialized] private SerializedProperty foldersProperty;

        [NonSerialized] private int lastItemIndex;

        [NonSerialized] private bool didInitialSelection;
        
        private Dictionary<int, PaletteFolder> itemIndexToFolder = new Dictionary<int, PaletteFolder>();
        
        public delegate void SelectedFolderHandler(AssetPaletteFolderTreeView treeView, PaletteFolder folder);
        public event SelectedFolderHandler SelectedFolderEvent;
        
        public delegate void RenamedFolderHandler(
            AssetPaletteFolderTreeView treeView, PaletteFolder folder, string oldName, string newName);
        public event RenamedFolderHandler RenamedFolderEvent;
        
        public AssetPaletteFolderTreeView(
            TreeViewState state, SerializedProperty foldersProperty, PaletteFolder selectedFolder)
            : base(state)
        {
            this.foldersProperty = foldersProperty;
            
            Reload();
            
            // Make sure we select whatever folder should currently be selected.
            TreeViewItem defaultSelectedItem = GetTreeViewItem(selectedFolder);
            SelectionClick(defaultSelectedItem, false);
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
        
        private PaletteFolder GetFolder(int id)
        {
            return itemIndexToFolder[id];
        }

        private TreeViewItem GetTreeViewItem(PaletteFolder folder)
        {
            foreach (KeyValuePair<int,PaletteFolder> kvp in itemIndexToFolder)
            {
                if (kvp.Value == folder)
                    return GetTreeViewItem(kvp.Key);
            }

            return null;
        }
        
        private TreeViewItem GetTreeViewItem(int id)
        {
            return FindItem(id, rootItem);
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return true;
        }

        public void BeginRename(PaletteFolder folder)
        {
            TreeViewItem item = GetTreeViewItem(folder);
            BeginRename(item);
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            base.RenameEnded(args);

            if (!args.acceptedRename)
                return;
            
            PaletteFolder folder = GetFolder(args.itemID);
            TreeViewItem item = GetTreeViewItem(args.itemID);
            item.displayName = args.newName;
            
            RenamedFolderEvent?.Invoke(this, folder, args.originalName, args.newName);
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
