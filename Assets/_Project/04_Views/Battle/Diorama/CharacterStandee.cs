using UnityEngine;

namespace Diceforge.View
{
    // Explicit temporary representation until the character's rigged model is approved.
    public sealed class CharacterStandee : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cameraView = Camera.main;
            if (cameraView != null) transform.rotation = cameraView.transform.rotation;
        }
    }
}
