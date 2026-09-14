// Run after the opening cinematic in Play mode.
var b=UnityEngine.Object.FindFirstObjectByType<Board>();
int checks=0;
void Check(bool value,string message) { if(!value) throw new System.Exception(message); checks++; }
Check(BoardSkin.Default.openTable,"Open table skin is not active.");
foreach(var zone in b.GetComponentsInChildren<CardZoneVisualizer>())
{
    var surface=zone.transform.parent.Find("Zone surface");
    if(surface!=null) Check(surface.GetComponentInChildren<BoardSurface>()==null,"A boxed lane survived.");
}
var inlay=b.transform.Find("Battlefield inlay").GetComponent<TableInlay>();
Check(inlay.GetComponent<UnityEngine.CanvasRenderer>()!=null && !inlay.raycastTarget,"Inlay cannot render or blocks input.");
Check(inlay.transform.GetSiblingIndex()<b.hand.transform.parent.GetSiblingIndex(),"Inlay covers the cards.");
var hand=b.hand.GetComponentsInChildren<BoardCardView>();
Check(hand.Length>=2,"Wait for the hand to be dealt.");
b.hand.Arrange();
Check(hand[0].Rect.localEulerAngles.z>0 && hand[0].Rect.localEulerAngles.z<20,"Hand does not fan left.");
Check(hand[hand.Length-1].Rect.localEulerAngles.z>340,"Hand does not fan right.");
var next=b.transform.Find("CONTINUE") as UnityEngine.RectTransform;
Check(next!=null && next.anchorMax.y<.15f,"Turn action did not move to the player station.");
var destination=b.humanPopulationCenters.GetComponentInChildren<BoardCardView>();
Check(destination!=null && destination.GetComponentInChildren<Card>()!=null,"Destination is still a square token.");
b.preview.Show(destination);
Check(b.preview.IsShowing,"Destination inspection failed.");
b.preview.Hide();
foreach(var pile in new []{b.humanDiscard,b.opponentDiscard,b.humanVictoryPoints,b.opponentVictoryPoints})
    if(pile.Count==0) Check(!pile.GetComponentsInChildren<UnityEngine.UI.Image>().Any(x=>x.name=="Empty pile"),"An empty dashboard placeholder survived.");
return checks+" open table structure and interaction checks passed.";
