using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    public sealed class WorldSelectionView : IDisposable
    {
        private readonly VisualElement root;
        private readonly VisualElement content;
        private readonly WorldCurvedTitle curvedTitle;
        private readonly WorldAtmosphere atmosphere;
        private readonly Button replay;
        private readonly Button home;
        private readonly Button website;
        private readonly Button review;
        private readonly Button[] islandActions = new Button[3];
        private PlayerFeedbackWindow feedback;
        internal Action<string> OpenExternalUrl = Application.OpenURL;
        [Serializable] private sealed class AtlasRegions { public AtlasRegion[] regions; }
        [Serializable] private sealed class AtlasRegion { public float x, y, width, height; }
        private readonly Action onReplay;
        private readonly Action onHome;
        private readonly MenuLocalization localization;
        private readonly Sprite[] sprites = new Sprite[6];
        private readonly IVisualElementScheduledItem refresh;
        private string language;
        private bool navigating;
        private bool failed;
        private Vector2 layoutSize;
        private Rect safeArea;
        public bool IsVisible => !root.ClassListContains("world-hidden");

        public WorldSelectionView(VisualElement parent, Action onReplay, Action onHome)
        {
            this.onReplay = onReplay;
            this.onHome = onHome;
            Resources.Load<VisualTreeAsset>("WorldSelection/WorldSelection").CloneTree(parent);
            root = parent.Q("worldSelectionRoot");
            content = root.Q("worldSafeContent");
            curvedTitle = new WorldCurvedTitle();
            root.Q("worldTitle").parent.Insert(0, curvedTitle);
            replay = root.Q<Button>("worldReplay");
            home = root.Q<Button>("worldHome");
            website = root.Q<Button>("worldWebsite");
            review = root.Q<Button>("worldReview");
            localization = new MenuLocalization(root, null);
            var font = Resources.Load<Font>("Localization/Nunito");
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
            LoadAtlas("WorldIslandsV2", 0);
            LoadAtlas("WorldPlates", 3);
            for (int index = 0; index < 3; index++)
            {
                var island = root.Q<Image>("worldIsland" + index);
                island.sprite = sprites[index];
                island.scaleMode = ScaleMode.ScaleToFit;
            }
            foreach (string name in new[] { "worldWebsite", "worldReview" })
                root.Q(name).style.backgroundImage = new StyleBackground(sprites[4]);
            replay.style.backgroundImage = new StyleBackground(sprites[3]);
            root.Q("worldCompletedPlate").style.backgroundImage = new StyleBackground(sprites[5]);
            WorldSelectionVisuals.Attach(home, "home");
            WorldSelectionVisuals.Attach(root.Q("mushroomLock"), "lock");
            WorldSelectionVisuals.Attach(root.Q("frostyLock"), "lock");
            WorldSelectionVisuals.Attach(root.Q("worldCheckIcon"), "check");
            WorldSelectionVisuals.Attach(root.Q("worldRouteLeft"), "route");
            WorldSelectionVisuals.Attach(root.Q("worldRouteRight"), "route");
            atmosphere = new WorldAtmosphere(root);
            for (int index = 0; index < islandActions.Length; index++)
            {
                var action = new Button { name = "worldIslandAction" + index };
                action.AddToClassList("world-island-action");
                action.clicked += index == 0 ? (Action)Replay : Review;
                root.Q<Image>("worldIsland" + index).parent.Add(action);
                islandActions[index] = action;
            }
            website.clicked += VisitWebsite;
            review.clicked += Review;
            replay.clicked += Replay;
            home.clicked += Home;
            root.RegisterCallback<GeometryChangedEvent>(GeometryChanged);
            refresh = root.schedule.Execute(Refresh).Every(120);
        }

        public void Show()
        {
            navigating = false;
            failed = false;
            replay.SetEnabled(true);
            home.SetEnabled(true);
            root.RemoveFromClassList("world-hidden");
            root.BringToFront();
            atmosphere.Resume();
            language = null;
            Refresh();
            root.Q<ScrollView>("worldScroll").scrollOffset = Vector2.zero;
            replay.Focus();
        }

        private void LoadAtlas(string name, int offset)
        {
            var atlas = Resources.Load<Texture2D>("WorldSelection/" + name);
            var regions = JsonUtility.FromJson<AtlasRegions>(Resources.Load<TextAsset>("WorldSelection/" + name + "Regions").text).regions;
            for (int index = 0; index < regions.Length; index++)
            {
                var region = regions[index];
                sprites[offset + index] = Sprite.Create(atlas, new Rect(region.x, region.y, region.width, region.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            }
        }

        public void Hide()
        {
            atmosphere.Pause();
            feedback?.Close();
            root.AddToClassList("world-hidden");
        }

        private void VisitWebsite()
        {
            if (!navigating && !Diceforge.Transitions.ScreenTransition.IsBusy) OpenExternalUrl("https://glimblehop.com/");
        }

        private void Review()
        {
            if (navigating || Diceforge.Transitions.ScreenTransition.IsBusy) return;
            if (feedback == null)
            {
                feedback = new PlayerFeedbackWindow(root, () => review.Focus());
                var modal = root.Q("FeedbackModal");
                var window = modal.Q(className: "pf-window");
                window.Add(modal.Q(className: "pf-actions"));
                var scroll = modal.Q<ScrollView>();
                scroll.mode = ScrollViewMode.Vertical;
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                var close = modal.Q<Button>("FeedbackModalCloseIcon");
                close.RemoveFromClassList("df-modal-close");
                close.RemoveFromClassList("df-modal-close-icon");
                var icon = DemoUiIcons.Attach(close, DemoUiIcon.Close);
                icon.style.width = icon.style.height = 24;
            }
            feedback.Open(true);
            var feedbackRoot = root.Q("FeedbackModal");
            var bodyFont = FontDefinition.FromFont(Resources.Load<Font>("Localization/Nunito"));
            foreach (var text in feedbackRoot.Query<TextElement>().ToList()) text.style.unityFontDefinition = bodyFont;
            feedbackRoot.Q<Label>(className: "pf-title").style.unityFontDefinition = FontDefinition.FromFont(Resources.Load<Font>("WorldSelection/GlimbleRoundBlack"));
        }

        private void Refresh()
        {
            if (!IsVisible) return;
            string current = PlayerPrefs.GetString("ui.language", "en");
            if (current != language)
            {
                language = current;
                localization.Refresh();
                Set("worldTitle", "New worlds await");
                curvedTitle.SetText(localization.T("New worlds await"));
                Set("worldSubtitle", "Woodland Trail complete. Your adventure continues!");
                Set("woodlandName", "Woodland Trail");
                Set("mushroomName", "Mushroom Hollow");
                Set("frostyName", "Frosty Pass");
                root.Q<Label>("worldCompleted").text = "6 / 6";
                Set("mushroomSoon", "Coming soon");
                Set("frostySoon", "Coming soon");
                Set("worldFooter", "Thank you for completing the first chapter!");
                replay.text = localization.T("Play again");
                replay.tooltip = localization.T("Start a new six-level journey. Your rewards are kept.");
                home.tooltip = localization.T("Home");
                website.text = localization.T("Visit website");
                review.text = localization.T("Write a review");
                islandActions[0].tooltip = localization.T("Woodland Trail") + " — " + localization.T("Play again");
                islandActions[1].tooltip = localization.T("Mushroom Hollow") + " — " + localization.T("Write a review");
                islandActions[2].tooltip = localization.T("Frosty Pass") + " — " + localization.T("Write a review");
            }
            root.Q<Label>("worldError").text = localization.T("Could not save the new journey. Please try again.");
            root.Q("worldError").EnableInClassList("world-hidden", !failed);
            UpdateLayout();
        }

        private void Set(string name, string source) => root.Q<Label>(name).text = localization.T(source);
        private void GeometryChanged(GeometryChangedEvent evt) => UpdateLayout();
        private void UpdateLayout()
        {
            var size = root.layout.size;
            if (size.x <= 0 || size.y <= 0 || float.IsNaN(size.x)) return;
            if (layoutSize == size && safeArea == Screen.safeArea) return;
            layoutSize = size;
            safeArea = Screen.safeArea;
            bool portrait = size.y > size.x;
            root.EnableInClassList("world-portrait", portrait);
            root.EnableInClassList("world-short", !portrait && size.y < 640);
            foreach (var card in root.Query<VisualElement>(className: "world-card").ToList())
                card.style.height = portrait ? new StyleLength(Mathf.Clamp(size.y * .54f / 3 - 10, 150, 195)) : new StyleLength(StyleKeyword.Null);
            float scaleX = size.x / Mathf.Max(1, Screen.width);
            float scaleY = size.y / Mathf.Max(1, Screen.height);
            content.style.left = safeArea.xMin * scaleX;
            content.style.right = (Screen.width - safeArea.xMax) * scaleX;
            content.style.top = (Screen.height - safeArea.yMax) * scaleY;
            content.style.bottom = safeArea.yMin * scaleY;
            curvedTitle.SetSize(portrait ? 32 : Mathf.Clamp(size.x * .045f, 34, 72));
            foreach (string name in new[] { "woodlandName", "mushroomName", "frostyName", "worldReplay", "worldWebsite", "worldReview" })
                root.Q(name).style.fontSize = portrait ? new StyleLength(StyleKeyword.Null) : new StyleLength(Mathf.Clamp(size.x * .020f, 20, 32));

        }

        private void Replay()
        {
            if (navigating || Diceforge.Transitions.ScreenTransition.IsBusy) return;
            if (!DemoWorldProgress.TryStartReplay())
            {
                failed = true;
                Refresh();
                return;
            }
            navigating = true;
            replay.SetEnabled(false);
            home.SetEnabled(false);
            onReplay();
        }

        private void Home()
        {
            if (navigating || Diceforge.Transitions.ScreenTransition.IsBusy) return;
            navigating = true;
            onHome();
        }

        public void Dispose()
        {
            refresh.Pause();
            atmosphere.Dispose();
            for (int index = 0; index < islandActions.Length; index++)
                islandActions[index].clicked -= index == 0 ? (Action)Replay : Review;
            feedback?.Dispose();
            website.clicked -= VisitWebsite;
            review.clicked -= Review;
            replay.clicked -= Replay;
            home.clicked -= Home;
            root.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);
            localization.Dispose();
            root.RemoveFromHierarchy();
            foreach (var sprite in sprites) if (sprite != null) UnityEngine.Object.Destroy(sprite);
        }
    }
}
