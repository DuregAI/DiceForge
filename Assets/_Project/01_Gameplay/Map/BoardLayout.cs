using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Map
{
    [Serializable]
    public sealed class CellData
    {
        public int cellId;
        public Vector3Int gridPos;
        public Vector3 worldPos;
        public bool isStart;
        public bool isEnd;
    }

    [CreateAssetMenu(menuName = "Diceforge/Map/Board Layout", fileName = "BoardLayout")]
    public sealed class BoardLayout : ScriptableObject
    {
        public List<CellData> cells = new();
    }
}
