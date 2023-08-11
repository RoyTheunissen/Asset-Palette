using System.Linq;
using UnityEditor;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// Finds out if a palette asset was importer.
    /// </summary>
    public class AssetPaletteAssetImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            AssetPaletteWindow window = EditorWindow.GetWindow<AssetPaletteWindow>();
            if (window == null || window.CurrentCollection == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(window.CurrentCollection);
            if (!importedAssets.Contains(assetPath))
                return;

            window.OnPaletteAssetImported();
        }
    }
}
