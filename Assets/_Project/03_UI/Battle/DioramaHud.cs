using Diceforge.Core;
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
        private UIDocument document;
        public UIDocument Document => document;
        private VisualElement root, panel, dice, menu;
        private Label turn, scoreA, scoreB, hint;
        private Button reroll;
        private bool paused;
        private float previousTimeScale;
        private string diceSignature;
        private MenuLocalization localization;
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
            localization=new MenuLocalization(root,()=>diceSignature=null);
            dice=root.Q("moves"); menu=root.Q("pauseOverlay");
            turn=root.Q<Label>("turn");scoreA=root.Q<Label>("scoreA");scoreB=root.Q<Label>("scoreB");hint=root.Q<Label>("hint");
            reroll=root.Q<Button>("reroll");reroll.clicked+=()=>battle.PresentationReroll();
            root.Q<Button>("pause").clicked+=()=>SetPaused(true);
            root.Q<Button>("resume").clicked+=()=>SetPaused(false);
            root.Q<Button>("surrender").clicked+=()=>root.Q("confirmSurrender").RemoveFromClassList("hidden");
            root.Q<Button>("cancelSurrender").clicked+=()=>root.Q("confirmSurrender").AddToClassList("hidden");
            root.Q<Button>("acceptSurrender").clicked+=()=>{SetPaused(false);battle.ConfirmSurrender();};
            var reduced=root.Q<Toggle>("reducedMotion");reduced.SetValueWithoutNotify(PlayerPrefs.GetInt("WoodlandReducedMotion",0)==1);
            DioramaBoard.ReducedMotion=reduced.value;
            reduced.RegisterValueChangedCallback(e=>{DioramaBoard.ReducedMotion=e.newValue;PlayerPrefs.SetInt("WoodlandReducedMotion",e.newValue?1:0);});
            var muted=root.Q<Toggle>("mute");
            muted.SetValueWithoutNotify(Diceforge.Audio.AudioManager.Instance != null && Diceforge.Audio.AudioManager.Instance.IsMuted);
            muted.RegisterValueChangedCallback(e=>{if(Diceforge.Audio.AudioManager.Instance!=null)Diceforge.Audio.AudioManager.Instance.SetMuted(e.newValue);});
            board.GeometryChanged+=OnGeometryChanged;
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
            localization?.Dispose();
        }
    }
}
