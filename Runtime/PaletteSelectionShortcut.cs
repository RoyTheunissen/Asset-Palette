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
        
        [SerializeField] private Object[] selection;
        public Object[] Selection => selection;

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
            this.selection = selection;
        }
    }
}
