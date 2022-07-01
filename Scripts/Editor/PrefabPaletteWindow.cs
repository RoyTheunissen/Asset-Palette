using UnityEditor;

namespace RoyTheunissen.PrefabPalette
{
    /// <summary>
    /// Helps organize collections of prefabs and drag them into scenes quickly.
    /// </summary>
    public class PrefabPaletteWindow : EditorWindow 
    {
        [MenuItem ("Window/General/Prefab Palette")]
        public static void Init() 
        {
            GetWindow<PrefabPaletteWindow>(false, "Prefab Palette");
        }
    
        private void OnGUI()
        {
            EditorGUILayout.LabelField("I'm walking here!!!");
        }
    }
}
