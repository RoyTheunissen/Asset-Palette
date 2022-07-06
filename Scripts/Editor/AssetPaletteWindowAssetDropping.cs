using System;
using System.Collections.Generic;
using System.IO;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
    public partial class AssetPaletteWindow
    {
        public enum AddFolderBehaviour
        {
            Undefined,
            AddShortcutToFolder,
            AddFolderContents,
        }

        [NonSerialized] private AddFolderBehaviour addFolderBehaviour;
        [NonSerialized] private readonly List<Object> draggedObjectsToProcess = new List<Object>();
        [NonSerialized] private readonly List<PaletteEntry> entriesToAddFromDraggedAssets = new List<PaletteEntry>();
        
        private bool HasEntryForAsset(Object asset)
        {
            foreach (PaletteEntry entry in GetEntries())
            {
                if (entry is PaletteAsset paletteAsset && paletteAsset.Asset == asset)
                    return true;
            }
            return false;
        }

        private void HandleAssetDropping()
        {
            Event @event = Event.current;
            if (@event.type != EventType.DragUpdated && @event.type != EventType.DragPerform)
                return;

            // NOTE: Ignore assets that are being dragged OUT of the entries panel as opposed to being dragged INTO it.
            bool isValidDrag = isMouseInEntriesPanel && DragAndDrop.objectReferences.Length > 0 &&
                               DragAndDrop.GetGenericData(EntryDragGenericDataType) == null;
            if (!isValidDrag)
                return;

            DragAndDrop.AcceptDrag();
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (@event.type != EventType.DragPerform)
                return;

            // Determine what entries are to be added as a result of these assets being dropped. Note that we may have
            // to ask the user how they want to handle certain special assets like folders. Because of a Unity bug this
            // means that processing will stop, a context menu will be displayed, two frames will have to be waited,
            // and THEN processing can resume. This bug doesn't seem to happen with Dialogs, only context menus. I
            // prefer to use context menus regardless because it's faster and less jarring for the user.
            draggedObjectsToProcess.Clear();
            draggedObjectsToProcess.AddRange(DragAndDrop.objectReferences);
            entriesToAddFromDraggedAssets.Clear();
            addFolderBehaviour = AddFolderBehaviour.Undefined;
            ProcessDraggedObjects();
        }

        private void ResumeDraggedObjectProcessing()
        {
            // BUG: We need to wait two frames before we resume. Not sure why. It has something to do with GenericMenu
            // and serialized objects, I think. Might be related to threads? I couldn't find anything online.
            // If we don't wait, the new entries will be added but will disappear again immediately, without any call
            // being mode to remove the entries.
            EditorApplication.delayCall += () => EditorApplication.delayCall += ProcessDraggedObjects;
        }

        private void ProcessDraggedObjects()
        {
            while (draggedObjectsToProcess.Count > 0)
            {
                Object draggedObject = draggedObjectsToProcess[0];

                TryProcessDraggedObject(draggedObject, out bool needsToAskForUserInputFirst);

                if (needsToAskForUserInputFirst)
                    return;

                // We processed it!
                draggedObjectsToProcess.RemoveAt(0);
            }

            AddEntriesFromDraggedAssets();
        }

        private void TryProcessDraggedObject(Object draggedObject, out bool needsToAskForUserInputFirst)
        {
            needsToAskForUserInputFirst = false;

            string path = AssetDatabase.GetAssetPath(draggedObject);
            if (AssetDatabase.IsValidFolder(path))
            {
                // If we don't know what to do with folders, figure that out first.
                if (addFolderBehaviour == AddFolderBehaviour.Undefined)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(
                        new GUIContent("Add Folder Contents"), false, SetAddFolderBehaviourAndResumeProcessing,
                        AddFolderBehaviour.AddFolderContents);
                    menu.AddItem(
                        new GUIContent("Add Shortcut To Folder"), false, SetAddFolderBehaviourAndResumeProcessing,
                        AddFolderBehaviour.AddShortcutToFolder);
                    menu.ShowAsContext();
                    needsToAskForUserInputFirst = true;
                    return;
                }

                if (addFolderBehaviour == AddFolderBehaviour.AddFolderContents)
                    AddFolderContents(draggedObject);
                else if (addFolderBehaviour == AddFolderBehaviour.AddShortcutToFolder)
                    AddShortcutToFolder(draggedObject);
                return;
            }

            // Basically any Object is fine as long as it's not a scene GameObject.
            if ((!(draggedObject is GameObject go) || go.IsPrefab()) && !HasEntryForAsset(draggedObject))
                entriesToAddFromDraggedAssets.Add(new PaletteAsset(draggedObject));
        }

        private void AddEntriesFromDraggedAssets()
        {
            if (entriesToAddFromDraggedAssets.Count == 0)
                return;

            bool addedAnEntry = false;
            CurrentCollectionSerializedObject.Update();
            foreach (PaletteEntry entry in entriesToAddFromDraggedAssets)
            {
                if (!addedAnEntry)
                {
                    ClearEntrySelection();
                    addedAnEntry = true;
                }

                AddEntry(entry, true);
            }

            SortEntriesInSerializedObject();
            CurrentCollectionSerializedObject.ApplyModifiedProperties();
            entriesToAddFromDraggedAssets.Clear();

            Repaint();
        }

        private void SetAddFolderBehaviourAndResumeProcessing(object userdata)
        {
            addFolderBehaviour = (AddFolderBehaviour)userdata;

            ResumeDraggedObjectProcessing();
        }

        private void AddShortcutToFolder(Object folder)
        {
            entriesToAddFromDraggedAssets.Add(new PaletteAsset(folder));
        }

        private void AddFolderContents(Object folder)
        {
            string path = AssetDatabase.GetAssetPath(folder);

            // Find all the assets within this folder.
            List<string> assetsInDraggedFolder = new List<string>();
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            string draggedFolderPath = path + Path.AltDirectorySeparatorChar;
            foreach (string assetPath in allAssetPaths)
            {
                if (!assetPath.StartsWith(draggedFolderPath) || AssetDatabase.IsValidFolder(assetPath))
                    continue;

                assetsInDraggedFolder.Add(assetPath);
            }

            assetsInDraggedFolder.Sort();

            for (int i = 0; i < assetsInDraggedFolder.Count; i++)
            {
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetsInDraggedFolder[i]);
                if (HasEntryForAsset(asset))
                    continue;

                entriesToAddFromDraggedAssets.Add(new PaletteAsset(asset));
            }
        }
    }
}
