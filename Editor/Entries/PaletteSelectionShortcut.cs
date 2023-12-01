using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Represents a project window selection that you want to get back to quickly.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "RoyTheunissen.AssetPalette.Runtime")]
    public class PaletteSelectionShortcut : PaletteEntry
    {
        public const int ItemNamesToDisplayMax = 3;
        
        [SerializeField] private GuidBasedReferenceList<Object> selectionReferences;
        
        // Keeping this for backwards compatibility.
        [SerializeField] private Object[] selection;
        
        public Object[] Selection
        {
            get
            {
                // Backwards compatibility with old palettes that had direct references.
                selectionReferences.InitializeFromExistingDirectReferences(ref selectionReferences, ref selection);
                
                return selectionReferences.Array;
            }
        }

        [NonSerialized] private bool didCacheName;
        [NonSerialized] private string cachedName;

        protected override string DefaultName
        {
            get
            {
                if (!didCacheName)
                {
                    didCacheName = true;

                    cachedName = "";

                    int validItemCount = 0;
                    for (int i = 0; i < Selection.Length; i++)
                    {
                        if (Selection[i] == null)
                            continue;

                        if (validItemCount > 0)
                            cachedName += ", ";

                        cachedName += Selection[i].name;

                        validItemCount++;
                    }
                    
                    if (validItemCount > ItemNamesToDisplayMax)
                    {
                        cachedName = $"{validItemCount} items";
                    }
                }

                return cachedName;
            }
        }

        public override string Tooltip => $"Selects {Selection.Length} entries when you click on this entry.";

        public override bool IsValid
        {
            get
            {
                for (int i = 0; i < Selection.Length; i++)
                {
                    if (Selection[i] != null)
                        return true;
                }
                return false;
            }
        }

        protected override PaletteEntrySortPriorities SortPriority => PaletteEntrySortPriorities.Shortcuts;

        public override void Open()
        {
            if (Selection.Length == 0)
                return;
            
            UnityEditor.Selection.objects = Selection;
            
            // Ping all of the objects in reverse order so all the corresponding folders open and you can see your
            // selection.
            for (int i = 0; i < Selection.Length; i++)
            {
                if (Selection[i] != null)
                    UnityEditor.EditorGUIUtility.PingObject(Selection[i]);
            }
        }
        
        public override void GetAssetsToSelect(ref List<Object> selection)
        {
            selection.AddRange(Selection);
        }

        public PaletteSelectionShortcut(Object[] selection)
        {
            selectionReferences = new GuidBasedReferenceList<Object>(selection);
        }
    }
}
