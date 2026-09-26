using UnityEngine;

namespace Diceforge.View
{
    public sealed class WoodlandBreeze : MonoBehaviour
    {
        private Quaternion rest;
        private void Awake(){rest=transform.localRotation;}
        private void Update(){transform.localRotation=rest*Quaternion.Euler(0,0,DioramaBoard.ReducedMotion?0:Mathf.Sin(Time.time*1.4f)*1.8f);}
    }
}
