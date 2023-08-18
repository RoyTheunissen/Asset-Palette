using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Windows;
using UnityEditor;

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static partial class SerializedObjectExtensions
    {
        /// <summary>
        /// Gets the corresponding serialized property from an "id path".
        /// See: SerializedPropertyExtensions.GetIdPath
        /// </summary>
        public static SerializedProperty FindPropertyFromIdPath(
            this SerializedObject serializedObject, string path, string idPropertyName,
            string childPropertyName, string rootCollectionPropertyName = null)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            // Optionally, you can specify that the collection of root entries has a different name than child entries. 
            if (string.IsNullOrEmpty(rootCollectionPropertyName))
                rootCollectionPropertyName = childPropertyName;

            SerializedProperty rootCollectionProperty = serializedObject.FindProperty(rootCollectionPropertyName);

            List<string> sections = new List<string>(path.Split(SerializedPropertyExtensions.GuidPathSeparator));

            SerializedProperty propertyOfFirstSection = GetArrayElementWithId(
                rootCollectionProperty, idPropertyName, sections[0]);

            if (propertyOfFirstSection == null)
                return null;
            
            // If there was only one section in the path, the property of the first section is the one we need.
            if (sections.Count == 1)
                return propertyOfFirstSection;

            // There were other sections in the path, so recurse deeper.
            sections.RemoveAt(0);
            string remainingPath = string.Join(SerializedPropertyExtensions.GuidPathSeparator, sections);
            
            return GetPropertyFromGuidPathRecursive(
                propertyOfFirstSection, remainingPath, idPropertyName, childPropertyName);
        }
        
        private static SerializedProperty GetArrayElementWithId(
            SerializedProperty collectionProperty, string idPropertyName, string id)
        {
            for (int i = 0; i < collectionProperty.arraySize; i++)
            {
                SerializedProperty childProperty = collectionProperty.GetArrayElementAtIndex(i);

                SerializedProperty idProperty = childProperty.FindPropertyRelative(idPropertyName);

                if (string.Equals(idProperty.GetIdForPath(), id))
                    return childProperty;
            }
            
            return null;
        }
        
        private static SerializedProperty GetPropertyFromGuidPathRecursive(
            SerializedProperty serializedProperty, string path, string idPropertyName, string childPropertyName)
        {
            SerializedProperty collectionProperty = serializedProperty.FindPropertyRelative(childPropertyName);

            List<string> sections = new List<string>(path.Split(SerializedPropertyExtensions.GuidPathSeparator));

            SerializedProperty propertyOfFirstSection = GetArrayElementWithId(
                collectionProperty, idPropertyName, sections[0]);

            if (propertyOfFirstSection == null)
                return null;
            
            // If there was only one section remaining in the path, the property of the first section is the one we need.
            if (sections.Count == 1)
                return propertyOfFirstSection;

            // There were other sections in the path, so recurse deeper.
            sections.RemoveAt(0);
            string remainingPath = string.Join(SerializedPropertyExtensions.GuidPathSeparator, sections);
            
            return GetPropertyFromGuidPathRecursive(
                propertyOfFirstSection, remainingPath, idPropertyName, childPropertyName);
        }
    }
}
