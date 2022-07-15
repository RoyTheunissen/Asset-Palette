using System;
using System.Reflection;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

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
        
        [SerializeField] private Texture2D customIcon;
        public Texture2D CustomIcon => customIcon;
        public bool HasCustomIcon => CustomIcon != null;

        protected override string DefaultName => methodName.ToHumanReadable();

        public override bool IsValid => script != null && !string.IsNullOrEmpty(methodName);

        public PaletteMacro(TextAsset script, string methodName)
        {
            this.script = script;
            this.methodName = methodName;
        }

        public override void Open()
        {
#if UNITY_EDITOR
            MonoScript monoScript = (MonoScript)script;

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
#endif // UNITY_EDITOR
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

#if UNITY_EDITOR
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
#endif // UNITY_EDITOR
    }
}
