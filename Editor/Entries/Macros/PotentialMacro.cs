using System;
using System.Collections.Generic;
using System.Reflection;
using RoyTheunissen.AssetPalette.Extensions;
using RoyTheunissen.AssetPalette.Runtime;
using UnityEditor;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Represents a viable macro that is found in a script.
    /// </summary>
    [Serializable]
    public struct PotentialMacro
    {
        public MonoScript script;
        public MethodInfo methodInfo;
        public string name;

        public PotentialMacro(MonoScript script, MethodInfo methodInfo)
        {
            this.script = script;
            this.methodInfo = methodInfo;

            name = methodInfo.Name.ToHumanReadable();
        }

        public static void FindPotentialMacros(ref List<PotentialMacro> macros, MonoScript script)
        {
            Type scriptClass = script.GetClass();
            if (scriptClass == null)
                return;

            MethodInfo[] publicStaticMethods = scriptClass.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (MethodInfo methodInfo in publicStaticMethods)
            {
                if (!PaletteMacro.CanCallMethodForMacro(methodInfo))
                    continue;

                // We've got a live one.
                macros.Add(new PotentialMacro(script, methodInfo));
            }
        }
    }
}
