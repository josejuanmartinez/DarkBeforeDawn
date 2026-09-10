var m=UnityEngine.Object.FindFirstObjectByType<TowerMatchController>();
if(m==null || !m.CanInteract) throw new System.Exception("Wait until the opening and draws finish.");
m.enabled=false;
var b=m.GetComponent<Board>();var chrome=b.GetComponent<BoardPresentation>();
var refresh=typeof(BoardPresentation).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
var sync=typeof(TowerMatchController).GetMethod("Sync",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
void Sync(){sync.Invoke(m,null);refresh.Invoke(chrome,null);}
void Check(bool condition,string message){if(!condition)throw new System.Exception(message);}
void CheckLabels(int[] own,int[] enemy){
    refresh.Invoke(chrome,null);
    for(int player=0;player<2;player++)for(int i=0;i<7;i++){
        var label=b.transform.Find((player==0?"Your materials/":"Opponent materials/")+PlayerMaterials.Names[i]+" amount").GetComponent<TMPro.TMP_Text>();
        int expected=(player==0?own:enemy)[i];
        Check(label.text.StartsWith(expected+" "),"Visible "+(player==0?"human ":"opponent ")+PlayerMaterials.Names[i]+" counter: expected "+expected+", found "+label.text);
    }
}
var r=new MatchRules();typeof(TowerMatchController).GetProperty("Rules").SetValue(m,r);
var land=new CardData{name="Counter test land",type="Land",leatherGranted=1,mountsGranted=2,timberGranted=3,ironGranted=4,steelGranted=5,mithrilGranted=6,goldGranted=7};
var ownUnit=new MatchRules.Unit{Card=land,Owner=0};var enemyUnit=new MatchRules.Unit{Card=land.Clone(),Owner=1};
r.Players[0].Field.Add(ownUnit);r.Players[1].Field.Add(enemyUnit);r.Begin(0);r.Next();r.Next();Sync();
var zeros=new int[7];CheckLabels(zeros,zeros);
var view=b.humanLands.GetComponentsInChildren<BoardCardView>().First(v=>object.ReferenceEquals(v.Data,land));
Check(b.TryTap(view),"Land tap rejected");CheckLabels(new[]{1,2,3,4,5,6,7},zeros);
Check(!b.TryTap(view),"Tapped twice");CheckLabels(new[]{1,2,3,4,5,6,7},zeros);
Sync();CheckLabels(new[]{1,2,3,4,5,6,7},zeros);
var army=new CardData{name="Counter cost test",type="Army",leatherRequired=1,mountsRequired=1,timberRequired=1,ironRequired=1,steelRequired=1,mithrilRequired=1,goldRequired=1};
r.Players[0].Hand.Add(army);Sync();Check(b.TryPlay(b.hand.GetComponentsInChildren<BoardCardView>().First(v=>object.ReferenceEquals(v.Data,army))),"Payment rejected");
CheckLabels(new[]{0,1,2,3,4,5,6},zeros);
while(r.Active==0)r.Next();Sync();CheckLabels(new[]{0,1,2,3,4,5,6},zeros);
r.Next();r.Next();Check(r.TapLand(enemyUnit),"Opponent tap rejected");Sync();CheckLabels(new[]{0,1,2,3,4,5,6},new[]{1,2,3,4,5,6,7});
typeof(TowerMatchController).GetProperty("Rules").SetValue(m,new MatchRules());Sync();CheckLabels(zeros,zeros);
return "PASS: all 14 visible counters, land grants, no double tap, stage persistence, paid costs, turn carryover, opponent grants and replacement match pools.";
