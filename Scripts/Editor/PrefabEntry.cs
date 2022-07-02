using System;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.PrefabPalette
{
    /// <summary>
    /// Represents one entry in the palette and manages its properties.
    /// </summary>
    [Serializable]
    public class PrefabEntry
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
}
