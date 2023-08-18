using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette.Runtime
{
    /// <summary>
    /// Represents a project window selection that you want to get back to quickly.
    /// </summary>
    [Serializable]
    public class PaletteSelectionShortcut : PaletteEntry
    {
        public const int ItemNamesToDisplayMax = 3;
        
        [SerializeField]
        private GuidBasedReference<Object>[] guidBasedReferences;
        
        //Keeping this for backwards compatibility.
        [SerializeField] private Object[] selection;
        public Object[] Selection
        {
            get
            {
#if UNITY_EDITOR
                //Migration from old system to new guid system
                if(guidBasedReferences == null || selection != null && guidBasedReferences.Length != selection.Length)
                {
                    guidBasedReferences = new GuidBasedReference<Object>[selection.Length];

                    for (int i = 0; i < selection.Length; i++)
                    {
                        guidBasedReferences[i] = new GuidBasedReference<Object>(this.selection[i]);
                    }

                    selection = null;
                }
#endif

                Object[] returnSelection = new Object[guidBasedReferences.Length];
                for (int i = 0; i < guidBasedReferences.Length; i++)
                {
                    returnSelection[i] = guidBasedReferences[i].Asset;
                }

                return returnSelection;
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
            
#if UNITY_EDITOR
            UnityEditor.Selection.objects = Selection;
            
            // Ping all of the objects in reverse order so all the corresponding folders open and you can see your
            // selection.
            for (int i = 0; i < Selection.Length; i++)
            {
                if (Selection[i] != null)
                    UnityEditor.EditorGUIUtility.PingObject(Selection[i]);
            }
#endif // UNITY_EDITOR
        }
        
        public override void GetAssetsToSelect(ref List<Object> selection)
        {
            selection.AddRange(Selection);
        }

        public PaletteSelectionShortcut(Object[] selection)
        {
            guidBasedReferences = new GuidBasedReference<Object>[selection.Length];
            for (int i = 0; i < selection.Length; i++)
            {
                guidBasedReferences[i] = new GuidBasedReference<Object>(selection[i]);
            }
        }
    }
}
