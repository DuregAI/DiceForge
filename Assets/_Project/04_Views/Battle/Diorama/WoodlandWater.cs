using UnityEngine;

namespace Diceforge.View
{
    public sealed class WoodlandWater : MonoBehaviour
    {
        private Vector3 initial;
        private void Awake(){initial=transform.localScale;}
        private void Update()
        {
            if(DioramaBoard.ReducedMotion)return;
            transform.localScale=new Vector3(initial.x*(1+Mathf.Sin(Time.time*.7f)*.008f),initial.y,initial.z*(1+Mathf.Cos(Time.time*.8f)*.008f));
        }
    }
}
