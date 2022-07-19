using System;
using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Editor.CustomEditors
{
    /// <summary>
    /// Contains utilities for drawing palette entries and folders.
    /// </summary>
    public static class PaletteDrawing
    {
        [NonSerialized]
        private static readonly Dictionary<Type, PaletteEntryDrawerBase> entryTypeToDrawer =
            new Dictionary<Type, PaletteEntryDrawerBase>();
        
        public static void DrawFolder(Rect position, SerializedProperty property, PaletteFolder folder)
        {
            // NOTE: If you wanted to you could support different kinds of folders with custom drawers like you could
            // for entries. For now I've not found a reason to have custom folder types so I'll leave it out for now.
            SerializedProperty nameField = property.FindPropertyRelative("name");
            EditorGUI.LabelField(position, GUIContent.none, new GUIContent(nameField.stringValue));
        }

        public static void DrawEntry(Rect position, SerializedProperty property, PaletteEntry entry)
        {
            if (entry == null)
                return;
            
            PaletteEntryDrawerBase entryDrawer = GetEntryDrawer(entry.GetType());

            entryDrawer?.OnGUI(position, property, entry);
        }

        private static PaletteEntryDrawerBase GetEntryDrawer(Type entryType)
        {
            // First see if one has been cached already.
            bool hasDrawer = entryTypeToDrawer.TryGetValue(entryType, out PaletteEntryDrawerBase existingDrawer);
            if (hasDrawer)
                return existingDrawer;

            // No drawer existed yet. Figure out if we have a valid drawer type for this entry.
            Type entryDrawerType = GetEntryDrawerType(entryType);
            if (entryDrawerType == null)
                return null;
            
            // We have a valid entry drawer so instantiate one and cache it for later.
            PaletteEntryDrawerBase newEntryDrawerInstance =
                (PaletteEntryDrawerBase)Activator.CreateInstance(entryDrawerType);
            entryTypeToDrawer.Add(entryType, newEntryDrawerInstance);
            
            return newEntryDrawerInstance;
        }

        private static Type[] GetEntryDrawerTypeCandidates(Type entryType)
        {
            Type genericEntryDrawerType = typeof(PaletteEntryDrawer<>);
            Type specificEntryDrawerBaseType = genericEntryDrawerType.MakeGenericType(entryType);
            return specificEntryDrawerBaseType.GetAllAssignableClasses(false);
        }
        
        private static Type GetEntryDrawerType(Type entryType)
        {
            // Try to find a drawer for this exact entry type. Otherwise, keep trying to find a drawer for its base
            // classes instead until we reach PaletteEntry and there are no more base classes that could have drawers.
            Type[] entryDrawerCandidates = new Type[0];
            Type typeToFindAnEntryDrawerFor = entryType;
            while (entryDrawerCandidates.Length == 0 && typeToFindAnEntryDrawerFor != null &&
                   typeToFindAnEntryDrawerFor != typeof(PaletteEntry))
            {
                entryDrawerCandidates = GetEntryDrawerTypeCandidates(typeToFindAnEntryDrawerFor);
                
                // If we still didn't find any, continue on with the base type.
                typeToFindAnEntryDrawerFor = typeToFindAnEntryDrawerFor.BaseType;
            }

            string typeName = entryType.Name;

            if (entryDrawerCandidates.Length == 0)
            {
                Debug.LogError($"No drawer found for palette entry type '{entryType}'. " +
                               $"Try creating a drawer that inherits from PaletteEntryDrawer<{typeName}> " +
                               $"or make sure that {typeName} inherits from a class that has a valid drawer.");
                return null;
            }

            if (entryDrawerCandidates.Length > 1)
            {
                Debug.LogWarning($"Multiple drawers found for palette entry type '{entryType}'. " +
                                 $"Try making sure there's one PaletteEntryDrawer<{typeName}> for this type.");
            }

            return entryDrawerCandidates[0];
        }
    }
}
