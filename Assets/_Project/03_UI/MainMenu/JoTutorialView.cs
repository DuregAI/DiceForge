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
            tokens[i].Insert(0, new WoodenToken());
            tokens[i].Insert(1, new StepMark(i));
            tokens[i].text = string.Empty;
            var number = new Label(distances[i].ToString()) { pickingMode = PickingMode.Ignore };
            number.AddToClassList("jo-token-number");
            tokens[i].Add(number);
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
        Reset();
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
            ? "Pick a token. Its number is how many spaces you move."
            : finished ? "Nicely done! Choose a step, then choose a goblin. That’s the idea!"
            : "Now tap the goblin. The glowing stones show your route.");
        panel.Q("JoChoices").style.display = page == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        panel.Q("JoDemo").style.display = DisplayStyle.Flex;
        panel.EnableInClassList("jo-second-page", page == 1);
        goblin.style.visibility = page == 1 ? Visibility.Visible : Visibility.Hidden;
        back.text = translate(page == 0 ? "BACK TO MENU" : "BACK");
        panel.Q<Label>("JoHint").text = translate(page == 0 ? "Try any token."
            : finished ? "Ready for an adventure!" : "Tap the goblin to try your move.");
        for (int i = 0; i < tokens.Length; i++)
        {
            tokens[i].EnableInClassList("jo-selected", distances[i] == distance);
            tokens[i].tooltip = translate(i == 0 ? "STEP" : i == 1 ? "DASH" : "HOP");
        }
        var tiles = panel.Q("JoPath");
        for (int i = 0; i < tiles.childCount; i++)
            tiles[i].EnableInClassList("jo-lit", i > 0 && i <= distance);
        goblin.style.left = Length.Percent((finished ? distance : 0) * 16.6667f);
        goblin.SetEnabled(!finished && !moving);
        next.text = translate(page == 0 ? "NEXT" : "BACK TO MENU");
        next.SetEnabled(page == 0 ? distance > 0 : finished);
        next.style.visibility = next.enabledSelf ? Visibility.Visible : Visibility.Hidden;
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
    public void Dispose()
    {
        CancelAnimation();
        for (int i = 0; i < tokens.Length; i++) tokens[i].clicked -= selectActions[i];
        next.clicked -= Advance; back.clicked -= Back; exit.clicked -= Exit; goblin.clicked -= Move;
    }

    private sealed class WoodenToken : VisualElement
    {
        public WoodenToken()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("jo-token-wood");
            generateVisualContent += context =>
            {
                var p = context.painter2D;
                float w = contentRect.width, h = contentRect.height;
                var center = new Vector2(w / 2, h / 2 - 6);
                float radius = Mathf.Min(w, h) * .45f;
                p.fillColor = new Color32(103, 60, 29, 255);
                p.BeginPath(); p.Arc(center + new Vector2(0, 9), radius, 0, 360); p.Fill();
                p.fillColor = new Color32(219, 168, 103, 255);
                p.strokeColor = new Color32(133, 83, 40, 255); p.lineWidth = 3;
                p.BeginPath(); p.Arc(center, radius, 0, 360); p.Fill(); p.Stroke();
                p.strokeColor = new Color32(244, 206, 149, 255); p.lineWidth = 2;
                p.BeginPath(); p.Arc(center, radius - 6, 0, 360); p.Stroke();
                p.strokeColor = new Color32(177, 121, 65, 150); p.lineWidth = 1.5f;
                for (int i = -3; i <= 3; i++)
                {
                    float y = i * radius * .22f;
                    float x = Mathf.Sqrt(radius * radius * .78f - y * y);
                    p.BeginPath(); p.MoveTo(center + new Vector2(-x, y));
                    p.BezierCurveTo(center + new Vector2(-x * .4f, y - 5), center + new Vector2(x * .4f, y + 5), center + new Vector2(x, y)); p.Stroke();
                }
            };
        }
    }

    // Carved boot silhouettes, independent of font glyphs and crisp at UI scale.
    private sealed class StepMark : VisualElement
    {
        private readonly int kind;
        public StepMark(int kind)
        {
            this.kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("jo-step-mark");
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float w = contentRect.width, h = contentRect.height;
            p.fillColor = new Color32(74, 43, 24, 255);
            p.BeginPath();
            p.MoveTo(new Vector2(w * .38f, h * .13f));
            p.LineTo(new Vector2(w * .67f, h * .13f));
            p.LineTo(new Vector2(w * .65f, h * .51f));
            p.LineTo(new Vector2(w * .9f, h * .66f));
            p.LineTo(new Vector2(w * .92f, h * .84f));
            p.LineTo(new Vector2(w * .35f, h * .84f));
            p.ClosePath(); p.Fill();
            p.strokeColor = new Color32(74, 43, 24, 255); p.lineWidth = 5;
            if (kind == 2)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(w * .4f, h * .55f));
                p.BezierCurveTo(new Vector2(w * .08f, h * .55f), new Vector2(w * .04f, h * .2f), new Vector2(w * .08f, h * .03f));
                p.BezierCurveTo(new Vector2(w * .19f, h * .18f), new Vector2(w * .39f, h * .15f), new Vector2(w * .4f, h * .32f));
                p.LineTo(new Vector2(w * .21f, h * .25f));
                p.LineTo(new Vector2(w * .38f, h * .43f));
                p.LineTo(new Vector2(w * .2f, h * .36f));
                p.ClosePath(); p.Fill();
            }
            if (kind == 1)
                for (int i = 0; i < 3; i++)
                {
                    p.BeginPath(); p.MoveTo(new Vector2(w * .08f, h * (.37f + i * .17f)));
                    p.LineTo(new Vector2(w * .28f, h * (.37f + i * .17f - (kind == 2 ? .12f : 0)))); p.Stroke();
                }
            p.strokeColor = new Color32(225, 179, 114, 255); p.lineWidth = 3;
            for (int i = 0; i < 3; i++)
            {
                p.BeginPath(); p.MoveTo(new Vector2(w * .5f, h * (.26f + i * .1f)));
                p.LineTo(new Vector2(w * .67f, h * (.26f + i * .1f))); p.Stroke();
            }
        }
    }
}
