using UnityEngine;

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static partial class GameObjectExtensions
    {
        public static bool IsPrefab(this GameObject gameObject)
        {
            return !gameObject.scene.IsValid();
        }
    }
}
