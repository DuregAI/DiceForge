using System;
using System.Collections;
using System.Text.RegularExpressions;
using Diceforge.Transitions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public sealed class ScreenTransitionTests
{
    private ScreenTransition service;
    private CloudTransitionSettings original;
    private CloudTransitionSettings settings;
    private float originalTimeScale;
    private bool originalBackground;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        originalTimeScale = Time.timeScale;
        originalBackground = Application.runInBackground;
        Application.runInBackground = true;
        service = UnityEngine.Object.FindAnyObjectByType<ScreenTransition>();
        if (service == null) service = new GameObject("TransitionTest").AddComponent<ScreenTransition>();
        Assert.IsFalse(ScreenTransition.IsBusy);
        settings = service.Settings;
        original = UnityEngine.Object.Instantiate(settings);
        settings.closeDuration = settings.openDuration = 0.06f;
        settings.coveredHold = 0;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = originalTimeScale;
        Application.runInBackground = originalBackground;
        if (service != null) { service.enabled = false; service.enabled = true; }
        if (settings != null && original != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), settings);
        if (original != null) UnityEngine.Object.Destroy(original);
        yield return null;
    }

    private static IEnumerator WaitUntilIdle()
    {
        float deadline = Time.realtimeSinceStartup + 5;
        while (ScreenTransition.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsFalse(ScreenTransition.IsBusy, "Transition did not release input.");
    }

    [UnityTest]
    public IEnumerator ChangesOnlyUnderCoverAndRejectsDuplicates()
    {
        int changes = 0;
        Assert.IsTrue(ScreenTransition.Switch(() => { Assert.IsTrue(ScreenTransition.IsCovered); changes++; }));
        Assert.IsFalse(ScreenTransition.Switch(() => changes += 100));
        Assert.AreEqual(0, changes);
        yield return WaitUntilIdle();
        Assert.AreEqual(1, changes);
        Assert.AreEqual(DisplayStyle.None, service.GetComponent<UIDocument>().rootVisualElement.resolvedStyle.display);
    }

    [UnityTest]
    public IEnumerator KeepsCoverUntilDestinationIsReady()
    {
        bool ready = false;
        ScreenTransition.Switch(() => { }, () => ready);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.IsTrue(ScreenTransition.IsCovered);
        ready = true;
        yield return WaitUntilIdle();
    }

    [UnityTest]
    public IEnumerator WorksWhenTimeScaleIsZero()
    {
        Time.timeScale = 0;
        ScreenTransition.Switch(() => { });
        yield return WaitUntilIdle();
        Assert.AreEqual(0, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator ZeroDurationsStillConcealMutation()
    {
        settings.closeDuration = settings.openDuration = 0;
        bool changed = false;
        ScreenTransition.Switch(() => { Assert.IsTrue(ScreenTransition.IsCovered); changed = true; });
        yield return WaitUntilIdle();
        Assert.IsTrue(changed);
    }

    [UnityTest]
    public IEnumerator FailureIsVisibleAndDismissalReleasesInput()
    {
        LogAssert.Expect(LogType.Error, new Regex("\\[ScreenTransition\\] System.InvalidOperationException: injected"));
        ScreenTransition.Switch(() => throw new InvalidOperationException("injected"));
        yield return new WaitForSecondsRealtime(0.25f);
        Assert.AreEqual(TransitionPhase.Failed, service.Phase);
        Assert.AreEqual("injected", service.LastError);
        var button = service.GetComponent<UIDocument>().rootVisualElement.Q<Button>();
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        yield return WaitUntilIdle();
        Assert.IsTrue(ScreenTransition.Switch(() => { }));
        yield return WaitUntilIdle();
    }

    [UnityTest]
    public IEnumerator DisablingServiceReleasesBlockAndAllowsNextTransition()
    {
        ScreenTransition.Switch(() => { }, () => false);
        yield return new WaitForSecondsRealtime(0.15f);
        service.enabled = false;
        Assert.IsFalse(ScreenTransition.IsBusy);
        service.enabled = true;
        ScreenTransition.Switch(() => { });
        yield return WaitUntilIdle();
    }

    [Test]
    public void InvalidSceneDoesNotBeginNavigation()
    {
        Assert.Throws<ArgumentException>(() => ScreenTransition.LoadScene("__missing_transition_scene__"));
        Assert.IsFalse(ScreenTransition.IsBusy);
    }

    [UnityTest]
    public IEnumerator ReducedMotionAndMissingTextureStillComplete()
    {
        settings.reducedMotion = true;
        settings.cloudTexture = null;
        ScreenTransition.Switch(() => Assert.IsTrue(ScreenTransition.IsCovered));
        yield return WaitUntilIdle();
    }

    [UnityTest]
    public IEnumerator BlocksNavigationAndRestoresInputWithoutChangingEnabledState()
    {
        var go = new GameObject("InputTestDocument");
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = service.GetComponent<UIDocument>().panelSettings;
        var root = doc.rootVisualElement;
        var button = new Button();
        root.Add(button);
        int submits = 0;
        button.RegisterCallback<NavigationSubmitEvent>(_ => submits++);
        try
        {
            ScreenTransition.Switch(() => { });
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
            Assert.AreEqual(0, submits);
            Assert.IsTrue(root.enabledSelf);
            yield return WaitUntilIdle();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
            Assert.AreEqual(1, submits);
            Assert.IsTrue(root.enabledSelf);
        }
        finally { UnityEngine.Object.Destroy(go); }
    }
}
