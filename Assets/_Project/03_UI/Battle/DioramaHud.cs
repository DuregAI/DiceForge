using Diceforge.Core;
using Diceforge.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    public sealed class DioramaHud : MonoBehaviour
    {
        public static bool BlocksGameplay => instance != null && instance.paused;
        private static DioramaHud instance;
        private BattleDebugController battle;
        private DioramaBoard board;
        private GameObject settingsObject;
        private UIDocument document, settingsDocument;
        public UIDocument Document => document;
        private VisualElement root, panel, dice, menu, settingsPanel;
        private Label turn, scoreA, scoreB, hint;
        private Button reroll, settingsButton, settingsMute;
        private Toggle pauseMute;
        private Slider musicSlider, sfxSlider;
        private AudioManager audioManager;
        private Diceforge.UI.ModalDismiss settingsDismiss;
        private bool paused;
        private bool settingsOpenedFromPause;
        private float previousTimeScale;
        private string diceSignature;
        private MenuLocalization localization, settingsLocalization;
        private int width,height;
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
                if(settingsPanel!=null) settingsLocalization=new MenuLocalization(settingsRoot,null);
            }
            else Debug.LogError("[Settings] BattleSettingsPanel is missing.");
            localization=new MenuLocalization(root,()=>diceSignature=null);
            dice=root.Q("moves"); menu=root.Q("pauseOverlay");
            turn=root.Q<Label>("turn");scoreA=root.Q<Label>("scoreA");scoreB=root.Q<Label>("scoreB");hint=root.Q<Label>("hint");
            reroll=root.Q<Button>("reroll");reroll.clicked+=()=>battle.PresentationReroll();
            settingsButton=root.Q<Button>("settings");
            settingsButton.SetEnabled(settingsPanel!=null);
            if(settingsPanel!=null) InitializeSettings();
            root.Q<Button>("pauseSettings").SetEnabled(settingsPanel!=null);
            if(settingsPanel!=null) root.Q<Button>("pauseSettings").clicked+=OpenSettings;
            audioManager ??= AudioManager.Instance!=null?AudioManager.Instance:FindAnyObjectByType<AudioManager>();
            root.Q<Button>("pause").clicked+=()=>SetPaused(true);
            root.Q<Button>("resume").clicked+=()=>SetPaused(false);
            root.Q<Button>("surrender").clicked+=()=>root.Q("confirmSurrender").RemoveFromClassList("hidden");
            root.Q<Button>("cancelSurrender").clicked+=()=>root.Q("confirmSurrender").AddToClassList("hidden");
            root.Q<Button>("acceptSurrender").clicked+=()=>{SetPaused(false);battle.ConfirmSurrender();};
            var reduced=root.Q<Toggle>("reducedMotion");reduced.SetValueWithoutNotify(PlayerPrefs.GetInt("WoodlandReducedMotion",0)==1);
            DioramaBoard.ReducedMotion=reduced.value;
            reduced.RegisterValueChangedCallback(e=>{DioramaBoard.ReducedMotion=e.newValue;PlayerPrefs.SetInt("WoodlandReducedMotion",e.newValue?1:0);});
            pauseMute=root.Q<Toggle>("mute");
            pauseMute.SetValueWithoutNotify(audioManager!=null && audioManager.IsMuted);
            pauseMute.RegisterValueChangedCallback(e=>audioManager?.SetMuted(e.newValue));
            board.GeometryChanged+=OnGeometryChanged;
        }
        private void InitializeSettings()
        {
            settingsPanel.style.display=DisplayStyle.None;
            settingsPanel.Q(className:"gh-settings-actions").style.display=DisplayStyle.None;
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
            settingsOpenedFromPause=paused;
            SetPaused(true);
            menu.AddToClassList("hidden");
            settingsPanel.style.display=DisplayStyle.Flex;
            settingsPanel.AddToClassList("is-visible");
            settingsPanel.Q<Button>("btnCloseSettings")?.Focus();
        }
        private void CloseSettings()
        {
            settingsPanel.RemoveFromClassList("is-visible");
            settingsPanel.style.display=DisplayStyle.None;
            if(settingsOpenedFromPause) menu.RemoveFromClassList("hidden");
            else SetPaused(false);
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
            turn.text=localization.T(battle.IsMatchEnded?"MATCH COMPLETE":can?"YOUR TURN":"OPPONENT'S TURN");
            scoreA.text=$"{localization.T("LEAF")}  {state.GetBorneOff(PlayerId.A)} / {state.Rules.totalStonesPerPlayer}";
            scoreB.text=$"{localization.T("MOON")}  {state.GetBorneOff(PlayerId.B)} / {state.Rules.totalStonesPerPlayer}";
            hint.text=localization.T(can?"Choose a step. Choose a goblin.":"The forest awaits the next move…");
            bool showReroll=can && battle.PresentationCanReroll;
            reroll.style.display=showReroll?DisplayStyle.Flex:DisplayStyle.None;
            string signature=can+":"+battle.PresentationSelectedDie+":";
            if(battle.PresentationDice!=null)foreach(int value in battle.PresentationDice)signature+=value+",";
            if(signature!=diceSignature)
            {
                diceSignature=signature;dice.Clear();
                if(battle.PresentationDice!=null)for(int i=0;i<battle.PresentationDice.Count;i++)
                {
                    int index=i;
                    var button=new Button(()=>{battle.SelectPresentationDie(index);Diceforge.Audio.AudioManager.Instance?.PlayUiClick();}){text=battle.PresentationDice[i].ToString()};
                    button.AddToClassList("move-token");button.EnableInClassList("selected",i==battle.PresentationSelectedDie);button.SetEnabled(can);dice.Add(button);
                }
            }
            if(width!=Screen.width || height!=Screen.height)
            {
                width=Screen.width;height=Screen.height;panel.EnableInClassList("portrait",height>width);
                var bar=turn.parent;bar.Remove(turn);bar.Insert(height>width?0:1,turn);
                Rect safe=Screen.safeArea;
                float scale=root.resolvedStyle.width/Mathf.Max(1,width);
                panel.style.paddingLeft=safe.xMin*scale+12;panel.style.paddingRight=(width-safe.xMax)*scale+12;
                panel.style.paddingTop=(height-safe.yMax)*scale+12;panel.style.paddingBottom=safe.yMin*scale+12;
            }
        }
        private void SetPaused(bool value)
        {
            if(paused==value)return;
            paused=value;
            if(value){previousTimeScale=Time.timeScale;Time.timeScale=0;}else Time.timeScale=previousTimeScale;
            menu.EnableInClassList("hidden",!value);
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
            if(audioManager!=null)
            {
                audioManager.OnMuteChanged-=OnMuteChanged;
                audioManager.OnVolumesChanged-=OnVolumesChanged;
            }
            musicSlider?.UnregisterValueChangedCallback(OnMusicVolumeChanged);
            sfxSlider?.UnregisterValueChangedCallback(OnSfxVolumeChanged);
            localization?.Dispose();
            settingsLocalization?.Dispose();
            if(settingsObject!=null) Destroy(settingsObject);
        }
    }
}
