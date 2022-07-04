using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.PrefabPalette
{
    /// <summary>
    /// Draws a folder.
    /// </summary>
    [CustomPropertyDrawer(typeof(PaletteFolder))]
    public class PaletteFolderPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            //PaletteFolder folder = this.GetActualObject<PaletteFolder>(fieldInfo, property);

            SerializedProperty nameField = property.FindPropertyRelative("name");
            EditorGUI.LabelField(position, GUIContent.none, new GUIContent(nameField.stringValue));
        }
    }
}
