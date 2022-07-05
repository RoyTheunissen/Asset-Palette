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
        public enum AddFolderBehaviour
        {
            Undefined,
            AddShortcutToFolder,
            AddFolderContents,
        }
        
        private const string EditorPrefPrefix = "RoyTheunissen/PrefabPalette/";
        private const string CurrentCollectionGUIDEditorPref = EditorPrefPrefix + "CurrentCollectionGUID";
        private const string FolderPanelWidthEditorPref = EditorPrefPrefix + "FolderPanelWidth";
        private const string ZoomLevelEditorPref = EditorPrefPrefix + "ZoomLevel";
        private const string SelectedFolderIndexEditorPref = EditorPrefPrefix + "SelectedFolderIndex";

        private const string FolderDragGenericDataType = "AssetPaletteFolderDrag";
        private const string EntryDragGenericDataType = "AssetPaletteEntryDrag";

        public const int EntrySizeMax = 128;
        private const float EntrySizeMin = EntrySizeMax * 0.45f;
        
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

        private const float EntrySpacing = 4;
        
        private const float DividerBrightness = 0.13f;
        private static readonly Color DividerColor = new Color(DividerBrightness, DividerBrightness, DividerBrightness);

        private const string InitialFolderName = "Default";
        private const string NewFolderName = "New Folder";
        private const int MaxUniqueFolderNameAttempts = 100;
        
        private const string ZoomLevelControlName = "AssetPaletteEntriesZoomLevelControl";
        
        [NonSerialized] private readonly List<PaletteEntry> entriesSelected = new List<PaletteEntry>();
        [NonSerialized] private readonly List<PaletteEntry> entriesIndividuallySelected = new List<PaletteEntry>();

        private Vector2 entriesPreviewsScrollPosition;
        private Vector2 folderPanelScrollPosition;
        
        [NonSerialized] private bool isResizingFolderPanel;

        [NonSerialized] private GUIStyle cachedMessageTextStyle;
        [NonSerialized] private bool didCacheMessageTextStyle;
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
        
        [NonSerialized] private SerializedObject cachedCurrentCollectionSerializedObject;
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

        private bool IsZoomLevelFocused => GUI.GetNameOfFocusedControl() == ZoomLevelControlName;
        
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
                int clampedValue = Mathf.Clamp(value, 0, CurrentCollection.Folders.Count - 1);
                
                if (SelectedFolderIndex == clampedValue)
                    return;
                
                EditorPrefs.SetInt(SelectedFolderIndexEditorPref, clampedValue);
                
                // Need to clear the cached serializedproperty now.
                didCacheSelectedFolderSerializedProperty = false;
                
                // If you change the folder that's selected, we need to clear the selection.
                ClearEntrySelection();
            }
        }

        private PaletteFolder SelectedFolder
        {
            get
            {
                EnsureFolderExists();
                return CurrentCollection.Folders[SelectedFolderIndex];
            }
            set => SelectedFolderIndex = CurrentCollection.Folders.IndexOf(value);
        }

        [NonSerialized] private bool didCacheSelectedFolderSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderSerializedProperty;
        private SerializedProperty SelectedFolderSerializedProperty
        {
            get
            {
                if (!didCacheSelectedFolderSerializedProperty)
                {
                    EnsureFolderExists();
                    didCacheSelectedFolderSerializedProperty = true;
                    cachedSelectedFolderSerializedProperty = CurrentCollectionSerializedObject.FindProperty("folders")
                        .GetArrayElementAtIndex(SelectedFolderIndex);
                }

                return cachedSelectedFolderSerializedProperty;
            }
        }
        
        [NonSerialized] private bool didCacheSelectedFolderEntriesSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderEntriesSerializedProperty;
        private SerializedProperty SelectedFolderEntriesSerializedProperty
        {
            get
            {
                if (!didCacheSelectedFolderEntriesSerializedProperty)
                {
                    didCacheSelectedFolderEntriesSerializedProperty = true;
                    cachedSelectedFolderEntriesSerializedProperty =
                        SelectedFolderSerializedProperty.FindPropertyRelative("entries");
                }
                return cachedSelectedFolderEntriesSerializedProperty;
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
        
        [NonSerialized] private AddFolderBehaviour addFolderBehaviour;
        [NonSerialized] private readonly List<Object> draggedObjectsToProcess = new List<Object>();
        [NonSerialized] private readonly List<PaletteEntry> entriesToAddFromDraggedAssets = new List<PaletteEntry>();

        [NonSerialized] private bool isMouseInHeader;
        [NonSerialized] private bool isMouseInFooter;
        [NonSerialized] private bool isMouseInFolderPanel;
        [NonSerialized] private bool isMouseInEntriesPanel;

        [MenuItem ("Window/General/Asset Palette")]
        public static void Init() 
        {
            AssetPaletteWindow window = GetWindow<AssetPaletteWindow>(false, "Asset Palette");
            window.minSize = new Vector2(WindowWidthMin, WindowHeightMin);
            window.wantsMouseMove = true;
        }

        private void OnGUI()
        {
            UpdateMouseOverStates();
            
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
                    HandleAssetDropping();

                    DrawEntriesPanel();

                    DrawFooter();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void UpdateMouseOverStates()
        {
            // Need to do this here, because Event.current.mousePosition is relative to any scrollviews, so doing these
            // same checks within say the entries panel will yield different results.
            isMouseInHeader = Event.current.mousePosition.y <= HeaderHeight;
            isMouseInFooter = Event.current.mousePosition.y >= position.height - FooterHeight;
            isMouseInFolderPanel = !isMouseInHeader && Event.current.mousePosition.x < FolderPanelWidth;

            isMouseInEntriesPanel = !isMouseInHeader && !isMouseInFooter && !isMouseInFolderPanel;
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
            SerializedProperty newElement = foldersProperty.AddArrayElement();
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

        private void EnsureFolderExists()
        {
            if (CurrentCollection.Folders.Count > 0)
                return;
            
            // Make sure there is at least one folder.
            CurrentCollectionSerializedObject.Update();
            CreateNewFolder<PaletteFolder>(InitialFolderName);
            CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void DrawFolderPanel()
        {
            // It seems like mouse events are relative to scroll views.
            bool didClickAnywhereInWindow = Event.current.type == EventType.MouseDown;

            folderPanelScrollPosition = EditorGUILayout.BeginScrollView(
                folderPanelScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Width(FolderPanelWidth));

            // Cancel out if there's no collection available.
            if (CurrentCollection != null)
            {
                EnsureFolderExists();

                currentFolderDragIndex = -1;
                
                if (CurrentCollection != null)
                {
                    CurrentCollectionSerializedObject.Update();
                    using (SerializedProperty foldersProperty = CurrentCollectionSerializedObject.FindProperty("folders"))
                    {
                        DrawFolders(foldersProperty, didClickAnywhereInWindow);
                    }
                    CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            
            EditorGUILayout.EndScrollView();

            if (isDraggingFolder && Event.current.type == EventType.DragUpdated ||
                Event.current.type == EventType.DragPerform)
            {
                if (!isMouseInFolderPanel)
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

        private void DrawFolders(SerializedProperty foldersProperty, bool didClickAnywhereInWindow)
        {
            Color selectionColor = SelectionColor;
            selectionColor.a = 0.25f;

            for (int i = 0; i < foldersProperty.arraySize; i++)
            {
                SerializedProperty folderProperty = foldersProperty.GetArrayElementAtIndex(i);
                float folderHeight = EditorGUI.GetPropertyHeight(folderProperty, GUIContent.none, true);
                float folderWidth = FolderPanelWidth;
                Rect folderRect = GUILayoutUtility.GetRect(folderWidth, folderHeight);
                bool isSelected = SelectedFolderIndex == i;
                
                // Draw the actual folder itself.
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

        private Color cachedSelectionColor;
        private bool didCacheSelectionColor;
        private Color SelectionColor
        {
            get
            {
                if (!didCacheSelectionColor)
                {
                    didCacheSelectionColor = true;
                    cachedSelectionColor = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).settings.selectionColor;
                }
                return cachedSelectionColor;
            }
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

                    GUI.SetNextControlName(ZoomLevelControlName);
                    ZoomLevel = GUI.HorizontalSlider(zoomLevelRect, ZoomLevel, 0.0f, 1.0f);

                    GUILayout.Space(16);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndVertical();
        }

        private int GetEntryCount()
        {
            return SelectedFolder.Entries.Count;
        }
        
        private List<PaletteEntry> GetEntries()
        {
            return SelectedFolder.Entries;
        }
        
        private PaletteEntry GetEntry(int index)
        {
            return SelectedFolder.Entries[index];
        }
        
        private void AddEntry(PaletteEntry entry, bool partOfMultipleAdditions)
        {
            if (!partOfMultipleAdditions)
                CurrentCollectionSerializedObject.Update();
            
            SerializedProperty newEntryProperty = SelectedFolderEntriesSerializedProperty.AddArrayElement();
            newEntryProperty.managedReferenceValue = entry;
            
            if (!partOfMultipleAdditions)
                CurrentCollectionSerializedObject.ApplyModifiedProperties();
            
            SelectEntry(entry, false);
        }

        private void RemoveEntry(PaletteEntry entry)
        {
            int index = GetEntries().IndexOf(entry);
            if (index != -1)
                RemoveEntryAt(index);
        }
        
        private void RemoveEntryAt(int index)
        {
            CurrentCollectionSerializedObject.Update();
            SelectedFolderEntriesSerializedProperty.DeleteArrayElementAtIndex(index);
            CurrentCollectionSerializedObject.ApplyModifiedProperties();
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
                SelectEntries(GetEntries(), true);
                if (GetEntryCount() > 0)
                    entriesIndividuallySelected.Add(GetEntry(GetEntryCount() - 1));
                Repaint();
                return;
            }

            if (Event.current.keyCode == KeyCode.Delete)
            {
                if (isMouseInEntriesPanel)
                {
                    // Pressing Delete will remove all selected entries from the palette.
                    foreach (PaletteEntry entry in entriesSelected)
                    {
                        RemoveEntry(entry);
                    }

                    ClearEntrySelection();
                    Repaint();
                }
                else if (isMouseInFolderPanel && CurrentCollection != null && CurrentCollection.Folders.Count > 1 &&
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

            // Draw a dark background for the entries panel.
            Rect entriesPanelRect = new Rect(0, 0, position.width - FolderPanelWidth, 90000000);
            EditorGUI.DrawRect(entriesPanelRect, new Color(0, 0, 0, 0.1f));
            
            // If the current state is invalid, draw a message instead.
            AssetPaletteCollection currentCollection = CurrentCollection;
            bool hasCollection = currentCollection != null;
            bool hasEntries = hasCollection && GetEntryCount() > 0;
            if (!hasCollection || !hasEntries)
            {
                DrawEntryPanelMessage(hasCollection);
                GUILayout.EndScrollView();
                return;
            }
            
            float containerWidth = Mathf.Floor(EditorGUIUtility.currentViewWidth) - FolderPanelWidth;

            const float padding = 2;
            int entrySize = Mathf.RoundToInt(Mathf.Lerp(EntrySizeMin, EntrySizeMax, ZoomLevel));

            int columnCount = Mathf.FloorToInt(containerWidth / (entrySize + EntrySpacing));
            int rowCount = Mathf.CeilToInt((float)GetEntryCount() / columnCount);
                
            GUILayout.Space(padding);
            
            bool didClickASpecificEntry = false;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(padding);
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    int index = rowIndex * columnCount + columnIndex;
                    
                    // Purge invalid entries.
                    while (index < GetEntryCount() && !GetEntry(index).IsValid)
                    {
                        RemoveEntryAt(index);
                    }

                    if (index >= GetEntryCount())
                    {
                        GUILayout.FlexibleSpace();
                        break;
                    }
                    
                    DrawEntry(index, entrySize, ref didClickASpecificEntry);
                    
                    if (columnIndex < columnCount - 1)
                        EditorGUILayout.Space(EntrySpacing);
                    else
                        GUILayout.FlexibleSpace();
                }
                GUILayout.Space(padding);
                EditorGUILayout.EndHorizontal();
                
                if (rowIndex < rowCount - 1)
                    EditorGUILayout.Space(EntrySpacing);
            }
            
            GUILayout.Space(padding);

            // If you didn't click an entry and weren't pressing SHIFT, clear the selection.
            if (Event.current.type == EventType.MouseDown && !didClickASpecificEntry && !Event.current.shift)
            {
                ClearEntrySelection();
                Repaint();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(int index, int entrySize, ref bool didClickASpecificEntry)
        {
            PaletteEntry entry = GetEntry(index);

            Rect rect = GUILayoutUtility.GetRect(
                0, 0, GUILayout.Width(entrySize), GUILayout.Height(entrySize));

            // Allow this entry to be selected by clicking it.
            bool isMouseOnEntry = rect.Contains(Event.current.mousePosition) && isMouseInEntriesPanel;
            bool wasAlreadySelected = entriesSelected.Contains(entry);
            if (Event.current.type == EventType.MouseDown && isMouseOnEntry)
            {
                if (Event.current.shift)
                {
                    // Shift+click to grow selection until this point.
                    if (entriesSelected.Count == 0)
                    {
                        SelectEntry(entry, true);
                    }
                    else
                    {
                        if (wasAlreadySelected)
                        {
                            // Define a new extremity away from the first individually selected entry.
                            // Might seem convoluted and with weird edge cases, but this is how Unity does it...
                            PaletteEntry firstEntryIndividuallySelected = entriesIndividuallySelected[0];
                            int indexOfFirstIndividuallySelectedEntry =
                                GetEntries().IndexOf(firstEntryIndividuallySelected);
                            if (index > indexOfFirstIndividuallySelectedEntry)
                            {
                                PaletteEntry lowestSelectedEntry = null;
                                for (int i = 0; i < GetEntryCount(); i++)
                                {
                                    if (entriesSelected.Contains(GetEntry(i)))
                                    {
                                        lowestSelectedEntry = GetEntry(i);
                                        break;
                                    }
                                }

                                entriesIndividuallySelected.Clear();
                                entriesIndividuallySelected.Add(lowestSelectedEntry);
                                indexOfFirstIndividuallySelectedEntry = GetEntries().IndexOf(lowestSelectedEntry);
                            }
                            else if (index < indexOfFirstIndividuallySelectedEntry)
                            {
                                PaletteEntry highestSelectedEntry = null;
                                for (int i = GetEntryCount() - 1; i >= 0; i--)
                                {
                                    if (entriesSelected.Contains(GetEntry(i)))
                                    {
                                        highestSelectedEntry = GetEntry(i);
                                        break;
                                    }
                                }

                                entriesIndividuallySelected.Clear();
                                entriesIndividuallySelected.Add(highestSelectedEntry);
                                indexOfFirstIndividuallySelectedEntry = GetEntries().IndexOf(highestSelectedEntry);
                            }

                            SelectEntriesByRange(indexOfFirstIndividuallySelectedEntry, index, true);
                        }
                        else
                        {
                            // Grow the selection from the last individually selected entry.
                            PaletteEntry lastEntryIndividuallySelected = entriesIndividuallySelected[^1];
                            int indexOfLastIndividuallySelectedEntry =
                                GetEntries().IndexOf(lastEntryIndividuallySelected);
                            SelectEntriesByRange(indexOfLastIndividuallySelectedEntry, index, false);
                        }
                    }
                }
                else if (Event.current.control)
                {
                    // Control+click to add specific files to the selection.
                    if (!wasAlreadySelected)
                        SelectEntry(entry, false);
                    else
                        DeselectEntry(entry);
                }
                else
                {
                    // Regular click to select only this entry.
                    if (!wasAlreadySelected)
                        SelectEntry(entry, true);

                    // Allow assets to be opened by double clicking on them.
                    if (Event.current.clickCount == 2)
                        entry.Open();
                }

                didClickASpecificEntry = true;
                Repaint();
            }
            else if (Event.current.type == EventType.MouseUp && isMouseOnEntry && !Event.current.control &&
                     !Event.current.shift)
            {
                // Regular click to select only this entry.
                SelectEntry(entry, true);
                Repaint();
            }
            else if (Event.current.type == EventType.MouseDrag && isMouseOnEntry && !isResizingFolderPanel &&
                     isMouseInEntriesPanel && !IsZoomLevelFocused)
            {
                DragAndDrop.PrepareStartDrag();
                List<Object> selectedAssets = new List<Object>();
                foreach (PaletteEntry selectedEntry in entriesSelected)
                {
                    if (selectedEntry is PaletteAsset paletteAsset)
                        selectedAssets.Add(paletteAsset.Asset);
                }

                DragAndDrop.objectReferences = selectedAssets.ToArray();
                // Mark the drag as being an asset palette entry drag, so we know not to accept it again ourselves.
                DragAndDrop.SetGenericData(EntryDragGenericDataType, true);
                DragAndDrop.StartDrag("Drag from Asset Palette");
            }

            bool isSelected = entriesSelected.Contains(entry);

            Color borderColor = isSelected ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            float borderWidth = isSelected ? 2 : 1;
            Rect borderRect = rect.Expand(borderWidth);
            GUI.DrawTexture(
                borderRect, EditorGUIUtility.whiteTexture, ScaleMode.ScaleToFit, true, 0.0f, borderColor, borderWidth,
                borderWidth);

            // Actually draw the contents of the entry.
            Rect entryContentsRect = rect;
            SerializedProperty entryProperty = SelectedFolderEntriesSerializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(entryContentsRect, entryProperty, GUIContent.none);
        }

        private void DrawEntryPanelMessage(bool hasCollection)
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
        }

        private void DeselectEntry(PaletteEntry entry)
        {
            entriesSelected.Remove(entry);
            entriesIndividuallySelected.Remove(entry);
        }

        private void SelectEntry(PaletteEntry entry, bool exclusively)
        {
            if (exclusively)
                ClearEntrySelection();
            entriesSelected.Add(entry);
            entriesIndividuallySelected.Add(entry);
        }

        private void SelectEntriesByRange(int from, int to, bool exclusively)
        {
            if (exclusively)
            {
                entriesSelected.Clear();
                // NOTE: Do NOT clear the individually selected entries. These are used to determine from what point we 
                // define the range. Used for SHIFT-selecting ranges of entries.
            }

            int direction = from <= to ? 1 : -1;
            for (int i = from; i != to; i += direction)
            {
                if (!entriesSelected.Contains(GetEntry(i)))
                    entriesSelected.Add(GetEntry(i));
            }
            if (!entriesSelected.Contains(GetEntry(to)))
                entriesSelected.Add(GetEntry(to));
        }
        
        private void SelectEntries(List<PaletteEntry> entries, bool exclusively)
        {
            if (exclusively)
                ClearEntrySelection();

            foreach (PaletteEntry entry in entries)
            {
                entriesSelected.Add(entry);
            }
        }

        private void ClearEntrySelection()
        {
            entriesSelected.Clear();
            entriesIndividuallySelected.Clear();
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
