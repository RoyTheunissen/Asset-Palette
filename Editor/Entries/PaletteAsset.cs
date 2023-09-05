using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Represents an asset that is added to the palette.
    /// </summary>
    [Serializable]
    public class PaletteAsset : PaletteEntry
    {
        private const int TextureSize = 128;
        
        [SerializeField] private GuidBasedReference<Object> assetReference;
        
        // Keeping this for backwards compatibility.
        [SerializeField] private Object asset;
        
        public Object Asset
        {
            get
            {
                // Backwards compatibility with old palettes that had direct references.
                assetReference.InitializeFromExistingDirectReference(ref assetReference, ref asset);
                
                return assetReference.Asset;
            }
        }

        public override bool IsValid => Asset != null;

        protected override string DefaultName => Asset.name;

        private Editor cachedEditor;
        
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

                    Editor.CreateCachedEditor(Asset, null, ref cachedEditor);
                    cachedPreviewTexture = cachedEditor.RenderStaticPreview(path, null, TextureSize, TextureSize);
                }
                return cachedPreviewTexture;
            }
        }

        public PaletteAsset(Object asset)
        {
            assetReference = new GuidBasedReference<Object>(asset);
        }

        public override void Open()
        {
            AssetDatabase.OpenAsset(Asset);
        }

        public override void GetAssetsToSelect(ref List<Object> selection)
        {
            selection.Add(Asset);
        }

        public override void Refresh()
        {
            base.Refresh();
            
            didCachePreviewTexture = false;
            cachedPreviewTexture = null;
        }

    }
}
