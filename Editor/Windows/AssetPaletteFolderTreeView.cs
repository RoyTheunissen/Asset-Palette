using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// Visualizes the palette's folders using a treeview.
    /// </summary>
    public class AssetPaletteFolderTreeView : TreeView
    {
        private const string FolderDragGenericDataType = "AssetPaletteFolderDrag";

        private const float FolderSpacing = 1;
        
        [NonSerialized] private readonly SerializedProperty foldersProperty;

        [NonSerialized] private int lastItemIndex = 1;

        [NonSerialized] private bool didInitialSelection;

        public bool IsDragging => isDragging;
        
        private bool isRenaming;
        public bool IsRenaming => isRenaming;
        
        private bool IsDraggingAssets => DragAndDrop.objectReferences.Length > 0;

        private readonly List<AssetPaletteFolderTreeViewItem> items = new List<AssetPaletteFolderTreeViewItem>();
        
        private string FolderDraggedFromName => (string)DragAndDrop.GetGenericData(
            AssetPaletteWindow.EntryDragGenericDataType);

        public delegate void SelectedFolderHandler(AssetPaletteFolderTreeView treeView, PaletteFolder folder);
        public event SelectedFolderHandler SelectedFolderEvent;
        
        public delegate void RenamedFolderHandler(
            AssetPaletteFolderTreeView treeView, PaletteFolder folder, string oldName, string newName);
        public event RenamedFolderHandler RenamedFolderEvent;
        
        public delegate void MovedFolderHandler(AssetPaletteFolderTreeView treeView, PaletteFolder folder, int toIndex);
        public event MovedFolderHandler MovedFolderEvent;
        
        public delegate void DeleteFolderRequestedHandler(AssetPaletteFolderTreeView treeView, PaletteFolder folder);
        public event DeleteFolderRequestedHandler DeleteFolderRequestedEvent;
        
        public delegate void CreateFolderRequestedHandler(AssetPaletteFolderTreeView treeView);
        public event CreateFolderRequestedHandler CreateFolderRequestedEvent;
        
        public delegate void DroppedAssetsIntoFolderHandler(
            AssetPaletteFolderTreeView treeView, Object[] assets, PaletteFolder folder, bool isDraggedFromFolder);
        public event DroppedAssetsIntoFolderHandler DroppedAssetsIntoFolderEvent;
        
        public AssetPaletteFolderTreeView(
            TreeViewState state, SerializedProperty foldersProperty, PaletteFolder selectedFolder)
            : base(state)
        {
            this.foldersProperty = foldersProperty;
            
            Reload();
            
            // Make sure we select whatever folder should currently be selected.
            TreeViewItem defaultSelectedItem = GetItem(selectedFolder);
            SelectionClick(defaultSelectedItem, false);
        }

        protected override TreeViewItem BuildRoot()
        {
            TreeViewItem root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            // Add an item for every folder.
            for (int i = 0; i < foldersProperty.arraySize; i++)
            {
                SerializedProperty folderProperty = foldersProperty.GetArrayElementAtIndex(i);
                PaletteFolder folder = SerializedPropertyExtensions.GetValue<PaletteFolder>(folderProperty);
                AssetPaletteFolderTreeViewItem item = new AssetPaletteFolderTreeViewItem(
                    lastItemIndex++, 0, folder.Name, folderProperty, folder);
                root.AddChild(item);
                items.Add(item);
            }
            
            return root;
        }
        private AssetPaletteFolderTreeViewItem GetItem(PaletteFolder folder)
        {
            foreach (AssetPaletteFolderTreeViewItem item in items)
            {
                if (item.Folder == folder)
                    return item;
            }

            return null;
        }
        
        private AssetPaletteFolderTreeViewItem GetItem(int id)
        {
            return (AssetPaletteFolderTreeViewItem)FindItem(id, rootItem);
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return true;
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            return items.Count > 1;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            int draggedItemID = args.draggedItemIDs[0];
            PaletteFolder folder = GetItem(draggedItemID).Folder;
            
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(FolderDragGenericDataType, folder);
            DragAndDrop.StartDrag($"{folder.Name} (Asset Palette Folder)");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (args.performDrop)
                Drop(args);
            
            if (IsDraggingAssets)
            {
                AssetPaletteFolderTreeViewItem itemDraggedInto = args.parentItem as AssetPaletteFolderTreeViewItem;
                PaletteFolder folderDraggedInto = itemDraggedInto?.Folder;

                return args.dragAndDropPosition == DragAndDropPosition.UponItem
                       && folderDraggedInto != null && FolderDraggedFromName != folderDraggedInto.Name
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.None;
            }

            return args.dragAndDropPosition != DragAndDropPosition.UponItem
                ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
        }

        private void Drop(DragAndDropArgs args)
        {
            AssetPaletteFolderTreeViewItem itemDraggedInto = args.parentItem as AssetPaletteFolderTreeViewItem;
            PaletteFolder folderDraggedInto = itemDraggedInto?.Folder;
            
            if (IsDraggingAssets)
            {
                bool isDraggingEntriesFromAFolder = !string.IsNullOrEmpty(FolderDraggedFromName);
                DroppedAssetsIntoFolderEvent?.Invoke(
                    this, DragAndDrop.objectReferences, folderDraggedInto, isDraggingEntriesFromAFolder);
                return;
            }
            
            PaletteFolder folderDragged = (PaletteFolder)DragAndDrop.GetGenericData(FolderDragGenericDataType);

            // If you dragged a folder outside, it should just be at the bottom of the root.
            if (folderDraggedInto == null && args.insertAtIndex == -1)
                args.insertAtIndex = rootItem.children.Count;

            DragAndDrop.SetGenericData(FolderDragGenericDataType, null);
            MovedFolderEvent?.Invoke(this, folderDragged, args.insertAtIndex);
        }

        public void BeginRename(PaletteFolder folder)
        {
            TreeViewItem item = GetItem(folder);
            BeginRename(item);
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            base.RenameEnded(args);
            
            isRenaming = false;

            if (!args.acceptedRename)
                return;
            
            AssetPaletteFolderTreeViewItem item = GetItem(args.itemID);
            item.displayName = args.newName;
            
            RenamedFolderEvent?.Invoke(this, item.Folder, args.originalName, args.newName);
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

            AssetPaletteFolderTreeViewItem item = GetItem(selectedIds[0]);
            if (item == null)
                return;
            
            SelectedFolderEvent?.Invoke(this, item.Folder);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);

            AssetPaletteFolderTreeViewItem item = GetItem(id);
            PaletteFolder folder = item.Folder;
            if (folder == null)
                return;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rename"), false, () => BeginRename(folder));
            
            if (items.Count > 1)
                menu.AddItem(new GUIContent("Delete"), false, () => DeleteFolderRequestedEvent?.Invoke(this, folder));
            
            menu.AddItem(new GUIContent("Create Folder"), false, () => CreateFolderRequestedEvent?.Invoke(this));
            
            menu.ShowAsContext();
            
            Event.current.Use();
        }

        protected override void ContextClicked()
        {
            base.ContextClicked();

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Create Folder"), false, () => CreateFolderRequestedEvent?.Invoke(this));
            menu.ShowAsContext();
        }
        
        protected override float GetCustomRowHeight(int row, TreeViewItem item)
        {
            return base.GetCustomRowHeight(row, item) + FolderSpacing * 2;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            args.rowRect.y += FolderSpacing;
            args.rowRect.height -= FolderSpacing * 2;

            base.RowGUI(args);

            // This is the only way we can detect if we're currently renaming or not.
            if (args.isRenaming)
                isRenaming = true;
        }
    }
}