using System;
using System.Collections.Generic;
using System.Reflection;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// Palette entry for a function defined in script that you can run as a macro.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "RoyTheunissen.AssetPalette.Runtime")]
    public class PaletteMacro : PaletteEntry
    {
        [SerializeField] private GuidBasedReference<TextAsset> scriptReference;
        
        // Keeping this for backwards compability.
        [SerializeField] private TextAsset script;
        
        public TextAsset Script
        {
            get
            {
                // Backwards compatibility with old palettes that had direct references.
                scriptReference.InitializeFromExistingDirectReference(ref scriptReference, ref script);
                
                return scriptReference.Asset;
            }
        }

        [SerializeField] private string methodName;
        
        [SerializeField] private Texture2D customIcon;
        public Texture2D CustomIcon => customIcon;
        public bool HasCustomIcon => CustomIcon != null;

        protected override string DefaultName => methodName.ToHumanReadable();

        public override string Tooltip => 
            $"Performs the '{methodName}' method in the '{(script == null ? null : script.name)}' script";

        public override bool IsValid => Script != null && !string.IsNullOrEmpty(methodName);

        protected override PaletteEntrySortPriorities SortPriority => PaletteEntrySortPriorities.Macros;

        public PaletteMacro(TextAsset script, string methodName)
        {
            scriptReference = new GuidBasedReference<TextAsset>(script);
            this.methodName = methodName;
        }

        public override void Open()
        {
            MonoScript monoScript = (MonoScript)Script;

            if (monoScript == null)
            {
                Debug.LogError($"Tried to run macro '{Name}' but its script appeared to be missing.");
                return;
            }

            Type macroClass = monoScript.GetClass();
            if (macroClass == null)
            {
                Debug.LogError($"Tried to run macro '{Name}' but its script didn't appear to define a valid class. " +
                               $"Make sure it defines one top-level non-generic class.");
                return;
            }

            MethodInfo methodInfo = macroClass.GetMethod(methodName);
            if (methodInfo == null)
            {
                Debug.LogError($"Tried to run macro '{Name}' but method '{methodName}' didn't seem to exist. " +
                               $"Did you remove or rename it?");
                return;
            }
            
            if (!CanCallMethodForMacro(methodInfo))
            {
                Debug.LogError($"Tried to run macro '{Name}' but method '{methodName}' couldn't seem to be called. " +
                               $"Check that it's static, non-generic, doesn't require parameters and doesn't return a value.");
                return;
            }

            // Actually call the method.
            methodInfo.Invoke(null, new object[] { });
        }
        
        public override void GetAssetsToSelect(ref List<Object> selection)
        {
            selection.Add(Script);
        }

        public static bool CanCallMethodForMacro(MethodInfo methodInfo)
        {
            if (methodInfo.IsGenericMethod || methodInfo.ReturnType != typeof(void))
                return false;
                
            ParameterInfo[] parameters = methodInfo.GetParameters();
                
            // Right now we only support parameterless methods.
            if (parameters.Length > 0)
                return false;

            return true;
        }
        
        public override bool CanAcceptDraggedAssets(Object[] objectReferences)
        {
            return objectReferences.Length == 1 && objectReferences[0] is Texture2D;
        }

        public override void AcceptDraggedAssets(Object[] objectReferences, SerializedProperty serializedProperty)
        {
            base.AcceptDraggedAssets(objectReferences, serializedProperty);

            Texture2D draggedTexture = (Texture2D)objectReferences[0];

            SerializedProperty customIconProperty = serializedProperty.FindPropertyRelative(nameof(customIcon));
            customIconProperty.objectReferenceValue = draggedTexture;
        }
    }
}
