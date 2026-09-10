// Run using unity command eval_file --file Tests/PlayerMaterialsChecks.cs in Play mode.
var b = UnityEngine.Object.FindFirstObjectByType<Board>();
int checks = 0;
void Check(bool value, string message) { if (!value) throw new System.Exception(message); checks++; }
var purse = new PlayerMaterials();
var land = new CardData { name="Test land", type="Land", timberGranted=3, ironGranted=2 };
purse.Grant(land);
Check(!purse.TrySpend(new CardData {type="Army", timberRequired=2, ironRequired=3}), "Unaffordable payment accepted");
Check(purse[2]==3 && purse[3]==2, "Failed payment changed balance");
Check(purse.TrySpend(new CardData {type="Army", timberRequired=2, jokerRequired=2}), "Mixed generic payment rejected");
Check(purse[2]==0 && purse[3]==1, "Payment totals incorrect");
purse.Grant(new CardData {type="PC", goldGranted=10});
Check(purse[6]==0, "Settlement granted materials");
var own = new System.Collections.Generic.List<CardData>(b.humanLands.Cards);
var enemy = new System.Collections.Generic.List<CardData>(b.opponentLands.Cards);
var hand = new System.Collections.Generic.List<CardData>(b.hand.Cards);
var armies = new System.Collections.Generic.List<CardData>(b.humanArmies.Cards);
try {
 b.BeginTurn(false); b.BeginTurn(true);
 b.humanLands.SetCards(new[]{land,land}); b.opponentLands.SetCards(new[]{land.Clone()});
 var views=b.humanLands.GetComponentsInChildren<BoardCardView>();
 Check(b.TryTap(views[0]),"Land did not tap");
 Check(!b.TryTap(views[0]),"Land tapped twice");
 Check(b.OpponentMaterials[2]==0,"Own tapping changed opponent pool");
 b.humanLands.RefreshSkin(); views=b.humanLands.GetComponentsInChildren<BoardCardView>();
 Check(b.IsTapped(views[0]) && !b.IsTapped(views[1]),"Duplicate card tap state or rebuild broken");
 Check(b.TryTap(views[1]) && b.HumanMaterials[2]==6,"Second copy did not produce independently");
 Check(b.TryTap(b.opponentLands.GetComponentInChildren<BoardCardView>()) && b.OpponentMaterials[2]==3,"Opponent tapping failed");
 var expensive=new CardData {name="Expensive",type="Army",timberRequired=20};
 var cheap=new CardData {name="Affordable",type="Army",timberRequired=2,ironRequired=1};
 b.hand.SetCards(new[]{expensive,cheap});
 Check(!b.TryPlay(b.hand.GetComponentsInChildren<BoardCardView>()[0]) && b.hand.Count==2 && b.HumanMaterials[2]==6,"Rejected play mutated state");
 Check(b.TryPlay(b.hand.GetComponentsInChildren<BoardCardView>()[1]),"Affordable play failed");
 Check(b.hand.Count==1 && b.HumanMaterials[2]==4 && b.HumanMaterials[3]==3 && b.OpponentMaterials[2]==3,"Play accounting/ownership broken");
 Check(b.humanArmies.Cards[b.humanArmies.Count-1]==cheap,"Played card destination incorrect");
 b.BeginTurn(false);
 Check(b.HumanMaterials[2]==0 && b.OpponentMaterials[2]==0,"Turn did not empty pools");
 Check(!b.IsTapped(b.humanLands.GetComponentInChildren<BoardCardView>()) && b.IsTapped(b.opponentLands.GetComponentInChildren<BoardCardView>()),"Wrong player's lands readied");
 Check(b.GetComponentsInChildren<AvatarZoneVisualizer>().Length==2,"Missing avatars");
 foreach(var avatar in b.GetComponentsInChildren<AvatarZoneVisualizer>()) {
  Check(avatar.Count==1,"Missing avatar card");
  b.preview.Show(avatar.GetComponentInChildren<BoardCardView>());
  Check(b.preview.IsShowing,"Avatar inspection failed"); b.preview.Hide();
 }
} finally {
 b.humanLands.SetCards(own); b.opponentLands.SetCards(enemy); b.hand.SetCards(hand); b.humanArmies.SetCards(armies);
 b.BeginTurn(false); b.BeginTurn(true); b.preview.Hide();
}
return checks + " materials, tap, payment, turn and avatar assertions passed.";
