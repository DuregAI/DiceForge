using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Map
{
    /// <summary>Presentation only. The owner supplies progress and handles navigation.</summary>
    public sealed class WoodlandMapView : IDisposable
    {
        public const float ReferenceWidth = 1672f;
        public const float ReferenceHeight = 941f;
        private static readonly Vector2[] Anchors =
        {
            new(.145f, .65f), new(.32f, .47f), new(.465f, .565f),
            new(.66f, .465f), new(.565f, .265f), new(.785f, .27f)
        };

        public VisualElement Root { get; }
        public VisualElement Stage { get; }
        public Image HeroImage { get; }
        private readonly VisualElement heroShadow;
        private readonly Button[] nodes = new Button[6];
        private readonly Button play;
        private readonly Label actionLabel, progress;
        private int completed;
        private int nextLevel = 1;
        private bool travelLocked;
        public event Action<int> LevelRequested;
        public event Action BackRequested;

        public WoodlandMapView(StyleSheet styleSheet)
        {
            if (styleSheet == null) throw new ArgumentNullException(nameof(styleSheet));
            Root = new VisualElement { name = "WoodlandMapRoot" };
            Root.AddToClassList("woodland-map");
            Root.styleSheets.Add(styleSheet);
            Stage = Element("WoodlandMapStage", "wm-stage", Root);
            var scenery = Element("WoodlandMapScenery", "wm-scenery", Stage);
            scenery.pickingMode = PickingMode.Ignore;

            for (int i = 0; i < nodes.Length; i++)
            {
                int level = i + 1;
                var node = new Button(() => RequestLevel(level)) { name = "WoodlandLevel" + level };
                node.AddToClassList("wm-node");
                node.style.left = Anchors[i].x * ReferenceWidth - 90;
                node.style.top = Anchors[i].y * ReferenceHeight - 56;
                Stage.Add(node);
                Element("Stone", "wm-stone", node);
                Element("Glow", "wm-glow", node);
                Label("Number", "wm-number", level.ToString(), node);
                Element("Badge", "wm-badge", node);
                nodes[i] = node;
            }

            heroShadow = Element("WoodlandHeroShadow", "wm-hero-shadow", Stage);
            HeroImage = new Image { name = "WoodlandHero", pickingMode = PickingMode.Ignore };
            HeroImage.AddToClassList("wm-hero");
            Stage.Add(HeroImage);
            var title = Element("WoodlandTitlePlaque", "wm-title-plaque", Stage);
            Label("WoodlandChapter", "wm-chapter", "CHAPTER 1", title);
            Label("WoodlandTitle", "wm-title", "WOODLAND TRAIL", title);
            var back = new Button(() => BackRequested?.Invoke()) { name = "WoodlandBack", tooltip = "Back to menu" };
            back.AddToClassList("wm-back");
            Stage.Add(back);
            var counter = Element("WoodlandProgressPanel", "wm-progress-panel", Stage);
            Label("WoodlandProgressCaption", "wm-progress-caption", "PROGRESS", counter);
            progress = Label("WoodlandProgress", "wm-progress", "0 / 6", counter);
            play = new Button(() => { if (completed == 6) BackRequested?.Invoke(); else RequestLevel(nextLevel); })
                { name = "WoodlandPlay" };
            play.AddToClassList("wm-play");
            actionLabel = Label("WoodlandActionText", "wm-action-text", "PLAY LEVEL 1", play);
            Element("Arrow", "wm-arrow", play);
            Stage.Add(play);
            Root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            SetProgress(0);
        }

        public void SetProgress(int completedLevels)
        {
            completed = Mathf.Clamp(completedLevels, 0, 6);
            nextLevel = completed + 1;
            play.SetEnabled(true);
            progress.text = completed + " / 6";
            actionLabel.text = completed == 6 ? "BACK TO MENU" : "PLAY LEVEL " + (completed + 1);
            for (int i = 0; i < nodes.Length; i++)
            {
                bool done = i < completed;
                bool current = i == completed;
                nodes[i].EnableInClassList("is-completed", done);
                nodes[i].EnableInClassList("is-current", current);
                nodes[i].EnableInClassList("is-locked", !done && !current);
                nodes[i].SetEnabled(current);
                nodes[i].tooltip = "Level " + (i + 1) + (done ? " — completed" : current ? " — ready" : " — locked");
            }
            PlaceHero(completed == 6 ? 5 : completed);
        }

        private void RequestLevel(int level)
        {
            if (!travelLocked && level >= 1 && level <= nodes.Length && nodes[level - 1].enabledSelf) LevelRequested?.Invoke(level);
        }

        public void SetMapProgress(MapDefinitionSO map, MapRunState state)
        {
            if (map.nodes.Count != nodes.Length) throw new ArgumentException("Woodland map requires six nodes.", nameof(map));
            completed = 0;
            nextLevel = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = map.nodes[i];
                nodes[i].style.left = node.positionNormalized.x * ReferenceWidth - 90;
                nodes[i].style.top = (1f - node.positionNormalized.y) * ReferenceHeight - 56;
                bool done = state.IsCompleted(node.id);
                bool current = !done && state.IsUnlocked(node.id) && state.currentNodeId == node.id;
                if (done) completed++;
                if (current) nextLevel = i + 1;
                nodes[i].EnableInClassList("is-completed", done);
                nodes[i].EnableInClassList("is-current", current);
                nodes[i].EnableInClassList("is-locked", !done && !current);
                nodes[i].SetEnabled(current);
                nodes[i].tooltip = "Level " + (i + 1) + (done ? " — completed" : current ? " — ready" : " — locked");
            }
            progress.text = completed + " / 6";
            actionLabel.text = completed == 6 ? "BACK TO MENU" : "PLAY LEVEL " + nextLevel;
            play.SetEnabled(completed == 6 || nextLevel > 0);
            PlaceHero(completed == 6 ? 5 : nextLevel - 1);
        }

        private void PlaceHero(int index)
        {
            bool visible = index >= 0 && index < nodes.Length;
            HeroImage.style.display = heroShadow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            SetHeroGroundPosition(GetHeroGroundPosition(index));
        }

        public Vector2 GetHeroGroundPosition(int index)
        {
            if (index < 0 || index >= nodes.Length) throw new ArgumentOutOfRangeException(nameof(index));
            float left = nodes[index].style.left.value.value;
            float top = nodes[index].style.top.value.value;
            // Upper clearings sit beside the title. Stand next to their stone, below the plaque.
            bool upperClearing = top < 290f;
            float sideOffset = left > ReferenceWidth * .7f ? -79f : 115f;
            float heroLeft = left + (upperClearing ? sideOffset : 18f);
            float heroTop = upperClearing ? 184f : top - 103f;
            return new Vector2(heroLeft + 72f, heroTop + 135f);
        }

        public void SetHeroGroundPosition(Vector2 position)
        {
            HeroImage.style.left = position.x - 72f;
            HeroImage.style.top = position.y - 135f;
            heroShadow.style.left = position.x - 30f;
            heroShadow.style.top = position.y;
        }

        public void SetTravelLocked(bool locked)
        {
            travelLocked = locked;
            play.SetEnabled(!locked && (completed == 6 || nextLevel > 0));
            for (int i = 0; i < nodes.Length; i++) nodes[i].SetEnabled(!locked && i + 1 == nextLevel && completed < 6);
        }

        private void OnGeometry(GeometryChangedEvent evt)
        {
            float width = Root.contentRect.width;
            float height = Root.contentRect.height;
            if (width <= 0 || height <= 0) return;
            float scale = Mathf.Min(width / ReferenceWidth, height / ReferenceHeight);
            Stage.style.scale = new Scale(new Vector3(scale, scale, 1));
            // Transform origin is explicitly top-left: terrain and all controls share one transform.
            Stage.style.left = (width - ReferenceWidth * scale) * .5f;
            Stage.style.top = (height - ReferenceHeight * scale) * .5f;
        }

        private static VisualElement Element(string name, string className, VisualElement parent)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            parent.Add(element);
            return element;
        }

        private static Label Label(string name, string className, string text, VisualElement parent)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        public void Dispose()
        {
            Root.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            Root.RemoveFromHierarchy();
            LevelRequested = null;
            BackRequested = null;
        }
    }
}
