using System;
using System.IO;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using RectExtensions = RoyTheunissen.AssetPalette.Extensions.RectExtensions;
using TypeExtensions = RoyTheunissen.AssetPalette.Extensions.TypeExtensions;

namespace RoyTheunissen.AssetPalette.Editor.Windows
{
    public partial class AssetPaletteWindow
    {
        private const string AddEntryForCurrentSelectionText = "Add Shortcut For Project Window Selection";
        
        private string CurrentCollectionGuid
        {
            get => EditorPrefs.GetString(CurrentCollectionGUIDEditorPref);
            set
            {
                if (CurrentCollectionGuid == value)
                    return;
                
                EditorPrefs.SetString(CurrentCollectionGUIDEditorPref, value);
                
                // Need to re-cache the current collection now.
                cachedCurrentCollection = null;
                
                // Need to re-cache the serialized object, too.
                cachedCurrentCollectionSerializedObject?.Dispose();
                cachedCurrentCollectionSerializedObject = null;
            }
        }

        [NonSerialized] private AssetPaletteCollection cachedCurrentCollection;
        private AssetPaletteCollection CurrentCollection
        {
            get
            {
                if (cachedCurrentCollection == null)
                {
                    string guid = CurrentCollectionGuid;
                    
                    if (string.IsNullOrEmpty(guid))
                        return null;

                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                        return null;

                    cachedCurrentCollection = AssetDatabase.LoadAssetAtPath<AssetPaletteCollection>(path);
                }

                return cachedCurrentCollection;
            }
            set
            {
                if (value == CurrentCollection)
                    return;
                
                if (value == null)
                {
                    cachedCurrentCollection = null;
                    CurrentCollectionGuid = null;
                    return;
                }
                
                // Need to cache the serialized property now.
                didCacheSelectedFolderSerializedProperty = false;

                CurrentCollectionGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            }
        }

        private bool HasCollection => CurrentCollection != null;
        
        [NonSerialized] private SerializedObject cachedCurrentCollectionSerializedObject;
        private SerializedObject CurrentCollectionSerializedObject
        {
            get
            {
                if (cachedCurrentCollectionSerializedObject == null)
                {
                    cachedCurrentCollectionSerializedObject = new SerializedObject(CurrentCollection);
                    
                    // Need to re-cache these now.
                    didCacheSelectedFolderSerializedProperty = false;
                    didCacheSelectedFolderEntriesSerializedProperty = false;
                }
                return cachedCurrentCollectionSerializedObject;
            }
        }
        
        [NonSerialized] private static Type[] cachedFolderTypes;
        [NonSerialized] private static bool didCacheFolderTypes;
        private static Type[] FolderTypes
        {
            get
            {
                if (!didCacheFolderTypes)
                {
                    didCacheFolderTypes = true;
                    cachedFolderTypes = TypeExtensions.GetAllAssignableClasses(typeof(PaletteFolder), false, true);
                }
                return cachedFolderTypes;
            }
        }

        private void DrawHeader()
        {
            Rect headerRect = GUILayoutUtility.GetRect(position.width, HeaderHeight);
            GUI.Box(headerRect, GUIContent.none, EditorStyles.toolbar);

            AssetPaletteCollection currentCollection = CurrentCollection;
            string name = currentCollection == null ? "[No Collection]" : currentCollection.name;

            Rect folderPanelHeaderRect = RectExtensions.GetSubRectFromLeft(headerRect, FolderPanelWidth);
            DrawFolderPanelHeader(folderPanelHeaderRect, name);

            Rect entryPanelHeaderRect =
                RectExtensions.GetSubRectFromRight(headerRect, position.width - FolderPanelWidth);
            DrawEntryPanelHeader(entryPanelHeaderRect);
        }

        private void DrawFolderPanelHeader(Rect rect, string name)
        {
            // Allow a new collection to be created or loaded.
            Rect collectionRect = RectExtensions.GetSubRectFromLeft(rect, CollectionButtonWidth);
            bool createNewCollection = GUI.Button(collectionRect, name, EditorStyles.toolbarDropDown);
            if (createNewCollection)
                DoCollectionDropDown(collectionRect);

            // Allow a new folder to be created. Supports derived types of PaletteFolder as an experimental feature.
            Rect newFolderRect = RectExtensions.GetSubRectFromRight(rect, NewFolderButtonWidth);
            GUI.enabled = HasCollection;
            bool createNewFolder = GUI.Button(
                newFolderRect, "New Folder",
                HasMultipleFolderTypes ? EditorStyles.toolbarDropDown : EditorStyles.toolbarButton);
            GUI.enabled = true;
            if (createNewFolder)
            {
                if (HasMultipleFolderTypes)
                    DoCreateNewFolderDropDown(newFolderRect);
                else
                    CreateNewFolderFromDropDown(typeof(PaletteFolder));
            }
        }

        private void DrawEntryPanelHeader(Rect headerRect)
        {
            // Dropdown for changing the sort mode.
            Rect sortModeButtonRect = RectExtensions.GetSubRectFromRight(
                headerRect, SortModeButtonWidth, out Rect remainder);
            GUI.enabled = HasCollection;
            bool showSortModeRect = GUI.Button(
                sortModeButtonRect, SortMode.ToString().ToHumanReadable(), EditorStyles.toolbarDropDown);
            GUI.enabled = true;
            if (showSortModeRect)
                DoSortModeDropDown(sortModeButtonRect);

            // Dropdown for adding a special entry.
            Rect addEntryForProjectWindowSelectionRect = RectExtensions.GetSubRectFromRight(
                remainder, AddEntryForProjectWindowSelectionButtonWidth);
            bool addEntryForProjectWindowSelection = GUI.Button(
                addEntryForProjectWindowSelectionRect, "Add Special", EditorStyles.toolbarDropDown);
            if (addEntryForProjectWindowSelection)
                DoAddSpecialDropDown(addEntryForProjectWindowSelectionRect);
        }

