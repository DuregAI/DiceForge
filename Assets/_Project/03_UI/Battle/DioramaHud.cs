using Diceforge.Core;
using Diceforge.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    public sealed class DioramaHud : MonoBehaviour
    {
        public static bool BlocksGameplay => instance != null && (instance.paused || instance.battle?.DemoNarrative?.IsModal == true || instance.battle?.DemoNarrative?.IsHelpVisible == true);
        private static DioramaHud instance;
        public static void SetResultVisible(bool visible)
        {
            if (instance?.root != null)
            {
                if (visible) instance.CloseBattleMenus();
                instance.root.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
        private BattleDebugController battle;
        private DioramaBoard board;
        private GameObject settingsObject;
        private UIDocument document, settingsDocument;
        public UIDocument Document => document;
        private VisualElement root, panel, dice, menu, settingsPanel;
        private Label turn, scoreA, scoreB, roleLabelA, roleLabelB, hint;
        private Label demoTitle, stepRule, selectedHero;
        private string previewHero;
        private readonly System.Collections.Generic.Dictionary<string, Button> heroButtons = new();
        private Button reroll, settingsButton, settingsMute;
        private Toggle pauseMute;
        private Slider musicSlider, sfxSlider;
        private AudioManager audioManager;
        private Diceforge.UI.ModalDismiss settingsDismiss, pauseDismiss;
        private bool paused;
        private bool settingsOpenedFromPause;
        private float previousTimeScale;
        private string diceSignature;
        private MenuLocalization localization, settingsLocalization;
        private Font demoFont;
        private PlayerFeedbackWindow feedbackWindow;
        private int width,height;
        private float logicalWidth,logicalHeight;
        private Rect safeArea;
        public void Initialize(BattleDebugController controller,DioramaBoard geometry)
        {
            instance=this; battle=controller; board=geometry;
            var old=FindAnyObjectByType<DebugHudUITK>();
            document=gameObject.AddComponent<UIDocument>();
            document.panelSettings=Resources.Load<PanelSettings>("WoodlandPanel");
            document.visualTreeAsset=Resources.Load<VisualTreeAsset>("WoodlandHud");
            document.sortingOrder=10;
            root=document.rootVisualElement; root.pickingMode=PickingMode.Ignore;
            if(old?.Document!=null)old.Document.rootVisualElement.style.display=DisplayStyle.None;
            panel=root.Q("woodlandRoot");panel.pickingMode=PickingMode.Ignore;
            var settingsPanelSettings=Resources.Load<PanelSettings>("BattleSettingsPanel");
            if(settingsPanelSettings!=null)
            {
                settingsObject=new GameObject("BattleSettingsWindow");
                settingsDocument=settingsObject.AddComponent<UIDocument>();
                settingsDocument.panelSettings=settingsPanelSettings;
                var settingsRoot=settingsDocument.rootVisualElement;
                settingsRoot.pickingMode=PickingMode.Ignore;
                settingsPanel=SharedSettingsPanel.AttachTo(settingsRoot);
                if(settingsPanel!=null) settingsLocalization=new MenuLocalization(settingsRoot,RefreshHudLocalization);
            }
            else Debug.LogError("[Settings] BattleSettingsPanel is missing.");
            localization=new MenuLocalization(root,OnHudLocaleChanged);
            dice=root.Q("moves"); menu=root.Q("pauseOverlay");
            pauseDismiss=new Diceforge.UI.ModalDismiss(menu,root.Q("pauseWindow"),HandlePauseDismiss);
            turn=root.Q<Label>("turn");scoreA=root.Q<Label>("scoreA");scoreB=root.Q<Label>("scoreB");hint=root.Q<Label>("hint");
            roleLabelA=root.Q<Label>("roleA");roleLabelB=root.Q<Label>("roleB");
            InitializeDemoControls();
            AttachTeamIcon(root.Q("teamIconA"),PlayerId.A);
            AttachTeamIcon(root.Q("teamIconB"),PlayerId.B);
            reroll=root.Q<Button>("reroll");reroll.clicked+=()=>battle.PresentationReroll();
            settingsButton=root.Q<Button>("settings");
            if (battle.DemoLevel != null) AttachDemoMenuIcons(root.Q<Button>("pause"), settingsButton);
            settingsButton.SetEnabled(settingsPanel!=null);
            if(settingsPanel!=null) InitializeSettings();
            root.Q<Button>("pauseSettings").SetEnabled(settingsPanel!=null);
            if(settingsPanel!=null) root.Q<Button>("pauseSettings").clicked+=OpenSettings;
            audioManager ??= AudioManager.Instance!=null?AudioManager.Instance:FindAnyObjectByType<AudioManager>();
            root.Q<Button>("pause").clicked+=()=>SetPaused(true);
            root.Q<Button>("resume").clicked+=()=>SetPaused(false);
            root.Q<Button>("surrender").clicked+=()=>SetSurrenderConfirmation(true);
            root.Q<Button>("cancelSurrender").clicked+=()=>{SetSurrenderConfirmation(false);root.Q<Button>("surrender")?.Focus();};
            root.Q<Button>("acceptSurrender").clicked+=()=>{SetPaused(false);if(!battle.IsMatchEnded)battle.ConfirmSurrender();};
            var reduced=root.Q<Toggle>("reducedMotion");reduced.SetValueWithoutNotify(PlayerPrefs.GetInt("WoodlandReducedMotion",0)==1);
            AddPauseToggleCheck(reduced);
            DioramaBoard.ReducedMotion=reduced.value;
            reduced.RegisterValueChangedCallback(e=>{DioramaBoard.ReducedMotion=e.newValue;PlayerPrefs.SetInt("WoodlandReducedMotion",e.newValue?1:0);});
            pauseMute=root.Q<Toggle>("mute");
            AddPauseToggleCheck(pauseMute);
            pauseMute.SetValueWithoutNotify(audioManager!=null && audioManager.IsMuted);
            pauseMute.RegisterValueChangedCallback(e=>audioManager?.SetMuted(e.newValue));
            board.GeometryChanged+=OnGeometryChanged;
            ApplyDemoFont();
        }

        private void RefreshHudLocalization()
        {
            localization?.Refresh();
            OnHudLocaleChanged();
        }

        private string demoLanguage;
        private void OnHudLocaleChanged()
        {
            diceSignature = null;
            demoLanguage = PlayerPrefs.GetString("ui.language", "en");
            ApplyDemoFont();
        }

        private void ApplyDemoFont()
        {
            if (battle?.DemoLevel == null || panel == null) return;
            demoFont ??= Resources.Load<Font>("Localization/Nunito");
            if (demoFont == null) return;
            var definition = new StyleFontDefinition(FontDefinition.FromFont(demoFont));
            panel.style.unityFontDefinition = definition;
            foreach (string className in new[] { "hud-header", "bottom-bar", "pause-window" })
            {
                var section = panel.Q(className: className);
                if (section == null) continue;
                section.Query<TextElement>().ForEach(text =>
                {
                    text.style.unityFontDefinition = definition;
                    text.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(StyleKeyword.Null);
                });
            }
        }

        private void InitializeDemoControls()
        {
            if (battle.DemoLevel == null) return;
            panel.AddToClassList("demo-mode");
            demoTitle = root.Q<Label>("demoTitle");
            root.Q(className: "top-bar").Insert(0, demoTitle);
            stepRule = root.Q<Label>("stepRule");
            selectedHero = root.Q<Label>("selectedHero");
            var heroes = root.Q("demoHeroes");
            root.Q("scorePanelB")?.AddToClassList("hidden");
            demoTitle.RemoveFromClassList("hidden");
            stepRule.RemoveFromClassList("hidden");
            selectedHero.RemoveFromClassList("hidden");
            heroes.RemoveFromClassList("hidden");
            foreach (string id in battle.DemoLevel.heroIds)
            {
                string heroId = id;
                var button = new Button(() => battle.MovePresentationHero(heroId)) { name = "hero_" + heroId };
                button.AddToClassList("demo-hero");
                button.AddToClassList("demo-hero-" + heroId);
                heroes.Add(button);
                heroButtons.Add(heroId, button);
                button.RegisterCallback<PointerEnterEvent>(_ => previewHero = heroId);
                button.RegisterCallback<FocusInEvent>(_ => previewHero = heroId);
                button.RegisterCallback<PointerLeaveEvent>(_ => previewHero = null);
                button.RegisterCallback<FocusOutEvent>(_ => previewHero = null);
            }
            var restart = root.Q<Button>("restartTrail");
            restart.RemoveFromClassList("hidden");
            restart.clicked += () => { CloseBattleMenus(); battle.RestartMatch(); };
            localization.Refresh();
            ApplyDemoFont();
        }

        private static void AttachDemoMenuIcons(Button pauseButton, Button settingsButton)
        {
            DemoUiIcons.Attach(pauseButton, DemoUiIcon.Pause);
            DemoUiIcons.Attach(settingsButton, DemoUiIcon.Settings);
        }
        private readonly System.Collections.Generic.HashSet<string> presentedExited = new();
        private void RefreshDemoControls(bool can)
        {
            if (battle.DemoLevel == null) return;
            if (demoLanguage != PlayerPrefs.GetString("ui.language", "en")) RefreshHudLocalization();
            SetText(demoTitle, localization.T(battle.DemoLevel.title));
            SetText(stepRule, localization.T(battle.PresentationState.Rules.soloTrailStepOfferMode == SoloTrailStepOfferMode.Sequential
                ? "Two steps per turn" : "One step per turn"));
            SetText(selectedHero, string.IsNullOrEmpty(battle.SelectedHeroId) ? localization.T("Choose a friend by name.")
                : string.Format(localization.T("Selected: {0}"), localization.T(Diceforge.GameModes.DemoLevelDefinition.HeroName(battle.SelectedHeroId))));
            foreach (var pair in heroButtons)
            {
                bool found = battle.TryGetHero(pair.Key, out _, out _, out bool exited);
                if (!battle.PresentationIsAnimating)
                {
                    if (exited) presentedExited.Add(pair.Key);
                    else presentedExited.Remove(pair.Key);
                }
                pair.Value.text = localization.T(Diceforge.GameModes.DemoLevelDefinition.HeroName(pair.Key))
                    + (presentedExited.Contains(pair.Key) ? " · " + localization.T("Arrived") : string.Empty);
                pair.Value.SetEnabled(can && found && !exited);
                pair.Value.EnableInClassList("available", can && found && !exited);
                pair.Value.EnableInClassList("arrived", presentedExited.Contains(pair.Key));
                pair.Value.EnableInClassList("selected", battle.SelectedHeroId == pair.Key);
            }
            var actions = root.Q<Label>("actionsRemaining");
            bool sequential = battle.PresentationState.Rules.soloTrailStepOfferMode == SoloTrailStepOfferMode.Sequential;
            actions.EnableInClassList("hidden", !sequential);
            if (sequential && !battle.PresentationIsAnimating)
                SetText(actions, string.Format(localization.T("Actions left: {0}"), battle.PresentationDice.Count));
            var hazard = root.Q<Label>("hazardHint");
            var state = battle.PresentationState;
            hazard.EnableInClassList("hidden", state.Rules.soloTrailHazard == SoloTrailHazard.None);
            if (state.Rules.soloTrailHazard != SoloTrailHazard.None && !battle.PresentationIsAnimating)
                SetText(hazard, localization.T(state.Rules.soloTrailHazard == SoloTrailHazard.Bark ? "Bark blocks the landing. Jump over him."
                    : state.TrailHazardCell >= 0 ? "Rusty moves after both steps." : "Rusty has stepped aside. The trail is clear."));
            root.Q<Button>("surrender").text = localization.T("Leave trail");
            root.Q<Label>("leaveTitle").text = localization.T("Leave trail?");
            root.Q<Label>("leaveMessage").text = localization.T("Return to the map without completing this trail?");
            root.Q<Button>("acceptSurrender").text = localization.T("To map");
        }
        private static void AddPauseToggleCheck(Toggle toggle)
        {
            if(toggle==null || toggle.Q(className:"pause-toggle-check")!=null)return;
            var check=new VisualElement{pickingMode=PickingMode.Ignore};
            check.AddToClassList("pause-toggle-check");
            check.generateVisualContent+=context=>
            {
                if(!toggle.value)return;
                float width=check.contentRect.width,height=check.contentRect.height;
                if(width<=0 || height<=0)return;
                var painter=context.painter2D;
                painter.strokeColor=new Color32(255,241,202,255);
                painter.lineWidth=3;
                painter.BeginPath();
                painter.MoveTo(new Vector2(width*.20f,height*.50f));
                painter.LineTo(new Vector2(width*.43f,height*.73f));
                painter.LineTo(new Vector2(width*.82f,height*.25f));
                painter.Stroke();
            };
            toggle.RegisterValueChangedCallback(_=>check.MarkDirtyRepaint());
            (toggle.Q(className:"unity-toggle__input") ?? toggle).Add(check);
        }
        private void InitializeSettings()
        {
            settingsPanel.style.display=DisplayStyle.None;
            settingsPanel.Q<Button>("btnCopyLog").style.display=DisplayStyle.None;
            feedbackWindow=new PlayerFeedbackWindow(settingsDocument.rootVisualElement,()=>settingsPanel.Q<Button>("btnOpenFeedback")?.Focus());
            settingsPanel.Q<Button>("btnOpenFeedback").clicked+=()=>feedbackWindow.Open();
            settingsPanel.Q<Label>("lblAboutVersion").text=$"{localization.T("Version")} {Application.version}";
            settingsPanel.Q<Label>("lblBuildInfo").style.display=DisplayStyle.None;
            settingsButton.clicked+=OpenSettings;
            settingsPanel.Q<Button>("btnCloseSettings").clicked+=CloseSettings;
            settingsPanel.Q<Button>("btnSettingsDone").clicked+=CloseSettings;
            settingsDismiss=new Diceforge.UI.ModalDismiss(settingsPanel,settingsPanel.Q("SettingsWindow"),CloseSettings);
            settingsMute=settingsPanel.Q<Button>("btnSettingsMute");
            settingsMute.clicked+=ToggleSettingsMute;
            musicSlider=settingsPanel.Q<Slider>("sliderMusicVolume");
            sfxSlider=settingsPanel.Q<Slider>("sliderSfxVolume");
            musicSlider.RegisterValueChangedCallback(OnMusicVolumeChanged);
            sfxSlider.RegisterValueChangedCallback(OnSfxVolumeChanged);
            InitializeVolumeFill(musicSlider);
            InitializeVolumeFill(sfxSlider);
            audioManager=AudioManager.Instance!=null?AudioManager.Instance:FindAnyObjectByType<AudioManager>();
            if(audioManager!=null)
            {
                audioManager.OnMuteChanged+=OnMuteChanged;
                audioManager.OnVolumesChanged+=OnVolumesChanged;
                OnMuteChanged(audioManager.IsMuted);
                OnVolumesChanged(audioManager.MusicVolume,audioManager.SfxVolume);
            }
        }
        private void OpenSettings()
        {
            if(settingsPanel==null || battle==null || battle.IsMatchEnded)return;
            settingsOpenedFromPause=paused;
            SetPaused(true);
            menu.AddToClassList("hidden");
            settingsPanel.style.display=DisplayStyle.Flex;
            settingsPanel.AddToClassList("is-visible");
            settingsPanel.Q<Button>("btnCloseSettings")?.Focus();
        }
        private void CloseSettings()
        {
            if(settingsPanel==null)return;
            settingsPanel.RemoveFromClassList("is-visible");
            settingsPanel.style.display=DisplayStyle.None;
            if(settingsOpenedFromPause && battle!=null && !battle.IsMatchEnded)
            {
                SetSurrenderConfirmation(false);
                menu.RemoveFromClassList("hidden");
                root.Q<Button>("resume")?.Focus();
            }
            else SetPaused(false);
            settingsOpenedFromPause=false;
        }
        private void CloseBattleMenus()
        {
            SetPaused(false);
            if(settingsPanel!=null)
            {
                settingsPanel.RemoveFromClassList("is-visible");
                settingsPanel.style.display=DisplayStyle.None;
            }
            settingsOpenedFromPause=false;
        }
        private void ToggleSettingsMute() => audioManager?.SetMuted(!audioManager.IsMuted);
        private void OnMuteChanged(bool muted)
        {
            settingsMute.EnableInClassList("is-muted",muted);
            settingsPanel.Q("SettingsAudioControls")?.EnableInClassList("is-muted",muted);
            settingsMute.tooltip=localization.T(muted?"Unmute audio":"Mute audio");
            pauseMute?.SetValueWithoutNotify(muted);
        }
        private void OnVolumesChanged(float music,float sfx)
        {
            musicSlider.SetValueWithoutNotify(music);
            sfxSlider.SetValueWithoutNotify(sfx);
            UpdateVolumeFill(musicSlider,music);
            UpdateVolumeFill(sfxSlider,sfx);
        }
        private void OnMusicVolumeChanged(ChangeEvent<float> evt)
        {
            if(audioManager!=null && audioManager.IsMuted) audioManager.SetMuted(false);
            audioManager?.SetMusicVolume(evt.newValue);
            UpdateVolumeFill(musicSlider,evt.newValue);
        }
        private void OnSfxVolumeChanged(ChangeEvent<float> evt)
        {
            if(audioManager!=null && audioManager.IsMuted) audioManager.SetMuted(false);
            audioManager?.SetSfxVolume(evt.newValue);
            UpdateVolumeFill(sfxSlider,evt.newValue);
        }
        private static void InitializeVolumeFill(Slider slider)
        {
            var tracker=slider.Q(className:"unity-base-slider__tracker");
            if(tracker==null)return;
            var fill=new VisualElement{name="VolumeFill",pickingMode=PickingMode.Ignore};
            fill.AddToClassList("gh-volume-fill");
            tracker.Add(fill);
        }
        private static void UpdateVolumeFill(Slider slider,float value)
        {
            var fill=slider?.Q("VolumeFill");
            if(fill!=null)fill.style.width=Length.Percent(Mathf.InverseLerp(slider.lowValue,slider.highValue,value)*100f);
        }
        private void OnGeometryChanged(){diceSignature=null;battle.RefreshPresentation();}
        private void LateUpdate()
        {
            if(battle==null || root==null)return;
            // Keep the existing adapter alive for tutorial events, but hide its debug presentation.
            if(battle.Hud?.Document!=null) battle.Hud.Document.rootVisualElement.style.display=DisplayStyle.None;
            var state=battle.PresentationState;
            if(state==null)return;
            bool can=battle.PresentationCanInteract;
            string turnKey=ResolveTurnKey(battle.PresentationIsAnimating,battle.IsMatchEnded,state.CurrentPlayer==battle.LocalPlayer);
            SetText(turn,localization.T(turnKey));
            string roleA=localization.T(battle.LocalPlayer==PlayerId.A?"You":"Opponent");
            string roleB=localization.T(battle.LocalPlayer==PlayerId.B?"You":"Opponent");
            SetText(roleLabelA,roleA);SetText(roleLabelB,roleB);
            if (battle.DemoLevel != null) SetText(roleLabelA,localization.T("Arrived"));
            if (battle.DemoLevel == null || !battle.PresentationIsAnimating)
                SetText(scoreA,$"{state.GetBorneOff(PlayerId.A)} / {state.Rules.totalStonesPerPlayer}");
            SetText(scoreB,$"{state.GetBorneOff(PlayerId.B)} / {state.Rules.totalStonesPerPlayer}");
            scoreA.parent.tooltip=$"{localization.T("LEAF")} · {roleA}";
            scoreB.parent.tooltip=$"{localization.T("MOON")} · {roleB}";
            string hintKey=ResolveHintKey(battle.PresentationIsAnimating,can,battle.PresentationHasLegalMove,
                battle.PresentationSelectedDie.HasValue,battle.PresentationDice?.Count ?? 0);
            if (battle.DemoLevel != null && can)
                hintKey = string.IsNullOrEmpty(battle.DemoInputFeedback)
                    ? battle.PresentationSelectedDie.HasValue
                        ? state.Rules.soloTrailStepOfferMode == SoloTrailStepOfferMode.Sequential && battle.PresentationDice.Count == 1
                            ? "One more action. Choose a friend." : "Choose a friend."
                        : "Choose a step, then a friend." : battle.DemoInputFeedback;
            if (battle.IsPassingTrailTurn) hintKey = "No steps available. Starting a new turn.";
            SetText(hint,localization.T(hintKey));
            if (battle.DemoLevel != null)
                panel.EnableInClassList("has-input-feedback", !string.IsNullOrEmpty(battle.DemoInputFeedback));
            RefreshDemoControls(can && !paused);
            if (battle.DemoLevel != null)
            {
                string routeHero = previewHero;
                if (routeHero == null && battle.HoveredDemoCell.HasValue)
                    foreach (string id in battle.DemoLevel.heroIds)
                        if (battle.TryGetHero(id, out int hoveredCell, out _, out bool arrived) && !arrived &&
                            hoveredCell == battle.HoveredDemoCell.Value) { routeHero = id; break; }
                routeHero ??= battle.SelectedHeroId;
                if (string.IsNullOrEmpty(routeHero) || !battle.TryGetHero(routeHero, out _, out _, out bool routeExited) || routeExited)
                    foreach (string id in battle.DemoLevel.heroIds)
                        if (battle.TryGetHero(id, out _, out _, out bool arrived) && !arrived) { routeHero = id; break; }
                var cameraControl = board.GetComponent<DioramaCameraController>();
                if (can && !paused && cameraControl?.IsGestureActive != true && battle.DemoNarrative?.IsHelpVisible != true &&
                    routeHero != null && battle.TryGetHero(routeHero, out int routeCell, out _, out bool left) && !left)
                    battle.PreviewPresentationCell(board, routeCell, routeHero);
                else board.Preview(null, null, -1);
            }
            bool showReroll=can && battle.PresentationCanReroll;
            reroll.style.display=showReroll?DisplayStyle.Flex:DisplayStyle.None;
            string signature=can+":"+state.CurrentPlayer+":"+battle.PresentationSelectedDie+":";
            if (battle.DemoLevel != null) signature += paused + ":";
            if(battle.PresentationDice!=null)foreach(int value in battle.PresentationDice)signature+=value+",";
            if (battle.DemoLevel != null && battle.PresentationIsAnimating)
                foreach (var button in dice.Children()) button.SetEnabled(false);
            else if(signature!=diceSignature)
            {
                diceSignature=signature;dice.Clear();
                if(battle.PresentationDice!=null)for(int i=0;i<battle.PresentationDice.Count;i++)
                {
                    int index=i;
                    var button=new Button(()=>{battle.SelectPresentationDie(index);Diceforge.Audio.AudioManager.Instance?.PlayUiClick();})
                    {
                        text=battle.DemoLevel == null ? battle.PresentationDice[i].ToString()
                            : string.Format(localization.T("Step {0}"), battle.PresentationDice[i])
                    };
                    var marker=new VisualElement{pickingMode=PickingMode.Ignore};
                    marker.AddToClassList("token-marker");
                    AttachTeamIcon(marker,state.CurrentPlayer);
                    button.Add(marker);
                    bool available = can && (battle.DemoLevel == null || !paused);
                    button.AddToClassList("move-token");
                    button.EnableInClassList("selected",i==battle.PresentationSelectedDie);
                    button.EnableInClassList("available",available);
                    button.SetEnabled(available);
                    dice.Add(button);
                }
                ApplyDemoFont();
            }
            UpdateLayout();
            RefreshDemoBoardViewport();
        }
        private void RefreshDemoBoardViewport()
        {
            if (battle.DemoLevel == null || panel.ClassListContains("narrative-modal") || logicalWidth <= 0 || logicalHeight <= 0) return;
            var header = panel.Q(className: "hud-header");
            var surface = panel.Q("demoActionSurface");
            float top = header.worldBound.yMax + 12;
            float bottom = surface.worldBound.yMin - 12;
            if (bottom <= top) return;
            float left = panel.resolvedStyle.paddingLeft / logicalWidth;
            float right = panel.resolvedStyle.paddingRight / logicalWidth;
            board.SetDemoGameplayViewport(new Rect(left, 1 - bottom / logicalHeight,
                1 - left - right, (bottom - top) / logicalHeight));
        }
        private void UpdateLayout()
        {
            float nextWidth=root.resolvedStyle.width;
            float nextHeight=root.resolvedStyle.height;
            if(float.IsNaN(nextWidth) || float.IsNaN(nextHeight) || nextWidth<=0 || nextHeight<=0)return;
            Rect nextSafeArea=Screen.safeArea;
            if(width!=Screen.width || height!=Screen.height || logicalWidth!=nextWidth || logicalHeight!=nextHeight || safeArea!=nextSafeArea)
            {
                width=Screen.width;height=Screen.height;
                logicalWidth=nextWidth;logicalHeight=nextHeight;safeArea=nextSafeArea;
                panel.EnableInClassList("portrait",logicalHeight>logicalWidth);
                panel.EnableInClassList("compact",logicalWidth<900);
                panel.EnableInClassList("narrow",logicalWidth<600);
                float scaleX=logicalWidth/Mathf.Max(1,width);
                float scaleY=logicalHeight/Mathf.Max(1,height);
                panel.style.paddingLeft=safeArea.xMin*scaleX+12;panel.style.paddingRight=(width-safeArea.xMax)*scaleX+12;
                panel.style.paddingTop=(height-safeArea.yMax)*scaleY+12;panel.style.paddingBottom=safeArea.yMin*scaleY+12;
            }
        }
        private static void AttachTeamIcon(VisualElement element,PlayerId team)
        {
            if(element==null)return;
            element.pickingMode=PickingMode.Ignore;
            element.generateVisualContent+=context=>DrawTeamIcon(context,element.contentRect,team);
        }
        private static void DrawTeamIcon(MeshGenerationContext context,Rect rect,PlayerId team)
        {
            float size=Mathf.Min(rect.width,rect.height);
            if(size<1f)return;
            Vector2 origin=rect.center-Vector2.one*(size*0.5f);
            Vector2 Point(float x,float y)=>origin+new Vector2(x,y)*size;
            var painter=context.painter2D;
            painter.fillColor=team==PlayerId.A?new Color32(227,110,75,255):new Color32(113,199,189,255);
            painter.strokeColor=team==PlayerId.A?new Color32(255,211,142,255):new Color32(219,249,224,255);
            painter.lineWidth=Mathf.Max(1f,size*0.035f);
            painter.lineJoin=LineJoin.Round;
            painter.BeginPath();
            if(team==PlayerId.A)
            {
                painter.MoveTo(Point(0.19f,0.79f));
                painter.BezierCurveTo(Point(0.08f,0.41f),Point(0.37f,0.16f),Point(0.84f,0.09f));
                painter.BezierCurveTo(Point(0.86f,0.48f),Point(0.67f,0.87f),Point(0.19f,0.79f));
            }
            else
            {
                painter.MoveTo(Point(0.65f,0.1f));
                painter.BezierCurveTo(Point(0.18f,0.01f),Point(0.01f,0.63f),Point(0.4f,0.85f));
                painter.BezierCurveTo(Point(0.64f,0.99f),Point(0.89f,0.83f),Point(0.94f,0.64f));
                painter.BezierCurveTo(Point(0.46f,0.83f),Point(0.3f,0.34f),Point(0.65f,0.1f));
            }
            painter.ClosePath();painter.Fill();painter.Stroke();
            if(team!=PlayerId.A)return;
            painter.strokeColor=new Color32(115,54,37,255);
            painter.lineWidth=Mathf.Max(1f,size*0.045f);
            painter.lineCap=LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(Point(0.12f,0.94f));painter.LineTo(Point(0.66f,0.31f));
            painter.MoveTo(Point(0.39f,0.62f));painter.LineTo(Point(0.33f,0.39f));
            painter.MoveTo(Point(0.4f,0.62f));painter.LineTo(Point(0.65f,0.62f));
            painter.Stroke();
        }
        private static void SetText(Label label,string value)
        {
            if(label!=null && label.text!=value)label.text=value;
        }
        internal static string ResolveTurnKey(bool animating,bool ended,bool playersTurn)
        {
            if(animating)return "MOVING";
            if(ended)return "MATCH COMPLETE";
            return playersTurn?"YOUR TURN":"OPPONENT'S TURN";
        }
        internal static string ResolveHintKey(bool animating,bool canInteract,bool hasLegalMove,bool stepSelected,int stepsRemaining)
        {
            if(animating)return "Moving…";
            if(!canInteract)return "The forest awaits the next move…";
            if(!hasLegalMove || stepsRemaining==0)return "No move available. Wait for the next turn.";
            return !stepSelected && stepsRemaining>1?"Choose a step.":"Choose a goblin.";
        }
        private void SetPaused(bool value)
        {
            if(value && (battle==null || battle.IsMatchEnded))return;
            SetSurrenderConfirmation(false);
            if(paused==value)return;
            paused=value;
            if(value){previousTimeScale=Time.timeScale;Time.timeScale=0;}else Time.timeScale=previousTimeScale;
            menu.EnableInClassList("hidden",!value);
            if(value)
            {
                pauseMute?.SetValueWithoutNotify(audioManager!=null && audioManager.IsMuted);
                root.Q<Button>("resume")?.Focus();
            }
        }
        private void SetSurrenderConfirmation(bool visible)
        {
            if(visible && (!paused || battle==null || battle.IsMatchEnded))return;
            root.Q("confirmSurrender")?.EnableInClassList("hidden",!visible);
            root.Q("pauseMain")?.EnableInClassList("hidden",visible);
            if(visible)root.Q<Button>("cancelSurrender")?.Focus();
        }
        private void HandlePauseDismiss()
        {
            var confirmation=root.Q("confirmSurrender");
            if(confirmation!=null && !confirmation.ClassListContains("hidden"))
            {
                SetSurrenderConfirmation(false);
                root.Q<Button>("surrender")?.Focus();
                return;
            }
            SetPaused(false);
        }
        public static bool IsOverInterface(Vector2 screen)
        {
            foreach(var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var root=doc.rootVisualElement;
                if(root?.panel==null || root.resolvedStyle.display==DisplayStyle.None)continue;
                var p=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(screen.x,Screen.height-screen.y));
                var hit=root.panel.Pick(p);
                if(hit!=null && hit!=root && hit.pickingMode!=PickingMode.Ignore)return true;
            }
            return false;
        }
        private void OnDestroy()
        {
            if(paused)Time.timeScale=previousTimeScale;
            if(board!=null)board.GeometryChanged-=OnGeometryChanged;
            if(instance==this)instance=null;
            settingsDismiss?.Dispose();
            pauseDismiss?.Dispose();
            if(audioManager!=null)
            {
                audioManager.OnMuteChanged-=OnMuteChanged;
                audioManager.OnVolumesChanged-=OnVolumesChanged;
            }
            musicSlider?.UnregisterValueChangedCallback(OnMusicVolumeChanged);
            sfxSlider?.UnregisterValueChangedCallback(OnSfxVolumeChanged);
            localization?.Dispose();
            settingsLocalization?.Dispose();
            feedbackWindow?.Dispose();
            if(settingsObject!=null) Destroy(settingsObject);
        }
    }
}
