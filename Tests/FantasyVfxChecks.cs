// Run in Play mode after the opening. Uses a temporary battle; restart Play afterwards.
var board=UnityEngine.Object.FindFirstObjectByType<Board>();
var match=board.Match;
if(!UnityEngine.Application.isPlaying || board.GetComponent<MatchCinematic>()==null)throw new System.InvalidOperationException("Wait for the cinematic to initialize.");
match.StopAllCoroutines();match.enabled=false;
var cinematic=board.GetComponent<MatchCinematic>();cinematic.StopAllCoroutines();cinematic.Hide();
board.preview.Hide();
var rules=new MatchRules();rules.Begin(0);
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
typeof(TowerMatchController).GetField("board",flags).SetValue(match,board);
void Set(string name,object value)=>typeof(MatchRules).GetProperty(name).SetValue(rules,value);
Set("Stage",MatchStage.Travel);Set("Phase",TravelPhase.Defend);
var journey=new MatchRules.Journey();journey.Stops.Add("Golden Vale");Set("Travel",journey);
var roster=CardCatalog.AllCards().Where(c=>c.GetCardType()==CardTypeEnum.Character || c.GetCardType()==CardTypeEnum.Army).Take(6).Select(c=>c.Clone()).ToArray();
var attackers=new System.Collections.Generic.List<MatchRules.Unit>();var defenders=new System.Collections.Generic.List<MatchRules.Unit>();
for(int i=0;i<3;i++)
{
    var attacker=new MatchRules.Unit {Card=roster[i],Owner=1,Tapped=true};
    var defender=new MatchRules.Unit {Card=roster[i+3],Owner=0};
    rules.Players[1].Field.Add(attacker);rules.Players[0].Field.Add(defender);attackers.Add(attacker);defenders.Add(defender);
    var strike=new MatchRules.Strike {Attacker=attacker,Stats=(4,3)};
    if(i<2)strike.Blockers.Add(defender);
    rules.Attacks.Add(strike);
}
typeof(TowerMatchController).GetProperty("Rules").SetValue(match,rules);
typeof(TowerMatchController).GetProperty("Busy").SetValue(match,false);
try { typeof(TowerMatchController).GetMethod("Sync",flags).Invoke(match,null); }
catch(System.Reflection.TargetInvocationException e) { throw new System.Exception(e.InnerException.ToString()); }
var vfx=BoardBattleVfx.For(board);
int checks=0;
void Check(bool ok,string message){if(!ok)throw new System.Exception(message);checks++;}
System.Collections.IEnumerator Run()
{
    yield return new UnityEngine.WaitForSecondsRealtime(.4f);
    Check(board.GetComponentsInChildren<BoardBattleVfx>().Length==1,"Duplicate effects layers.");
    Check(!vfx.raycastTarget && !vfx.GetComponent<UnityEngine.CanvasGroup>().blocksRaycasts,"Effects block pointer input.");
    Check(vfx.CommittedCards==5,"Attack and defender commitments not observed.");
    foreach(var aura in board.GetComponentsInChildren<FantasyCardAura>())Check(!aura.raycastTarget,"Aura intercepts clicks.");
    var mesh=vfx.canvasRenderer.GetMesh();Check(mesh!=null && mesh.vertexCount>100,"Battle arrows did not render.");
    var before=mesh.vertices;yield return new UnityEngine.WaitForSecondsRealtime(.25f);mesh=vfx.canvasRenderer.GetMesh();
    Check(!before.SequenceEqual(mesh.vertices),"Battle arrows are static.");
    UnityEngine.ScreenCapture.CaptureScreenshot("Docs/Fantasy-Battle-Arrows.png");
    var victim=board.GetComponentsInChildren<BoardCardView>().First(v=>ReferenceEquals(v.Data,defenders[0].Card));
    board.preview.Show(victim);Check(board.preview.IsShowing,"Effects prevent inspection.");board.preview.Hide();
    for(int i=0;i<2;i++)rules.Fights.Add(new MatchRules.Fight {Attacker=attackers[i],Defender=defenders[i],Loser=defenders[i],Blow=Blow.Wounded,AttackerRoll=6,DefenderRoll=1,AttackerStats=(4,3),DefenderStats=(2,3)});
    rules.Players[0].Life-=4;
    yield return new UnityEngine.WaitForSecondsRealtime(.12f);
    Check(vfx.ActiveBolts==2,"Simultaneous duels did not each spawn a strike.");
    Check(vfx.ActiveBursts>0,"Damage did not spawn feedback.");
    Check(rules.Players[0].Life==16 && rules.Attacks.Count==3,"VFX mutated match rules.");
    yield return new UnityEngine.WaitForSecondsRealtime(.45f);
    UnityEngine.ScreenCapture.CaptureScreenshot("Docs/Fantasy-Battle-Impact.png");
    yield return new UnityEngine.WaitForSecondsRealtime(2);
    Check(vfx.ActiveBolts==0 && vfx.ActiveBursts==0,"Transient effects leaked or replayed the same fight.");
    Check(vfx.GetComponentsInChildren<UnityEngine.UI.Text>().Length==0,"Floating labels did not expire.");
    System.IO.File.WriteAllText("Temp/FantasyVfxResult.txt","PASS: "+checks+" checks; animated arrows, commitment colors, input transparency, inspection, simultaneous impacts, health feedback, rule immutability, expiry and no replay.");
}
System.Collections.IEnumerator Guard()
{
    var run=Run();while(true){object current;try{if(!run.MoveNext())break;current=run.Current;}catch(System.Exception e){System.IO.File.WriteAllText("Temp/FantasyVfxResult.txt","FAIL: "+e);throw;}yield return current;}
}
System.IO.File.WriteAllText("Temp/FantasyVfxResult.txt","RUNNING");vfx.StartCoroutine(Guard());
return "Visual regression running; read Temp/FantasyVfxResult.txt.";