        private void DoSortModeDropDown(Rect buttonRect)
        {
            GenericMenu menu = new GenericMenu();
            Array sortModesNames = Enum.GetNames(typeof(SortModes));
            Array sortModesValues = Enum.GetValues(typeof(SortModes));
            for (int i = 0; i < sortModesNames.Length; i++)
            {
                string name = ((string)sortModesNames.GetValue(i)).ToHumanReadable();
                GUIContent label = new GUIContent(name);
                int value = (int)sortModesValues.GetValue(i);
                
                // Note that we need to set the sort mode after a delay of one frame because there seems to be a bug
                // with modifying serialized objects straight out of a Generic Menu.
                menu.AddItem(
                    label, false,
                    userData => EditorApplication.delayCall +=
                        EditorApplication.delayCall += () => SetSortModeAndSortCurrentEntries((SortModes)value), value);
            }

            menu.DropDown(buttonRect);
        }

        private void SetSortModeAndSortCurrentEntries(SortModes sortMode)
        {
            SortMode = sortMode;
            
            // TODO: Maybe we should consider sorting the entries in ALL the folders at this time.. ?
            CurrentCollectionSerializedObject.Update();
            SortEntriesInSerializedObject();
            CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void DoCollectionDropDown(Rect collectionRect)
        {
            string[] existingCollectionGuids = AssetDatabase.FindAssets($"t:{typeof(AssetPaletteCollection).Name}");

            // Allow a new collection to be created.
            GenericMenu dropdownMenu = new GenericMenu();
            dropdownMenu.AddItem(new GUIContent("Create New"), false, CreateNewCollection);

            if (existingCollectionGuids.Length > 0)
                dropdownMenu.AddSeparator("");

            // Add any existing collections that we find as options.
            foreach (string collection in existingCollectionGuids)
            {
                bool isCurrentCollection = collection == CurrentCollectionGuid;
                string collectionName = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(collection));
                dropdownMenu.AddItem(
                    new GUIContent(collectionName), isCurrentCollection, LoadExistingCollection, collection);
            }

            dropdownMenu.DropDown(collectionRect);
        }

        private void CreateNewCollection()
        {
            string directory = Application.dataPath;
            string path = EditorUtility.SaveFilePanel("Create New Asset Palette", directory, "Asset Palette", "asset");
            if (string.IsNullOrEmpty(path))
                return;

            // Make the path relative to the project.
            path = Path.Combine("Assets", Path.GetRelativePath(Application.dataPath, path));

            Directory.CreateDirectory(path);

            AssetPaletteCollection newCollection = CreateInstance<AssetPaletteCollection>();
            AssetDatabase.CreateAsset(newCollection, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(newCollection);

            CurrentCollection = newCollection;

            Repaint();
        }

        private void LoadExistingCollection(object userdata)
        {
            string path = (string)userdata;
            string guid = AssetDatabase.AssetPathToGUID(path);
            CurrentCollectionGuid = guid;

            Repaint();
        }
        
        private void DoCreateNewFolderDropDown(Rect position)
        {
            GenericMenu dropdownMenu = new GenericMenu();

            foreach (Type type in FolderTypes)
            {
                string name = type.Name.RemoveSuffix("Folder").ToHumanReadable();
                dropdownMenu.AddItem(new GUIContent(name), false, CreateNewFolderFromDropDown, type);
            }

            dropdownMenu.DropDown(position);
        }
        
        private void CreateNewFolderFromDropDown(object userdata)
        {
            // Create a new instance of the specified folder type.
            PaletteFolder newFolder = CreateNewFolderOfType((Type)userdata, GetUniqueFolderName(NewFolderName));
            StartFolderRename(newFolder);
        }
        
        private bool HasProjectWindowSelection()
        {
            Object[] selectionFiltered = Selection.GetFiltered<Object>(SelectionMode.Assets);
            return selectionFiltered.Length > 0;
        }
        
        private void DoAddSpecialDropDown(Rect position)
        {
            GenericMenu dropdownMenu = new GenericMenu();

            DoAddSpecialDropDown(dropdownMenu, "");

            dropdownMenu.DropDown(position);
        }
        
        /// <summary>
        /// This is for right-clicking the window or clicking on the hamburger icon.
        /// </summary>
        public void AddItemsToMenu(GenericMenu menu)
        {
            DoAddSpecialDropDown(menu, "Add Special/");
        }

        private void DoAddSpecialDropDown(GenericMenu menu, string prefix)
        {
            GUIContent addEntryForCurrentSelectionLabel =
                new GUIContent(prefix + AddEntryForCurrentSelectionText);
            if (HasProjectWindowSelection())
                menu.AddItem(addEntryForCurrentSelectionLabel, false, AddEntryForProjectWindowSelection);
            else
                menu.AddDisabledItem(addEntryForCurrentSelectionLabel, false);
        }
    }
}
