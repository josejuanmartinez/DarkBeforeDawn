// Run with: unity command eval_file --file Tests/ChampionChecks.cs
// With nothing ready the champion leads the company into town, and takes the dwellers' blows: on the
// field it is wounded, off it the company's life is.
void Check(bool ok, string message) { if (!ok) throw new System.Exception(message); }
CardData Card(string name, string type, int attack = 2, int defense = 2) => new CardData { name = name, type = type, attack = attack, defense = defense };
CardData Town(string name, int side, string dwellers) => new CardData { name = name, type = "PC", region = "Vale", settlementAlignment = side, dwellers = dwellers };
var garrison = Card("Garrison", "Army", 2, 2); var wardens = Card("Wardens", "Army", 3, 3);
MatchRules Fresh(CardData avatar)
{
    var rules = new MatchRules { Pick = n => 0, Roll = () => 3 };
    rules.ResolveDwellers = pc => pc.dwellers == "Garrison" ? garrison : pc.dwellers == "Wardens" ? wardens : null;
    rules.Players[0].Alignment = CardData.FreePeople; rules.Players[1].Alignment = CardData.DarkServants;
    rules.Players[0].Avatar = avatar;
    var home = Town("Home", CardData.FreePeople, ""); rules.Players[0].Settlements.Add(home); rules.StartAt(0, home);
    rules.Players[0].Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 0 });
    return rules;
}
// --- Nothing on the field at all: the champion (off the field) leads into a friendly town ---------------
var orren = Card("Orren", "Character", 3, 2); orren.cardId = 50001; orren.startingPC = "Home";
var r = Fresh(orren);
var friend = Card("Friend", "Character"); friend.startingPC = "Home"; r.Players[0].Hand.Add(friend);
r.Begin(0); r.Next(); r.Next(); r.Next();
Check(r.Stage == MatchStage.Travel, "Travel expected"); r.Next();
Check(r.Stage == MatchStage.Arrival && r.NoneReady(0) && r.NeedsEntering() && r.CanEnter(null) && !r.CanSecure(null), "With nothing ready the champion may still lead the company in");
Check(!r.CanPlay(friend) && r.Enter(null) && r.Players[0].Destination.Entered && r.Players[0].Destination.Garrison == null, "The champion leads the way in; nobody garrisons");
Check(!r.CanPlay(friend) && r.PlayBlockReason(friend).Contains("Muster"), "Arrival never hosts plays, even once entered");
r.Next(); Check(r.Stage == MatchStage.Muster && r.CanPlay(friend) && r.Play(friend), "The entered town must host plays in Muster");
// --- Neutral held town, nothing ready, champion off the field: a retention duel it wins ------------------
var r2 = Fresh(orren);
var cross = Town("Crossroads", CardData.NeutralAlignment, "Garrison"); r2.Players[0].Foreign.Add(cross);
var wanderer = Card("Wanderer", "Character"); wanderer.startingPC = "Crossroads"; r2.Players[0].Hand.Add(wanderer);
r2.Begin(0); r2.Next(); r2.Next(); Check(r2.ChooseDestination(cross), "Choice failed"); r2.Next(); r2.Next();
Check(r2.Stage == MatchStage.Arrival && r2.NeedsSecuring() && r2.CanSecure(null) && !r2.CanEnter(null), "The champion may face the dwellers when nothing is ready");
int life = r2.Players[0].Life;
Check(r2.Secure(null) && r2.Players[0].Destination.Entered && r2.Players[0].Life == life, "Orren 3+3 beats the garrison 2+3: the town opens and the champion walks in");
Check(r2.LastBattle.Led && r2.LastBattle.Clashes.Single().Attacker.Card == orren, "The battle must record the champion leading");
r2.Next(); Check(r2.Stage == MatchStage.Muster && r2.CanPlay(wanderer), "Plays must open in Muster after the champion enters");
// --- Hostile town, champion off the field, duel lost: the wound is the company's ---------------------------
var weak = Card("Orren", "Character", 1, 2); weak.cardId = 50001;
var r3 = Fresh(weak);
var hold = Town("Dark Hold", CardData.DarkServants, "Wardens"); r3.Players[0].Foreign.Add(hold);
var spy = Card("Spy", "Character"); spy.startingPC = "Dark Hold"; r3.Players[0].Hand.Add(spy);
r3.Begin(0); r3.Next(); r3.Next(); Check(r3.ChooseDestination(hold), "Choice failed"); r3.Next(); r3.Next();
life = r3.Players[0].Life;
Check(r3.Secure(null) && !r3.Players[0].Destination.Secured && r3.Players[0].Destination.Tapped, "Orren 1+3 loses to the wardens 3+3: the town closes");
Check(r3.Players[0].Life == life - 2, "The stand-in champion cannot be wounded: the company takes the margin (2): " + r3.Players[0].Life);
Check(!r3.CanSecure(null) && !r3.CanPlay(spy), "A closed town cannot be fought for again");
// --- Champion on the field but tapped: it leads, and it is the one wounded ------------------------------
var r4 = Fresh(weak);
var hold4 = Town("Dark Hold", CardData.DarkServants, "Wardens"); r4.Players[0].Foreign.Add(hold4);
var onField = new MatchRules.Unit { Card = weak.Clone(), Owner = 0, Tapped = true, EnteredTurn = -1 }; r4.Players[0].Field.Add(onField);
var spy4 = Card("Spy", "Character"); spy4.startingPC = "Dark Hold"; r4.Players[0].Hand.Add(spy4);
r4.Begin(0); onField.Tapped = true; r4.Next(); r4.Next(); Check(r4.ChooseDestination(hold4), "Choice failed"); r4.Next(); r4.Next();
Check(r4.NoneReady(0) && r4.Leader(0) == onField && r4.IsChampionUnit(onField), "The tapped champion on the field must be the leader");
life = r4.Players[0].Life;
Check(r4.Secure(null) && onField.Wounded && r4.Players[0].Life == life, "The tapped champion (0+3 vs 3+3, margin 3 over defense 1) is struck down and wounded; life untouched");
// --- With a ready unit the champion must not be asked to lead ------------------------------------------
var r5 = Fresh(orren);
var friend5 = Card("Friend", "Character"); friend5.startingPC = "Home"; r5.Players[0].Hand.Add(friend5);
var guard = new MatchRules.Unit { Card = Card("Guard", "Army"), Owner = 0, EnteredTurn = -1 }; r5.Players[0].Field.Add(guard);
r5.Begin(0); r5.Next(); r5.Next(); r5.Next(); r5.Next();
Check(r5.Stage == MatchStage.Arrival && !r5.CanEnter(null) && r5.EnterBlockReason(null).Contains("ready") && r5.CanEnter(guard), "A ready unit must be chosen when there is one");
// --- No champion known and nothing ready: entering is still possible, fighting is not --------------------
var r6 = Fresh(null);
var cross6 = Town("Crossroads", CardData.NeutralAlignment, "Garrison"); r6.Players[0].Foreign.Add(cross6);
var wanderer6 = Card("Wanderer", "Character"); wanderer6.startingPC = "Crossroads"; r6.Players[0].Hand.Add(wanderer6);
r6.Begin(0); r6.Next(); r6.Next(); Check(r6.ChooseDestination(cross6), "Choice failed"); r6.Next(); r6.Next();
Check(r6.Stage == MatchStage.Muster && !r6.CanSecure(null) && r6.SecureBlockReason(null).Contains("champion"), "Without a champion or a ready unit the held town cannot be fought for, so Arrival is skipped");
return "PASS: the champion leads with nothing ready, wins a retention duel off the field, loses a hostile one to the company's life, is wounded when on the field, yields to a ready unit, and is required for a fight.";
