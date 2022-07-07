using UnityEditor;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette.Utilities
{
    /// <summary>
    /// Helps you access an asset at editor time, for automated workflows and the like.
    /// </summary>
    public class EditorAssetReference<T> where T : Object
    {
        private T cachedAsset;
        public T Asset
        {
            get
            {
                if (cachedAsset == null)
                    cachedAsset = AssetDatabase.LoadAssetAtPath<T>(path);
                return cachedAsset;
            }
        }

        private string path;

        public EditorAssetReference(string path)
        {
            this.path = path;
        }

        public static implicit operator T(EditorAssetReference<T> editorAssetReference)
        {
            return editorAssetReference.Asset;
        }
    }
}
