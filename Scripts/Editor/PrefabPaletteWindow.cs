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
    public class PrefabPaletteWindow : EditorWindow
    {
        private const string EditorPrefPrefix = "RoyTheunissen/PrefabPalette/";
        private const string CurrentCollectionGUIDEditorPref = EditorPrefPrefix + "CurrentCollectionGUID";
        private const string NavigationPanelWidthEditorPref = EditorPrefPrefix + "NavigationPanelWidth";
        private const string ZoomLevelEditorPref = EditorPrefPrefix + "ZoomLevel";
        private const string SelectedFolderIndexEditorPref = EditorPrefPrefix + "SelectedFolderIndex";

        private const float PrefabSizeMax = PrefabEntry.TextureSize;
        private const float PrefabSizeMin = PrefabEntry.TextureSize * 0.45f;
        
        private const float NavigationPanelWidthMin = 100;
        private const float PrefabPanelWidthMin = 200;
        private const float PrefabPanelHeightMin = 50;
        private const float WindowWidthMin = NavigationPanelWidthMin + PrefabPanelWidthMin;
        
        private static float HeaderHeight => EditorGUIUtility.singleLineHeight + 3;
        private static float FooterHeight => EditorGUIUtility.singleLineHeight + 6;
        private static readonly float WindowHeightMin = FooterHeight + HeaderHeight + PrefabPanelHeightMin;
        
        private const float DividerBrightness = 0.13f;
        private static readonly Color DividerColor = new Color(DividerBrightness, DividerBrightness, DividerBrightness);

        private const string InitialFolderName = "Default";
        private const string NewFolderName = "New Folder";
        private const int MaxUniqueFolderNameAttempts = 100;
        
        [NonSerialized] private readonly List<PrefabEntry> prefabsToDisplay = new List<PrefabEntry>();
        [NonSerialized] private readonly List<PrefabEntry> prefabsSelected = new List<PrefabEntry>();
        
        [NonSerialized] private readonly List<GameObject> draggedPrefabs = new List<GameObject>();
        
        private Vector2 prefabPreviewsScrollPosition;
        private Vector2 navigationPanelScrollPosition;
        
        [NonSerialized] private bool isResizingNavigationPanel;
        
        [NonSerialized] private GUIStyle cachedPrefabPreviewTextStyle;
        [NonSerialized] private bool didCachePrefabPreviewTextStyle;
        private GUIStyle PrefabPreviewTextStyle
        {
            get
            {
                if (!didCachePrefabPreviewTextStyle)
                {
                    didCachePrefabPreviewTextStyle = true;
                    cachedPrefabPreviewTextStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                    {
                        alignment = TextAnchor.LowerCenter
                    };
                    cachedPrefabPreviewTextStyle.normal.textColor = Color.white;
                }
                return cachedPrefabPreviewTextStyle;
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

        private float NavigationPanelWidth
        {
            get
            {
                if (!EditorPrefs.HasKey(NavigationPanelWidthEditorPref))
                    NavigationPanelWidth = NavigationPanelWidthMin;
                return Mathf.Max(EditorPrefs.GetFloat(NavigationPanelWidthEditorPref), NavigationPanelWidthMin);
            }
            set => EditorPrefs.SetFloat(NavigationPanelWidthEditorPref, value);
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

        private PrefabPaletteCollection cachedCurrentCollection;
        private PrefabPaletteCollection CurrentCollection
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

                    cachedCurrentCollection = AssetDatabase.LoadAssetAtPath<PrefabPaletteCollection>(path);
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
        
        [NonSerialized] private Type[] cachedFolderTypes;
        [NonSerialized] private bool didCacheFolderTypes;
        private Type[] FolderTypes
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

        private string renameText;
        
        private bool IsMouseInHeader => Event.current.mousePosition.y <= HeaderHeight;
        private bool IsMouseInFooter => Event.current.mousePosition.y >= position.height - FooterHeight;

        private bool IsMouseInNavigationPanel =>
            !IsMouseInHeader && Event.current.mousePosition.x < NavigationPanelWidth;
        
        private bool IsMouseInPrefabPanel => !IsMouseInNavigationPanel && !IsMouseInFooter;

        [MenuItem ("Window/General/Prefab Palette")]
        public static void Init() 
        {
            PrefabPaletteWindow window = GetWindow<PrefabPaletteWindow>(false, "Prefab Palette");
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
                    DrawNavigationPanel();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                {
                    DropAreaGUI();

                    DrawPrefabs();

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

            PrefabPaletteCollection currentCollection = CurrentCollection;
            string name = currentCollection == null ? "[No Collection]" : currentCollection.name;
            
            // Allow a new collection to be created or loaded.
            Rect collectionRect = headerRect.GetSubRectFromLeft(130);
            bool createNewCollection = GUI.Button(collectionRect, name, EditorStyles.toolbarDropDown);
            if (createNewCollection)
                DoCollectionDropDown(collectionRect);

            // Allow a new folder to be created. Supports derived types of PaletteFolder as an experimental feature.
            bool hasMultipleFolderTypes = FolderTypes.Length > 1;
            int newFolderButtonWidth = 76 + (hasMultipleFolderTypes ? 9 : 0);
            Rect newFolderRect = new Rect(
                NavigationPanelWidth - newFolderButtonWidth, 0, newFolderButtonWidth, headerRect.height);
            GUI.enabled = CurrentCollection != null;
            bool createNewFolder = GUI.Button(
                newFolderRect, "New Folder", hasMultipleFolderTypes ? EditorStyles.toolbarDropDown : EditorStyles.toolbarButton);
            GUI.enabled = true;
            if (createNewFolder)
            {
                if (hasMultipleFolderTypes)
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
            string[] existingCollectionGuids = AssetDatabase.FindAssets($"t:{typeof(PrefabPaletteCollection).Name}");

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
            string path = EditorUtility.SaveFilePanel("Create New Prefab Palette", null, "Prefab Palette", "asset");
            if (string.IsNullOrEmpty(path))
                return;

            // Make the path relative to the project.
            string absolutePath = path;
            path = Path.Combine("Assets", Path.GetRelativePath(Application.dataPath, path));

            Directory.CreateDirectory(path);

            PrefabPaletteCollection newCollection = CreateInstance<PrefabPaletteCollection>();
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

        private void DrawNavigationPanel()
        {
            // It seems like mouse down events only trigger inside the scroll view if we check it from inside there.
            bool didClickAnywhereInWindow = Event.current.type == EventType.MouseDown;

            navigationPanelScrollPosition = EditorGUILayout.BeginScrollView(
                navigationPanelScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Width(NavigationPanelWidth));

            EnsureFolderExists();

            Color selectionColor = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).settings.selectionColor;
            selectionColor.a = 0.25f;
            
            CurrentCollectionSerializedObject.Update();
            if (CurrentCollection != null)
            {
                using (SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders"))
                {
                    for (int i = 0; i < foldersProperty.arraySize; i++)
                    {
                        SerializedProperty folderProperty = foldersProperty.GetArrayElementAtIndex(i);
                        float folderHeight = EditorGUI.GetPropertyHeight(folderProperty, GUIContent.none, true);
                        float folderWidth = NavigationPanelWidth;
                        Rect folderRect = GUILayoutUtility.GetRect(folderWidth, folderHeight);
                        bool isSelected = SelectedFolderIndex == i;
                        if (isSelected)
                            EditorGUI.DrawRect(folderRect, selectionColor);

                        PaletteFolder folder = folderProperty.GetValue<PaletteFolder>();
                        folderRect = folderRect.Indent(1);
                        if (folder.IsRenaming)
                        {
                            SerializedProperty nameProperty = folderProperty.FindPropertyRelative("name");
                            
                            GUI.SetNextControlName(folder.RenameControlId);
                            renameText = EditorGUI.TextField(folderRect, renameText);
                            GUI.FocusControl(folder.RenameControlId);
                        }
                        else
                        {
                            EditorGUI.PropertyField(folderRect, folderProperty, GUIContent.none);
                        }

                        // Allow users to select a folder by clicking with LMB.
                        if (didClickAnywhereInWindow)
                        {
                            bool isMouseOver = folderRect.Contains(Event.current.mousePosition);
                            if (folder.IsRenaming && !isMouseOver)
                            {
                                StopRename();
                            }
                            else if (!PaletteFolder.IsFolderBeingRenamed && isMouseOver)
                            {
                                SelectedFolderIndex = i;
                                Repaint();
                            }
                        }
                    }
                }
            }
            CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            
            EditorGUILayout.EndScrollView();

            DrawResizableNavigationPanelDivider();
        }

        private void DrawResizableNavigationPanelDivider()
        {
            const int thickness = 1;
            Rect dividerRect = new Rect(
                NavigationPanelWidth - thickness, HeaderHeight, thickness, position.height);
            EditorGUI.DrawRect(dividerRect, DividerColor);

            const int expansion = 1;
            Rect resizeRect = dividerRect;
            resizeRect.xMin -= expansion;
            resizeRect.xMax += expansion;
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
            {
                isResizingNavigationPanel = true;
            }

            if (isResizingNavigationPanel &&
                (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag))
            {
                NavigationPanelWidth = Mathf.Clamp(
                    Event.current.mousePosition.x, NavigationPanelWidthMin, position.width - PrefabPanelWidthMin);
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
                isResizingNavigationPanel = false;
        }

        private void DrawFooter()
        {
            Rect separatorRect = new Rect(
                NavigationPanelWidth,
                position.height - FooterHeight, position.width - NavigationPanelWidth, 1);
                
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
            
            // Allow all currently visible prefabs to be selected if CTRL+A is pressed. 
            if (Event.current.control && Event.current.keyCode == KeyCode.A)
            {
                prefabsSelected.Clear();
                prefabsSelected.AddRange(prefabsToDisplay);
                Repaint();
                return;
            }

            if (Event.current.keyCode == KeyCode.Delete)
            {
                if (IsMouseInPrefabPanel)
                {
                    // Pressing Delete will remove all selected prefabs from the palette.
                    foreach (PrefabEntry prefabEntry in prefabsSelected)
                    {
                        prefabsToDisplay.Remove(prefabEntry);
                    }

                    prefabsSelected.Clear();
                    Repaint();
                }
                else if (IsMouseInNavigationPanel && CurrentCollection != null && CurrentCollection.Folders.Count > 1)
                {
                    CurrentCollectionSerializedObject.Update();
                    SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders");
                    foldersProperty.DeleteArrayElementAtIndex(SelectedFolderIndex);
                    CurrentCollectionSerializedObject.ApplyModifiedProperties();
                    Repaint();
                }
            }
        }

        private void DrawPrefabs()
        {
            prefabPreviewsScrollPosition = GUILayout.BeginScrollView(
                prefabPreviewsScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
            Rect prefabPanelRect = new Rect(0, 0, position.width - NavigationPanelWidth, 90000000);
            EditorGUI.DrawRect(prefabPanelRect, new Color(0, 0, 0, 0.1f));

            PrefabPaletteCollection currentCollection = CurrentCollection;
            
            bool hasCollection = currentCollection != null;
            bool hasPrefabs = hasCollection && prefabsToDisplay.Count > 0;
            
            if (!hasCollection || !hasPrefabs)
            {
                GUILayout.FlexibleSpace();

                if (!hasCollection)
                {
                    EditorGUILayout.LabelField(
                        "To begin organizing prefabs, create a collection.", MessageTextStyle);
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
                    EditorGUILayout.LabelField("Drag prefabs here!", MessageTextStyle);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndScrollView();
                return;
            }
            
            float containerWidth = Mathf.Floor(EditorGUIUtility.currentViewWidth) - NavigationPanelWidth;

            const float padding = 2;
            const float spacing = 2;
            int prefabSize = Mathf.RoundToInt(Mathf.Lerp(PrefabSizeMin, PrefabSizeMax, ZoomLevel));

            int columnCount = Mathf.FloorToInt(containerWidth / (prefabSize + spacing));
            int rowCount = Mathf.CeilToInt((float)prefabsToDisplay.Count / columnCount);
                
            GUILayout.Space(padding);
            
            bool didClickASpecificPrefab = false;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(padding);
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    int index = rowIndex * columnCount + columnIndex;
                    if (index >= prefabsToDisplay.Count)
                    {
                        GUILayout.FlexibleSpace();
                        break;
                    }

                    // Purge invalid entries.
                    while (!prefabsToDisplay[index].IsValid && index < prefabsToDisplay.Count)
                    {
                        prefabsToDisplay.RemoveAt(index);
                    }
                    
                    if (index >= prefabsToDisplay.Count)
                    {
                        GUILayout.FlexibleSpace();
                        break;
                    }
                    
                    PrefabEntry prefab = prefabsToDisplay[index];

                    Rect rect = GUILayoutUtility.GetRect(
                        0, 0, GUILayout.Width(prefabSize), GUILayout.Height(prefabSize));

                    // Allow this prefab to be selected by clicking it.
                    bool isMouseOnPrefab = rect.Contains(Event.current.mousePosition) && IsMouseInPrefabPanel;
                    bool wasAlreadySelected = prefabsSelected.Contains(prefab);
                    if (Event.current.type == EventType.MouseDown && isMouseOnPrefab)
                    {
                        if ((Event.current.modifiers & EventModifiers.Shift) == EventModifiers.Shift &&
                            !wasAlreadySelected)
                        {
                            // Shift+click to add.
                            prefabsSelected.Add(prefab);
                        }
                        else if ((Event.current.modifiers & EventModifiers.Control) == EventModifiers.Control &&
                                 !wasAlreadySelected)
                        {
                            // Control+click to remove.
                            prefabsSelected.Remove(prefab);
                        }
                        else if (!wasAlreadySelected)
                        {
                            // Regular click to select only this prefab.
                            prefabsSelected.Clear();
                            prefabsSelected.Add(prefab);
                        }

                        didClickASpecificPrefab = true;
                        Repaint();
                    }
                    else if (Event.current.type == EventType.MouseUp && isMouseOnPrefab && !Event.current.control &&
                             !Event.current.shift)
                    {
                        // Regular click to select only this prefab.
                        prefabsSelected.Clear();
                        prefabsSelected.Add(prefab);
                        Repaint();
                    }
                    else if (Event.current.type == EventType.MouseDrag && isMouseOnPrefab)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences =
                            prefabsSelected.Select(prefabEntry => (Object)prefabEntry.Prefab).ToArray();
                        DragAndDrop.StartDrag("Drag from Prefab Palette");
                    }
                    bool isSelected = prefabsSelected.Contains(prefab);
                
                    Color borderColor = isSelected ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                    EditorGUI.DrawRect(rect, borderColor);
                
                    Rect textureRect = rect.Inset(isSelected ? 2 : 1);
                    if (prefab.PreviewTexture != null)
                    {
                        EditorGUI.DrawPreviewTexture(
                            textureRect, prefab.PreviewTexture, null, ScaleMode.ScaleToFit, 0.0f);
                    }
                    else
                    {
                        const float brightness = 0.325f;
                        EditorGUI.DrawRect(textureRect, new Color(brightness, brightness, brightness));
                    }

                    // Draw a label with a nice semi-transparent backdrop.
                    GUIContent title = new GUIContent(prefab.Prefab.name);
                    float height = PrefabPreviewTextStyle.CalcHeight(title, textureRect.width);
                    
                    EditorGUI.DrawRect(textureRect.GetSubRectFromBottom(height), new Color(0, 0, 0, 0.15f));
                    EditorGUI.LabelField(textureRect, title, PrefabPreviewTextStyle);

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

            // If you didn't click a prefab and weren't pressing SHIFT, clear the selection.
            if (Event.current.type == EventType.MouseDown && !didClickASpecificPrefab && !Event.current.shift)
            {
                prefabsSelected.Clear();
                Repaint();
            }
            
            EditorGUILayout.EndScrollView();
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

        private bool HasEntry(GameObject prefab)
        {
            foreach (PrefabEntry prefabEntry in prefabsToDisplay)
            {
                if (prefabEntry.Prefab == prefab)
                    return true;
            }
            return false;
        }

        private void DropAreaGUI()
        {
            Event @event = Event.current;
            switch (@event.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!IsMouseInPrefabPanel)
                        return;

                    DragAndDrop.AcceptDrag();

                    // Find dragged prefabs.
                    draggedPrefabs.Clear();
                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is GameObject go && go.IsPrefab())
                        {
                            draggedPrefabs.Add(go);
                            continue;
                        }

                        string path = AssetDatabase.GetAssetPath(draggedObject);
                        if (AssetDatabase.IsValidFolder(path))
                        {
                            string[] files = Directory.GetFiles(path, "*.prefab", SearchOption.AllDirectories);
                            for (int i = 0; i < files.Length; i++)
                            {
                                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(files[i]);
                                draggedPrefabs.Add(prefab);
                            }

                            continue;
                        }
                        
                        // Unhandled dragged object. Probably a different asset, like a texture.
                    }

                    DragAndDrop.visualMode = draggedPrefabs.Count > 0
                        ? DragAndDropVisualMode.Copy
                        : DragAndDropVisualMode.Rejected;

                    if (@event.type == EventType.DragPerform)
                    {
                        foreach (GameObject draggedPrefab in draggedPrefabs)
                        {
                            if (HasEntry(draggedPrefab))
                                continue;
                            
                            PrefabEntry entry = new PrefabEntry(draggedPrefab);
                            prefabsToDisplay.Add(entry);
                        }
                    }
                    
                    break;
            }
        }
    }
}
