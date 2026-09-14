using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Transitions
{
    // Block events without disabling roots: no disabled tint or lost authored state.
    internal sealed class TransitionInputBlock
    {
        private readonly List<VisualElement> roots = new List<VisualElement>();

        public void Capture(UIDocument overlay)
        {
            foreach (var document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var root = document.rootVisualElement;
                if (document == overlay || root == null || roots.Contains(root)) continue;
                roots.Add(root);
                Register(root, true);
            }
        }

        public void Release()
        {
            foreach (var root in roots) Register(root, false);
            roots.Clear();
        }

        private static void Block<T>(T evt) where T : EventBase<T>, new()
        {
            evt.StopImmediatePropagation();
            evt.PreventDefault();
        }

        private static void Hook<T>(VisualElement root, bool add) where T : EventBase<T>, new()
        {
            if (add) root.RegisterCallback<T>(Block, TrickleDown.TrickleDown);
            else root.UnregisterCallback<T>(Block, TrickleDown.TrickleDown);
        }

        private static void Register(VisualElement root, bool add)
        {
            Hook<PointerDownEvent>(root, add);
            Hook<PointerUpEvent>(root, add);
            Hook<ClickEvent>(root, add);
            Hook<WheelEvent>(root, add);
            Hook<KeyDownEvent>(root, add);
            Hook<KeyUpEvent>(root, add);
            Hook<NavigationMoveEvent>(root, add);
            Hook<NavigationSubmitEvent>(root, add);
            Hook<NavigationCancelEvent>(root, add);
        }
    }
}
