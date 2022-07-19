using System;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Editor.CustomEditors
{
    /// <summary>
    /// Contains utilities for drawing palette entries and folders.
    /// </summary>
    public static class PaletteDrawing 
    {
        public static void DrawFolder(Rect position, SerializedProperty property, PaletteFolder folder)
        {
            // NOTE: If you wanted to you could support different kinds of folders with custom drawers like you could
            // for entries. For now I've not found a reason to have custom folder types so I'll leave it out for now.
            SerializedProperty nameField = property.FindPropertyRelative("name");
            EditorGUI.LabelField(position, GUIContent.none, new GUIContent(nameField.stringValue));
        }
    }
}
