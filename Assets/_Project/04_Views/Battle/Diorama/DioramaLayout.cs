using System;
using UnityEngine;

namespace Diceforge.View
{
    [CreateAssetMenu(menuName = "Diceforge/Diorama Layout")]
    public sealed class DioramaLayout : ScriptableObject
    {
        public int[] cellIds;
        public Vector3[] landscape;
        public Vector3[] portrait;
        public Vector3 landscapeSize;
        public Vector3 portraitSize;
        public bool Validate(int count, out string error)
        {
            error = null;
            if (cellIds == null || landscape == null || portrait == null || cellIds.Length != count || landscape.Length != count || portrait.Length != count)
                error = "Diorama poses must match the logical cell count.";
            else
            {
                var seen = new System.Collections.Generic.HashSet<int>();
                foreach (int id in cellIds) if (id < 0 || id >= count || !seen.Add(id)) { error = "Diorama cell IDs must uniquely cover the logical board."; break; }
            }
            return error == null;
        }
    }
}
