using System;
using UnityEngine;

namespace RoyTheunissen.PrefabPalette
{
    [Serializable]
    public class PaletteFolder
    {
        [SerializeField] private string name;
        public string Name => name;

        public void Initialize(string name)
        {
            this.name = name;
        }
    }
}
