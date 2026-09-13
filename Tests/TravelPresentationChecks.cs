// Run after AgentScripts/PreviewBoardPresentation.cs. Restart Play after visual fixtures.
var board=UnityEngine.Object.FindFirstObjectByType<Board>();
var rules=board.Match.Rules;
var travel=board.GetComponent<TravelBanner>();
int checks=0;
void Check(bool ok,string message) { if(!ok) throw new System.Exception(message);checks++; }
Check(board.opponentLands.transform.parent.GetComponent<UnityEngine.CanvasGroup>().alpha==0,"Travel overlaps the original land labels.");
Check(board.GetComponentInChildren<AvatarHealthBar>()!=null,"Missing health.");
foreach(var owner in new[]{1,0})
{
    typeof(MatchRules).GetProperty("Active").SetValue(rules,owner);
    travel.Sync();
    var root=System.Linq.Enumerable.Last(board.GetComponentsInChildren<UnityEngine.RectTransform>(),t=>t.name=="Travel banner" && t.parent==board.transform);
    var labels=root.GetComponentsInChildren<UnityEngine.UI.Text>();
    Check(System.Linq.Enumerable.Any(labels,t=>t.text.Contains(owner==0?"YOUR COMPANY":"THE ENEMY COMPANY")),"Travel identifies the wrong company.");
    Check(root.Find("Road/Company/Travelling champion").GetComponent<UnityEngine.UI.Image>().sprite!=null,"Travelling champion is missing artwork.");
    Check(travel.IsTravelling,"Fresh travel should retain time for the arrival animation.");
    var panel=(UnityEngine.RectTransform)root;
    Check(panel.sizeDelta.x<=((UnityEngine.RectTransform)board.transform).rect.width*.72f,"Route exceeds the central board.");
}
travel.enabled=false;
var group=board.opponentLands.transform.parent.GetComponent<UnityEngine.CanvasGroup>();
Check(group==null||group.alpha==1,"Closing travel did not restore the land lane.");
travel.enabled=true;travel.Sync();
return checks+" travel presentation checks passed: both companies, artwork, travel timing, bounds and lane restoration.";
