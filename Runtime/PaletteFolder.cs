using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyTheunissen.AssetPalette
{
    [Serializable]
    public class PaletteFolder
    {
        [SerializeField] private string name;
        public string Name => name;

        [SerializeReference] private List<PaletteFolder> children = new List<PaletteFolder>();
        public List<PaletteFolder> Children => children;
        
        [SerializeReference] private List<PaletteEntry> entries = new List<PaletteEntry>();
        public List<PaletteEntry> Entries => entries;

        [SerializeField] private int selectionId;
        public int SelectionId => selectionId;

        public void Initialize(string name, int selectionId)
        {
            this.name = name;
            this.selectionId = selectionId;
        }

        public override string ToString()
        {
            return $"{GetType().Name}({Name}, {selectionId})";
        }
    }
}
