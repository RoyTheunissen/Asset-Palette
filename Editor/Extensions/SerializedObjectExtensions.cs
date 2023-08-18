using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Windows;
using UnityEditor;

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static partial class SerializedObjectExtensions
    {
        /// <summary>
        /// Gets the corresponding serialized property from a "reference id path".
        /// See: SerializedPropertyExtensions.GetReferenceIdPath
        /// </summary>
        public static SerializedProperty FindPropertyFromGuidPath(
            this SerializedObject serializedObject, string path,
            string rootCollectionName, string childPropertyName = null)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (string.IsNullOrEmpty(childPropertyName))
                childPropertyName = rootCollectionName;

            SerializedProperty rootCollectionProperty = serializedObject.FindProperty(rootCollectionName);

            List<string> sections = new List<string>(path.Split(SerializedPropertyExtensions.GuidPathSeparator));

            SerializedProperty propertyOfFirstSection = GetArrayElementWithGuid(
                rootCollectionProperty, sections[0]);

            if (propertyOfFirstSection == null)
                return null;
            
            // If there was only one section in the path, the property of the first section is the one we need.
            if (sections.Count == 1)
                return propertyOfFirstSection;

            // There were other sections in the path, so recurse deeper.
            sections.RemoveAt(0);
            string remainingPath = string.Join(SerializedPropertyExtensions.GuidPathSeparator, sections);
            
            return GetPropertyFromGuidPathRecursive(propertyOfFirstSection, remainingPath, childPropertyName);
        }
        
        private static SerializedProperty GetArrayElementWithGuid(
            SerializedProperty collectionProperty, string id)
        {
            for (int i = 0; i < collectionProperty.arraySize; i++)
            {
                SerializedProperty childProperty = collectionProperty.GetArrayElementAtIndex(i);
                if (string.Equals(childProperty.FindPropertyRelative(FolderPanel.GuidPropertyName).stringValue, id))
                    return childProperty;
            }
            
            return null;
        }
        
        private static SerializedProperty GetPropertyFromGuidPathRecursive(
            SerializedProperty serializedProperty, string path, string childPropertyName)
        {
            SerializedProperty collectionProperty = serializedProperty.FindPropertyRelative(childPropertyName);

            List<string> sections = new List<string>(path.Split(SerializedPropertyExtensions.GuidPathSeparator));

            SerializedProperty propertyOfFirstSection = GetArrayElementWithGuid(
                collectionProperty, sections[0]);

            if (propertyOfFirstSection == null)
                return null;
            
            // If there was only one section remaining in the path, the property of the first section is the one we need.
            if (sections.Count == 1)
                return propertyOfFirstSection;

            // There were other sections in the path, so recurse deeper.
            sections.RemoveAt(0);
            string remainingPath = string.Join(SerializedPropertyExtensions.GuidPathSeparator, sections);
            
            return GetPropertyFromGuidPathRecursive(
                propertyOfFirstSection, remainingPath, childPropertyName);
        }
    }
}
