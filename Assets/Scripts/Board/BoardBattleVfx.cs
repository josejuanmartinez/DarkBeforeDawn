using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Presentation-only combat effects. Tracks card identity through layout rebuilds and never intercepts input.</summary>
public sealed class BoardBattleVfx : MaskableGraphic
{
    public static readonly Color Amber=new(1,.73f,.27f);
    public static readonly Color Ember=new(1,.31f,.12f);
    public static readonly Color Ward=new(.56f,.9f,.55f);
    Board board;
    MatchRules observedRules;
    readonly List<BoardCardView> views=new();
    readonly Dictionary<CardData,Anchor> anchors=new();
    readonly Dictionary<CardData,int> commitments=new();
    readonly HashSet<MatchRules.Fight> seenFights=new();
    readonly List<Burst> bursts=new();
    readonly List<Bolt> bolts=new();
    readonly List<Floating> labels=new();
    readonly Vector3[] corners=new Vector3[4];
    readonly int[] life={-1,-1};
    CardData lastPending;
    MatchStage lastStage;
    bool stageKnown;
    CanvasGroup visibility;
    Canvas canvas;
    float stageFlare;
    const int MaxBursts=24, MaxBolts=24;
    struct Anchor { public Vector2 center,size; public float seen; }
    sealed class Burst { public Vector2 at; public Color tint; public float start,power; public bool cast; }
    sealed class Bolt { public Vector2 from,to; public Color tint; public float start; public bool hit; public string caption; }
    sealed class Floating { public Text text; public Vector2 at; public Color tint; public float start; }

    public static BoardBattleVfx For(Board source)
    {
        var found=source.GetComponentInChildren<BoardBattleVfx>(true);
        if(found!=null)return found;
        var go=new GameObject("Fantasy battle effects",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(BoardBattleVfx));
        go.transform.SetParent(source.transform,false);
        BoardPresentation.Stretch((RectTransform)go.transform,Vector2.zero,Vector2.one);
        var effect=go.GetComponent<BoardBattleVfx>();effect.board=source;effect.raycastTarget=false;
        effect.canvas=go.GetComponent<Canvas>();effect.canvas.overrideSorting=true;effect.canvas.sortingOrder=120;
        effect.canvas.sortingLayerID=source.GetComponentInParent<Canvas>().sortingLayerID;
        effect.visibility=go.GetComponent<CanvasGroup>();effect.visibility.blocksRaycasts=false;effect.visibility.interactable=false;
        return effect;
    }
    public void Register(BoardCardView view) { if(!views.Contains(view))views.Add(view); }
    public void Unregister(BoardCardView view) { views.Remove(view); }
    public Vector2 Position(RectTransform target) => rectTransform.InverseTransformPoint(target.TransformPoint(target.rect.center));
    public int Commitment(CardData card) => card!=null && commitments.TryGetValue(card,out int value) ? value : 0;
    public int ActiveBolts => bolts.Count;
    public int ActiveBursts => bursts.Count;
    public int CommittedCards => commitments.Count;
    protected override void OnEnable()
    {
        base.OnEnable();
        if(board==null)board=GetComponentInParent<Board>();
        canvas=GetComponent<Canvas>();visibility=GetComponent<CanvasGroup>();
        if(board!=null)
        {
            views.Clear();views.AddRange(board.GetComponentsInChildren<BoardCardView>(true));
        }
    }
    public void Selection(RectTransform card) => AddBurst(Position(card),Amber,.65f,true);
    public void Cast(Vector2 position) => AddBurst(position,Amber,1.2f,true);

