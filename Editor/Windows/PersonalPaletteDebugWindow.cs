using RoyTheunissen.AssetPalette.Windows;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Helps you debug Personal Palette JSON.
    /// </summary>
    public sealed class PersonalPaletteDebugWindow : EditorWindow
    {
        [SerializeField] private Vector2 scrollPosition;

        private void OnEnable()
        {
            AssetPaletteWindow.SavedPersonalPaletteJsonEvent -= HandleSavedPersonalPaletteJsonEvent;
            AssetPaletteWindow.SavedPersonalPaletteJsonEvent += HandleSavedPersonalPaletteJsonEvent;
        }

        private void OnDisable()
        {
            AssetPaletteWindow.SavedPersonalPaletteJsonEvent -= HandleSavedPersonalPaletteJsonEvent;
        }

        private void HandleSavedPersonalPaletteJsonEvent()
        {
            Repaint();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            string json = EditorPrefs.GetString(AssetPaletteWindow.PersonalPaletteStorageKeyEditorPref);
            EditorGUILayout.LabelField(json, EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.EndScrollView();
        }
    }
}
