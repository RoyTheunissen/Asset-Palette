using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// There's two reasons why you should be using this class instead of having direct Object references:
    /// 
    /// - This way the actual loading of the assets is delayed as much as possible. The palette will not load any of
    /// the references until it's necessary, for example for generating a preview or actually using a palette entry.
    /// 
    /// - This way the Personal Palette can correctly serialize and deserialize the asset reference. The Personal
    /// Palette is not stored in an asset but is stored in the EditorPrefs as JSON instead. By default Unity's
    /// JSON utility does not seem to deserialize asset references correctly and they will become corrupt.
    /// </summary>
    [Serializable]
    public class GuidBasedReference<T>  where T : Object
    {
        [SerializeField] private string guid;
        public bool HasGuid => !string.IsNullOrEmpty(guid);


        private bool didCacheAsset;
        private T cachedAsset;
        public T Asset
        {
            get
            {
                if (!didCacheAsset)
                {
#if UNITY_EDITOR
                    cachedAsset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                    didCacheAsset = cachedAsset != null;
#endif
                }

                return cachedAsset;
            }
        }

        public GuidBasedReference(T targetObject)
        {
#if UNITY_EDITOR
            guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(targetObject));
#endif
            cachedAsset = targetObject;
            didCacheAsset = true;
        }
    }
}
