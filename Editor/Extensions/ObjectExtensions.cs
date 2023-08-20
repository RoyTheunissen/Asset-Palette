using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static class ObjectExtensions
    {
        public static bool IsFolder(this Object @object)
        {
            string path = AssetDatabase.GetAssetPath(@object);
            return AssetDatabase.IsValidFolder(path);
        }
    }
}
