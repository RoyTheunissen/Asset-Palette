using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Windows;
using UnityEditor;

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static partial class SerializedObjectExtensions
    {
        public static SerializedProperty FindPropertyFromId(
            this SerializedObject serializedObject, int id, string rootCollectionPath)
        {
            SerializedProperty rootCollectionProperty = serializedObject.FindProperty(rootCollectionPath);
            if (rootCollectionProperty == null)
                return null;

            SerializedProperty targetFolder = null;
            for (int i = 0; i < rootCollectionProperty.arraySize; i++)
            {
                SerializedProperty childProperty = rootCollectionProperty.GetArrayElementAtIndex(i);

                targetFolder = FindFolderByIdRecursively(childProperty, id);
                if (targetFolder != null)
                    break;
            }

            return targetFolder;
        }

        private static SerializedProperty FindFolderByIdRecursively(
            SerializedProperty paletteFolderProperty, int targetID)
        {
            if (paletteFolderProperty.FindPropertyRelative(AssetPaletteWindow.IdPropertyName).intValue == targetID)
                return paletteFolderProperty;

            SerializedProperty childrenProperty = paletteFolderProperty.FindPropertyRelative(AssetPaletteWindow.ChildFoldersPropertyName);
            for (int i = 0; i < childrenProperty.arraySize; i++)
            {
                SerializedProperty result = FindFolderByIdRecursively(
                    childrenProperty.GetArrayElementAtIndex(i), targetID);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Gets the corresponding serialized property from a "reference id path".
        /// See: SerializedPropertyExtensions.GetReferenceIdPath
        /// </summary>
        public static SerializedProperty FindPropertyFromReferenceIdPath(
            this SerializedObject serializedObject, string path,
            string rootCollectionName, string childPropertyName = null)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (string.IsNullOrEmpty(childPropertyName))
                childPropertyName = rootCollectionName;

            SerializedProperty rootCollectionProperty = serializedObject.FindProperty(rootCollectionName);

            List<string> sections = new List<string>(path.Split(SerializedPropertyExtensions.ReferenceIdSeparator));

            SerializedProperty propertyOfFirstSection = GetArrayElementWithReferenceId(
                rootCollectionProperty, sections[0]);

            if (propertyOfFirstSection == null)
                return null;
            
            // If there was only one section in the path, the property of the first section is the one we need.
            if (sections.Count == 1)
                return propertyOfFirstSection;

            // There were other sections in the path, so recurse deeper.
            sections.RemoveAt(0);
            string remainingPath = string.Join(SerializedPropertyExtensions.ReferenceIdSeparator, sections);
            
            return GetPropertyFromReferenceIdPathRecursive(
                propertyOfFirstSection, remainingPath, childPropertyName);
        }
        
        private static SerializedProperty GetArrayElementWithReferenceId(
            SerializedProperty collectionProperty, string id)
        {
            for (int i = 0; i < collectionProperty.arraySize; i++)
            {
                SerializedProperty childProperty = collectionProperty.GetArrayElementAtIndex(i);
                if (string.Equals(childProperty.managedReferenceId.ToString(), id))
                    return childProperty;
            }
            
            return null;
        }
        
        private static SerializedProperty GetPropertyFromReferenceIdPathRecursive(
            SerializedProperty serializedProperty, string path, string childPropertyName)
        {
            SerializedProperty collectionProperty = serializedProperty.FindPropertyRelative(childPropertyName);

            List<string> sections = new List<string>(path.Split(SerializedPropertyExtensions.ReferenceIdSeparator));

            SerializedProperty propertyOfFirstSection = GetArrayElementWithReferenceId(
                collectionProperty, sections[0]);

            if (propertyOfFirstSection == null)
                return null;
            
            // If there was only one section remaining in the path, the property of the first section is the one we need.
            if (sections.Count == 1)
                return propertyOfFirstSection;

            // There were other sections in the path, so recurse deeper.
            sections.RemoveAt(0);
            string remainingPath = string.Join(SerializedPropertyExtensions.ReferenceIdSeparator, sections);
            
            return GetPropertyFromReferenceIdPathRecursive(
                propertyOfFirstSection, remainingPath, childPropertyName);
        }
    }
}
