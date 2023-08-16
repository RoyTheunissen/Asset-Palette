using System;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Windows
{
    public partial class AssetPaletteWindow
    {
        public const string PersonalPaletteGuid = "Personal Palette Guid";
        private AssetPaletteCollection cachedPersonalPalette;

        public AssetPaletteCollection PersonalPalette
        {
            get
            {
                if (cachedPersonalPalette == null)
                {
                    cachedPersonalPalette = CreateInstance<AssetPaletteCollection>();
                    cachedPersonalPalette.name = "Personal Palette";

                    string storedJson = EditorPrefs.GetString(PersonalPaletteStorageKeyEditorPref, "");
                    if (!string.IsNullOrEmpty(storedJson))
                        JsonUtility.FromJsonOverwrite(storedJson, cachedPersonalPalette);
                }

                return cachedPersonalPalette;
            }
        }

        public string CurrentCollectionGuid
        {
            get => EditorPrefs.GetString(CurrentCollectionGUIDEditorPref);
            set
            {
                if (CurrentCollectionGuid == value)
                    return;

                EditorPrefs.SetString(CurrentCollectionGUIDEditorPref, value);

                ClearCachedCollection();
            }
        }

        [NonSerialized] private AssetPaletteCollection cachedCurrentCollection;

        public AssetPaletteCollection CurrentCollection
        {
            get
            {
                if (cachedCurrentCollection == null)
                {
                    if (CurrentCollectionGuid == PersonalPaletteGuid)
                    {
                        cachedCurrentCollection = PersonalPalette;
                    }
                    else
                    {
                        string guid = CurrentCollectionGuid;

                        if (string.IsNullOrEmpty(guid))
                            return null;

                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path))
                            return null;

                        cachedCurrentCollection = AssetDatabase.LoadAssetAtPath<AssetPaletteCollection>(path);
                    }
                }

                return cachedCurrentCollection;
            }
            set
            {
                if (value == CurrentCollection)
                    return;

                if (value == null)
                {
                    CurrentCollectionGuid = null;
                    return;
                }


                if (value == PersonalPalette)
                {
                    CurrentCollectionGuid = PersonalPaletteGuid;
                }
                else
                {
                    CurrentCollectionGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
                }
            }
        }

        public bool HasCollection => CurrentCollection != null;
        [NonSerialized] private SerializedObject cachedCurrentCollectionSerializedObject;
        [NonSerialized] private bool didCacheCurrentCollectionSerializedObject;

        public SerializedObject CurrentCollectionSerializedObject
        {
            get
            {
                if (!didCacheCurrentCollectionSerializedObject || cachedCurrentCollectionSerializedObject == null
                                                               || cachedCurrentCollectionSerializedObject
                                                                   .targetObject == null)
                {
                    didCacheCurrentCollectionSerializedObject = true;
                    cachedCurrentCollectionSerializedObject = new SerializedObject(CurrentCollection);

                    ClearCachedFoldersSerializedProperties();
                }

                return cachedCurrentCollectionSerializedObject;
            }
        }
        
        [NonSerialized] private bool didCacheFoldersSerializedProperty;
        [NonSerialized] private SerializedProperty cachedFoldersSerializedProperty;

        public SerializedProperty FoldersSerializedProperty
        {
            get
            {
                if (!didCacheFoldersSerializedProperty)
                {
                    didCacheFoldersSerializedProperty = true;
                    cachedFoldersSerializedProperty = CurrentCollectionSerializedObject
                        .FindProperty(AssetPaletteWindowFolderPanel.RootFoldersPropertyName);
                    folderPanel.UpdateFoldersTreeView(true);
                }

                return cachedFoldersSerializedProperty;
            }
        }

        private string SelectedFolderReferenceIdPath
        {
            get => EditorPrefs.GetString(SelectedFolderReferenceIdPathEditorPref);
            set => EditorPrefs.SetString(SelectedFolderReferenceIdPathEditorPref, value);
        }

        [NonSerialized] private bool didCacheSelectedFolderSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderSerializedPropertyParent;
        [NonSerialized] private string cachedSelectedFolderSerializedPropertyPath;

        public SerializedProperty SelectedFolderSerializedProperty
        {
            get
            {
                // Sanity check: if the folder selection is no longer valid, reset the selected folder.
                // This is something that can happen with undo...
                if (didCacheSelectedFolderSerializedProperty && (cachedSelectedFolderSerializedProperty == null
                                    || !cachedSelectedFolderSerializedProperty.ExistsInParentArray(
                                        cachedSelectedFolderSerializedPropertyParent,
                                        cachedSelectedFolderSerializedPropertyPath)
                                    || cachedSelectedFolderSerializedProperty.GetValue<PaletteFolder>() == null))
                {
                    ClearCachedSelectedFolderSerializedProperties();
                    SelectedFolderReferenceIdPath = null;
                }
                
                if (!didCacheSelectedFolderSerializedProperty)
                {
                    folderPanel.EnsureFolderExists();
                    didCacheSelectedFolderSerializedProperty = true;
                    
                    // First try to find the selected folder by reference id path. Don't use a regular property path
                    // because those have indices baked into it and those get real screwy when you move things around.
                    cachedSelectedFolderSerializedProperty =
                        CurrentCollectionSerializedObject.FindPropertyFromReferenceIdPath(
                            SelectedFolderReferenceIdPath,
                            AssetPaletteWindowFolderPanel.RootFoldersPropertyName,
                            AssetPaletteWindowFolderPanel.ChildFoldersPropertyName);
                    
                    // Did not exist. Just select the first folder.
                    if (cachedSelectedFolderSerializedProperty == null)
                        cachedSelectedFolderSerializedProperty = FoldersSerializedProperty.GetArrayElementAtIndex(0);

                    cachedSelectedFolderSerializedPropertyParent = cachedSelectedFolderSerializedProperty.GetParent();
                    cachedSelectedFolderSerializedPropertyPath = cachedSelectedFolderSerializedProperty.propertyPath;
                }

                return cachedSelectedFolderSerializedProperty;
            }
            set
            {
                string referenceIdPath = value.GetReferenceIdPath(
                    AssetPaletteWindowFolderPanel.ChildFoldersPropertyName);
                if (string.Equals(SelectedFolderReferenceIdPath, referenceIdPath, StringComparison.Ordinal))
                    return;

                SelectedFolderReferenceIdPath = referenceIdPath;
                
                ClearCachedSelectedFolderSerializedProperties();
                
                // If you change the folder that's selected, we need to clear the selection.
                entryPanel.ClearEntrySelection();
                
                // Now is actually also a good time to make sure it's sorted correctly, because sorting modes configured
                // while on another folder are meant to apply to newly selected folders too.
                CurrentCollectionSerializedObject.Update();
                entryPanel.SortEntriesInSerializedObject();
                CurrentCollectionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        
        [NonSerialized] private bool didCacheSelectedFolderEntriesSerializedProperty;
        [NonSerialized] private SerializedProperty cachedSelectedFolderEntriesSerializedProperty;

        public SerializedProperty SelectedFolderEntriesSerializedProperty
        {
            get
            {
                if (!didCacheSelectedFolderEntriesSerializedProperty)
                {
                    didCacheSelectedFolderEntriesSerializedProperty = true;
                    cachedSelectedFolderEntriesSerializedProperty =
                        SelectedFolderSerializedProperty.FindPropertyRelative("entries");
                }
                return cachedSelectedFolderEntriesSerializedProperty;
            }
        }
        
        private void ClearCachedCollection()
        {
            // Need to re-cache the current collection now.
            cachedCurrentCollection = null;
                
            // Need to re-cache the serialized objects, too.
            cachedCurrentCollectionSerializedObject?.Dispose();
            cachedCurrentCollectionSerializedObject = null;
            didCacheCurrentCollectionSerializedObject = false;
            ClearCachedFoldersSerializedProperties();

            // Clear the selected folder.
            SelectedFolderReferenceIdPath = null;
        }
        
        private void ClearCachedSelectedFolderSerializedProperties()
        {
            didCacheSelectedFolderSerializedProperty = false;
            didCacheSelectedFolderEntriesSerializedProperty = false;
            
            entryPanel.ClearEntrySelection();
        }

        private void ClearCachedFoldersSerializedProperties()
        {
            ClearCachedSelectedFolderSerializedProperties();
            
            didCacheFoldersSerializedProperty = false;
            folderPanel.ClearFoldersTreeView(true);
        }
        
        public void ApplyModifiedProperties()
        {
            CurrentCollectionSerializedObject.ApplyModifiedProperties();
            if (CurrentCollection == PersonalPalette)
                SavePersonalPaletteCollection();
        }

        private void SavePersonalPaletteCollection()
        {
            if (cachedPersonalPalette == null)
                return;

            string personalPaletteJson = JsonUtility.ToJson(cachedPersonalPalette);
            EditorPrefs.SetString(PersonalPaletteStorageKeyEditorPref, personalPaletteJson);
        }
    }
}
