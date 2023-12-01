namespace RoyTheunissen.AssetPalette.Runtime
{
    /// <summary>
    /// Defines that this asset should show a tooltip when hovered in the Asset Palette.
    /// </summary>
    public interface IAssetPaletteTooltip 
    {
        string Tooltip { get; }
    }
}
