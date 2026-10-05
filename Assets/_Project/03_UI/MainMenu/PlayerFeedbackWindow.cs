using System;
using System.Collections.Generic;
using Diceforge.Integrations.SpacetimeDb;
using Diceforge.Map;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

internal sealed class PlayerFeedbackWindow : IDisposable
{
    private static readonly HashSet<string> postponedPrompts = new();
    [Serializable] internal sealed class RatingMessage
    {
        public int rating;
        public string comment;
    }

    private readonly VisualElement overlay;
    private readonly Label title, subtitle, status, ratingLabel;
    private readonly TextField name, message;
    private readonly DropdownField category;
    private readonly VisualElement stars;
    private readonly List<Button> starButtons = new();
    private readonly List<VisualElement> starImages = new();
    private readonly Button submit, cancel;
    private readonly MenuLocalization localization;
    private readonly Diceforge.UI.ModalDismiss dismiss;
    private readonly Action closed;
    private bool ratingMode, sending, disposed, submitted;
    private int rating;
    private string promptKey;
    internal bool IsVisible => overlay.style.display == DisplayStyle.Flex;

    internal PlayerFeedbackWindow(VisualElement parent, Action closed = null)
    {
        this.closed = closed;
        overlay = new VisualElement { name = "FeedbackModal" };
        overlay.AddToClassList("pf-overlay");
        overlay.style.display = DisplayStyle.None;
        var style = Resources.Load<StyleSheet>("PlayerFeedbackWindow");
        if (style != null) overlay.styleSheets.Add(style);
        var window = new VisualElement();
        var content = new ScrollView();
        content.AddToClassList("pf-scroll");
        window.Add(content);
        window.AddToClassList("pf-window");
        overlay.Add(window);
        parent.Add(overlay);
        title = new Label(); title.AddToClassList("pf-title"); content.Add(title);
        subtitle = new Label(); subtitle.AddToClassList("pf-subtitle"); content.Add(subtitle);
        name = new TextField("Player name") { name = "feedbackNameField", maxLength = 23 };
        name.AddToClassList("pf-input"); content.Add(name);
        category = new DropdownField("Category", new List<string> { "bug", "balance", "ui", "audio", "other" }, 0);
        category.AddToClassList("pf-input"); content.Add(category);
        stars = new VisualElement(); stars.AddToClassList("pf-stars"); content.Add(stars);
        for (int i = 1; i <= 5; i++)
        {
            int value = i;
            var button = new Button(() => SetRating(value)) { name = "rating" + i, tooltip = i + " / 5" };
            button.AddToClassList("pf-star-button");
            var image = new VisualElement { pickingMode = PickingMode.Ignore };
            image.AddToClassList("pf-star-image");
            image.generateVisualContent += context => DrawStar(context, image, rating >= value);
            button.Add(image); stars.Add(button); starButtons.Add(button); starImages.Add(image);
        }
        ratingLabel = new Label(); ratingLabel.AddToClassList("pf-subtitle"); content.Add(ratingLabel);
        message = new TextField("Message") { name = "feedbackMessageField", multiline = true };
        message.AddToClassList("pf-input"); message.AddToClassList("pf-message"); content.Add(message);
        status = new Label(); status.AddToClassList("pf-status"); content.Add(status);
        var actions = new VisualElement(); actions.AddToClassList("pf-actions"); content.Add(actions);
        submit = new Button(Submit) { name = "btnFeedbackSubmit" }; submit.AddToClassList("pf-button"); submit.AddToClassList("pf-primary"); actions.Add(submit);
        cancel = new Button(Close) { name = "btnFeedbackCancel" }; cancel.AddToClassList("pf-button"); actions.Add(cancel);
        localization = new MenuLocalization(overlay, RefreshLanguage);
        category.formatListItemCallback = T;
        category.formatSelectedValueCallback = T;
        message.RegisterValueChangedCallback(_ => SaveDraft());
        category.RegisterValueChangedCallback(_ => SaveDraft());
        dismiss = new Diceforge.UI.ModalDismiss(overlay, window, Close);
    }

    internal static bool IsCampaignComplete(MapDefinitionSO map, MapRunState state)
    {
        if (map == null || !map.useWoodlandLayout || map.nodes == null || map.nodes.Count != 6 || state == null) return false;
        foreach (var node in map.nodes)
            if (node == null || !state.IsCompleted(node.id)) return false;
        return true;
    }

    internal void TryOfferRating(string chapterId)
    {
        if (IsVisible || sending) return;
        var map = MapDefinitionSO.LoadChapter(chapterId);
        var state = MapProgressService.Load(map);
        if (!IsCampaignComplete(map, state)) return;
        string key = "feedback.rating.prompt." + ProfileService.Current.playerGuid + "." + MapProgressService.GetRunId(chapterId);
        if (PlayerPrefs.GetInt(key, 0) != 0 || postponedPrompts.Contains(key)) return;
        Open(true, key);
    }

    internal void Open(bool forRating = false, string completionKey = null)
    {
        if (disposed || sending) return;
        ratingMode = forRating;
        submitted = false;
        SetSending(false);
        promptKey = completionKey;
        category.style.display = forRating ? DisplayStyle.None : DisplayStyle.Flex;
        stars.style.display = ratingLabel.style.display = forRating ? DisplayStyle.Flex : DisplayStyle.None;
        name.SetValueWithoutNotify(ProfileService.Current.playerName);
        category.SetValueWithoutNotify(FeedbackDraft.Category);
        message.maxLength = forRating ? 900 : SpacetimeDbFeedbackSink.MaxMessageLength;
        message.SetValueWithoutNotify(forRating ? PlayerPrefs.GetString("feedback.rating.comment", "") : FeedbackDraft.Message);
        SetRating(forRating ? Mathf.Clamp(PlayerPrefs.GetInt("feedback.rating.stars", 0), 0, 5) : 0);
        localization.Refresh(); RefreshLanguage();
        category.SetValueWithoutNotify(category.value);
        SetStatus("Tell us what happened.");
        overlay.style.display = DisplayStyle.Flex;
        overlay.BringToFront();
        if (forRating) starButtons[0].Focus(); else message.Focus();
    }

