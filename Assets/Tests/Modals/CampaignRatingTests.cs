using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class CampaignRatingTests
{
    private const BindingFlags InternalStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [Test]
    public void ReopenedDraftShowsItsCategoryAfterLocalization()
    {
        const string key = "feedback.draft.category";
        bool hadKey = PlayerPrefs.HasKey(key);
        string previous = PlayerPrefs.GetString(key);
        object window = null;
        try
        {
            PlayerPrefs.SetString(key, "other");
            Type type = Type.GetType("PlayerFeedbackWindow, Assembly-CSharp", true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            window = Activator.CreateInstance(type, flags, null, new object[] { new VisualElement(), null }, null);
            type.GetMethod("Open", flags).Invoke(window, new object[] { false, null });
            var field = (DropdownField)type.GetField("category", flags).GetValue(window);
            string expected = PlayerPrefs.GetString("ui.language", "en") == "ru" ? "Другое" : "other";
            Assert.That(field.value, Is.EqualTo("other"));
            Assert.That(field.Q<TextElement>(className: "unity-base-popup-field__text").text, Is.EqualTo(expected));
        }
        finally
        {
            (window as IDisposable)?.Dispose();
            if (hadKey) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
        }
    }

    [TestCase(5, false)]
    [TestCase(6, true)]
    public void RatingRequiresEveryAuthoredLevel(int completed, bool expected)
    {
        Type window = Type.GetType("PlayerFeedbackWindow, Assembly-CSharp", true);
        Type mapType = Type.GetType("Diceforge.Map.MapDefinitionSO, Assembly-CSharp", true);
        Type stateType = Type.GetType("Diceforge.Map.MapRunState, Assembly-CSharp", true);
        var map = ScriptableObject.CreateInstance(mapType);
        try
        {
            mapType.GetField("useWoodlandLayout").SetValue(map, true);
            var nodes = (IList)mapType.GetField("nodes").GetValue(map);
            Type nodeType = nodes.GetType().GetGenericArguments()[0];
            object state = Activator.CreateInstance(stateType);
            var ids = (IList)stateType.GetField("completedNodeIds").GetValue(state);
            for (int i = 0; i < 6; i++)
            {
                object node = Activator.CreateInstance(nodeType);
                nodeType.GetField("id").SetValue(node, "level" + i);
                nodes.Add(node);
                if (i < completed) ids.Add("level" + i);
            }
            ids.Add("unrelated-level");
            Assert.That(window.GetMethod("IsCampaignComplete", InternalStatic).Invoke(null, new[] { map, state }), Is.EqualTo(expected));
        }
        finally { UnityEngine.Object.DestroyImmediate(map); }
    }

    [Test]
    public void RatingWithoutCommentAndEscapedCommentsHaveStructuredPayloads()
    {
        Type type = Type.GetType("PlayerFeedbackWindow, Assembly-CSharp", true).GetNestedType("RatingMessage", BindingFlags.NonPublic);
        foreach (string comment in new[] { "", "Отзыв: \"класс\"\nНовая строка <script>" })
        {
            object review = Activator.CreateInstance(type);
            type.GetField("rating").SetValue(review, 3);
            type.GetField("comment").SetValue(review, comment);
            string json = JsonUtility.ToJson(review);
            object restored = JsonUtility.FromJson(json, type);
            Assert.That(type.GetField("rating").GetValue(restored), Is.EqualTo(3));
            Assert.That(type.GetField("comment").GetValue(restored), Is.EqualTo(comment));
        }
    }
}

