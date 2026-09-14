using Diceforge.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ModalDismissTests
{
    private GameObject go;
    private VisualElement overlay;
    private VisualElement window;
    private VisualElement child;
    private ModalDismiss binding;
    private int closed;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("ModalTest");
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("Transitions/TransitionPanelSettings");
        overlay = new VisualElement();
        window = new VisualElement();
        child = new VisualElement();
        doc.rootVisualElement.Add(overlay);
        overlay.Add(window);
        window.Add(child);
        closed = 0;
        binding = new ModalDismiss(overlay, window, () => closed++);
    }

    [TearDown]
    public void TearDown() { binding.Dispose(); Object.DestroyImmediate(go); }

    private static void Down(VisualElement target, int button = 0)
    {
        using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = button }))
        { evt.target = target; target.SendEvent(evt); }
    }

    private static void Up(VisualElement target, int button = 0)
    {
        using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = button }))
        { evt.target = target; target.SendEvent(evt); }
    }

    [Test] public void BackdropClosesOnce() { Down(overlay); Up(overlay); Up(overlay); Assert.AreEqual(1, closed); }
    [Test] public void WindowAndChildrenDoNotClose() { Down(child); Up(child); Down(window); Up(window); Assert.AreEqual(0, closed); }
    [Test] public void DragOutDoesNotClose() { Down(child); Up(overlay); Assert.AreEqual(0, closed); }
    [Test] public void DragInDoesNotClose() { Down(overlay); Up(child); Assert.AreEqual(0, closed); }
    [Test] public void RightClickDoesNotClose() { Down(overlay, 1); Up(overlay, 1); Assert.AreEqual(0, closed); }
    [Test] public void DisposedBindingDoesNotClose() { binding.Dispose(); Down(overlay); Up(overlay); Assert.AreEqual(0, closed); }
}
