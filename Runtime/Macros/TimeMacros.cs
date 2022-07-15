using UnityEngine;

namespace RoyTheunissen.AssetPalette.Runtime.Macros
{
    /// <summary>
    /// Macros for dealing with time and time scale.
    /// </summary>
    public static class TimeMacros 
    {
        public static void ResetTimeScale()
        {
            Time.timeScale = 1.0f;
        }
        
        public static void SetTimeScaleToZeroPointOne()
        {
            Time.timeScale = 0.1f;
        }
    }
}
