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

        private SerializedObject SerializedObject => foldersProperty.serializedObject;

        [NonSerialized] private int lastItemIndex = 1;

        public bool IsDragging => isDragging;
        
        private bool isRenaming;
        public bool IsRenaming => isRenaming;

        private bool isDoingInitialSelection;

        private readonly List<AssetPaletteFolderTreeViewItem> items = new List<AssetPaletteFolderTreeViewItem>();

        public delegate void SelectedFolderHandler(AssetPaletteFolderTreeView treeView, SerializedProperty folderProperty);
        public event SelectedFolderHandler SelectedFolderEvent;
        
        public delegate void RenamedFolderHandler(
            AssetPaletteFolderTreeView treeView, SerializedProperty folderProperty, string oldName, string newName);
        public event RenamedFolderHandler RenamedFolderEvent;
        
        public delegate void MovedFolderHandler(AssetPaletteFolderTreeView treeView,
            SerializedProperty folderProperty, SerializedProperty targetFolderProperty, int toIndex);
        public event MovedFolderHandler MovedFolderEvent;
        
        public delegate void DeleteFolderRequestedHandler(
            AssetPaletteFolderTreeView treeView, SerializedProperty folderProperty);
        public event DeleteFolderRequestedHandler DeleteFolderRequestedEvent;
        
        public delegate void CreateFolderRequestedHandler(
            AssetPaletteFolderTreeView treeView, SerializedProperty parentFolderProperty);
        public event CreateFolderRequestedHandler CreateFolderRequestedEvent;
        
        public delegate void EntryMoveRequestedHandler(
            AssetPaletteFolderTreeView treeView, SerializedProperty[] entryProperties,
            SerializedProperty folderFromProperty, SerializedProperty folderToProperty);
        public event EntryMoveRequestedHandler EntryMoveRequestedEvent;
        
        public delegate void DroppedAssetsIntoFolderHandler(
            AssetPaletteFolderTreeView treeView, Object[] assets, SerializedProperty folderProperty);
        public event DroppedAssetsIntoFolderHandler DroppedAssetsIntoFolderEvent;
        
        public AssetPaletteFolderTreeView(
            TreeViewState state, SerializedProperty foldersProperty, PaletteFolder selectedFolder)
            : base(state)
        {
            this.foldersProperty = foldersProperty;
            
            Reload();
            
            // Make sure we select whatever folder should currently be selected.
            TreeViewItem defaultSelectedItem = GetItem(selectedFolder);
            isDoingInitialSelection = true;
            SelectionClick(defaultSelectedItem, false);
            isDoingInitialSelection = false;
        }

        protected override TreeViewItem BuildRoot()
        {
            TreeViewItem root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            // Add an item for every folder.
            AddChildFolders(root, foldersProperty);

            return root;
        }

        private void AddChildFolders(TreeViewItem parent, SerializedProperty folderListProperty)
        {
            for (int i = 0; i < folderListProperty.arraySize; i++)
            {
                SerializedProperty folderProperty = folderListProperty.GetArrayElementAtIndex(i);
                PaletteFolder folder = SerializedPropertyExtensions.GetValue<PaletteFolder>(folderProperty);
                AssetPaletteFolderTreeViewItem item = new AssetPaletteFolderTreeViewItem(
                    lastItemIndex++, parent.depth + 1, folder.Name, folderProperty, folder);

                parent.AddChild(item);
                items.Add(item);
                
                // Make sure that the folder is expanded if the property is expanded.
                SetExpanded(item.id, item.Property.isExpanded);

                // Keep going recursively until we've added all the children.
                SerializedProperty children = folderProperty.FindPropertyRelative(
                    FolderPanel.ChildFoldersPropertyName);
                AddChildFolders(item, children);
            }
        }

        protected override void ExpandedStateChanged()
        {
            base.ExpandedStateChanged();

            // Make sure that when an item is expanded or collapsed, we update the corresponding serialized property.
            bool didChange = false;
            foreach (AssetPaletteFolderTreeViewItem item in items)
            {
                bool isItemExpanded = IsExpanded(item.id);
                if (item.Property.isExpanded == isItemExpanded)
                    continue;
                
                item.Property.isExpanded = isItemExpanded;
                didChange = true;
            }

            if (didChange)
                SerializedObject.ApplyModifiedProperties();
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
            AssetPaletteFolderTreeViewItem draggedItem = GetItem(draggedItemID);
            PaletteFolder folder = draggedItem.Folder;
            
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(FolderDragGenericDataType, draggedItem);
            DragAndDrop.StartDrag($"{folder.Name} (Asset Palette Folder)");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            AssetPaletteFolderTreeViewItem itemDraggedInto = args.parentItem as AssetPaletteFolderTreeViewItem;
            
            bool canDoDrag = true;
            
            // Figure out if this is an entry drag.
            EntryDragData entryDragData = (EntryDragData)DragAndDrop.GetGenericData(
                EntryPanel.EntryDragGenericDataType);
            if (entryDragData != null)
            {
                for (int i = 0; i < entryDragData.EntryProperties.Count; i++)
                {
                    if (!CanDragEntries(
                        entryDragData.FolderDraggingFromPath, itemDraggedInto, args.dragAndDropPosition))
                    {
                        canDoDrag = false;
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                {
                    Object asset = DragAndDrop.objectReferences[i];
                    if (!CanDragAsset(asset, itemDraggedInto, args.dragAndDropPosition))
                    {
                        canDoDrag = false;
                        break;
                    }
                }
            }

            if (args.performDrop && canDoDrag)
                Drop(args);

            return canDoDrag ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.None;
        }
        
        private bool CanDragEntries(
            string folderDraggingFromPath, AssetPaletteFolderTreeViewItem itemDraggedInto, DragAndDropPosition position)
        {
            // Disallow dragging back into the folder it came from.
            if (itemDraggedInto != null && folderDraggingFromPath == itemDraggedInto.Path)
                return false;

            // Entries can only be dragged into a folder and not next to one.
            return position == DragAndDropPosition.UponItem;
        }

        private bool CanDragAsset(
            Object asset, AssetPaletteFolderTreeViewItem itemDraggedInto, DragAndDropPosition position)
        {
            // Folders can go anywhere. Between other folders, inside other folders, inside the root...
            if (asset.IsFolder())
                return true;
            
            // Files can only be dragged into a folder.
            return position == DragAndDropPosition.UponItem;
        }

        private void Drop(DragAndDropArgs args)
        {
            AssetPaletteFolderTreeViewItem itemDraggedInto = args.parentItem as AssetPaletteFolderTreeViewItem;
            PaletteFolder folderDraggedInto = itemDraggedInto?.Folder;
            SerializedProperty folderDraggedIntoProperty = itemDraggedInto?.Property;
            
            // Figure out if this is an entry drag.
            EntryDragData entryDragData = (EntryDragData)DragAndDrop.GetGenericData(
                EntryPanel.EntryDragGenericDataType);
            if (entryDragData != null)
            {
                DragAndDrop.SetGenericData(EntryPanel.EntryDragGenericDataType, null);
                DragAndDrop.AcceptDrag();
                
                EntryMoveRequestedEvent?.Invoke(this, entryDragData.EntryProperties.ToArray(),
                    entryDragData.FolderDraggingFromProperty, folderDraggedIntoProperty);
                return;
            }

            if (DragAndDrop.objectReferences.Length > 0)
            {
                DragAndDrop.AcceptDrag();
                
                // Assets are being dragged in from outside of the palette.
                DroppedAssetsIntoFolderEvent?.Invoke(this, DragAndDrop.objectReferences, folderDraggedIntoProperty);
                return;
            }

            // Probably a folder drag.
            AssetPaletteFolderTreeViewItem draggedItem = (AssetPaletteFolderTreeViewItem)DragAndDrop
                .GetGenericData(FolderDragGenericDataType);

            if (draggedItem == null)
            {
                Debug.LogWarning($"Dragged something into the Folder Panel that is neither a entry nor an asset nor " +
                                 $"a folder. Currently unsupported, probably an edge case!");
                return;
            }

            // If you dragged a folder outside, it should just be at the bottom of the root.
            if (args.insertAtIndex == -1)
            {
                if (folderDraggedInto == null)
                    args.insertAtIndex = rootItem.children.Count;
                else
                    args.insertAtIndex = folderDraggedInto.Children.Count;
            }

            DragAndDrop.SetGenericData(FolderDragGenericDataType, null);
            DragAndDrop.AcceptDrag();
            
            MovedFolderEvent?.Invoke(this, draggedItem.Property, folderDraggedIntoProperty, args.insertAtIndex);
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
            
            RenamedFolderEvent?.Invoke(this, item.Property, args.originalName, args.newName);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
            
            if (isDoingInitialSelection)
                return;

            if (selectedIds.Count != 1)
                return;

            AssetPaletteFolderTreeViewItem item = GetItem(selectedIds[0]);
            if (item == null)
                return;
            
            SelectedFolderEvent?.Invoke(this, item.Property);
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
                menu.AddItem(new GUIContent("Delete"), false, () => DeleteFolderRequestedEvent?.Invoke(this, item.Property));
            
            menu.AddItem(new GUIContent("Create Folder"), false, () => CreateNewFolder(item));

            menu.ShowAsContext();
            
            Event.current.Use();
        }

        private void CreateNewFolder(AssetPaletteFolderTreeViewItem parentItem = null)
        {
            CreateFolderRequestedEvent?.Invoke(this, parentItem?.Property);
        }

        protected override void ContextClicked()
        {
            base.ContextClicked();

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Create Folder"), false, () => CreateNewFolder());
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
