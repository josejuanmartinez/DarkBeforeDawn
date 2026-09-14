var m=UnityEngine.Object.FindFirstObjectByType<TowerMatchController>();
if(m==null || m.Busy || !m.CanInteract) throw new System.Exception("Wait for the opening and draw animations.");
var b=m.GetComponent<Board>(); m.enabled=false;
var r=new MatchRules();
CardData Card(string name,string type) => new CardData {name=name,type=type,attack=2,defense=3};
var l1=new MatchRules.Unit{Card=Card("First land","Land"),Owner=0}; l1.Card.goldGranted=5;
var l2=new MatchRules.Unit{Card=Card("Tapped friendly land","Land"),Owner=0};
var enemy=new MatchRules.Unit{Card=Card("Tapped enemy land","Land"),Owner=1,Tapped=true};
var veteran=new MatchRules.Unit{Card=Card("Veteran army","Army"),Owner=0};
var pc=Card("Test PC","PC");pc.region=l1.Card.name;
var beast=Card("New Beasts","Army");
r.Players[0].Field.AddRange(new[]{l1,l2,veteran}); r.Players[1].Field.Add(enemy);
r.Players[0].Hand.Add(beast); r.Players[0].Settlements.Add(pc); r.Begin(0); l2.Tapped=true;
typeof(TowerMatchController).GetProperty("Rules").SetValue(m,r);
var sync=typeof(TowerMatchController).GetMethod("Sync",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
void Sync()=>sync.Invoke(m,null);
void Check(bool ok,string message) {if(!ok)throw new System.Exception(message);}
BoardCardView View(CardData card)=>b.GetComponentsInChildren<BoardCardView>().First(v=>object.ReferenceEquals(v.Data,card));
void Tick()=> typeof(BoardCardView).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(View(pc),null);
System.Collections.IEnumerator Run() {
    Sync(); yield return null;
    Check(!b.transform.Find("Match stages/CONTINUE").gameObject.activeSelf,"Replenish showed Next Stage");
    m.Advance();Check(r.Stage==MatchStage.Draw,"Manual draw progression allowed");
    Check(m.AdvanceIfNoActions()&&r.Stage==MatchStage.Realm,"Replenish did not auto-progress");yield return null;
    Check(!View(beast).ActionHighlighted,"Realm highlights incorrect");
    Check(m.AdvanceIfNoActions()&&r.Stage==MatchStage.Destination,"Empty Realm did not auto-progress to Select Destination");yield return null;
    var picker=m.GetComponent<DestinationPicker>();
    Check(b.humanPopulationCenters.Count==0&&picker!=null&&picker.IsOpen&&picker.Shown==pc,"Destination picker did not open on the settlement");
    Check(b.transform.Find("Destination picker/Travel")!=null&&b.transform.Find("Destination picker/Previous")!=null,"Picker lacks travel/arrow controls");
    Check(m.Travel(pc)&&r.Stage==MatchStage.Travel,"Travel did not set out on the road");yield return null;
    Check(m.AdvanceIfNoActions()&&r.Stage==MatchStage.Muster&&r.Players[0].Destination!=null&&r.Players[0].Destination.Card==pc,"An unopposed road did not arrive at Muster");yield return null;
    Check(!picker.IsOpen,"Picker stayed open after travelling");
    var heading=(UnityEngine.UI.Text)typeof(TowerMatchController).GetField("headline",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(m);
    Check(heading.text.Contains("5 · MUSTER")&&!heading.text.Contains("GATHER MANA"),"HUD stage numbering wrong: "+heading.text);
    Check(b.humanPopulationCenters.Count==1&&View(pc)!=null,"Settlement zone must show only the destination");
    var v1=View(l1.Card);var v2=View(l2.Card);var ve=View(enemy.Card);var vv=View(veteran.Card);
    Check(v1.ActionHighlighted&&!v2.ActionHighlighted&&!ve.ActionHighlighted,"Mana highlighted wrong lands");
    var old2=v2.transform.localRotation;var oldEnemy=ve.transform.localRotation;
    Check(m.Tap(v1)&&l1.Tapped,"Click land failed");
    Check(View(l1.Card)==v1&&View(l2.Card)==v2&&View(enemy.Card)==ve,"Tap rebuilt token instances");
    Check(v2.transform.localRotation==old2&&ve.transform.localRotation==oldEnemy,"Tap restarted other lands' animation");
    yield return new UnityEngine.WaitForSecondsRealtime(.5f);
    Check(UnityEngine.Quaternion.Angle(v1.transform.localRotation,UnityEngine.Quaternion.Euler(0,0,-90))<2,"Selected land did not animate");
    var old1=v1.transform.localRotation;
    Check(!m.AdvanceIfNoActions()&&r.Stage==MatchStage.Muster,"Playable Muster auto-skipped");
    Check(v1==View(l1.Card)&&UnityEngine.Quaternion.Angle(v1.transform.localRotation,old1)<.01f,"Stage change restarted tap animation");
    Check(m.ActionLabel(View(pc))==null,"PC offered play during Muster");
    b.preview.Show(View(pc));Check(!b.preview.PreviewRect.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("CLICK TO PLAY")),"Preview offered to play an illegal card");b.preview.Hide();
    // Nothing ready and the town waiting to be entered: the champion may lead from off the field, but that
    // must not make an unaffordable hand card glow or offer PLAY CARD (it used to, through CanEnter(null)).
    var pricey=Card("Pricey Hero","Character");pricey.startingPC=pc.name;pricey.mithrilRequired=9;
    r.Players[0].Hand.Add(pricey);veteran.Tapped=true;Sync();yield return null;
    Check(r.NoneReady(0)&&r.NeedsEntering()&&r.CanEnter(null),"Champion-leads precondition not met");
    Check(!r.CanPlay(pricey)&&m.ActionLabel(View(pricey))==null&&!m.IsActionable(View(pricey)),"Unaffordable hand card offered PLAY CARD while the champion may lead");
    b.preview.Show(View(pricey));Check(!b.preview.PreviewRect.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("CLICK TO PLAY")),"Preview offered to play an unaffordable card");b.preview.Hide();
    Check(!m.Play(View(pricey)),"Unaffordable card was played");
    r.Players[0].Hand.Remove(pricey);veteran.Tapped=false;Sync();yield return null;
    Check(m.Play(View(beast)),"Army deployment failed");
    Check(vv==View(veteran.Card)&&ve==View(enemy.Card),"Deployment rebuilt existing board tokens");
    m.Advance();Check(r.Stage==MatchStage.Events,"Expected Events");
    Check(m.AdvanceIfNoActions()&&r.Stage==MatchStage.Spoils,"Empty Events did not auto-skip to Recover Objects");yield return null;
    var vb=View(beast);
    Check(!vb.ActionHighlighted&&r.Players[0].Field.First(u=>u.Card==beast).Tapped,"A recruit must enter tapped and unhighlighted");
    Check(m.InspectionHint(vb).Contains("Mustering"),"Recruit explanation missing");
    Check(m.AdvanceIfNoActions()&&r.Stage==MatchStage.Draw&&r.Active==1,"Empty spoils did not hand off");
    Check(l1.Tapped&&l2.Tapped&&!enemy.Tapped,"Turn change untapped wrong player");
    Check(v1==View(l1.Card)&&v2==View(l2.Card)&&ve==View(enemy.Card),"Turn change rebuilt lands");
    System.IO.File.WriteAllText("Temp/MatchFeedbackLiveResult.txt","PASS: automatic stages, destination choice on the board, highlights, illegal-button removal, click attack, fresh-unit explanation, stable token identities and isolated tap/untap animations.");
}
System.Collections.IEnumerator Guard() {
    var run=Run();while(true) {object current;try{if(!run.MoveNext())break;current=run.Current;}catch(System.Exception e){System.IO.File.WriteAllText("Temp/MatchFeedbackLiveResult.txt","FAIL: "+e);throw;}yield return current;}
}
System.IO.File.WriteAllText("Temp/MatchFeedbackLiveResult.txt","RUNNING");m.StartCoroutine(Guard());return "Live regression running; read Temp/MatchFeedbackLiveResult.txt.";
