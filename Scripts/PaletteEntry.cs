using System;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Represents one entry in the palette and manages its properties.
    /// </summary>
    [Serializable]
    public abstract class PaletteEntry : IComparable<PaletteEntry>
    {
        public abstract string Name { get; }

        public abstract bool IsValid { get; }
    
        public abstract void Open();
        
        public override string ToString()
        {
            return $"{GetType().Name}({Name})";
        }

        public int CompareTo(PaletteEntry other)
        {
            return String.Compare(Name, other.Name, StringComparison.Ordinal);
        }
    }
}
