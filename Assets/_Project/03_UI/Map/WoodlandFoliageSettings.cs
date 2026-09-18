using System;
using UnityEngine;

namespace Diceforge.Map
{
    [Serializable]
    public sealed class WoodlandFoliageSettings
    {
        public bool enabled = true;
        [Range(0, 8)] public float swayDegrees = 2.5f;
        [Range(0, 3)] public float speed = 1;
        [Range(0, 1)] public float gustStrength = .3f;
        [Range(.5f, 1.5f)] public float size = 1;
        [Range(0, 1)] public float opacity = 1;
    }
}
