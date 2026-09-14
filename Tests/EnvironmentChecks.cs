// Run with: unity command eval_file --file Tests/EnvironmentChecks.cs
// Which side an environmental card touches, read off its per-side text, and which field units it therefore affects.
void Check(bool ok, string message) { if (!ok) throw new System.Exception(message); }
CardData Env(string name, string text) => new CardData { name = name, type = "Environmental", actionEffect = text };
var dawn = Env("Dawn", "<sprite name=\"freePeople\"> gain Hope; armies +8% attack\n<sprite name=\"darkServants\"> gain Despair; armies -10% attack\n<sprite name=\"Neutral\"> unaffected");
Check(dawn.EnvironmentEffectFor(CardData.FreePeople) == "gain Hope; armies +8% attack", "Free People line wrong: " + dawn.EnvironmentEffectFor(CardData.FreePeople));
Check(dawn.EnvironmentEffectFor(CardData.DarkServants) == "gain Despair; armies -10% attack", "Dark Servants line wrong");
Check(dawn.EnvironmentEffectFor(CardData.NeutralAlignment) == null, "Unaffected must read as no effect");
var pass = Env("Redhorn Pass", "<sprite name=\"darkServants\"><sprite name=\"freePeople\"><sprite name=\"Neutral\"> mountain non-Dwarf 20% freeze");
Check(pass.EnvironmentEffectFor(CardData.FreePeople) != null && pass.EnvironmentEffectFor(CardData.DarkServants) != null && pass.EnvironmentEffectFor(CardData.NeutralAlignment) != null, "A line opened by every glyph touches every side");
var redSun = Env("Red Sun", "<sprite name=\"darkServants\"><sprite name=\"Neutral\"> Easterners gain Encouraged <sprite name=\"encouraged\">\n<sprite name=\"freePeople\"> unaffected");
Check(redSun.EnvironmentEffectFor(CardData.FreePeople) == null && redSun.EnvironmentEffectFor(CardData.DarkServants).StartsWith("Easterners"), "Red Sun sides wrong");
var elves = Env("Elves Departing", "<sprite name=\"freePeople\"> Elves lose Hope\n<sprite name=\"darkServants\"> unaffected\n<sprite name=\"Neutral\"> unaffected");
Check(elves.EnvironmentEffectFor(CardData.DarkServants) == null && elves.EnvironmentEffectFor(CardData.FreePeople) == "Elves lose Hope", "Explicit unaffected lines wrong");
Check(new CardData { name = "Sword", type = "Object", actionEffect = "<sprite name=\"freePeople\"> x" }.EnvironmentEffectFor(CardData.FreePeople) == null, "Only environments carry weather");
// On the board: the units of a named side carry the weather, the environment's own token counts as one, nothing else does.
var r = new MatchRules(); r.Players[0].Alignment = CardData.FreePeople; r.Players[1].Alignment = CardData.DarkServants;
var knight = new MatchRules.Unit { Card = new CardData { name = "Knight", type = "Character" }, Owner = 0 };
var orc = new MatchRules.Unit { Card = new CardData { name = "Orc", type = "Army" }, Owner = 1 };
var land = new MatchRules.Unit { Card = new CardData { name = "Vale", type = "Land" }, Owner = 0 };
var weather = new MatchRules.Unit { Card = redSun, Owner = 1 };
r.Players[0].Field.AddRange(new[] { knight, land }); r.Players[1].Field.AddRange(new[] { orc, weather });
Check(r.Environments().Single() == weather, "Environments must list the weather in play");
Check(!r.EnvironmentsAffecting(knight).Any() && r.EnvironmentsAffecting(orc).Single() == weather && !r.EnvironmentsAffecting(land).Any(), "Red Sun must touch the Dark Servants army only");
knight.Card.alignment = CardData.DarkServants;
Check(r.AlignmentOf(knight) == CardData.FreePeople && !r.EnvironmentsAffecting(knight).Any(), "A unit answers to its company's side, not the stamp on the card");
r.Players[0].Field.Add(new MatchRules.Unit { Card = dawn, Owner = 0 });
Check(r.EnvironmentsAffecting(orc).Count() == 2, "Every environment naming the side counts");
return "PASS: per-side environment text, unaffected lines, all-sides lines, and which field units the weather touches.";
