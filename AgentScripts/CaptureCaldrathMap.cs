// Temporary Play-mode visual fixture for the Caldrath map in the travel popups. Restart Play afterwards.
// Opens the Select Destination picker with the company at a North Kingdom town and lands on the board
// across the map, then (second call, with "travel" in the file's directory name or via the flag below)
// the Travel popup two stops down a five-stop road. Capture with `unity command capture_game_view --source screen`.
var board = UnityEngine.Object.FindFirstObjectByType<Board>();
var match = board.Match;
board.preview.Hide(); board.preview.gameObject.SetActive(false);
match.StopAllCoroutines(); match.enabled = false;
var cinematic = board.GetComponent<MatchCinematic>(); cinematic.StopAllCoroutines(); cinematic.Hide();
var rules = match.Rules;
var cards = System.Linq.Enumerable.ToList(CardCatalog.AllCards());
var lands = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(cards, c => c.GetCardType() == CardTypeEnum.Land));
var pcs = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(cards, c => c.GetCardType() == CardTypeEnum.PC && !string.IsNullOrWhiteSpace(c.region)));
var p = rules.Players[0];
p.Field.RemoveAll(u => u.Card.GetCardType() == CardTypeEnum.Land || u.Card.GetCardType() == CardTypeEnum.PC);
foreach (var name in new[] { "North Kingdom", "Thornhollow", "West Downs", "Elderforge", "Longwater", "Greenmarch", "Slave Fields", "Amber Sea" })
{
    var land = System.Linq.Enumerable.FirstOrDefault(lands, l => l.name == name);
    if (land != null) p.Field.Add(new MatchRules.Unit { Card = land, Owner = 0 });
}
var home = System.Linq.Enumerable.First(pcs, c => c.region == "North Kingdom");
p.Destination = new MatchRules.Unit { Card = home, Owner = 0, Secured = true };
p.Field.Add(p.Destination);
foreach (var pc in pcs) if (!p.Settlements.Contains(pc) && !p.Foreign.Contains(pc)) p.Foreign.Add(pc);
p.Travelled = false;
typeof(MatchRules).GetProperty("Active").SetValue(rules, 0);
bool travel = System.IO.File.Exists("Temp/caldrath_travel.flag");
if (!travel)
{
    typeof(MatchRules).GetProperty("Stage").SetValue(rules, MatchStage.Destination);
    board.GetComponent<TravelBanner>().Sync();
    var picker = board.GetComponent<DestinationPicker>();
    picker.Sync(rules.RankedDestinations(0));
    // Browse to a far town so the road shows several stops.
    var far = System.Linq.Enumerable.ToList(rules.RankedDestinations(0));
    int index = far.FindIndex(c => c.region == "Greenmarch");
    for (int i = 0; i < index; i++) picker.Browse(1);
    return "Picker open on " + picker.Shown.name + " (" + picker.Shown.region + "), " + far.Count + " choices.";
}
var target = System.Linq.Enumerable.First(pcs, c => c.region == "Greenmarch");
var journey = new MatchRules.Journey { Destination = target, Moving = true, Stop = 1 };
journey.Stops.AddRange(rules.Stops(0, target));
typeof(MatchRules).GetProperty("Travel").SetValue(rules, journey);
typeof(MatchRules).GetProperty("Stage").SetValue(rules, MatchStage.Travel);
board.GetComponent<DestinationPicker>().Close();
board.GetComponent<TravelBanner>().Sync();
return "Travel open: " + string.Join(" > ", journey.Stops) + " at stop " + (journey.Stop + 1);