    private string T(string value) => localization?.T(value) ?? value;
    private void RefreshLanguage()
    {
        title.text = T(ratingMode ? "Thanks for playing!" : "Send Feedback");
        subtitle.text = T(ratingMode ? "All six levels completed. How was your adventure?" : "Your feedback helps us improve the game.");
        name.label = T("Player name");
        category.label = T("Category");
        message.label = T(ratingMode ? "Comment (optional)" : "Message");
        submit.text = T(ratingMode ? "Send rating" : "Submit");
        cancel.text = T(ratingMode ? "Later" : "Cancel");
        ratingLabel.text = rating > 0 ? rating + " / 5" : T("Choose 1 to 5 stars.");
    }

    private void SetRating(int value)
    {
        rating = value;
        for (int i = 0; i < starImages.Count; i++)
        {
            starImages[i].MarkDirtyRepaint();
            starButtons[i].EnableInClassList("is-selected", value == i + 1);
        }
        ratingLabel.text = value > 0 ? value + " / 5" : T("Choose 1 to 5 stars.");
        if (IsVisible) SaveDraft();
    }

    private void SaveDraft()
    {
        if (!IsVisible || submitted) return;
        if (ratingMode)
        {
            PlayerPrefs.SetInt("feedback.rating.stars", rating);
            PlayerPrefs.SetString("feedback.rating.comment", message.value ?? "");
            PlayerPrefs.Save();
        }
        else FeedbackDraft.Save(category.value, message.value);
    }

    private void Submit()
    {
        if (sending || disposed) return;
        if (ratingMode && rating == 0) { SetStatus("Choose 1 to 5 stars.", true); return; }
        if (!ratingMode && string.IsNullOrWhiteSpace(message.value)) { SetStatus("Enter feedback before submitting.", true); return; }
        string body = ratingMode ? JsonUtility.ToJson(new RatingMessage { rating = rating, comment = message.value?.Trim() ?? "" }) : message.value.Trim();
        if (body.Length > SpacetimeDbFeedbackSink.MaxMessageLength) { SetStatus("Please shorten your message.", true); return; }
        try { ProfileService.SetPlayerName(name.value); }
        catch (Exception) { SetStatus("Could not save your name. Please try again.", true); return; }
        SaveDraft(); SetSending(true); SetStatus("Sending feedback...");
        SpacetimeDbLocalDevRuntime.SubmitFeedback(ratingMode ? "rating" : category.value, body, Application.version, SceneManager.GetActiveScene().name, Completed);
    }

    private void Completed(FeedbackSubmissionResult result)
    {
        if (disposed) return;
        SetSending(false);
        if (result == FeedbackSubmissionResult.Sent)
        {
            submitted = true;
            if (ratingMode)
            {
                PlayerPrefs.DeleteKey("feedback.rating.comment");
                PlayerPrefs.DeleteKey("feedback.rating.stars");
                CompletePrompt();
                foreach (var button in starButtons) button.SetEnabled(false);
            }
            else FeedbackDraft.Save(category.value, "");
            message.SetValueWithoutNotify("");
            submit.SetEnabled(false); name.SetEnabled(false); message.SetEnabled(false); category.SetEnabled(false);
            cancel.text = T("Close");
        }
        SetStatus(FeedbackDraft.StatusText(result), result != FeedbackSubmissionResult.Sent);
    }

    private void SetSending(bool value)
    {
        sending = value;
        submit.SetEnabled(!value); cancel.SetEnabled(!value); name.SetEnabled(!value); message.SetEnabled(!value); category.SetEnabled(!value);
        foreach (var button in starButtons) button.SetEnabled(!value);
    }

    private void SetStatus(string text, bool error = false)
    {
        status.text = T(text);
        status.EnableInClassList("is-error", error);
    }

    private void CompletePrompt()
    {
        if (string.IsNullOrEmpty(promptKey)) return;
        PlayerPrefs.SetInt(promptKey, 1); PlayerPrefs.Save();
    }

    internal void Close()
    {
        if (sending || !IsVisible) return;
        SaveDraft();
        if (ratingMode && !string.IsNullOrEmpty(promptKey)) postponedPrompts.Add(promptKey);
        overlay.style.display = DisplayStyle.None;
        closed?.Invoke();
    }

    private static void DrawStar(MeshGenerationContext context, VisualElement image, bool filled)
    {
        var rect = image.contentRect;
        float radius = Mathf.Min(rect.width, rect.height) * .45f;
        if (radius <= 0) return;
        var painter = context.painter2D;
        painter.fillColor = filled ? new Color32(245, 188, 67, 255) : new Color32(215, 199, 163, 255);
        painter.strokeColor = new Color32(127, 89, 36, 255); painter.lineWidth = 2;
        painter.BeginPath();
        for (int i = 0; i < 10; i++)
        {
            float angle = -Mathf.PI * .5f + i * Mathf.PI / 5;
            float r = i % 2 == 0 ? radius : radius * .46f;
            var point = rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
        }
        painter.ClosePath(); painter.Fill(); painter.Stroke();
    }

    public void Dispose()
    {
        disposed = true;
        SpacetimeDbLocalDevRuntime.RemoveFeedbackCompletion(Completed);
        dismiss.Dispose(); localization.Dispose(); overlay.RemoveFromHierarchy();
    }
}
