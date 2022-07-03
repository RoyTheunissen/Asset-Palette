using UnityEngine;
using System.Collections.Generic;

namespace RoyTheunissen.PrefabPalette
{
    /// <summary>
    /// Stores prefab palettes.
    /// </summary>
    [CreateAssetMenu(fileName = "Prefab Palette Collection", menuName = "ScriptableObject/Prefab Palette Collection")]
    public class PrefabPaletteCollection : ScriptableObject 
    {
        [SerializeReference] private List<PaletteFolder> folders = new List<PaletteFolder>();
        public List<PaletteFolder> Folders => folders;
    }
}
