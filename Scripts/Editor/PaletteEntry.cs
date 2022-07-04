using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.PrefabPalette
{
    /// <summary>
    /// Represents one entry in the palette and manages its properties.
    /// </summary>
    [Serializable]
    public class PaletteEntry
    {
        public const int TextureSize = 128;
            
        [SerializeField] private Object asset;
        public Object Asset => asset;

        [NonSerialized] private Texture2D cachedPreviewTexture;
        [NonSerialized] private bool didCachePreviewTexture;
        public Texture2D PreviewTexture
        {
            get
            {
                if (!didCachePreviewTexture)
                {
                    didCachePreviewTexture = true;

                    string path = AssetDatabase.GetAssetPath(Asset.GetInstanceID());
                    cachedPreviewTexture = Editor.RenderStaticPreview(path, null, TextureSize, TextureSize);
                }
                return cachedPreviewTexture;
            }
        }

        public bool IsValid => Asset != null;

        [NonSerialized] private Editor cachedEditor;
        [NonSerialized] private bool didCacheEditor;
        private Editor Editor
        {
            get
            {
                if (!didCacheEditor)
                {
                    didCacheEditor = true;
                    Editor.CreateCachedEditor(Asset, null, ref cachedEditor);
                }
                return cachedEditor;
            }
        }

        public PaletteEntry(Object asset)
        {
            this.asset = asset;
        }
    }
}
