using UnityEngine;
using UnityEngine.Rendering;

namespace Diceforge.View
{
    /// <summary>Real wind-weighted branches frame the view at a stable screen size.</summary>
    public sealed class LivingForestForeground : MonoBehaviour
    {
        public const int ForegroundLayer = 29;
        public Transform leftBranch;
        public Transform rightBranch;

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCamera;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCamera;

        private void BeforeCamera(ScriptableRenderContext context, Camera view)
        {
            if (view == null || !view.CompareTag("MainCamera")) return;
            Frame(view);
        }

        public void Frame(Camera view)
        {
            if (view == null || !view.orthographic) return;
            float height = view.orthographicSize * 2;
            float width = height * view.aspect;
            // Bound the horizontal coverage in portrait as well as landscape.
            float scale = Mathf.Min(width * .19f, height * .36f);
            Place(leftBranch, view, new Vector3(-.035f, .055f, 5), scale);
            Place(rightBranch, view, new Vector3(1.035f, .035f, 5), scale * .83f);
        }

        private static void Place(Transform branch, Camera view, Vector3 viewport, float scale)
        {
            if (branch == null) return;
            branch.position = view.ViewportToWorldPoint(viewport);
            branch.rotation = view.transform.rotation * Quaternion.Euler(90, 0, 0);
            branch.localScale = Vector3.one * scale;
        }
    }
}
