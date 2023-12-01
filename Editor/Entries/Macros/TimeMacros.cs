using UnityEngine;

namespace RoyTheunissen.AssetPalette.Macros
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
        
        public static void SetTimeScaleToTwo()
        {
            Time.timeScale = 2.0f;
        }
    }
}
