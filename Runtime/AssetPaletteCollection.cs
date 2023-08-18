using UnityEngine;
using System.Collections.Generic;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Stores prefab palettes.
    /// </summary>
    [CreateAssetMenu(fileName = "Prefab Palette Collection", menuName = "ScriptableObject/Prefab Palette Collection")]
    public class AssetPaletteCollection : ScriptableObject 
    {
        [SerializeReference] private List<PaletteFolder> folders = new List<PaletteFolder>();
        public List<PaletteFolder> Folders => folders;
        
        [SerializeReference] private int lastSelectionId;
    }
}
