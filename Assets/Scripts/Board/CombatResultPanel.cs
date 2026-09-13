using UnityEngine;
using UnityEngine.UI;

/// <summary>Turns the latest resolved duel into a readable, timed scorecard over the world lane.</summary>
public sealed class CombatResultPanel : MonoBehaviour
{
    Board board;
    MatchRules.Fight last;
    GameObject panel;
    CanvasGroup fade;
    float shownAt;
    CanvasGroup world;
    float worldAlpha;
    bool worldBlocks, ownWorldGroup;
    public void Initialize(Board source) => board = source;
    void Update()
    {
        var fights = board?.Match?.Rules?.Fights;
        if (fights != null && fights.Count > 0 && !ReferenceEquals(last,fights[fights.Count-1]))
        {
            last = fights[fights.Count-1]; Show(last, fights.Count);
        }
        if (fade != null)
        {
            float target = Time.unscaledTime-shownAt < 7 ? 1 : 0;
            fade.alpha = Mathf.MoveTowards(fade.alpha,target,Time.unscaledDeltaTime*4);
            if (target == 0 && fade.alpha == 0) { Destroy(panel); panel = null; RestoreWorld(); }
        }
    }
    void Show(MatchRules.Fight fight, int count)
    {
        if (panel != null) { panel.SetActive(false); Destroy(panel); }
        var skin = BoardPresentation.SkinFor(transform);
        if (world == null && board.environmental != null)
        {
            world = board.environmental.transform.parent.GetComponent<CanvasGroup>(); ownWorldGroup = world == null;
            if(ownWorldGroup) world = board.environmental.transform.parent.gameObject.AddComponent<CanvasGroup>();
            worldAlpha=world.alpha;worldBlocks=world.blocksRaycasts;world.alpha=0;world.blocksRaycasts=false;
        }
        var plate = BoardPresentation.Panel(transform,"Combat result",skin.colors.ink);
        panel = plate.gameObject;
        BoardPresentation.Stretch(plate.rectTransform,new Vector2(.153f,.572f),new Vector2(.863f,.67f));
        BoardSurface.Dress(plate,skin.colors.gold,true,true);
        fade = panel.AddComponent<CanvasGroup>(); fade.alpha=0; fade.blocksRaycasts=false; shownAt=Time.unscaledTime;
        Text Label(string text,int size,Color color,Vector2 min,Vector2 max)
        {
            var t=BoardPresentation.TextLabel(plate.transform,text,board.interfaceFont,size,color,min,max,TextAnchor.MiddleCenter);
            t.resizeTextForBestFit=true;t.resizeTextMinSize=9;t.resizeTextMaxSize=size;return t;
        }
        void Side(MatchRules.Unit unit,int roll,int attack,int total,float x)
        {
            var accent=unit.Owner==0?skin.colors.teal:skin.colors.gold;
            var portrait=BoardPresentation.Panel(plate.transform,"Combatant",Color.white);
            BoardPresentation.Stretch(portrait.rectTransform,new Vector2(x,.14f),new Vector2(x+.055f,.86f));
            portrait.sprite=TravelBanner.Artwork(unit.Card);portrait.preserveAspect=true;
            Label(unit.Card.name,14,skin.colors.ivory,new Vector2(x+.06f,.65f),new Vector2(x+.28f,.92f));
            Label(attack+" ATTACK  +  "+roll+" ROLL",12,skin.colors.muted,new Vector2(x+.06f,.14f),new Vector2(x+.28f,.42f));
            Label(total.ToString(),28,accent,new Vector2(x+.06f,.35f),new Vector2(x+.28f,.72f));
        }
        Side(fight.Attacker,fight.AttackerRoll,fight.AttackerStats.attack,fight.AttackerTotal,.02f);
        Side(fight.Defender,fight.DefenderRoll,fight.DefenderStats.attack,fight.DefenderTotal,.70f);
        string outcome=fight.Loser==null?"STANDOFF":fight.Blow==Blow.Killed?(fight.Loser.IsCharacter?"STRUCK DOWN":"DEFEATED"):fight.Blow==Blow.Wounded?"WOUNDED":"TAPPED";
        Label(count>1?"LAST CLASH  /  "+count+" DUELS":"CLASH RESOLVED",10,skin.colors.gold,new Vector2(.31f,.72f),new Vector2(.69f,.93f));
        Label(outcome,21,skin.colors.ivory,new Vector2(.31f,.37f),new Vector2(.69f,.76f));
        Label(fight.Loser==null?"Neither side yields":fight.Loser.Card.name+"  ·  "+fight.Margin+" damage",12,skin.colors.muted,new Vector2(.31f,.12f),new Vector2(.69f,.38f));
        panel.transform.SetAsLastSibling();
    }
    void RestoreWorld()
    {
        if(world!=null) { world.alpha=worldAlpha;world.blocksRaycasts=worldBlocks;world=null; }
    }
    void OnDisable() { RestoreWorld();if(panel!=null) Destroy(panel);panel=null; }
}
