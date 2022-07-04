using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyTheunissen.PrefabPalette
{
    [Serializable]
    public class PaletteFolder
    {
        [SerializeField] private string name;
        public string Name => name;
        
        [SerializeField] private string guid;
        public string RenameControlId => $"{guid}Rename";
        
        [SerializeReference] private List<PaletteEntry> entries = new List<PaletteEntry>();
        public List<PaletteEntry> Entries => entries;

        private static PaletteFolder folderCurrentlyRenaming;
        public static PaletteFolder FolderCurrentlyRenaming => folderCurrentlyRenaming;

        public bool IsRenaming => folderCurrentlyRenaming == this;

        public static bool IsFolderBeingRenamed => folderCurrentlyRenaming != null;

        public void Initialize(string name)
        {
            this.name = name;
            
            guid = Guid.NewGuid().ToString();
        }

        public void StartRename()
        {
            folderCurrentlyRenaming = this;
        }

        public static void CancelRename()
        {
            folderCurrentlyRenaming = null;
        }
    }
}
