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
        private const string NavigationPanelWidthEditorPref = EditorPrefPrefix + "NavigationPanelWidth";
        private const string ZoomLevelEditorPref = EditorPrefPrefix + "ZoomLevel";

        private const float PrefabSizeMax = PrefabEntry.TextureSize;
        private const float PrefabSizeMin = PrefabEntry.TextureSize * 0.45f;
        
        private const float NavigationPanelWidthMin = 100;
        private const float PrefabPanelWidthMin = 200;
        private const float PrefabPanelHeightMin = 50;
        private const float WindowWidthMin = NavigationPanelWidthMin + PrefabPanelWidthMin;
        private static float FooterHeight => EditorGUIUtility.singleLineHeight + 6;
        private static readonly float WindowHeightMin = FooterHeight + PrefabPanelHeightMin;
        
        private const float DividerBrightness = 0.13f;
        private static readonly Color DividerColor = new Color(DividerBrightness, DividerBrightness, DividerBrightness);
        
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
                }
                return cachedPrefabPreviewTextStyle;
            }
        }

        private float NavigationPanelWidth
        {
            get
            {
                if (!EditorPrefs.HasKey(NavigationPanelWidthEditorPref))
                    NavigationPanelWidth = 200;
                return EditorPrefs.GetFloat(NavigationPanelWidthEditorPref);
            }
            set => EditorPrefs.SetFloat(NavigationPanelWidthEditorPref, value);
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

        [MenuItem ("Window/General/Prefab Palette")]
        public static void Init() 
        {
            PrefabPaletteWindow window = GetWindow<PrefabPaletteWindow>(false, "Prefab Palette");
            window.minSize = new Vector2(WindowWidthMin, WindowHeightMin);
            window.wantsMouseMove = true;
        }

        private void OnGUI()
        {
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

                    PerformKeyboardShortcuts();

                    DrawPrefabs();

                    DrawFooter();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNavigationPanel()
        {
            navigationPanelScrollPosition = EditorGUILayout.BeginScrollView(
                navigationPanelScrollPosition, GUILayout.Width(NavigationPanelWidth));
            EditorGUILayout.EndScrollView();

            const int thickness = 1;
            Rect dividerRect = new Rect(NavigationPanelWidth - thickness, 0, thickness, position.height);
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
            // Allow all currently visible prefabs to be selected if CTRL+A is pressed. 
            if (Event.current.type == EventType.KeyDown && Event.current.control && Event.current.keyCode == KeyCode.A)
            {
                prefabsSelected.Clear();
                prefabsSelected.AddRange(prefabsToDisplay);
                Repaint();
                return;
            }

            // Pressing Delete will remove all selected prefabs from the palette.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
            {
                foreach (PrefabEntry prefabEntry in prefabsSelected)
                {
                    prefabsToDisplay.Remove(prefabEntry);
                }
                prefabsSelected.Clear();
                Repaint();
            }
        }

        private void DrawPrefabs()
        {
            prefabPreviewsScrollPosition = GUILayout.BeginScrollView(prefabPreviewsScrollPosition, "Box");
            
            // Needs to be shortened because there must be space for a scrollbar and such...
            float containerWidth = Mathf.Floor(EditorGUIUtility.currentViewWidth) - NavigationPanelWidth - 22;
            const float spacing = 2;

            float prefabSize = Mathf.Lerp(PrefabSizeMin, PrefabSizeMax, ZoomLevel);

            int columnCount = Mathf.FloorToInt(containerWidth / (prefabSize + spacing));
            int rowCount = Mathf.CeilToInt((float)prefabsToDisplay.Count / columnCount);
                
            bool didClickASpecificPrefab = false;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                EditorGUILayout.BeginHorizontal();
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
                    bool isMouseOnPrefab = rect.Contains(Event.current.mousePosition);
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
                    EditorGUI.LabelField(textureRect, prefab.Prefab.name, PrefabPreviewTextStyle);

                    if (columnIndex < columnCount - 1)
                        EditorGUILayout.Space(spacing);
                    else
                        GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
                
                if (rowIndex < rowCount - 1)
                    EditorGUILayout.Space(spacing);
            }

            // If you didn't click a prefab and weren't pressing SHIFT, clear the selection.
            if (Event.current.type == EventType.MouseDown && !didClickASpecificPrefab && !Event.current.shift)
            {
                prefabsSelected.Clear();
                Repaint();
            }
            
            EditorGUILayout.EndScrollView();
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
                    // This is if you only want part of the screen be droppable.
//                    if (!position.Contains(@event.mousePosition))
//                        return;

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
