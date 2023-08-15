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
        public delegate void AssetsImportedHandler(string[] importedAssetPaths);
        public static event AssetsImportedHandler AssetsImportedEvent;
        
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            AssetsImportedEvent?.Invoke(importedAssets);
        }
    }
}
