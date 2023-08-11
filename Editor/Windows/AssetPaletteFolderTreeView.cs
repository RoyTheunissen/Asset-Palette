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
        
        [NonSerialized] private SerializedProperty foldersProperty;

        [NonSerialized] private int lastItemIndex = 1;

        [NonSerialized] private bool didInitialSelection;

        public bool IsDragging => isDragging;
        
        private bool isRenaming;
        public bool IsRenaming => isRenaming;
        
        private bool IsDraggingAssets => DragAndDrop.objectReferences.Length > 0;

        private readonly Dictionary<int, PaletteFolder> itemIndexToFolder = new Dictionary<int, PaletteFolder>();
        
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
            TreeViewItem defaultSelectedItem = GetTreeViewItem(selectedFolder);
            SelectionClick(defaultSelectedItem, false);
        }

        protected override TreeViewItem BuildRoot()
        {
            TreeViewItem root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            // Add an item for every folder.
            itemIndexToFolder.Clear();
            for (int i = 0; i < foldersProperty.arraySize; i++)
            {
                SerializedProperty folderProperty = foldersProperty.GetArrayElementAtIndex(i);
                PaletteFolder folder = SerializedPropertyExtensions.GetValue<PaletteFolder>(folderProperty);
                TreeViewItem folderItem = new TreeViewItem(lastItemIndex++, 0, folder.Name);
                itemIndexToFolder.Add(folderItem.id, folder);
                root.AddChild(folderItem);
            }
            
            return root;
        }
        
        private PaletteFolder GetFolder(int id)
        {
            return id != 0 ? itemIndexToFolder[id] : null;
        }
        
        private PaletteFolder GetFolder(TreeViewItem item)
        {
            return item == null ? null : GetFolder(item.id);
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

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            return itemIndexToFolder.Count > 1;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            int draggedItemID = args.draggedItemIDs[0];
            PaletteFolder folder = GetFolder(draggedItemID);
            
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
                PaletteFolder folderDraggedInto = GetFolder(args.parentItem);

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
            PaletteFolder parentFolder = args.parentItem == null ? null : GetFolder(args.parentItem.id);
            
            if (IsDraggingAssets)
            {
                bool isDraggingEntriesFromAFolder = !string.IsNullOrEmpty(FolderDraggedFromName);
                DroppedAssetsIntoFolderEvent?.Invoke(
                    this, DragAndDrop.objectReferences, parentFolder, isDraggingEntriesFromAFolder);
                return;
            }
            
            PaletteFolder folderDragged = (PaletteFolder)DragAndDrop.GetGenericData(FolderDragGenericDataType);

            // If you dragged a folder outside, it should just be at the bottom of the root.
            if (parentFolder == null && args.insertAtIndex == -1)
                args.insertAtIndex = rootItem.children.Count;

            DragAndDrop.SetGenericData(FolderDragGenericDataType, null);
            MovedFolderEvent?.Invoke(this, folderDragged, args.insertAtIndex);
        }

        public void BeginRename(PaletteFolder folder)
        {
            TreeViewItem item = GetTreeViewItem(folder);
            BeginRename(item);
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            base.RenameEnded(args);
            
            isRenaming = false;

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

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);

            PaletteFolder folder = GetFolder(id);
            if (folder == null)
                return;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rename"), false, () => BeginRename(folder));
            
            if (itemIndexToFolder.Count > 1)
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
