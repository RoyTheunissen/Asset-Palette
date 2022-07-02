using System;
using System.Collections.Generic;
using System.IO;
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
        [Serializable]
        private class PrefabEntry
        {
            public const int TextureSize = 100;
            
            [SerializeField] private GameObject prefab;
            public GameObject Prefab => prefab;

            [NonSerialized] private Texture2D cachedPreviewTexture;
            [NonSerialized] private bool didCachePreviewTexture;
            public Texture2D PreviewTexture
            {
                get
                {
                    if (!didCachePreviewTexture)
                    {
                        didCachePreviewTexture = true;

                        string path = AssetDatabase.GetAssetPath(Prefab.GetInstanceID());
                        cachedPreviewTexture = Editor.RenderStaticPreview(path, null, TextureSize, TextureSize);
                    }
                    return cachedPreviewTexture;
                }
            }

            public bool IsValid => Prefab != null;

            [NonSerialized] private Editor cachedEditor;
            [NonSerialized] private bool didCacheEditor;
            private Editor Editor
            {
                get
                {
                    if (!didCacheEditor)
                    {
                        didCacheEditor = true;
                        Editor.CreateCachedEditor(Prefab, null, ref cachedEditor);
                    }
                    return cachedEditor;
                }
            }

            public PrefabEntry(GameObject prefab)
            {
                this.prefab = prefab;
            }
        }

        [NonSerialized] private readonly List<PrefabEntry> prefabsToDisplay = new List<PrefabEntry>();
        [NonSerialized] private readonly List<PrefabEntry> prefabsSelected = new List<PrefabEntry>();
        
        [NonSerialized] private readonly List<GameObject> draggedPrefabs = new List<GameObject>();
        
        private Vector2 prefabPreviewsScrollPosition;
        
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
        
        [MenuItem ("Window/General/Prefab Palette")]
        public static void Init() 
        {
            GetWindow<PrefabPaletteWindow>(false, "Prefab Palette");
        }

        private void OnGUI()
        {
            DropAreaGUI();

            DrawPrefabs();
        }

        private void DrawPrefabs()
        {
            prefabPreviewsScrollPosition = GUILayout.BeginScrollView(prefabPreviewsScrollPosition, "Box");

            //Rect containerRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
            float containerWidth = Mathf.Floor(EditorGUIUtility.currentViewWidth) - 14;//containerRect.width;
            const float prefabWidth = 100;
            const float spacing = 2;

            int columnCount = Mathf.FloorToInt(containerWidth / (prefabWidth + spacing));
            int rowCount = Mathf.CeilToInt((float)prefabsToDisplay.Count / columnCount);

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

                    Rect rect = GUILayoutUtility.GetRect(0, 0, GUILayout.Width(prefabWidth), GUILayout.Height(prefabWidth));

                    // Allow this prefab to selected by clicking it.
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        if ((Event.current.modifiers & EventModifiers.Shift) == EventModifiers.Shift)
                        {
                            // Shift+click to add.
                            prefabsSelected.Add(prefab);
                        }
                        else if ((Event.current.modifiers & EventModifiers.Control) == EventModifiers.Control)
                        {
                            // Control+click to remove.
                            prefabsSelected.Remove(prefab);
                        }
                        else
                        {
                            // Regular click to select only this prefab.
                            prefabsSelected.Clear();
                            prefabsSelected.Add(prefab);
                        }
                        
                        Debug.Log($"Clicked on prefab '{prefab.Prefab.name}'");
                        Repaint();
                    }
                    bool isSelected = prefabsSelected.Contains(prefab);
                
                    Color borderColor = isSelected ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                    EditorGUI.DrawRect(rect, borderColor);
                
                    Rect textureRect = rect.Inset(isSelected ? 2 : 1);
                    if (prefab.PreviewTexture != null)
                        EditorGUI.DrawPreviewTexture(textureRect, prefab.PreviewTexture, null, ScaleMode.ScaleToFit, 0.0f);
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
                            Debug.Log($"Dragged prefab {draggedPrefab.name}");
                            
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
