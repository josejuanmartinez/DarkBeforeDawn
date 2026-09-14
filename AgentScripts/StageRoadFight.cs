// Play-mode fixture (run after the opening; restart Play afterwards): the enemy company travels through
// Longwater (Plains) while the human raids, for the travel popup and the combat screen. Temp/fx_mode.txt: "raid" (attack phase, nothing committed), "committed" (two attacks declared,
// enemy defenders assigned), "combat" (resolve the committed attacks into the combat screen),
// "badge" (raid + virtual mouse over the ground badge), "defend" (human travels, enemy attacks, human assigns).
string stage = "start";
try {
var board = UnityEngine.Object.FindFirstObjectByType<Board>();
var match = board.Match; var rules = match.Rules;
board.preview.Hide();
match.StopAllCoroutines(); match.enabled = false;
var cinematic = board.GetComponent<MatchCinematic>(); cinematic.StopAllCoroutines(); cinematic.Hide();
board.GetComponent<DestinationPicker>().Close();
string mode = System.IO.File.ReadAllText("Temp/fx_mode.txt").Trim();
var all = System.Linq.Enumerable.ToList(CardCatalog.AllCards());
CardData Find(string name) => System.Linq.Enumerable.First(all, c => c.name == name).Clone();
var pcs = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(all, c => c.GetCardType() == CardTypeEnum.PC && !string.IsNullOrWhiteSpace(c.region)));
stage = "players";
int traveller = mode == "defend" ? 0 : 1, raider = 1 - traveller;
var t = rules.Players[traveller]; var r = rules.Players[raider];
t.Field.RemoveAll(u => u.Card.GetCardType() != CardTypeEnum.Land); r.Field.RemoveAll(u => u.Card.GetCardType() != CardTypeEnum.Land);
r.Hand.Clear(); t.Hand.Clear();
var home = System.Linq.Enumerable.First(pcs, c => c.region == "Grimhold");
t.Destination = new MatchRules.Unit { Card = home, Owner = traveller, Secured = true }; t.Field.Add(t.Destination);
var rhome = System.Linq.Enumerable.First(pcs, c => c.region == "Thornhollow");
r.Destination = new MatchRules.Unit { Card = rhome, Owner = raider, Secured = true }; r.Field.Add(r.Destination);
// The traveller's escort: a plains army and a character, and a hills army that cannot stand here.
foreach (var name in new[] { "Swordsmen", "Hillmen" }) t.Field.Add(new MatchRules.Unit { Card = Find(name), Owner = traveller, EnteredTurn = -1 });
var escort = System.Linq.Enumerable.First(all, c => c.GetCardType() == CardTypeEnum.Character && c.GetCombatStats().attack >= 3).Clone();
t.Field.Add(new MatchRules.Unit { Card = escort, Owner = traveller, EnteredTurn = -1 });
// The raider's field and hand.
foreach (var name in new[] { "Light Cavalry", "Archers", "Wolves" }) r.Field.Add(new MatchRules.Unit { Card = Find(name), Owner = raider, EnteredTurn = -1 });
foreach (var name in new[] { "Hounds", "Forest Spiders" }) r.Hand.Add(Find(name));
var hero = System.Linq.Enumerable.First(all, c => c.GetCardType() == CardTypeEnum.Character && c.name != escort.name && c.GetCombatStats().attack >= 2).Clone();
r.Hand.Add(hero);
stage = "journey";
var target = System.Linq.Enumerable.First(pcs, c => c.region == "Goldenwood");
var journey = new MatchRules.Journey { Destination = target, Moving = true, Stop = 1 };
journey.Stops.AddRange(new[] { "The Palewall", "Longwater", "Goldenwood" });
void Set(string name, object value) => typeof(MatchRules).GetProperty(name).SetValue(rules, value);
Set("Active", traveller); Set("Travel", journey); Set("Stage", MatchStage.Travel); Set("Phase", TravelPhase.Attack);
rules.Attacks.Clear();
typeof(TowerMatchController).GetProperty("Busy").SetValue(match, false);
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
string result = "raid staged";
if (mode == "committed" || mode == "combat" || mode == "defend")
{
    stage = "attacks";
    var cav = System.Linq.Enumerable.First(r.Field, u => u.Card.name == "Light Cavalry");
    var arch = System.Linq.Enumerable.First(r.Field, u => u.Card.name == "Archers");
    if (!rules.Attack(cav)) throw new System.Exception("cav " + rules.Message);
    if (!rules.Attack(arch)) throw new System.Exception("arch " + rules.Message);
    if (!rules.AttackWith(hero)) throw new System.Exception("hero " + rules.Message);
    rules.Next(); // to Defend
    var sword = System.Linq.Enumerable.First(t.Field, u => u.Card.name == "Swordsmen");
    var esc = System.Linq.Enumerable.First(t.Field, u => u.Card == escort);
    if (mode != "defend")
    {
        if (!rules.Block(sword, rules.Attacks[0])) throw new System.Exception("block " + rules.Message);
        if (!rules.Block(esc, rules.Attacks[2], true)) throw new System.Exception("block2 " + rules.Message);
    }
    result = mode + " staged: " + rules.Attacks.Count + " attacks";
    if (mode == "combat") { rules.Next(); result = "combat: " + rules.LastBattle?.Clashes.Count + " clashes, " + rules.Message; }
}
stage = "sync";
typeof(TowerMatchController).GetMethod("Sync", flags).Invoke(match, null);
board.GetComponent<TravelBanner>().Sync();
if (mode == "badge")
{
    stage = "badge";
    var banner = board.GetComponent<TravelBanner>();
    UnityEngine.RectTransform badge = null;
    foreach (var img in board.GetComponentsInChildren<UnityEngine.UI.Image>()) if (img.name == "Terrain" && img.transform.IsChildOf(banner.transform)) badge = img.rectTransform;
    if (badge == null) foreach (var img in board.GetComponentsInChildren<UnityEngine.UI.Image>()) if (img.name == "Terrain") badge = img.rectTransform;
    var world = badge.TransformPoint(badge.rect.center);
    var screen = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, world);
    System.Collections.IEnumerator Drive(UnityEngine.Vector2 at)
    {
        while (true) { UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new UnityEngine.InputSystem.LowLevel.MouseState { position = at }); yield return null; }
    }
    board.GetComponent<DestinationPicker>().StopAllCoroutines();
    board.GetComponent<DestinationPicker>().StartCoroutine(Drive(screen));
    result += " badge at " + screen;
}
return result + " | " + rules.Message;
} catch (System.Exception e) { return "FAILED at " + stage + ": " + e; }
