using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoyTheunissen.AssetPalette.Runtime;
using UnityEditor;
using UnityEngine;
using GameObjectExtensions = RoyTheunissen.AssetPalette.Extensions.GameObjectExtensions;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette.Windows
{
    public partial class AssetPaletteWindow
    {
        [NonSerialized] private readonly List<Object> draggedObjectsToProcess = new List<Object>();
        [NonSerialized] private readonly List<PaletteEntry> entriesToAddFromDraggedAssets = new List<PaletteEntry>();
        
        [NonSerialized] private List<PotentialMacro> potentialMacros = new List<PotentialMacro>();
        [NonSerialized] private bool isDraggingAssetIntoEntryPanel;
        public bool IsDraggingAssetIntoEntryPanel
        {
            get => isDraggingAssetIntoEntryPanel;
            set => isDraggingAssetIntoEntryPanel = value;
        }

        [NonSerialized] private readonly List<Object> draggedAssets = new List<Object>();

        private PaletteEntry GetEntryForAsset(Object asset)
        {
            foreach (PaletteEntry entry in entryPanel.GetEntries())
            {
                if (entry is PaletteAsset paletteAsset && paletteAsset.Asset == asset)
                    return entry;
            }
            return null;
        }
        
        private bool HasEntryForAsset(Object asset)
        {
            return GetEntryForAsset(asset) != null;
        }

        private void HandleAssetDroppingInEntryPanel()
        {
            if (!isDraggingAssetIntoEntryPanel)
                return;

            DragAndDrop.AcceptDrag();
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (Event.current.type != EventType.DragPerform)
                return;

            HandleAssetDropping(DragAndDrop.objectReferences);
        }

        public void HandleAssetDropping(Object[] objectsToProcess)
        {
            // For processing you sometimes just want to know everything that was dragged.
            draggedAssets.Clear();
            draggedAssets.AddRange(objectsToProcess);
            
            // Determine what entries are to be added as a result of these assets being dropped. Note that we may have
            // to ask the user how they want to handle certain special cases like scripts. Because of a Unity bug this
            // means that processing will stop, a context menu will be displayed, two frames will have to be waited,
            // and THEN processing can resume. This bug doesn't seem to happen with Dialogs, only context menus. I
            // prefer to use context menus regardless because it's faster and less jarring for the user.
            draggedObjectsToProcess.Clear();
            draggedObjectsToProcess.AddRange(objectsToProcess);
            entriesToAddFromDraggedAssets.Clear();
            ProcessDraggedObjects();
        }
        
        private void ResumeDraggedObjectProcessing(bool didProcessCurrentDraggedObject)
        {
            if (didProcessCurrentDraggedObject)
                ProcessedCurrentDraggedObject();
            
            // BUG: We need to wait two frames before we resume. Not sure why. It has something to do with GenericMenu
            // and serialized objects, I think. Might be related to threads? I couldn't find anything online.
            // If we don't wait, the new entries will be added but will disappear again immediately, without any call
            // being mode to remove the entries.
            EditorApplication.delayCall += () => EditorApplication.delayCall += ProcessDraggedObjects;
        }

        private void ProcessedCurrentDraggedObject()
        {
            draggedObjectsToProcess.RemoveAt(0);
        }

        private void ProcessDraggedObjects()
        {
            while (draggedObjectsToProcess.Count > 0)
            {
                Object draggedObject = draggedObjectsToProcess[0];

                TryCreateEntriesForDraggedObject(draggedObject, out bool needsToAskForUserInputFirst);

                if (needsToAskForUserInputFirst)
                    return;

                // We processed it!
                ProcessedCurrentDraggedObject();
            }

            AddEntriesFromDraggedAssets();
        }

        private void TryCreateEntriesForDraggedObject(Object draggedObject, out bool needsToAskForUserInputFirst)
        {
            needsToAskForUserInputFirst = false;

            string path = AssetDatabase.GetAssetPath(draggedObject);

            bool didHandleSpecialImportCase = CheckUserInputForSpecialImportCases(draggedObject);
            if (didHandleSpecialImportCase)
            {
                needsToAskForUserInputFirst = true;
                return;
            }
            
            // If a folder is dragged in, add its contents.
            if (AssetDatabase.IsValidFolder(path))
            {
                CreateEntriesForFolderContents(draggedObject, ref needsToAskForUserInputFirst);
                return;
            }

            // Basically any Object is fine as long as it's not a scene GameObject.
            if (!HasEntryForAsset(draggedObject))
            {
                if (draggedObject is GameObject go && !GameObjectExtensions.IsPrefab(go))
                {
                    // You can't add scene game objects.
                    if (!PrefabUtility.IsPartOfAnyPrefab(go))
                        return;

                    // You CAN add an object from the scene if it's a valid prefab... 
                    draggedObject = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (draggedObject == null)
                        return;
                }
                
                //If this object is already on the list to be added, ignore it.
                if (entriesToAddFromDraggedAssets.Any(entry => entry is PaletteAsset paletteAsset && paletteAsset.Asset == draggedObject))
                    return;
                
                entriesToAddFromDraggedAssets.Add(new PaletteAsset(draggedObject));
            }
        }

        private bool CheckUserInputForSpecialImportCases(Object draggedObject)
        {
            // When you drag in ONE specific script, you may optionally add its static methods as macros.
            if (draggedObject is MonoScript script && draggedAssets.Count == 1)
            {
                potentialMacros.Clear();
                PotentialMacro.FindPotentialMacros(ref potentialMacros, script);

                if (potentialMacros.Count > 0)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent($"Add Script '{script.name}'"), false, AddEntryForScript, script);
                    foreach (PotentialMacro potentialMacro in potentialMacros)
                    {
                        menu.AddItem(
                            new GUIContent($"Add Macro '{potentialMacro.name}'"), false, AddEntryForMacro, potentialMacro);
                    }
                    menu.ShowAsContext();
                    return true;
                }
            }

            return false;
        }

        private void AddEntryForScript(object userData)
        {
            MonoScript script = (MonoScript)userData;
            
            entriesToAddFromDraggedAssets.Add(new PaletteAsset(script));
            
            // For processing special cases you can alternatively just define a behaviour to then apply to the rest of
            // the dragged objects. In that particular case, you resume but without processing the current dragged
            // object. We just wanted to know what to do with it first. In this case though, we specifically handle
            // the dragged object and make an entry for it.
            ResumeDraggedObjectProcessing(true);
        }

        private void AddEntryForMacro(object userData)
        {
            PotentialMacro potentialMacro = (PotentialMacro)userData;
            
            entriesToAddFromDraggedAssets.Add(new PaletteMacro(potentialMacro.script, potentialMacro.methodInfo.Name));
            
            // For processing special cases you can alternatively just define a behaviour to then apply to the rest of
            // the dragged objects. In that particular case, you resume but without processing the current dragged
            // object. We just wanted to know what to do with it first. In this case though, we specifically handle
            // the dragged object and make an entry for it.
            ResumeDraggedObjectProcessing(true);
        }

        private void AddEntriesFromDraggedAssets()
        {
            if (entriesToAddFromDraggedAssets.Count == 0)
                return;
            
            entryPanel.ClearEntrySelection();
            entryPanel.AddEntries(entriesToAddFromDraggedAssets);
            entriesToAddFromDraggedAssets.Clear();

            Repaint();
        }

        private void CreateEntriesForFolderContents(Object folder, ref bool needsToAskForUserInputFirst)
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
                
                TryCreateEntriesForDraggedObject(asset, out needsToAskForUserInputFirst);
            }
        }
    }
}
