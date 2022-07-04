using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.PrefabPalette
{
    /// <summary>
    /// Helps organize collections of prefabs and drag them into scenes quickly.
    /// </summary>
    public class AssetPaletteWindow : EditorWindow
    {
        private const string EditorPrefPrefix = "RoyTheunissen/PrefabPalette/";
        private const string CurrentCollectionGUIDEditorPref = EditorPrefPrefix + "CurrentCollectionGUID";
        private const string FolderPanelWidthEditorPref = EditorPrefPrefix + "FolderPanelWidth";
        private const string ZoomLevelEditorPref = EditorPrefPrefix + "ZoomLevel";
        private const string SelectedFolderIndexEditorPref = EditorPrefPrefix + "SelectedFolderIndex";

        private const string FolderDragGenericDataType = "AssetPaletteFolderDrag";
        
        private const string FileSearchPattern = "*.asset, *.prefab, *.unity";

        private const float EntrySizeMax = PaletteEntry.TextureSize;
        private const float EntrySizeMin = PaletteEntry.TextureSize * 0.45f;
        
        private static float FolderPanelWidthMin => CollectionButtonWidth + NewFolderButtonWidth;
        private static float EntriesPanelWidthMin => 200;
        private static float PrefabPanelHeightMin => 50;
        private static float WindowWidthMin => FolderPanelWidthMin + EntriesPanelWidthMin;
        public static float CollectionButtonWidth => 130;
        private static bool HasMultipleFolderTypes => FolderTypes.Length > 1;
        private static int NewFolderButtonWidth => 76 + (HasMultipleFolderTypes ? 9 : 0);
        
        private static float HeaderHeight => EditorGUIUtility.singleLineHeight + 3;
        private static float FooterHeight => EditorGUIUtility.singleLineHeight + 6;
        private static readonly float WindowHeightMin = FooterHeight + HeaderHeight + PrefabPanelHeightMin;
        
        private const float DividerBrightness = 0.13f;
        private static readonly Color DividerColor = new Color(DividerBrightness, DividerBrightness, DividerBrightness);

        private const string InitialFolderName = "Default";
        private const string NewFolderName = "New Folder";
        private const int MaxUniqueFolderNameAttempts = 100;
        
        [NonSerialized] private readonly List<PaletteEntry> entriesToDisplay = new List<PaletteEntry>();
        [NonSerialized] private readonly List<PaletteEntry> entriesSelected = new List<PaletteEntry>();
        [NonSerialized] private readonly List<PaletteEntry> entriesIndividuallySelected = new List<PaletteEntry>();
        
        [NonSerialized] private readonly List<Object> draggedAssets = new List<Object>();
        
        private Vector2 entriesPreviewsScrollPosition;
        private Vector2 folderPanelScrollPosition;
        
        [NonSerialized] private bool isResizingFolderPanel;

        [NonSerialized] private GUIStyle cachedEntryNameTextStyle;
        [NonSerialized] private bool didCacheEntryNameTextStyle;
        private GUIStyle EntryNameTextStyle
        {
            get
            {
                if (!didCacheEntryNameTextStyle)
                {
                    didCacheEntryNameTextStyle = true;
                    cachedEntryNameTextStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                    {
                        alignment = TextAnchor.LowerCenter
                    };
                    cachedEntryNameTextStyle.normal.textColor = Color.white;
                }
                return cachedEntryNameTextStyle;
            }
        }
        
        private GUIStyle cachedMessageTextStyle;
        private bool didCacheMessageTextStyle;
        private GUIStyle MessageTextStyle
        {
            get
            {
                if (!didCacheMessageTextStyle)
                {
                    didCacheMessageTextStyle = true;
                    cachedMessageTextStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return cachedMessageTextStyle;
            }
        }

        private float FolderPanelWidth
        {
            get
            {
                if (!EditorPrefs.HasKey(FolderPanelWidthEditorPref))
                    FolderPanelWidth = FolderPanelWidthMin;
                return Mathf.Max(EditorPrefs.GetFloat(FolderPanelWidthEditorPref), FolderPanelWidthMin);
            }
            set => EditorPrefs.SetFloat(FolderPanelWidthEditorPref, value);
        }
        
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

        private AssetPaletteCollection cachedCurrentCollection;
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

                CurrentCollectionGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            }
        }
        
        private SerializedObject cachedCurrentCollectionSerializedObject;
        private SerializedObject CurrentCollectionSerializedObject
        {
            get
            {
                if (cachedCurrentCollectionSerializedObject == null)
                    cachedCurrentCollectionSerializedObject = new SerializedObject(CurrentCollection);
                return cachedCurrentCollectionSerializedObject;
            }
        }

        private float ZoomLevel
        {
            get
            {
                if (!EditorPrefs.HasKey(ZoomLevelEditorPref))
                    ZoomLevel = 0.25f;
                return EditorPrefs.GetFloat(ZoomLevelEditorPref);
            }
            set => EditorPrefs.SetFloat(ZoomLevelEditorPref, value);
        }
        
        private int SelectedFolderIndex
        {
            get
            {
                int index = EditorPrefs.GetInt(SelectedFolderIndexEditorPref);
                index = Mathf.Clamp(index, 0, CurrentCollection.Folders.Count - 1);
                return index;
            }
            set
            {
                EditorPrefs.SetInt(
                    SelectedFolderIndexEditorPref, Mathf.Clamp(value, 0, CurrentCollection.Folders.Count - 1));
            }
        }

        private PaletteFolder SelectedFolder
        {
            get
            {
                EnsureFolderExists();
                return CurrentCollection.Folders[SelectedFolderIndex];
            }
            set
            {
                // TODO: Respect hierarchy.
                SelectedFolderIndex = CurrentCollection.Folders.IndexOf(value);
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
                    cachedFolderTypes = typeof(PaletteFolder).GetAllAssignableClasses(false, true);
                }
                return cachedFolderTypes;
            }
        }

        [NonSerialized] private string renameText;
        [NonSerialized] private bool isDraggingFolder;
        [NonSerialized] private int currentFolderDragIndex;
        [NonSerialized] private int folderToDragIndex;
        private PaletteFolder FolderBeingDragged => (PaletteFolder)DragAndDrop.GetGenericData(FolderDragGenericDataType);

        private bool IsMouseInHeader => Event.current.mousePosition.y <= HeaderHeight;
        private bool IsMouseInFooter => Event.current.mousePosition.y >= position.height - FooterHeight;

        private bool IsMouseInFolderPanel =>
            !IsMouseInHeader && Event.current.mousePosition.x < FolderPanelWidth;
        
        private bool IsMouseInEntriesPanel => !IsMouseInFolderPanel && !IsMouseInFooter;

        [MenuItem ("Window/General/Asset Palette")]
        public static void Init() 
        {
            AssetPaletteWindow window = GetWindow<AssetPaletteWindow>(false, "Asset Palette");
            window.minSize = new Vector2(WindowWidthMin, WindowHeightMin);
            window.wantsMouseMove = true;
        }

        private void OnGUI()
        {
            PerformKeyboardShortcuts();
            
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical();
                {
                    DrawFolderPanel();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                {
                    DropAreaGUI();

                    DrawEntriesPanel();

                    DrawFooter();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            Rect headerRect = GUILayoutUtility.GetRect(position.width, HeaderHeight);
            GUI.Box(headerRect, GUIContent.none, EditorStyles.toolbar);

            AssetPaletteCollection currentCollection = CurrentCollection;
            string name = currentCollection == null ? "[No Collection]" : currentCollection.name;
            
            // Allow a new collection to be created or loaded.
            Rect collectionRect = headerRect.GetSubRectFromLeft(CollectionButtonWidth);
            bool createNewCollection = GUI.Button(collectionRect, name, EditorStyles.toolbarDropDown);
            if (createNewCollection)
                DoCollectionDropDown(collectionRect);

            // Allow a new folder to be created. Supports derived types of PaletteFolder as an experimental feature.
            Rect newFolderRect = new Rect(
                FolderPanelWidth - NewFolderButtonWidth, 0, NewFolderButtonWidth, headerRect.height);
            GUI.enabled = CurrentCollection != null;
            bool createNewFolder = GUI.Button(
                newFolderRect, "New Folder", HasMultipleFolderTypes ? EditorStyles.toolbarDropDown : EditorStyles.toolbarButton);
            GUI.enabled = true;
            if (createNewFolder)
            {
                if (HasMultipleFolderTypes)
                    DoCreateNewFolderDropDown(newFolderRect);
                else
                    CreateNewFolder(typeof(PaletteFolder));
            }
        }

        private void DoCreateNewFolderDropDown(Rect position)
        {
            GenericMenu dropdownMenu = new GenericMenu();
            
            foreach (Type type in FolderTypes)
            {
                string name = type.Name.RemoveSuffix("Folder").ToHumanReadable();
                dropdownMenu.AddItem(new GUIContent(name), false, CreateNewFolder, type);
            }
            
            dropdownMenu.DropDown(position);
        }

        private void CreateNewFolder(object userdata)
        {
            // Create a new instance of the specified folder type.
            PaletteFolder newFolder = CreateNewFolderOfType((Type)userdata, GetUniqueFolderName(NewFolderName));
            StartRename(newFolder);
            GUI.FocusControl(newFolder.RenameControlId);
        }

        private string GetUniqueFolderName(string desiredName, int previousAttempts = 0)
        {
            if (previousAttempts > MaxUniqueFolderNameAttempts)
            {
                throw new Exception($"Tried to find a unique version of folder name '{desiredName}' but failed " +
                                    $"after {previousAttempts} attempts.");
            }
            
            bool alreadyTaken = false;
            
            // TODO: Respect folder hierarchy
            foreach (PaletteFolder folder in CurrentCollection.Folders)
            {
                if (folder.Name == desiredName)
                {
                    alreadyTaken = true;
                    break;
                }
            }

            if (!alreadyTaken)
                return desiredName;

            bool hadNumberPrefix = desiredName.TryGetNumberSuffix(out int number);
            if (!hadNumberPrefix)
                desiredName = desiredName.SetNumberSuffix(1);
            else
                desiredName = desiredName.SetNumberSuffix(number + 1);
            
            return GetUniqueFolderName(desiredName, previousAttempts + 1);
        }

        private PaletteFolder CreateNewFolderOfType(Type type, string name)
        {
            PaletteFolder newFolder = (PaletteFolder)Activator.CreateInstance(type);
            newFolder.Initialize(name);

            // Add it to the current collection's list of folders.
            CurrentCollectionSerializedObject.Update();
            SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                
            // Add it to the list.
            int newIndex = foldersProperty.arraySize;
            foldersProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = foldersProperty.GetArrayElementAtIndex(newIndex);
            newElement.managedReferenceValue = newFolder;
                
            CurrentCollectionSerializedObject.ApplyModifiedProperties();

            SelectedFolder = newFolder;
            
            return newFolder;
        }
        
        private FolderType CreateNewFolder<FolderType>(string name) where FolderType: PaletteFolder
        {
            return (FolderType)CreateNewFolderOfType(typeof(FolderType), name);
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
            string path = EditorUtility.SaveFilePanel("Create New Asset Palette", null, "Asset Palette", "asset");
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

        private void EnsureFolderExists()
        {
            CurrentCollectionSerializedObject.Update();
            if (CurrentCollection != null)
            {
                using (SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders"))
                {
                    // Make sure there is at least one folder.
                    if (foldersProperty.arraySize == 0)
                        CreateNewFolder<PaletteFolder>(InitialFolderName);
                }
            }
            CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void DrawFolderPanel()
        {
            // It seems like mouse down events only trigger inside the scroll view if we check it from inside there.
            bool didClickAnywhereInWindow = Event.current.type == EventType.MouseDown;

            folderPanelScrollPosition = EditorGUILayout.BeginScrollView(
                folderPanelScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Width(FolderPanelWidth));

            EnsureFolderExists();

            Color selectionColor = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).settings.selectionColor;
            selectionColor.a = 0.25f;
            
            currentFolderDragIndex = -1;
            
            CurrentCollectionSerializedObject.Update();
            if (CurrentCollection != null)
            {
                using (SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders"))
                {
                    for (int i = 0; i < foldersProperty.arraySize; i++)
                    {
                        SerializedProperty folderProperty = foldersProperty.GetArrayElementAtIndex(i);
                        float folderHeight = EditorGUI.GetPropertyHeight(folderProperty, GUIContent.none, true);
                        float folderWidth = FolderPanelWidth;
                        Rect folderRect = GUILayoutUtility.GetRect(folderWidth, folderHeight);
                        bool isSelected = SelectedFolderIndex == i;
                        if (isSelected)
                            EditorGUI.DrawRect(folderRect, selectionColor);

                        PaletteFolder folder = folderProperty.GetValue<PaletteFolder>();
                        folderRect = folderRect.Indent(1);
                        if (folder.IsRenaming)
                        {
                            GUI.SetNextControlName(folder.RenameControlId);
                            renameText = EditorGUI.TextField(folderRect, renameText);
                            GUI.FocusControl(folder.RenameControlId);
                        }
                        else
                        {
                            EditorGUI.PropertyField(folderRect, folderProperty, GUIContent.none);
                        }

                        bool isMouseOver = folderRect.Contains(Event.current.mousePosition);
                        
                        // Dragging and dropping folders.
                        if (Event.current.type == EventType.MouseDrag && isMouseOver && !isResizingFolderPanel)
                            StartFolderDrag(folder);
                        if (isDraggingFolder)
                        {
                            bool didFindDragIndex = false;
                            Rect dragMarkerRect = Rect.zero;
                            if (Event.current.mousePosition.y <= folderRect.center.y &&
                                (currentFolderDragIndex == -1 || i < currentFolderDragIndex))
                            {
                                currentFolderDragIndex = i;
                                didFindDragIndex = true;
                                dragMarkerRect = folderRect.GetSubRectFromTop(2);
                                Repaint();
                            }
                            else if (currentFolderDragIndex == -1 && i == foldersProperty.arraySize - 1)
                            {
                                currentFolderDragIndex = i + 1;
                                didFindDragIndex = true;
                                dragMarkerRect = folderRect.GetSubRectFromBottom(2);
                                Repaint();
                            }

                            if (didFindDragIndex && currentFolderDragIndex != folderToDragIndex &&
                                currentFolderDragIndex != folderToDragIndex + 1 &&
                                DragAndDrop.visualMode == DragAndDropVisualMode.Move)
                            {
                                EditorGUI.DrawRect(dragMarkerRect, Color.blue);
                            }
                        }

                        // Allow users to select a folder by clicking with LMB.
                        if (didClickAnywhereInWindow)
                        {
                            if (folder.IsRenaming && !isMouseOver)
                            {
                                StopRename();
                            }
                            else if (!PaletteFolder.IsFolderBeingRenamed && isMouseOver)
                            {
                                SelectedFolderIndex = i;
                                
                                // Allow starting a rename by clicking on it twice.
                                if (Event.current.clickCount == 2)
                                    StartRename(folder);
                                
                                Repaint();
                            }
                        }
                    }
                }
            }
            CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            
            EditorGUILayout.EndScrollView();

            if (isDraggingFolder && Event.current.type == EventType.DragUpdated ||
                Event.current.type == EventType.DragPerform)
            {
                if (!IsMouseInFolderPanel)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                else
                {
                    DragAndDrop.visualMode = currentFolderDragIndex < folderToDragIndex || currentFolderDragIndex > folderToDragIndex + 1
                        ? DragAndDropVisualMode.Move
                        : DragAndDropVisualMode.Rejected;
                }

                if (Event.current.type == EventType.DragPerform)
                    StopFolderDrag();
            }

            DrawResizableFolderPanelDivider();
        }

        private void StartFolderDrag(PaletteFolder folder)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(FolderDragGenericDataType, folder);
            DragAndDrop.StartDrag("Drag Palette Folder");
            isDraggingFolder = true;
            folderToDragIndex = CurrentCollection.Folders.IndexOf(folder);
        }

        private void StopFolderDrag()
        {
            isDraggingFolder = false;

            bool isValidDrop = DragAndDrop.visualMode == DragAndDropVisualMode.Move;
            if (!isValidDrop)
                return;

            PaletteFolder folderBeingDragged = (PaletteFolder)DragAndDrop.GetGenericData(FolderDragGenericDataType);
            DragAndDrop.AcceptDrag();

            // If you want to drag a folder downwards, keep in mind that the indices will shift as a result from the
            // dragged folder not being where it used to be any more.
            if (currentFolderDragIndex > folderToDragIndex)
                currentFolderDragIndex--;

            // Remove the folder from the list and add it back at the specified position.
            CurrentCollectionSerializedObject.Update();
            SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
            foldersProperty.DeleteArrayElementAtIndex(folderToDragIndex);
            foldersProperty.InsertArrayElementAtIndex(currentFolderDragIndex);
            SerializedProperty movedFolderProperty = foldersProperty.GetArrayElementAtIndex(currentFolderDragIndex);
            movedFolderProperty.managedReferenceValue = folderBeingDragged;
            CurrentCollectionSerializedObject.ApplyModifiedProperties();

            SelectedFolderIndex = currentFolderDragIndex;
            
            Repaint();
        }

        private void DrawResizableFolderPanelDivider()
        {
            const int thickness = 1;
            Rect dividerRect = new Rect(
                FolderPanelWidth - thickness, HeaderHeight, thickness, position.height);
            EditorGUI.DrawRect(dividerRect, DividerColor);

            const int expansion = 1;
            Rect resizeRect = dividerRect;
            resizeRect.xMin -= expansion;
            resizeRect.xMax += expansion;
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
            {
                isResizingFolderPanel = true;
            }

            if (isResizingFolderPanel &&
                (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag))
            {
                FolderPanelWidth = Mathf.Clamp(
                    Event.current.mousePosition.x, FolderPanelWidthMin, position.width - EntriesPanelWidthMin);
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
                isResizingFolderPanel = false;
        }

        private void DrawFooter()
        {
            Rect separatorRect = new Rect(
                FolderPanelWidth,
                position.height - FooterHeight, position.width - FolderPanelWidth, 1);
                
            EditorGUI.DrawRect(separatorRect, DividerColor);

            EditorGUILayout.BeginVertical(GUILayout.Height(FooterHeight));
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    Rect zoomLevelRect = GUILayoutUtility.GetRect(80, EditorGUIUtility.singleLineHeight);

                    ZoomLevel = GUI.HorizontalSlider(zoomLevelRect, ZoomLevel, 0.0f, 1.0f);

                    GUILayout.Space(16);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndVertical();
        }

        private void PerformKeyboardShortcuts()
        {
            if (Event.current.type != EventType.KeyDown)
                return;
            
            if (Event.current.keyCode == KeyCode.Return && PaletteFolder.IsFolderBeingRenamed)
            {
                StopRename();
                return;
            }
            
            // Allow all currently visible entries to be selected if CTRL+A is pressed. 
            if (Event.current.control && Event.current.keyCode == KeyCode.A)
            {
                entriesSelected.Clear();
                entriesSelected.AddRange(entriesToDisplay);
                entriesIndividuallySelected.Clear();
                if (entriesToDisplay.Count > 0)
                    entriesIndividuallySelected.Add(entriesToDisplay[^1]);
                Repaint();
                return;
            }

            if (Event.current.keyCode == KeyCode.Delete)
            {
                if (IsMouseInEntriesPanel)
                {
                    // Pressing Delete will remove all selected entries from the palette.
                    foreach (PaletteEntry entry in entriesSelected)
                    {
                        entriesToDisplay.Remove(entry);
                    }

                    entriesSelected.Clear();
                    entriesIndividuallySelected.Clear();
                    Repaint();
                }
                else if (IsMouseInFolderPanel && CurrentCollection != null && CurrentCollection.Folders.Count > 1 &&
                         !isDraggingFolder && !PaletteFolder.IsFolderBeingRenamed)
                {
                    CurrentCollectionSerializedObject.Update();
                    SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                    foldersProperty.DeleteArrayElementAtIndex(SelectedFolderIndex);
                    CurrentCollectionSerializedObject.ApplyModifiedProperties();
                    Repaint();
                }
            }
        }

        private void DrawEntriesPanel()
        {
            entriesPreviewsScrollPosition = GUILayout.BeginScrollView(
                entriesPreviewsScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
            Rect entriesPanelRect = new Rect(0, 0, position.width - FolderPanelWidth, 90000000);
            EditorGUI.DrawRect(entriesPanelRect, new Color(0, 0, 0, 0.1f));

            AssetPaletteCollection currentCollection = CurrentCollection;
            
            bool hasCollection = currentCollection != null;
            bool hasEntries = hasCollection && entriesToDisplay.Count > 0;
            
            if (!hasCollection || !hasEntries)
            {
                GUILayout.FlexibleSpace();

                if (!hasCollection)
                {
                    EditorGUILayout.LabelField(
                        "To begin organizing assets, create a collection.", MessageTextStyle);
                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();
                        bool shouldCreate = GUILayout.Button("Create", GUILayout.Width(100));
                        GUILayout.FlexibleSpace();
                        
                        if (shouldCreate)
                            CreateNewCollection();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("Drag assets here!", MessageTextStyle);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndScrollView();
                return;
            }
            
            float containerWidth = Mathf.Floor(EditorGUIUtility.currentViewWidth) - FolderPanelWidth;

            const float padding = 2;
            const float spacing = 2;
            int entrySize = Mathf.RoundToInt(Mathf.Lerp(EntrySizeMin, EntrySizeMax, ZoomLevel));

            int columnCount = Mathf.FloorToInt(containerWidth / (entrySize + spacing));
            int rowCount = Mathf.CeilToInt((float)entriesToDisplay.Count / columnCount);
                
            GUILayout.Space(padding);
            
            bool didClickASpecificEntry = false;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(padding);
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    int index = rowIndex * columnCount + columnIndex;
                    if (index >= entriesToDisplay.Count)
                    {
                        GUILayout.FlexibleSpace();
                        break;
                    }

                    // Purge invalid entries.
                    while (!entriesToDisplay[index].IsValid && index < entriesToDisplay.Count)
                    {
                        entriesToDisplay.RemoveAt(index);
                    }
                    
                    if (index >= entriesToDisplay.Count)
                    {
                        GUILayout.FlexibleSpace();
                        break;
                    }
                    
                    PaletteEntry entry = entriesToDisplay[index];

                    Rect rect = GUILayoutUtility.GetRect(
                        0, 0, GUILayout.Width(entrySize), GUILayout.Height(entrySize));

                    // Allow this entry to be selected by clicking it.
                    bool isMouseOnEntry = rect.Contains(Event.current.mousePosition) && !IsMouseInFooter &&
                                          !IsMouseInHeader;
                    bool wasAlreadySelected = entriesSelected.Contains(entry);
                    if (Event.current.type == EventType.MouseDown && isMouseOnEntry)
                    {
                        if (Event.current.shift)
                        {
                            // Shift+click to grow selection until this point.
                            if (entriesSelected.Count == 0)
                            {
                                entriesSelected.Add(entry);
                                entriesIndividuallySelected.Add(entry);
                            }
                            else
                            {
                                if (wasAlreadySelected)
                                {
                                    // Define a new extremity away from the first individually selected entry.
                                    // Might seem convoluted and with weird edge cases, but this is how Unity does it...
                                    PaletteEntry firstEntryIndividuallySelected = entriesIndividuallySelected[0];
                                    int indexOfFirstIndividuallySelectedEntry =
                                        entriesToDisplay.IndexOf(firstEntryIndividuallySelected);
                                    if (index > indexOfFirstIndividuallySelectedEntry)
                                    {
                                        PaletteEntry lowestSelectedEntry = null;
                                        for (int i = 0; i < entriesToDisplay.Count; i++)
                                        {
                                            if (entriesSelected.Contains(entriesToDisplay[i]))
                                            {
                                                lowestSelectedEntry = entriesToDisplay[i];
                                                break;
                                            }
                                        }
                                        entriesIndividuallySelected.Clear();
                                        entriesIndividuallySelected.Add(lowestSelectedEntry);
                                        indexOfFirstIndividuallySelectedEntry =
                                            entriesToDisplay.IndexOf(lowestSelectedEntry);
                                    }
                                    else if (index < indexOfFirstIndividuallySelectedEntry)
                                    {
                                        PaletteEntry highestSelectedEntry = null;
                                        for (int i = entriesToDisplay.Count - 1; i >= 0; i--)
                                        {
                                            if (entriesSelected.Contains(entriesToDisplay[i]))
                                            {
                                                highestSelectedEntry = entriesToDisplay[i];
                                                break;
                                            }
                                        }
                                        entriesIndividuallySelected.Clear();
                                        entriesIndividuallySelected.Add(highestSelectedEntry);
                                        indexOfFirstIndividuallySelectedEntry =
                                            entriesToDisplay.IndexOf(highestSelectedEntry);
                                    }

                                    entriesSelected.Clear();
                                    SelectEntries(indexOfFirstIndividuallySelectedEntry, index);
                                }
                                else
                                {
                                    // Grow the selection from the last individually selected entry.
                                    PaletteEntry lastEntryIndividuallySelected = entriesIndividuallySelected[^1];
                                    int indexOfLastIndividuallySelectedEntry =
                                        entriesToDisplay.IndexOf(lastEntryIndividuallySelected);
                                    SelectEntries(indexOfLastIndividuallySelectedEntry, index);
                                }
                            }
                        }
                        else if (Event.current.control)
                        {
                            // Control+click to add specific files to the selection.
                            if (!wasAlreadySelected)
                            {
                                entriesSelected.Add(entry);
                                entriesIndividuallySelected.Add(entry);
                            }
                            else
                            {
                                entriesSelected.Remove(entry);
                                entriesIndividuallySelected.Remove(entry);
                            }
                        }
                        else
                        {
                            // Regular click to select only this entry.
                            if (!wasAlreadySelected)
                            {
                                entriesSelected.Clear();
                                entriesSelected.Add(entry);
                                entriesIndividuallySelected.Clear();
                                entriesIndividuallySelected.Add(entry);
                            }

                            // Allow assets to be opened by double clicking on them.
                            if (Event.current.clickCount == 2)
                            {
                                Debug.Log($"Opening asset {entry.Asset}");
                                AssetDatabase.OpenAsset(entry.Asset);
                            }
                        }

                        didClickASpecificEntry = true;
                        Repaint();
                    }
                    else if (Event.current.type == EventType.MouseUp && isMouseOnEntry && !Event.current.control &&
                             !Event.current.shift)
                    {
                        // Regular click to select only this entry.
                        entriesSelected.Clear();
                        entriesSelected.Add(entry);
                        entriesIndividuallySelected.Clear();
                        entriesIndividuallySelected.Add(entry);
                        Repaint();
                    }
                    else if (Event.current.type == EventType.MouseDrag && isMouseOnEntry)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = entriesSelected.Select(entry => entry.Asset).ToArray();
                        DragAndDrop.StartDrag("Drag from Asset Palette");
                    }
                    bool isSelected = entriesSelected.Contains(entry);
                
                    Color borderColor = isSelected ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                    EditorGUI.DrawRect(rect, borderColor);
                
                    Rect textureRect = rect.Inset(isSelected ? 2 : 1);
                    if (entry.PreviewTexture != null)
                    {
                        EditorGUI.DrawPreviewTexture(
                            textureRect, entry.PreviewTexture, null, ScaleMode.ScaleToFit, 0.0f);
                    }
                    else
                    {
                        const float brightness = 0.325f;
                        EditorGUI.DrawRect(textureRect, new Color(brightness, brightness, brightness));
                    }

                    // Draw a label with a nice semi-transparent backdrop.
                    GUIContent title = new GUIContent(entry.Asset.name);
                    float height = EntryNameTextStyle.CalcHeight(title, textureRect.width);
                    
                    EditorGUI.DrawRect(textureRect.GetSubRectFromBottom(height), new Color(0, 0, 0, 0.15f));
                    EditorGUI.LabelField(textureRect, title, EntryNameTextStyle);

                    if (columnIndex < columnCount - 1)
                        EditorGUILayout.Space(spacing);
                    else
                        GUILayout.FlexibleSpace();
                }
                GUILayout.Space(padding);
                EditorGUILayout.EndHorizontal();
                
                if (rowIndex < rowCount - 1)
                    EditorGUILayout.Space(spacing);
            }
            
            GUILayout.Space(padding);

            // If you didn't click an entry and weren't pressing SHIFT, clear the selection.
            if (Event.current.type == EventType.MouseDown && !didClickASpecificEntry && !Event.current.shift)
            {
                entriesSelected.Clear();
                entriesIndividuallySelected.Clear();
                Repaint();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void SelectEntries(int from, int to)
        {
            int direction = from <= to ? 1 : -1;
            for (int i = from; i != to; i += direction)
            {
                if (!entriesSelected.Contains(entriesToDisplay[i]))
                    entriesSelected.Add(entriesToDisplay[i]);
            }
            if (!entriesSelected.Contains(entriesToDisplay[to]))
                entriesSelected.Add(entriesToDisplay[to]);
        }

        private void OnLostFocus()
        {
            StopRename();
        }

        private void OnSelectionChange()
        {
            StopRename();
        }

        private void OnFocus()
        {
            StopRename();
        }

        private void OnProjectChange()
        {
            StopRename();
        }

        private void StartRename(PaletteFolder folder)
        {
            renameText = folder.Name;
            folder.StartRename();
        }

        private void StopRename()
        {
            if (!PaletteFolder.IsFolderBeingRenamed)
                return;

            bool isValidRename = !string.IsNullOrEmpty(renameText) && !string.IsNullOrWhiteSpace(renameText) &&
                                 PaletteFolder.FolderCurrentlyRenaming.Name != renameText;
            if (isValidRename)
            {
                renameText = GetUniqueFolderName(renameText);
                CurrentCollectionSerializedObject.Update();
                SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                int index = CurrentCollection.Folders.IndexOf(PaletteFolder.FolderCurrentlyRenaming);
                SerializedProperty folderBeingRenamedProperty = foldersProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProperty = folderBeingRenamedProperty.FindPropertyRelative("name");
                nameProperty.stringValue = renameText;
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
            }
            
            PaletteFolder.CancelRename();
            Repaint();
        }

        private bool HasEntry(Object asset)
        {
            foreach (PaletteEntry entry in entriesToDisplay)
            {
                if (entry.Asset == asset)
                    return true;
            }
            return false;
        }

        private void DropAreaGUI()
        {
            Event @event = Event.current;
            if (@event.type != EventType.DragUpdated && @event.type != EventType.DragPerform)
                return;

            bool isValidDrag = IsMouseInEntriesPanel && DragAndDrop.objectReferences.Length > 0;
            if (!isValidDrag)
                return;

            DragAndDrop.AcceptDrag();
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (@event.type != EventType.DragPerform)
                return;

            // Find dragged assets.
            draggedAssets.Clear();
            foreach (Object draggedObject in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(draggedObject);
                if (AssetDatabase.IsValidFolder(path))
                {
                    bool addLinkToFolder = EditorUtility.DisplayDialog(
                        "Create Folder Entry",
                        "Do you want to add a link to the folder or the assets within it?", "Link To Folder",
                        "Assets In Folder");
                    if (addLinkToFolder)
                    {
                        // Just add the folder itself.
                        Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                        draggedAssets.Add(asset);
                    }
                    else
                    {
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
                            draggedAssets.Add(asset);
                        }
                    }

                    continue;
                }
                
                // Basically any Object is fine as long as it's not a scene GameObject.
                if (!(draggedObject is GameObject go) || go.IsPrefab())
                    draggedAssets.Add(draggedObject);
            }

            foreach (Object draggedAsset in draggedAssets)
            {
                if (HasEntry(draggedAsset))
                    continue;
                    
                PaletteEntry entry = new PaletteEntry(draggedAsset);
                entriesToDisplay.Add(entry);
            }
        }
    }
}
