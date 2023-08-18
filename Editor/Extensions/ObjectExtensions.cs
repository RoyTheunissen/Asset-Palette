using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static class ObjectExtensions
    {
        public static bool IsFolder(this Object @object)
        {
#if !UNITY_EDITOR
            return false;
#else
            string path = AssetDatabase.GetAssetPath(@object);
            return AssetDatabase.IsValidFolder(path);
#endif
        }
    }
}