    void Update()
    {
        if(board==null || board.Match?.Rules==null)return;
        var rules=board.Match.Rules;
        if(!ReferenceEquals(observedRules,rules))
        {
            observedRules=rules;seenFights.Clear();anchors.Clear();commitments.Clear();
            bursts.Clear();bolts.Clear();life[0]=life[1]=-1;stageKnown=false;lastPending=null;
        }
        for(int i=views.Count-1;i>=0;i--)
        {
            var view=views[i];if(view==null){views.RemoveAt(i);continue;}
            if(!view.gameObject.activeInHierarchy || view.Data==null)continue;
            view.Rect.GetWorldCorners(corners);
            var min=(Vector2)rectTransform.InverseTransformPoint(corners[0]);
            var max=(Vector2)rectTransform.InverseTransformPoint(corners[2]);
            anchors[view.Data]=new Anchor {center=Position(view.Rect),size=new Vector2(Mathf.Abs(max.x-min.x),Mathf.Abs(max.y-min.y)),seen=Time.unscaledTime};
        }
        // Keep recently removed cards briefly so a lethal hit still lands where its victim stood.
        if(anchors.Count>256)
        {
            var stale=new List<CardData>();
            foreach(var pair in anchors)if(Time.unscaledTime-pair.Value.seen>4)stale.Add(pair.Key);
            foreach(var card in stale)anchors.Remove(card);
        }
        commitments.Clear();
        foreach(var strike in rules.Attacks)
        {
            commitments[strike.Attacker.Card]=1;
            foreach(var defender in strike.Blockers)commitments[defender.Card]=2;
        }
        var pending=board.Match.PendingCombatCard;
        if(pending!=lastPending)
        {
            if(pending!=null && anchors.TryGetValue(pending,out var anchor))AddBurst(anchor.center,board.Match.ChoosingBlockTarget?Ward:Ember,.7f,true);
            lastPending=pending;
        }
        // Reference identity, not the list index: simultaneous duels all get their own impact.
        int next=0;
        foreach(var fight in rules.Fights)
        {
            if(!seenFights.Add(fight))continue;
            var source=fight.Loser==fight.Attacker ? fight.Defender : fight.Attacker;
            var victim=fight.Loser ?? fight.Defender;
            Vector2 from=Locate(source.Card,source.Owner),to=Locate(victim.Card,victim.Owner);
            if(bolts.Count>=MaxBolts)bolts.RemoveAt(0);
            bolts.Add(new Bolt {from=from,to=to,tint=fight.Loser==null?Amber:Ember,start=Time.unscaledTime+next++*.14f,
                caption=fight.Loser==null?"PARRIED":fight.Blow==Blow.Killed?"STRUCK DOWN":fight.Blow==Blow.Wounded?"WOUNDED":"STAGGERED"});
        }
        if(seenFights.Count>256){seenFights.Clear();foreach(var fight in rules.Fights)seenFights.Add(fight);}
        for(int owner=0;owner<2;owner++)
        {
            int current=rules.Players[owner].Life;
            if(life[owner]>=0 && current!=life[owner])
            {
                int difference=current-life[owner];var at=Champion(owner);
                AddBurst(at,difference<0?Ember:Ward,1.25f,false);
                Float(at,(difference>0?"+":"")+difference,difference<0?Ember:Ward);
            }
            life[owner]=current;
        }
        if(stageKnown && lastStage!=rules.Stage)stageFlare=Time.unscaledTime;
        lastStage=rules.Stage;stageKnown=true;
        for(int i=bolts.Count-1;i>=0;i--)
        {
            var bolt=bolts[i];float age=Time.unscaledTime-bolt.start;
            if(age>=.48f && !bolt.hit)
            { bolt.hit=true;AddBurst(bolt.to,bolt.tint,1.3f,false);Float(bolt.to,bolt.caption,bolt.tint); }
            if(age>1.3f)bolts.RemoveAt(i);
        }
        for(int i=bursts.Count-1;i>=0;i--)if(Time.unscaledTime-bursts[i].start>1.25f)bursts.RemoveAt(i);
        for(int i=labels.Count-1;i>=0;i--)
        {
            var label=labels[i];float age=(Time.unscaledTime-label.start)/1.6f;
            if(label.text==null || age>=1){if(label.text!=null)Destroy(label.text.gameObject);labels.RemoveAt(i);continue;}
            label.text.rectTransform.anchoredPosition=label.at+Vector2.up*(22+55*age);
            label.text.color=FantasyVfxMesh.Alpha(label.tint,Mathf.Min(age*10,1)*(1-Mathf.Pow(age,3)));
            label.text.transform.localScale=Vector3.one*(1+.18f*Mathf.Exp(-age*9)*Mathf.Sin(age*17));
        }
        var cinema=board.GetComponent<MatchCinematic>();
        visibility.alpha=cinema!=null && cinema.IsShowing ? 0 : 1;
        SetVerticesDirty();
    }

