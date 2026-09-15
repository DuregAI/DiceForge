using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI
{
    /// <summary>Dismiss only a click that starts and ends on the backdrop, never a drag from inside.</summary>
    public sealed class ModalDismiss : IDisposable
    {
        private readonly VisualElement overlay;
        private readonly VisualElement window;
        private readonly Action close;
        private int pointer = -1;

        public ModalDismiss(VisualElement overlay, VisualElement window, Action close)
        {
            this.overlay = overlay;
            this.window = window;
            this.close = close;
            if (overlay == null || window == null) return;
            var chrome = Resources.Load<StyleSheet>("ModalChrome");
            if (chrome != null && !overlay.styleSheets.Contains(chrome)) overlay.styleSheets.Add(chrome);
            overlay.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerCancelEvent>(OnCancel);
        }

        public static ModalDismiss BindButton(VisualElement root, string overlayName, string buttonName)
        {
            var overlay = root?.Q(overlayName);
            var button = overlay?.Q<Button>(buttonName);
            if (button != null && buttonName.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                button.AddToClassList("df-modal-close");
                if (button.text == "×" || button.text == "✕") button.AddToClassList("df-modal-close-icon");
            }
            VisualElement window = button;
            while (window != null && window.parent != overlay) window = window.parent;
            return new ModalDismiss(overlay, window, () =>
            {
                // Reuse Cancel/Close, including its readiness and unsaved-result guards.
                if (button == null || !button.enabledInHierarchy || button.resolvedStyle.display == DisplayStyle.None) return;
                using (var evt = NavigationSubmitEvent.GetPooled())
                {
                    evt.target = button;
                    button.SendEvent(evt);
                }
            });
        }

        private bool Outside(EventBase evt) => evt.target is VisualElement target
            && target != window && !window.Contains(target);

        private void OnDown(PointerDownEvent evt)
        {
            pointer = evt.button == 0 && Outside(evt) ? evt.pointerId : -1;
            if (pointer >= 0) evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            bool dismiss = pointer == evt.pointerId && evt.button == 0 && Outside(evt);
            pointer = -1;
            if (!dismiss) return;
            evt.StopImmediatePropagation();
            close?.Invoke();
        }

        private void OnCancel(PointerCancelEvent evt) => pointer = -1;

        public void Dispose()
        {
            if (overlay == null) return;
            overlay.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            overlay.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            overlay.UnregisterCallback<PointerCancelEvent>(OnCancel);
        }
    }
}
