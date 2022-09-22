using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Represents an asset that is added to the palette.
    /// </summary>
    [Serializable]
    public class PaletteAsset : PaletteEntry
    {
        private const int TextureSize = 128;
        
        [SerializeField] private Object asset;
        public Object Asset => asset;

        public override bool IsValid => Asset != null;

        protected override string DefaultName => Asset.name;

#if UNITY_EDITOR
        [NonSerialized] private Texture2D cachedPreviewTexture;
        [NonSerialized] private bool didCachePreviewTexture;
        public Texture2D PreviewTexture
        {
            get
            {
                if (!didCachePreviewTexture)
                {
                    didCachePreviewTexture = true;

                    string path = UnityEditor.AssetDatabase.GetAssetPath(Asset.GetInstanceID());
                    cachedPreviewTexture = Editor.RenderStaticPreview(path, null, TextureSize, TextureSize);
                }
                return cachedPreviewTexture;
            }
        }

        [NonSerialized] private UnityEditor.Editor cachedEditor;
        [NonSerialized] private bool didCacheEditor;
        private UnityEditor.Editor Editor
        {
            get
            {
                if (!didCacheEditor)
                {
                    didCacheEditor = true;
                    UnityEditor.Editor.CreateCachedEditor(Asset, null, ref cachedEditor);
                }
                return cachedEditor;
            }
        }
#endif // UNITY_EDITOR

        public PaletteAsset(Object asset)
        {
            this.asset = asset;
        }

        public override void Open()
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.OpenAsset(Asset);
#endif // UNITY_EDITOR
        }

        public override void SelectAsset()
        {
            base.SelectAsset();
            
#if UNITY_EDITOR
            UnityEditor.Selection.activeObject = asset;
#endif // UNITY_EDITOR
        }

        public override void Refresh()
        {
            base.Refresh();
            
#if UNITY_EDITOR
            didCachePreviewTexture = false;
            cachedPreviewTexture = null;
#endif // UNITY_EDITOR
        }
    }
}
