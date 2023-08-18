using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
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
