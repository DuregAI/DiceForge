using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

public sealed class FeedbackDeliveryTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private Type sinkType;
    private Type resultType;
    private object sink;
    private Delegate completion;
    private readonly List<string> results = new();

    [SetUp]
    public void SetUp()
    {
        sinkType = Type.GetType("Diceforge.Integrations.SpacetimeDb.SpacetimeDbFeedbackSink, Assembly-CSharp", true);
        resultType = Type.GetType("Diceforge.Integrations.SpacetimeDb.FeedbackSubmissionResult, Assembly-CSharp", true);
        sink = Activator.CreateInstance(sinkType, new object[] { null });
        completion = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(resultType), this,
            GetType().GetMethod(nameof(RecordResult), Flags).MakeGenericMethod(resultType));
        results.Clear();
    }

    private void RecordResult<T>(T result) => results.Add(result.ToString());
    private object Call(string name, params object[] args) => sinkType.GetMethod(name, Flags).Invoke(sink, args);
    private object Pending => sinkType.GetField("_pendingFeedback", Flags).GetValue(sink);

    // Seed an already transmitted item without a real socket or writing to the server.
    private void SeedInFlight()
    {
        var pendingType = sinkType.GetNestedType("PendingFeedbackSubmission", BindingFlags.NonPublic);
        var pending = Activator.CreateInstance(pendingType,
            new object[] { "test-id", "session", "guid", "player", "bug", "draft", 1L, "test", "scene" });
        sinkType.GetField("_pendingFeedback", Flags).SetValue(sink, pending);
        sinkType.GetField("_completion", Flags).SetValue(sink, completion);
        sinkType.GetField("_deadline", Flags).SetValue(sink, 15f);
    }

    private object CreateReducerContext(string statusName)
    {
        var contextType = sinkType.GetMethod("HandleSubmitFeedback").GetParameters()[0].ParameterType;
        var eventType = contextType.GetField("Event").FieldType;
        var statusType = eventType.GetProperty("Status").PropertyType.GetNestedType(statusName);
        var statusCtor = statusType.GetConstructors()[0];
        var statusArgumentType = statusCtor.GetParameters()[0].ParameterType;
        var status = statusCtor.Invoke(new[] { statusArgumentType == typeof(string) ? (object)"rejected" : Activator.CreateInstance(statusArgumentType) });
        var eventCtor = eventType.GetConstructors()[0];
        var parameters = eventCtor.GetParameters();
        var arguments = new object[parameters.Length];
        for (int i = 0; i < arguments.Length; i++)
            arguments[i] = parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null;
        arguments[1] = status;
        var reducerEvent = eventCtor.Invoke(arguments);
        return Activator.CreateInstance(contextType, Flags, null, new[] { null, reducerEvent }, null);
    }

    [TestCase("Committed", "Sent")]
    [TestCase("Failed", "Failed")]
    public void OnlyCommittedAcknowledgement_ReportsSuccess(string status, string expected)
    {
        SeedInFlight();
        if (status == "Failed") LogAssert.Expect(LogType.Warning, new Regex("Feedback rejected:"));
        var context = CreateReducerContext(status);
        Call("HandleSubmitFeedback", context, "unrelated-id", "session", "guid", "player", "bug", "draft", 1L, "test", "scene");
        Assert.That(results, Is.Empty);
        Assert.That(Pending, Is.Not.Null);
        Call("HandleSubmitFeedback", context, "test-id", "session", "guid", "player", "bug", "draft", 1L, "test", "scene");
        Assert.That(results, Is.EqualTo(new[] { expected }));
        Assert.That(Pending, Is.Null);
    }

    [Test]
    public void OfflineSubmission_FailsWithoutQueuingOrReportingSuccess()
    {
        Call("SubmitFeedback", "session", "guid", "player", "bug", "draft", "test", "scene", completion);
        Assert.That(results, Is.EqualTo(new[] { "Unavailable" }));
        Assert.That(Pending, Is.Null);
        Call("HandleConnected");
        Assert.That(results.Count, Is.EqualTo(1));
    }

    [Test]
    public void InFlightSubmission_StaysPendingUntilDeadline_ThenCompletesOnce()
    {
        SeedInFlight();
        Call("Tick", 14.99f);
        Assert.That(Pending, Is.Not.Null);
        Assert.That(results, Is.Empty);
        Call("Tick", 15f);
        Call("Tick", 20f);
        Call("HandleDisconnected");
        Assert.That(Pending, Is.Null);
        Assert.That(results, Is.EqualTo(new[] { "TimedOut" }));
    }

    [Test]
    public void DuplicateSubmission_DoesNotReplaceOriginalPendingItem()
    {
        SeedInFlight();
        Call("SubmitFeedback", "session", "guid", "player", "bug", "second", "test", "scene", completion);
        Assert.That(results, Is.EqualTo(new[] { "Busy" }));
        Assert.That(Pending.GetType().GetProperty("FeedbackId").GetValue(Pending), Is.EqualTo("test-id"));
        Call("Tick", 15f);
        Assert.That(results, Is.EqualTo(new[] { "Busy", "TimedOut" }));
    }

    [Test]
    public void Disconnection_CompletesFailureOnce_AndReleasesSlot()
    {
        SeedInFlight();
        Call("HandleDisconnected");
        Call("HandleDisconnected");
        Assert.That(Pending, Is.Null);
        Assert.That(results, Is.EqualTo(new[] { "Unavailable" }));
    }

    [Test]
    public void RemovedCompletion_DoesNotCallDisposedController()
    {
        SeedInFlight();
        Call("RemoveCompletion", completion);
        Call("Tick", 15f);
        Assert.That(Pending, Is.Null);
        Assert.That(results, Is.Empty);
    }
}
