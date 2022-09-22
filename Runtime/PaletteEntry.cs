using System;
using RoyTheunissen.AssetPalette.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Represents one entry in the palette and manages its properties.
    /// </summary>
    [Serializable]
    public abstract class PaletteEntry : IComparable<PaletteEntry>
    {
        [SerializeField] private string customName;
        private string CustomName => customName;
        public bool HasCustomName => !string.IsNullOrEmpty(CustomName);

        protected abstract string DefaultName { get; }
        public string Name => HasCustomName ? CustomName : DefaultName;

        public abstract bool IsValid { get; }

        public virtual bool CanRename => true;
        
        [NonSerialized] private static PaletteEntry entryCurrentlyRenaming;
        public static PaletteEntry EntryCurrentlyRenaming => entryCurrentlyRenaming;

        public bool IsRenaming => entryCurrentlyRenaming == this;
        public static bool IsEntryBeingRenamed => entryCurrentlyRenaming != null;

        protected virtual PaletteEntrySortPriorities SortPriority => PaletteEntrySortPriorities.Default;

        public abstract void Open();
        
        public virtual void SelectAsset()
        {
        }

        public void StartRename()
        {
            entryCurrentlyRenaming = this;
        }

        public static void CancelRename()
        {
            entryCurrentlyRenaming = null;
        }
        
        public virtual void Refresh()
        {
        }
        
        public override string ToString()
        {
            return $"{GetType().Name}({Name})";
        }

        public int CompareTo(PaletteEntry other)
        {
            // First check if the sort priorities are different.
            int priorityOrder = SortPriority.CompareTo(other.SortPriority);
            if (priorityOrder != 0)
                return priorityOrder;
            
            // Entries with the same sort priority are sorted by name.
            return String.Compare(Name, other.Name, StringComparison.Ordinal);
        }

#if UNITY_EDITOR
        public virtual bool CanAcceptDraggedAssets(Object[] objectReferences)
        {
            return false;
        }

        public virtual void AcceptDraggedAssets(Object[] objectReferences, SerializedProperty serializedProperty)
        {
        }
#endif // UNITY_EDITOR
    }
}
