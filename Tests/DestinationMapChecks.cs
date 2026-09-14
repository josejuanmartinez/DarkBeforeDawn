// Live check for the Select Destination popup on the Caldrath map. Run in Play mode once the match has
// started; it stops the match coroutine like the visual fixtures, so restart Play afterwards.
var board=UnityEngine.Object.FindFirstObjectByType<Board>();
var match=board.Match;
board.preview.Hide();
match.StopAllCoroutines(); match.enabled=false;
var cinematic=board.GetComponent<MatchCinematic>(); cinematic.StopAllCoroutines(); cinematic.Hide();
var rules=match.Rules;
int checks=0;
void Check(bool ok,string message) { if(!ok) throw new System.Exception(message);checks++; }
var cards=System.Linq.Enumerable.ToList(CardCatalog.AllCards());
var lands=System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(cards,c=>c.GetCardType()==CardTypeEnum.Land));
var pcs=System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(cards,c=>c.GetCardType()==CardTypeEnum.PC && !string.IsNullOrWhiteSpace(c.region)));
var p=rules.Players[0];
p.Field.RemoveAll(u=>u.Card.GetCardType()==CardTypeEnum.Land || u.Card.GetCardType()==CardTypeEnum.PC);
foreach(var name in new[]{"North Kingdom","Longwater","Greenmarch"}) p.Field.Add(new MatchRules.Unit{Card=System.Linq.Enumerable.First(lands,l=>l.name==name),Owner=0});
var home=System.Linq.Enumerable.First(pcs,c=>c.region=="North Kingdom");
p.Destination=new MatchRules.Unit{Card=home,Owner=0,Secured=true}; p.Field.Add(p.Destination);
foreach(var pc in pcs) if(!p.Settlements.Contains(pc) && !p.Foreign.Contains(pc)) p.Foreign.Add(pc);
p.Travelled=false;
typeof(MatchRules).GetProperty("Active").SetValue(rules,0);
typeof(MatchRules).GetProperty("Stage").SetValue(rules,MatchStage.Destination);
board.GetComponent<TravelBanner>().Sync();
var picker=board.GetComponent<DestinationPicker>();
var choices=System.Linq.Enumerable.ToList(rules.RankedDestinations(0));
picker.Sync(choices);
Check(picker.IsOpen,"The picker did not open.");
UnityEngine.RectTransform Root() => System.Linq.Enumerable.Last(board.GetComponentsInChildren<UnityEngine.RectTransform>(),t=>t.name=="Destination picker" && t.parent==board.transform);
UnityEngine.Transform Overlay() => Root().Find("Map/Clip/Overlay");
int Count(string name) { int n=0; foreach(UnityEngine.Transform child in Overlay()) if(child.name==name) n++; return n; }
Check(Root().Find("Map/Clip/Caldrath").GetComponent<UnityEngine.UI.Image>().sprite==CaldrathMapView.Sheet,"The picker does not show the Caldrath map.");
Check(Count("Here")==1,"The company's own region should be marked once.");
// Browse to a far town: the road on the map has one stop per region entered.
int far=choices.FindIndex(c=>c.region=="Greenmarch");
Check(far>=0,"No Greenmarch town among the choices.");
while(choices.IndexOf(picker.Shown)<far) picker.Browse(1);
Check(picker.Shown.region=="Greenmarch","Browsing did not reach Greenmarch.");
var stops=rules.Stops(0,picker.Shown);
Check(Count("Stop")==stops.Count,"Expected "+stops.Count+" stops on the map, found "+Count("Stop")+".");
Check(Count("Road")==stops.Count,"Expected "+stops.Count+" road legs, found "+Count("Road")+".");
Check(System.Linq.Enumerable.Any(Root().GetComponentsInChildren<TMPro.TMP_Text>(),t=>t.text==picker.Shown.name.ToUpperInvariant()),"The destination is not named on the map.");
// Every other region on offer is a hotspot; clicking one browses to a town there.
var hotspot=System.Linq.Enumerable.First(Overlay().GetComponentsInChildren<UnityEngine.UI.Button>(),b=>b.name=="Choose Longwater");
hotspot.onClick.Invoke();
Check(picker.Shown.region=="Longwater","Clicking a region on the map did not browse to it.");
Check(Count("Stop")==rules.Stops(0,picker.Shown).Count,"The road was not redrawn for the new town.");
// Every region's painted terrain symbol in view carries an invisible mark the hover explains: region, ground, rule.
var marks=System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(Overlay().GetComponentsInChildren<UnityEngine.UI.Image>(),i=>i.name.StartsWith("Terrain ")));
Check(marks.Count>=10,"Expected terrain marks over the regions in view, found "+marks.Count+".");
Check(System.Linq.Enumerable.All(marks,m=>m.color.a==0 && !m.raycastTarget),"Terrain marks must be invisible and pass clicks through.");
var longwaterMark=System.Linq.Enumerable.FirstOrDefault(marks,m=>m.name=="Terrain Longwater");
Check(longwaterMark!=null,"Longwater has no terrain mark.");
var badgeList=(System.Collections.IList)typeof(CardKeywordHover).GetField("badges",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(Overlay().GetComponent<CardKeywordHover>());
Check(badgeList.Count==marks.Count,"Each terrain mark should be registered with the map's hover.");
var longwaterBadge=System.Linq.Enumerable.First(System.Linq.Enumerable.Cast<object>(badgeList),o=>((UnityEngine.RectTransform)o.GetType().GetField("Item1").GetValue(o))==longwaterMark.rectTransform);
string badgeTitle=(string)longwaterBadge.GetType().GetField("Item2").GetValue(longwaterBadge), badgeBody=(string)longwaterBadge.GetType().GetField("Item3").GetValue(longwaterBadge);
Check(badgeTitle.Contains("Longwater") && badgeTitle.Contains("Plains ground") && badgeBody.Contains("Plains"),"Longwater's terrain mark does not explain its ground: "+badgeTitle);
Check(CardKeywordGlossary.TryGet("ambush:Hills",out var ambushTitle,out var ambushBody) && ambushTitle.StartsWith("Ambush: Hills") && ambushTitle.Contains("<sprite name=\"hills\">") && ambushBody.Contains(CardKeywordGlossary.AmbushBody) && ambushBody.Contains("hillmen"),"The ambush link is not explained.");
Check(CardKeywordGlossary.TryGet("icon:swamp",out var iconTitle,out _) && iconTitle=="Marsh ground","The terrain glyph alone is not explained.");
// The map window stays inside the popup, and the popup inside the board.
var window=(UnityEngine.RectTransform)Root().Find("Map");
var panel=Root();
Check(UnityEngine.Mathf.Abs(window.anchoredPosition.x)+window.sizeDelta.x*.5f<=panel.sizeDelta.x*.5f,"The map spills past the popup's edge.");
Check(panel.sizeDelta.x<=((UnityEngine.RectTransform)board.transform).rect.width && panel.sizeDelta.y<=((UnityEngine.RectTransform)board.transform).rect.height,"The popup exceeds the board.");
// Staying put: the current town draws a single stop and no separate HERE caption.
int current=choices.IndexOf(home);
Check(current>=0,"The current town is missing from the choices.");
while(choices.IndexOf(picker.Shown)!=current) picker.Browse(current>choices.IndexOf(picker.Shown)?1:-1);
Check(Count("Stop")==1 && Count("Road")==0,"Staying should draw one stop and no road.");
Check(!System.Linq.Enumerable.Any(Root().GetComponentsInChildren<TMPro.TMP_Text>(),t=>t.text=="HERE"),"HERE should not double the current town's name.");
picker.Close();
Check(!picker.IsOpen,"The picker did not close.");
return checks+" destination map checks passed: the map, the company's region, stops and road legs, the named destination, map hotspots, hoverable terrain symbols, bounds, staying put and closing.";
