using UnityEngine;

namespace Diceforge.View
{
    public sealed class DioramaCell : MonoBehaviour
    {
        public int cellId;
        public Renderer highlight;
        public bool available;
        public void Highlight(bool enabled) { available = enabled; if (highlight != null) highlight.enabled = enabled; }
    }
    public sealed class DioramaToken : MonoBehaviour
    {
        public int player;
        public int cellId = -1;
        public string logicalId;
    }
}