    Vector2 Champion(int owner)
    {
        var skin=BoardPresentation.SkinFor(board.transform);
        var bounds=owner==0?skin.players.humanAvatar:skin.players.opponentAvatar;
        var point=(bounds.min+bounds.max)*.5f;Rect r=rectTransform.rect;
        return new Vector2(r.xMin+point.x*r.width,r.yMin+point.y*r.height);
    }
    Vector2 Locate(CardData card,int owner) => card!=null && anchors.TryGetValue(card,out var anchor) ? anchor.center : Champion(owner);
    void AddBurst(Vector2 at,Color tint,float power,bool cast)
    {
        if(bursts.Count>=MaxBursts)bursts.RemoveAt(0);
        bursts.Add(new Burst {at=at,tint=tint,power=power,cast=cast,start=Time.unscaledTime});
    }
    void Float(Vector2 at,string caption,Color tint)
    {
        if(labels.Count>=16){Destroy(labels[0].text.gameObject);labels.RemoveAt(0);}
        var label=BoardPresentation.TextLabel(transform,caption,board.interfaceFont,caption.Length>5?19:34,tint,Vector2.one*.5f,Vector2.one*.5f,TextAnchor.MiddleCenter);
        label.name="Floating battle feedback";label.fontStyle=FontStyle.Bold;label.rectTransform.sizeDelta=new Vector2(240,44);
        // Keep labels in view even when their target is beside a screen edge.
        Rect r=rectTransform.rect;at.x=Mathf.Clamp(at.x,r.xMin+120,r.xMax-120);at.y=Mathf.Clamp(at.y,r.yMin+30,r.yMax-100);
        label.rectTransform.anchoredPosition=at;label.raycastTarget=false;
        var shadow=label.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.06f,.025f,.01f,.95f);shadow.effectDistance=new Vector2(2,-2);
        labels.Add(new Floating {text=label,at=at,tint=tint,start=Time.unscaledTime});
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(board?.Match?.Rules==null)return;
        var rules=board.Match.Rules;int lane=0;
        if(rules.Stage==MatchStage.Travel)
        foreach(var strike in rules.Attacks)
        {
            if(!anchors.TryGetValue(strike.Attacker.Card,out var attacker))continue;
            if(strike.Blockers.Count>0)
            {
                foreach(var blocker in strike.Blockers)
                    if(anchors.TryGetValue(blocker.Card,out var defender))Arrow(vh,defender.center,attacker.center,defender.size,attacker.size,Ward,lane++,false);
            }
            else
            {
                Vector2 end=Locate(strike.Target?.Card,rules.Active);
                Vector2 size=strike.Target!=null && anchors.TryGetValue(strike.Target.Card,out var target)?target.size:new Vector2(90,100);
                Arrow(vh,attacker.center,end,attacker.size,size,Ember,lane++,false);
            }
        }
        if(board.Match.PendingCombatCard!=null && anchors.TryGetValue(board.Match.PendingCombatCard,out var source) && Mouse.current!=null)
        {
            var root=canvas.rootCanvas;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,Mouse.current.position.ReadValue(),root.renderMode==RenderMode.ScreenSpaceOverlay?null:root.worldCamera,out var mouse))
            {
                mouse.x=Mathf.Clamp(mouse.x,rectTransform.rect.xMin+12,rectTransform.rect.xMax-12);
                mouse.y=Mathf.Clamp(mouse.y,rectTransform.rect.yMin+12,rectTransform.rect.yMax-12);
                var tint=board.Match.ChoosingBlockTarget?Ward:Amber;
                Arrow(vh,source.center,mouse,source.size,Vector2.zero,tint,lane,true);
                FantasyVfxMesh.Ring(vh,mouse,14+Mathf.Sin(Time.unscaledTime*4)*2,1.2f,tint,.65f,Time.unscaledTime, .7f);
            }
        }
        foreach(var bolt in bolts)
        {
            float age=Time.unscaledTime-bolt.start;if(age<0 || age>.52f)continue;
            float t=Mathf.Clamp01(age/.48f);var bend=(bolt.from+bolt.to)*.5f+Vector2.up*75;
            for(int segment=0;segment<12;segment++)
            {
                float s=Mathf.Max(0,t-segment*.022f),previous=Mathf.Max(0,s-.022f);
                FantasyVfxMesh.GlowLine(vh,FantasyVfxMesh.Curve(bolt.from,bend,bolt.to,previous),FantasyVfxMesh.Curve(bolt.from,bend,bolt.to,s),5*(1-segment/13f),bolt.tint,1-segment/13f);
            }
            FantasyVfxMesh.Spark(vh,FantasyVfxMesh.Curve(bolt.from,bend,bolt.to,t),8,bolt.tint,1);
        }
        foreach(var burst in bursts)DrawBurst(vh,burst);
        float stageAge=Time.unscaledTime-stageFlare;
        if(stageFlare>0 && stageAge<1)
        {
            Rect r=rectTransform.rect;float x=Mathf.Lerp(r.xMin,r.xMax,stageAge);
            FantasyVfxMesh.Spark(vh,new Vector2(x,r.yMax-r.height*.057f),5,Amber,Mathf.Sin(stageAge*Mathf.PI));
        }
    }

    static Vector2 Edge(Vector2 center,Vector2 toward,Vector2 size)
    {
        var direction=(toward-center).normalized;
        float dx=Mathf.Abs(direction.x)>.001f?size.x*.5f/Mathf.Abs(direction.x):float.MaxValue;
        float dy=Mathf.Abs(direction.y)>.001f?size.y*.5f/Mathf.Abs(direction.y):float.MaxValue;
        return center+direction*(Mathf.Min(dx,dy)+8);
    }
    void Arrow(VertexHelper vh,Vector2 from,Vector2 to,Vector2 fromSize,Vector2 toSize,Color tint,int lane,bool pending)
    {
        if(Vector2.Distance(from,to)<40)return;
        Vector2 start=Edge(from,to,fromSize),end=toSize==Vector2.zero?to:Edge(to,from,toSize);
        if(Vector2.Dot(end-start,to-from)<=0)return;
        Vector2 direction=(end-start).normalized;
        var bend=(start+end)*.5f+new Vector2(-direction.y,direction.x)*(36+lane%4*20);
        float pulse=.64f+.16f*Mathf.Sin(Time.unscaledTime*3+lane);
        for(int i=0;i<32;i++)
        {
            if(pending && i%4==0)continue;
            FantasyVfxMesh.GlowLine(vh,FantasyVfxMesh.Curve(start,bend,end,i/32f),FantasyVfxMesh.Curve(start,bend,end,(i+1)/32f),pending?1.8f:2.6f,tint,pulse);
        }
        var tip=(end-FantasyVfxMesh.Curve(start,bend,end,.94f)).normalized;
        var normal=new Vector2(-tip.y,tip.x);
        FantasyVfxMesh.GlowLine(vh,end-tip*15+normal*7,end,3,tint,.95f);
        FantasyVfxMesh.GlowLine(vh,end-tip*15-normal*7,end,3,tint,.95f);
        for(int i=0;i<3;i++)
        {
            float t=Mathf.Repeat(Time.unscaledTime*.45f+i/3f+lane*.11f,1);
            FantasyVfxMesh.Spark(vh,FantasyVfxMesh.Curve(start,bend,end,t),2.5f,tint,Mathf.Sin(t*Mathf.PI));
        }
    }
    void DrawBurst(VertexHelper vh,Burst burst)
    {
        float t=Mathf.Clamp01((Time.unscaledTime-burst.start)/1.25f),fade=(1-t)*(1-t);
        float spread=(1-Mathf.Pow(1-t,3))*70*burst.power;
        FantasyVfxMesh.Disc(vh,burst.at,(18+spread)*burst.power,FantasyVfxMesh.Alpha(burst.tint,fade*(burst.cast?.22f:.6f)));
        FantasyVfxMesh.Ring(vh,burst.at,12+spread,burst.cast?1.2f:2.2f,burst.tint,fade*.8f,t*2,burst.cast?.85f:1);
        if(burst.cast)FantasyVfxMesh.Ring(vh,burst.at,8+spread*.68f,1,Amber,fade*.65f,-t*3,.7f);
        else
        {
            float slash=50*burst.power*(1-Mathf.Pow(1-t,4));
            FantasyVfxMesh.GlowLine(vh,burst.at-new Vector2(slash,slash*.65f),burst.at+new Vector2(slash,slash*.65f),4,Amber,fade);
            FantasyVfxMesh.GlowLine(vh,burst.at-new Vector2(-slash*.6f,slash*.8f),burst.at+new Vector2(-slash*.6f,slash*.8f),2,burst.tint,fade);
        }
        for(int i=0;i<20;i++)
        {
            float angle=i*2.39996f;
            float distance=spread*(.5f+Mathf.Repeat(i*.713f,1));
            var p=burst.at+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*distance+Vector2.down*t*t*28;
            FantasyVfxMesh.Spark(vh,p,(1+Mathf.Repeat(i*.31f,1)*2)*(1-t),burst.tint,fade);
        }
    }
}
