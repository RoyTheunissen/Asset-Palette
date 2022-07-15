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
            Debug.Log("Resetting time scale.");
            Time.timeScale = 1.0f;
        }
        
        public static void SetTimeScaleToZeroPointOne()
        {
            Debug.Log("Setting time scale to 0.1");
            Time.timeScale = 0.1f;
        }
    }
}
