using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Represents an asset that is added to the palette.
    /// </summary>
    [Serializable]
    public class PaletteAsset : PaletteEntry, ISerializationCallbackReceiver
    {
        private const int TextureSize = 128;

        [SerializeField] private GuidBasedReference<Object> guidBasedReference;
        
        //Keeping this for backwards compatibility.
        [SerializeField] private Object asset;
        public Object Asset
        {
            get
            {
#if UNITY_EDITOR
                //Migration from old system to new guid system
                if(guidBasedReference == null || !guidBasedReference.HasGuid && asset != null)
                {
                    guidBasedReference = new GuidBasedReference<Object>(asset);
                    asset = null;
                }
#endif
                return guidBasedReference.Asset;
            }
        }

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

                    string path = AssetDatabase.GetAssetPath(Asset.GetInstanceID());
                    
                    Editor editor = Editor.CreateEditor(Asset, null);
                    cachedPreviewTexture = editor.RenderStaticPreview(path, null, TextureSize, TextureSize);
                    Object.DestroyImmediate(editor);
                }
                return cachedPreviewTexture;
            }
        }
#endif // UNITY_EDITOR

        public PaletteAsset(Object asset)
        {
            guidBasedReference = new GuidBasedReference<Object>(asset);
        }

        public override void Open()
        {
#if UNITY_EDITOR
            AssetDatabase.OpenAsset(Asset);
#endif // UNITY_EDITOR
        }

        public override void GetAssetsToSelect(ref List<Object> selection)
        {
            selection.Add(Asset);
        }

        public override void Refresh()
        {
            base.Refresh();
            
#if UNITY_EDITOR
            didCachePreviewTexture = false;
            cachedPreviewTexture = null;
#endif // UNITY_EDITOR
        }

        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
        }
    }
}
