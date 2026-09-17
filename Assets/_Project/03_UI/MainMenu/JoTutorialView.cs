using System;
using UnityEngine;
using UnityEngine.UIElements;

// A menu-only demonstration. Never launches or mutates a campaign or battle.
internal sealed class JoTutorialView : IDisposable
{
    private readonly VisualElement panel;
    private readonly Func<string, string> translate;
    private readonly Action close;
    private readonly Button[] tokens;
    private readonly Action[] selectActions;
    private readonly int[] distances = { 1, 3, 5 };
    private readonly Button next, back, exit, goblin;
    private IVisualElementScheduledItem animation;
    private int distance, page;
    private readonly VisualElement stage, ghost;
    private readonly TutorialArt[] tiles = new TutorialArt[6];
    private readonly TutorialArt reminder;
    private readonly VisualElement arrows;
    private bool moving, finished;

    public JoTutorialView(VisualElement panel, Func<string, string> translate, Action close)
    {
        this.panel = panel;
        this.translate = translate;
        this.close = close;
        tokens = new Button[3];
        selectActions = new Action[3];
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            tokens[i] = panel.Q<Button>("JoStep" + distances[i]);
            tokens[i].Add(new TutorialArt(i));
            selectActions[i] = () => Select(distances[index]);
            tokens[i].clicked += selectActions[i];
        }
        next = panel.Q<Button>("JoNext");
        back = panel.Q<Button>("JoBack");
        exit = panel.Q<Button>("JoClose");
        goblin = panel.Q<Button>("JoGoblin");
        next.clicked += Advance;
        back.clicked += Back;
        exit.clicked += Exit;
        goblin.clicked += Move;
        stage = panel.Q("JoStage");
        ghost = panel.Q("JoGhost");
        ghost.Add(new TutorialArt(5));
        goblin.Add(new TutorialArt(5));
        reminder = new TutorialArt(0);
        panel.Q("JoSelected").Add(reminder);
        for (int i = 0; i < 6; i++)
        {
            tiles[i] = new TutorialArt(3);
            panel.Q("JoPath")[i].Add(tiles[i]);
        }
        arrows = new VisualElement { pickingMode = PickingMode.Ignore };
        arrows.style.position = Position.Absolute;
        arrows.style.left = 0; arrows.style.right = 0;
        arrows.style.top = 35; arrows.style.height = 55;
        arrows.generateVisualContent += DrawArrows;
        panel.Q("JoDemo").Add(arrows);
        panel.RegisterCallback<GeometryChangedEvent>(OnGeometry);
        Reset();
    }
    private void OnGeometry(GeometryChangedEvent evt)
    {
        float scale = Mathf.Min(panel.contentRect.width / 1672f, panel.contentRect.height / 941f);
        stage.style.scale = new Scale(new Vector3(scale, scale, 1));
        stage.style.left = (panel.contentRect.width - 1672) / 2;
        stage.style.top = (panel.contentRect.height - 941) / 2;
    }

    public void Reset()
    {
        CancelAnimation();
        distance = page = 0;
        finished = false;
        Render();
    }

    private void Select(int value) { distance = value; Render(); }
    private void Advance()
    {
        if (page == 0 && distance > 0) { page = 1; Render(); goblin.Focus(); }
        else if (page == 1 && finished) Exit();
    }
    private void Back()
    {
        if (page == 0) { Exit(); return; }
        CancelAnimation();
        page = 0;
        finished = false;
        Render();
    }
    public void Exit() { CancelAnimation(); close(); }
    public void CancelAnimation() { animation?.Pause(); animation = null; moving = false; }
    private void Render()
    {
        panel.Q<Label>("JoCounter").text = (page + 1) + " / 2";
        panel.Q<Label>("JoTitle").text = translate(page == 0 ? "CHOOSE A STEP" : "CHOOSE A GOBLIN");
        panel.Q<Label>("JoSpeech").text = translate(page == 0
            ? "Pick a token. Its number shows how far you move."
            : finished ? "Well done! Pick a step, then a goblin."
            : "Tap the goblin to make your move.");
        FitSpeech();
        panel.Q("JoChoices").style.display = page == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        panel.Q("JoDemo").style.display = DisplayStyle.Flex;
        panel.EnableInClassList("jo-second-page", page == 1);
        goblin.style.visibility = Visibility.Visible;
        panel.Q("JoSelected").style.display = page == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        reminder.SetIndex(System.Array.IndexOf(distances, distance));
        back.text = translate(page == 0 ? "BACK TO MENU" : finished ? "TRY AGAIN" : "BACK");
        panel.Q<Label>("JoHint").text = translate(page == 0 ? "Try any token."
            : finished ? "Ready for an adventure!" : "Tap the goblin to try your move.");
        for (int i = 0; i < tokens.Length; i++)
        {
            tokens[i].EnableInClassList("jo-selected", distances[i] == distance);
            tokens[i].tooltip = translate(i == 0 ? "STEP" : i == 1 ? "DASH" : "HOP");
        }
        var tiles = panel.Q("JoPath");
        for (int i = 0; i < tiles.childCount; i++)
            this.tiles[i].SetIndex(i > 0 && i <= distance ? 4 : 3);
        arrows.MarkDirtyRepaint();
        ghost.style.left = Length.Percent(distance * 16.6667f);
        ghost.style.display = distance > 0 && !finished ? DisplayStyle.Flex : DisplayStyle.None;
        goblin.style.left = Length.Percent((finished ? distance : 0) * 16.6667f);
        goblin.SetEnabled(page == 1 && !finished && !moving);
        next.text = translate(page == 0 ? "NEXT" : "BACK TO MENU");
        next.SetEnabled(page == 0 ? distance > 0 : finished);
        next.style.visibility = next.enabledSelf ? Visibility.Visible : Visibility.Hidden;
    }
    private void FitSpeech()
    {
        var label = panel.Q<Label>("JoSpeech");
        for (int size = 24; size >= 18; size--)
        {
            label.style.fontSize = size;
            var measured = label.MeasureTextSize(label.text, 236, VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined);
            if (measured.y <= 108) break;
        }
    }
    private void Move()
    {
        if (page != 1 || moving || finished) return;
        moving = true;
        goblin.SetEnabled(false);
        float start = Time.realtimeSinceStartup;
        animation = panel.schedule.Execute(() =>
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / .85f);
            goblin.style.left = Length.Percent(Mathf.SmoothStep(0, distance * 16.6667f, t));
            if (t < 1) return;
            CancelAnimation();
            finished = true;
            Render();
            next.Focus();
        }).Every(16);
    }
    private void DrawArrows(MeshGenerationContext context)
    {
        if (finished) return;
        var p = context.painter2D;
        p.strokeColor = new Color32(91, 59, 29, 255);
        p.lineWidth = 4;
        float stride = arrows.contentRect.width / 6;
        for (int i = 0; i < distance; i++)
        {
            float x = (i + .65f) * stride, end = (i + 1.35f) * stride;
            p.BeginPath(); p.MoveTo(new Vector2(x, 38));
            p.BezierCurveTo(new Vector2(x + 15, 15), new Vector2(end - 15, 15), new Vector2(end, 38)); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(end - 12, 32)); p.LineTo(new Vector2(end,38)); p.LineTo(new Vector2(end,24)); p.Stroke();
        }
    }
    public void Dispose()
    {
        CancelAnimation();
        panel.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
        for (int i = 0; i < tokens.Length; i++) tokens[i].clicked -= selectActions[i];
        next.clicked -= Advance; back.clicked -= Back; exit.clicked -= Exit; goblin.clicked -= Move;
    }

}
