using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Windows
{
    /// <summary>
    /// Draws a nice "Open In Asset Palette Window" button when you inspect a collection.
    /// </summary>
    [CustomEditor(typeof(AssetPaletteCollection))]
    public class AssetPaletteCollectionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            AssetPaletteCollection collection = (AssetPaletteCollection)target;

            bool shouldOpen = GUILayout.Button("Open In Asset Palette Window", GUILayout.Height(32));

            if (shouldOpen)
                OpenInAssetPaletteWindow(collection);
        }

        private void OpenInAssetPaletteWindow(AssetPaletteCollection collection)
        {
            AssetPaletteWindow window = AssetPaletteWindow.GetWindow<AssetPaletteWindow>();
            window.CurrentCollection = collection;
            window.Repaint();
        }
    }
}
