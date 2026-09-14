// Run after AgentScripts/PreviewBoardPresentation.cs. Restart Play after visual fixtures.
var board=UnityEngine.Object.FindFirstObjectByType<Board>();
var rules=board.Match.Rules;
var travel=board.GetComponent<TravelBanner>();
int checks=0;
void Check(bool ok,string message) { if(!ok) throw new System.Exception(message);checks++; }
Check(board.GetComponentInChildren<AvatarHealthBar>()!=null,"Missing health.");
Check(CaldrathMapView.Sheet!=null,"The Caldrath map (Resources/Maps/Caldrath) did not load.");
foreach(var owner in new[]{1,0})
{
    typeof(MatchRules).GetProperty("Active").SetValue(rules,owner);
    travel.Sync();
    var root=System.Linq.Enumerable.Last(board.GetComponentsInChildren<UnityEngine.RectTransform>(),t=>t.name=="Travel popup" && t.parent==board.transform);
    var labels=root.GetComponentsInChildren<UnityEngine.UI.Text>();
    Check(System.Linq.Enumerable.Any(labels,t=>t.text.Contains(owner==0?"YOUR COMPANY":"THE ENEMY COMPANY")),"Travel identifies the wrong company.");
    // The road is walked on the map: the picture, one stop per region entered, and the company marker over it.
    var road=root.Find("Road");
    Check(road!=null && road.Find("Clip/Caldrath").GetComponent<UnityEngine.UI.Image>().sprite==CaldrathMapView.Sheet,"The travel popup does not show the Caldrath map.");
    var overlay=road.Find("Clip/Overlay");
    int stops=0; foreach(UnityEngine.Transform child in overlay) if(child.name=="Stop") stops++;
    Check(stops==rules.Travel.Stops.Count,"Expected "+rules.Travel.Stops.Count+" stops on the map, found "+stops+".");
    Check(road.Find("Company/Travelling champion").GetComponent<UnityEngine.UI.Image>().sprite!=null,"Travelling champion is missing artwork.");
    var marker=(UnityEngine.RectTransform)road.Find("Company");
    var window=((UnityEngine.RectTransform)road).rect;
    Check(UnityEngine.Mathf.Abs(marker.anchoredPosition.x)<=window.width*.5f && UnityEngine.Mathf.Abs(marker.anchoredPosition.y)<=window.height*.5f,"The company marker stands outside the map window.");
    Check(travel.IsTravelling,"Fresh travel should retain time for the arrival animation.");
    var panel=(UnityEngine.RectTransform)root;
    Check(panel.sizeDelta.x<=((UnityEngine.RectTransform)board.transform).rect.width*.72f,"Route exceeds the central board.");
}
// A route the map does not know still lays out a road to walk.
var unknown=new MatchRules.Journey { Destination=rules.Travel.Destination, Moving=true, Stop=0 };
unknown.Stops.Add("Nowhere"); unknown.Stops.Add("Elsewhere");
var known=rules.Travel;
typeof(MatchRules).GetProperty("Travel").SetValue(rules,unknown);
travel.Sync();
var blind=System.Linq.Enumerable.Last(board.GetComponentsInChildren<UnityEngine.RectTransform>(),t=>t.name=="Travel popup" && t.parent==board.transform);
int blindStops=0; foreach(UnityEngine.Transform child in blind.Find("Road/Clip/Overlay")) if(child.name=="Stop") blindStops++;
Check(blindStops==2,"Unknown regions should still be drawn as stops, found "+blindStops+".");
typeof(MatchRules).GetProperty("Travel").SetValue(rules,known);
travel.enabled=false;
travel.enabled=true;travel.Sync();
return checks+" travel presentation checks passed: both companies, the map and its stops, the marker, artwork, travel timing, bounds and unknown regions.";
