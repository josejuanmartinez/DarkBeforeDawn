// Play-mode fixture (run after the opening; restart Play afterwards): Muster with untapped lands and a hand whose cards need them. Checks that a card the
// lands could pay for is actionable, that clicking it gathers the lands and plays it, and captures.
string stage = "start";
try {
var board = UnityEngine.Object.FindFirstObjectByType<Board>();
var match = board.Match; var rules = match.Rules;
board.preview.Hide();
match.StopAllCoroutines(); match.enabled = false;
var cinematic = board.GetComponent<MatchCinematic>(); cinematic.StopAllCoroutines(); cinematic.Hide();
board.GetComponent<DestinationPicker>().Close();
var all = System.Linq.Enumerable.ToList(CardCatalog.AllCards());
CardData Find(string name) => System.Linq.Enumerable.First(all, c => c.name == name).Clone();
var p = rules.Players[0];
p.Field.RemoveAll(u => u.Card.GetCardType() != CardTypeEnum.PC);
p.Hand.Clear(); p.Mana.Clear();
foreach (var name in new[] { "Thornhollow", "Hollowvale", "Longwater" }) p.Field.Add(new MatchRules.Unit { Card = Find(name), Owner = 0 });
var pcs = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(all, c => c.GetCardType() == CardTypeEnum.PC));
var delving = System.Linq.Enumerable.First(pcs, c => c.name == "Great Delving");
p.Field.Remove(p.Destination);
p.Destination = new MatchRules.Unit { Card = delving, Owner = 0, Secured = true, Tapped = true }; p.Field.Add(p.Destination);
var paldo = Find("Paldo Grubb"); var archers = Find("Halfling Archers"); var beasts = Find("Beasts");
var dear = Find("War Elephants");
p.Hand.AddRange(new[] { paldo, archers, beasts, dear });
void Set(string name, object value) => typeof(MatchRules).GetProperty(name).SetValue(rules, value);
Set("Active", 0); Set("Stage", MatchStage.Muster); Set("Travel", null);
typeof(TowerMatchController).GetProperty("Busy").SetValue(match, false);
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
typeof(TowerMatchController).GetMethod("Sync", flags).Invoke(match, null);
stage = "checks";
var report = new System.Text.StringBuilder();
foreach (var c in p.Hand) report.Append(c.name + ": " + (rules.PlayBlockReason(c, includeReadyMana: true) ?? "ok") + " | pool: " + (rules.PlayBlockReason(c) ?? "ok") + "\n");
string mode = System.IO.File.ReadAllText("Temp/fx_mode.txt").Trim();
if (mode == "play")
{
    var view = match.ViewOf(paldo);
    bool actionable = match.IsActionable(view); string label = match.ActionLabel(view);
    bool played = match.Play(view);
    report.Append("paldo actionable=" + actionable + " label=" + label + " played=" + played + " onField=" + p.Field.Any(u => u.Card == paldo) + " tapped=" + string.Join(",", p.Field.Where(u => u.Tapped && u.Card.GetCardType() == CardTypeEnum.Land).Select(u => u.Card.name)) + " message=" + rules.Message);
}
return report.ToString();
} catch (System.Exception e) { return "FAILED at " + stage + ": " + e; }
