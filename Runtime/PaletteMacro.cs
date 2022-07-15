using System;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEngine;

namespace RoyTheunissen.AssetPalette.Runtime
{
    /// <summary>
    /// Palette entry for a function defined in script that you can run as a macro.
    /// </summary>
    [Serializable]
    public class PaletteMacro : PaletteEntry
    {
        [SerializeField] private TextAsset script;
        public TextAsset Script => script;

        [SerializeField] private string methodName;
        
        protected override string DefaultName => methodName.ToHumanReadable();

        public override bool IsValid => script != null && !string.IsNullOrEmpty(methodName);

        public PaletteMacro(TextAsset script, string methodName)
        {
            this.script = script;
            this.methodName = methodName;
        }

        public override void Open()
        {
            Debug.Log($"Should be calling script {script.name}'s {methodName}()");
        }
    }
}
