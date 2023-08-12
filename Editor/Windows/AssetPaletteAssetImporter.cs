using System.Linq;
using UnityEditor;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// Finds out if a currently viewed palette asset was imported. If so we need to notify the window so it can
    /// correctly update its treeview for the folders. Otherwise if you discard folder changes in Git then the folders
    /// will not correctly update accordingly.
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

            window.OnCurrentPaletteAssetImported();
        }
    }
}
