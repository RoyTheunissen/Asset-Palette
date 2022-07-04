using System;

namespace RoyTheunissen.PrefabPalette
{
    /// <summary>
    /// Represents one entry in the palette and manages its properties.
    /// </summary>
    [Serializable]
    public abstract class PaletteEntry
    {
        public abstract bool IsValid { get; }
    
        public abstract void Open();
    }
}
