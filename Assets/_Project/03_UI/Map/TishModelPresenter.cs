using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Map
{
    /// <summary>Owns the model, camera and render target for a UI character.</summary>
    public sealed class TishModelPresenter : MonoBehaviour
    {
        private GameObject stage;
        private GameObject model;
        private RenderTexture texture;
        private Image target;
        private Animator animator;
        private bool moving;
        private string character;
        private string lineKey;
        private bool newLine;
        private Diceforge.View.CharacterDialoguePerformance performance;

        public bool Show(Image image, bool speaking = false, string characterId = "tish", string presentationKey = null)
        {
            if (character != characterId && stage != null) ReleaseStage();
            if (stage == null)
            {
                string resource;
                switch (characterId)
                {
                    case "tish": resource = "TishDialogue"; break;
                    case "luma": resource = "LumaDialogue"; break;
                    case "bum": resource = "BumDialogue"; break;
                    case "scratchy": resource = "ScratchyDialogue"; break;
                    default: return false;
                }
                var prefab = Resources.Load<GameObject>("Characters/" + resource);
                if (prefab == null) return false;
                character = characterId;
                stage = new GameObject(characterId + " UI Stage");
                stage.transform.position = new Vector3(10000, 10000, 10000);
                model = Instantiate(prefab, stage.transform);
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                animator = model.GetComponentInChildren<Animator>();
                performance = model.GetComponent<Diceforge.View.CharacterDialoguePerformance>();
                model.GetComponent<Diceforge.View.CharacterAppearance>()?.Apply(true);
                if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                texture = new RenderTexture(384, 512, 24, RenderTextureFormat.ARGB32);
                texture.Create();
                var camera = new GameObject("Portrait Camera").AddComponent<Camera>();
                camera.transform.SetParent(stage.transform, false);
                camera.transform.localPosition = new Vector3(0, characterId == "bum" ? .72f : characterId == "scratchy" ? .59f : .65f, 3);
                camera.transform.localRotation = Quaternion.Euler(0, 180, 0);
                camera.orthographic = true; camera.orthographicSize = characterId == "bum" ? .85f : characterId == "scratchy" ? .76f : .72f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 30; camera.targetTexture = texture;
                Diceforge.View.CharacterAppearance.ConfigurePortraitCamera(camera);
                Diceforge.View.CharacterAppearance.CreatePortraitLights(stage.transform);
            }
            if (target != null && target != image) target.image = null;
            target = image;
            target.sprite = null; target.image = texture; target.scaleMode = ScaleMode.ScaleToFit;
            target.style.display = DisplayStyle.Flex;
            stage.SetActive(true);
            newLine = presentationKey == null || presentationKey != lineKey;
            lineKey = presentationKey;
            if (newLine && performance != null) performance.BeginLine(speaking);
            else if (animator != null) animator.SetInteger("DialogueMode", speaking ? 2 : 1);
            return true;
        }

        public void SetMoving(bool value) { moving = value; }
        public void PlayGesture(string gesture)
        {
            if (!newLine || animator == null || Diceforge.View.DioramaBoard.ReducedMotion) return;
            newLine = false;
            if (performance != null) { performance.PlayGesture(gesture); return; }
            switch (gesture)
            {
                case "Greet": case "Agree": case "Laugh": case "Doubt":
                    animator.SetTrigger(gesture);
                    break;
            }
        }
        private void LateUpdate()
        {
            if (stage == null || !stage.activeSelf) return;
            if (target == null || target.panel == null) { Hide(); return; }
            // Travel follows the map's authored path; a gentle lift gives each step weight.
            model.transform.localPosition = new Vector3(0, moving ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9)) * .035f : 0, 0);
        }
        public void Hide()
        {
            if (target != null) target.image = null;
            target = null; moving = false; lineKey = null;
            if (stage != null) stage.SetActive(false);
        }
        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            ReleaseStage();
        }
        private void ReleaseStage()
        {
            Hide();
            if (stage != null) Destroy(stage);
            if (texture != null) { texture.Release(); Destroy(texture); }
            stage = null; model = null; texture = null; animator = null; performance = null;
        }
    }
}
