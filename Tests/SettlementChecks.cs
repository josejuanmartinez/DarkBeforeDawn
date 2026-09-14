// Run with: unity command eval_file --file Tests/SettlementChecks.cs
// Settlement standing, dwellers fights, ambushes from the hand, two companies in one town, wounds
// and healing, the tapped -1/-1, journeys with their typed draws, the end-of-turn discard, and the map.
void Check(bool ok, string message) { if (!ok) throw new System.Exception(message); }
CardData Card(string name, string type, int attack = 2, int defense = 2) => new CardData { name = name, type = type, attack = attack, defense = defense };
CardData Town(string name, string region, int side, string dwellers) => new CardData { name = name, type = "PC", region = region, settlementAlignment = side, dwellers = dwellers };
var garrison = Card("Garrison", "Army", 2, 2); var wardens = Card("Wardens", "Army", 3, 3);
MatchRules Fresh()
{
    var rules = new MatchRules { Pick = n => 0, Roll = () => 3 }; // every die shows 3: duels go by printed attack
    rules.ResolveDwellers = pc => pc.dwellers == "Garrison" ? garrison : pc.dwellers == "Wardens" ? wardens : null;
    rules.Players[0].Alignment = CardData.FreePeople; rules.Players[1].Alignment = CardData.DarkServants;
    return rules;
}
// --- Standing -------------------------------------------------------------------------------------
var r = Fresh();
var home = Town("Home", "Vale", CardData.DarkServants, "Garrison"); r.Players[0].Settlements.Add(home);
var friendly = Town("Friendly", "Vale", CardData.FreePeople, "Garrison");
var neutral = Town("Crossroads", "Vale", CardData.NeutralAlignment, "Garrison");
var hostile = Town("Dark Hold", "Vale", CardData.DarkServants, "Wardens");
var unguarded = Town("Ruin", "Vale", CardData.DarkServants, "");
r.Players[0].Foreign.AddRange(new[] { friendly, neutral, hostile, unguarded });
Check(r.StandingAt(0, home) == Standing.Friendly, "A settlement of your own deck is safe whatever its side");
Check(r.StandingAt(0, friendly) == Standing.Friendly && r.StandingAt(0, neutral) == Standing.Neutral && r.StandingAt(0, hostile) == Standing.Hostile, "Standing by alignment wrong");
Check(r.StandingAt(1, hostile) == Standing.Friendly && r.StandingAt(1, friendly) == Standing.Hostile, "Standing must follow the player's own side");
Check(r.Dwellers(hostile) == wardens && r.Dwellers(unguarded) == null, "Dwellers resolution wrong");
// --- Start, land, choices ----------------------------------------------------------------------------
var vale = Card("Vale", "Land");
r.Players[0].Hand.Add(vale);
r.StartAt(0, home);
Check(r.Players[0].Destination != null && r.Players[0].Destination.Card == home && r.Players[0].Destination.Secured, "StartAt must place the company at a secured home");
var hero = Card("Hero", "Character", 3, 3); hero.startingPC = "Crossroads";
var knight = Card("Knight", "Character", 4, 4); knight.startingPC = "Dark Hold";
var scout = Card("Scout", "Character", 1, 1); scout.startingPC = "Ruin";
var army = new MatchRules.Unit { Card = Card("Levies", "Army", 3, 2), Owner = 0, EnteredTurn = -1 };
var champion = new MatchRules.Unit { Card = Card("Champion", "Character", 3, 3), Owner = 0, EnteredTurn = -1 };
r.Players[0].Field.Add(army); r.Players[0].Field.Add(champion);
r.Players[0].Hand.AddRange(new[] { hero, knight, scout });
r.Begin(0); r.Next(); Check(r.Play(vale), "Land rejected"); r.Next();
Check(r.Stage == MatchStage.Destination && r.DestinationChoices(0).Count() == 5, "Every settlement on a played land, home or foreign, must be a choice: " + r.DestinationChoices(0).Count());
Check(r.PlayableAt(0, neutral).SequenceEqual(new[] { hero }) && !r.PlayableAt(0, home).Any(), "PlayableAt wrong");
// --- Neutral ground: a retention attack ---------------------------------------------------------------
Check(r.ChooseDestination(neutral), "Choice failed"); r.Next(); r.Next();
Check(r.Stage == MatchStage.Arrival && r.Players[0].Destination.Card == neutral && !r.Players[0].Destination.Secured, "A neutral town must start unsecured on arrival, and a company with ready units gets its Arrival");
Check(!r.CanPlay(hero) && r.PlayBlockReason(hero).Contains("Muster"), "Arrival is for the gates: nothing is played from the hand there, and the reason says so");
r.Next(); Check(r.Stage == MatchStage.Muster && !r.CanPlay(hero) && r.PlayBlockReason(hero).Contains("dwellers"), "An unsecured town must block plays in Muster and say why");
Check(r.NeedsSecuring() && r.HasLegalAction() && r.CanSecure(army) && r.CanSecure(champion), "Securing must be a legal action");
Check(r.Secure(army) && r.Players[0].Destination.Secured && army.Tapped && r.Players[0].Destination.Entered && r.Players[0].Destination.Garrison == army, "Retention attack won (3+3 vs 2+3) must open the town, and the winner walks in: unit and town tap");
Check(r.LastBattle != null && r.LastBattle.Kind == MatchRules.BattleKind.Dwellers && r.LastBattle.Neutral && r.LastBattle.Settlement == neutral && r.LastBattle.Clashes.Single().Attacker == army, "The dwellers fight must be reported");
Check(r.Fights.Count == 1 && r.Fights[0].Loser != army && r.Fights[0].Blow == Blow.Tapped, "The dwellers duel must be logged");
Check(!r.CanUndo, "A fight cannot be undone");
Check(r.CanPlay(hero) && r.Play(hero) && r.Players[0].Destination.Tapped, "Play after securing must work and tap the town");
// Losing a retention attack: unit and town tap, nobody bleeds.
var r2 = Fresh(); var weak = new MatchRules.Unit { Card = Card("Weakling", "Army", 1, 1), Owner = 0, EnteredTurn = -1 }; r2.Players[0].Field.Add(weak);
var cross2 = Town("Crossroads", "Vale", CardData.NeutralAlignment, "Garrison"); r2.Players[0].Foreign.Add(cross2);
r2.Players[0].Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 0 });
r2.Begin(0); r2.Next(); r2.Next(); Check(r2.ChooseDestination(cross2), "Choice failed"); r2.Next(); r2.Next();
Check(r2.Secure(weak) && weak.Tapped && r2.Players[0].Field.Contains(weak) && !weak.Wounded && r2.Players[0].Destination.Tapped && !r2.Players[0].Destination.Secured, "Lost retention attack: unit and town tapped, unit unharmed");
Check(!r2.CanSecure(weak) && r2.PlayBlockReason(Card("X", "Character")) != null, "A closed town cannot be fought for again this turn");
// --- Hostile ground: a normal attack -------------------------------------------------------------------
var r3 = Fresh();
var levy = new MatchRules.Unit { Card = Card("Levies", "Army", 3, 2), Owner = 0, EnteredTurn = -1 }; r3.Players[0].Field.Add(levy);
var lord = new MatchRules.Unit { Card = Card("Lord", "Character", 3, 2), Owner = 0, EnteredTurn = -1 }; r3.Players[0].Field.Add(lord);
var hold3 = Town("Dark Hold", "Vale", CardData.DarkServants, "Wardens"); r3.Players[0].Foreign.Add(hold3);
r3.Players[0].Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 0 });
r3.Begin(0); r3.Next(); r3.Next(); Check(r3.ChooseDestination(hold3), "Choice failed"); r3.Next(); r3.Next();
levy.Card.attack = 5;
Check(r3.Secure(levy) && r3.Players[0].Field.Contains(levy) && levy.Tapped && !levy.Wounded, "Hostile fight won (5+3 vs 3+3): the unit taps for attacking and takes no blow");
Check(r3.Players[0].Destination.Secured && r3.Players[0].Destination.Garrison == levy, "Winning the duel opens the town and garrisons it");
levy.Tapped = false; lord.Card.attack = 1; r3.Players[0].Destination.Secured = false; r3.Players[0].Destination.Tapped = false;
Check(r3.Secure(lord) && lord.Wounded && !r3.Players[0].Destination.Secured && r3.Players[0].Destination.Tapped, "Hostile fight lost (1+3 vs 3+3): margin 2 equals defense 2, the character is wounded and the town closes");
var r4 = Fresh(); var lord4 = new MatchRules.Unit { Card = Card("Lord", "Character", 1, 2), Owner = 0, EnteredTurn = -1 }; r4.Players[0].Field.Add(lord4);
var hold4 = Town("Dark Hold", "Vale", CardData.DarkServants, "Wardens"); r4.Players[0].Foreign.Add(hold4);
r4.Players[0].Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 0 });
r4.Begin(0); r4.Next(); r4.Next(); r4.ChooseDestination(hold4); r4.Next(); r4.Next();
Check(r4.Secure(lord4) && lord4.Wounded && lord4.Tapped && r4.Players[0].Field.Contains(lord4), "A character struck down is wounded, not lost");
Check(!r4.CanAttack(lord4) && !r4.CanSecure(lord4), "A wounded character cannot act");
// --- Wounds heal at home or with a healing object, to tapped, and sit out a turn ---------------------------
while (r4.Stage != MatchStage.Spoils) r4.Next();
Check(r4.Next() && r4.Active == 1 && lord4.Wounded, "No healing away from home without a healing object");
while (r4.Active == 1) r4.Next();
Check(lord4.Wounded && lord4.Tapped, "A wounded character must not untap");
lord4.Objects.Add(new CardData { name = "Salve", type = "Object", healPerTurn = 1 });
while (r4.Stage != MatchStage.Spoils) r4.Next(); r4.Next();
Check(!lord4.Wounded && lord4.Tapped && lord4.Recovering, "A healing object must heal at the end of the turn, to tapped");
while (r4.Active == 1) r4.Next();
Check(!lord4.Wounded && lord4.Tapped && !lord4.Recovering, "A healed character sits out the next untap");
while (r4.Stage != MatchStage.Spoils) r4.Next(); r4.Next(); while (r4.Active == 1) r4.Next();
Check(!lord4.Tapped, "The turn after, a healed character readies normally");
// --- The tapped -1/-1 ---------------------------------------------------------------------------------
Check(r4.Stats(lord4) == (1, 2), "Ready stats must be printed stats");
lord4.Tapped = true; Check(r4.Stats(lord4) == (0, 1), "A tapped unit acts at -1/-1"); lord4.Tapped = false;
// --- Ambush: the other player holds a card born where the company taps ----------------------------------
var r5 = Fresh();
var town5 = Town("Home", "Vale", CardData.FreePeople, "Garrison"); r5.Players[0].Settlements.Add(town5); r5.StartAt(0, town5);
r5.Players[0].Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 0 });
var settler = Card("Settler", "Character", 2, 2); settler.startingPC = "Home"; r5.Players[0].Hand.Add(settler);
var porter = new MatchRules.Unit { Card = Card("Porter", "Character", 1, 1), Owner = 0, EnteredTurn = -1 }; r5.Players[0].Field.Add(porter);
var lurker = Card("Lurker", "Character", 3, 3); lurker.startingPC = "Home"; r5.Players[1].Hand.Add(lurker);
var trap = Card("Trap", "Encounter"); trap.birthplaces.Add("Home"); r5.Players[1].Hand.Add(trap);
r5.Begin(0); r5.Next(); r5.Next(); r5.Next(); r5.Next(); Check(r5.Stage == MatchStage.Arrival && r5.NeedsEntering(), "Arrival not reached");
Check(!r5.CanPlay(settler) && r5.Enter(porter) && r5.PendingAmbush != null && r5.PendingAmbush.Defender == 1 && r5.PendingAmbush.Options.Count == 2, "Entering with enemy cards born there must raise an ambush");
Check(!r5.CanUndo && !r5.Next() && r5.PlayBlockReason(Card("Any", "Army")) != null && r5.HasLegalAction(), "Nothing moves while an ambush waits");
Check(r5.PendingAmbush.Target == porter, "The ambush targets the unit that walked in");
Check(!r5.AmbushWith(settler) && r5.CanAmbush(lurker) && r5.AmbushWith(lurker), "Ambushing with the born character must work");
var lurkerUnit = r5.Players[1].Field.FirstOrDefault(u => u.Card == lurker);
Check(lurkerUnit != null && lurkerUnit.Tapped && !r5.Players[1].Hand.Contains(lurker), "The ambusher enters the field tapped");
Check(porter.Wounded && !lurkerUnit.Wounded, "Ambush duel: 3+3 beats the tapped porter's 0+3 by 3 over defense 0: struck down, wounded");
Check(r5.LastBattle.Kind == MatchRules.BattleKind.Ambush && r5.LastBattle.Clashes.Single().Target == porter, "The ambush must be reported");
Check(r5.PendingAmbush == null && !r5.CanPlay(settler) && r5.Next() && r5.Stage == MatchStage.Muster && r5.CanPlay(settler) && r5.Play(settler) && r5.Next(), "Play resumes in Muster after the ambush at the entered town");
// Declining, and springing an encounter instead.
var r6 = Fresh();
var town6 = Town("Home", "Vale", CardData.FreePeople, "Garrison"); r6.Players[0].Settlements.Add(town6); r6.StartAt(0, town6);
r6.Players[0].Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 0 });
var settler6 = Card("Settler", "Character", 2, 2); settler6.startingPC = "Home"; r6.Players[0].Hand.Add(settler6);
var porter6 = new MatchRules.Unit { Card = Card("Porter", "Character", 1, 1), Owner = 0, EnteredTurn = -1 }; r6.Players[0].Field.Add(porter6);
var trap6 = Card("Trap", "Encounter"); trap6.birthplaces.Add("Home"); r6.Players[1].Hand.Add(trap6);
int sprungOn = -1; r6.ResolveEncounter = (card, target, rules) => sprungOn = target;
r6.Begin(0); r6.Next(); r6.Next(); r6.Next(); r6.Next(); Check(r6.Enter(porter6) && r6.PendingAmbush != null, "Encounter-only ambush not raised");
Check(r6.AmbushWith(trap6) && sprungOn == 0 && r6.Players[1].Discard.Contains(trap6) && r6.PendingAmbush == null, "Springing an encounter must resolve it against the tapper and spend it");
var r7 = Fresh();
var town7 = Town("Home", "Vale", CardData.FreePeople, "Garrison"); r7.Players[0].Settlements.Add(town7); r7.StartAt(0, town7);
r7.Players[0].Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 0 });
var settler7 = Card("Settler", "Character", 2, 2); settler7.startingPC = "Home"; r7.Players[0].Hand.Add(settler7);
var porter7 = new MatchRules.Unit { Card = Card("Porter", "Character", 1, 1), Owner = 0, EnteredTurn = -1 }; r7.Players[0].Field.Add(porter7);
var lurker7 = Card("Lurker", "Character", 3, 3); lurker7.startingPC = "Home"; r7.Players[1].Hand.Add(lurker7);
r7.Begin(0); r7.Next(); r7.Next(); r7.Next(); r7.Next(); r7.Enter(porter7);
Check(r7.DeclineAmbush() && r7.PendingAmbush == null && r7.Players[1].Hand.Contains(lurker7), "Declining must keep the card and resume");
// --- Two companies in one town -------------------------------------------------------------------------
var r8 = Fresh();
var shared0 = Town("Market", "Vale", CardData.NeutralAlignment, ""); var shared1 = Town("Market", "Vale", CardData.NeutralAlignment, "");
r8.Players[0].Settlements.Add(shared0); r8.Players[1].Settlements.Add(shared1);
r8.StartAt(0, shared0); r8.StartAt(1, shared1);
r8.Players[0].Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 0 });
var ours = new MatchRules.Unit { Card = Card("Ours", "Character", 2, 2), Owner = 0, EnteredTurn = -1 }; r8.Players[0].Field.Add(ours);
var theirs = new MatchRules.Unit { Card = Card("Theirs", "Army", 3, 1), Owner = 1, EnteredTurn = -1 }; r8.Players[1].Field.Add(theirs);
var trader = Card("Trader", "Character", 1, 1); trader.startingPC = "Market"; r8.Players[0].Hand.Add(trader);
r8.Players[1].Destination.Tapped = true; // the other company already entered it on its turn
r8.Begin(0); r8.Next(); r8.Next(); r8.Next();
Check(r8.Stage == MatchStage.Travel && r8.CanAttack(theirs), "The enemy army may fall on the company holding still");
r8.Next(); Check(r8.Stage == MatchStage.Arrival, "Undeclared attacks must let the road end at the gates");
Check(r8.Enter(ours) && r8.Players[0].Destination.Garrison == ours, "The unit that walked in garrisons the town");
Check(ours.Wounded && r8.Players[1].Field.Contains(theirs), "Meeting: the character that walked in fights tapped at 1/1, loses 4 to 6 by 2 over defense 1: struck down, wounded");
Check(r8.LastBattle.Kind == MatchRules.BattleKind.Meeting && r8.LastBattle.Settlement == shared0, "The meeting must be reported");
r8.Next(); Check(r8.Stage == MatchStage.Muster && r8.Play(trader) && r8.Players[0].Field.Any(u => u.Card == trader), "Play at the entered shared town failed");
// --- Journeys: one typed draw per stop, capped at five ----------------------------------------------------
var map = new RegionMap(new RegionMapData
{
    regions = new List<RegionEntry>
    {
        new RegionEntry { name = "A", adjacent = new List<string> { "B" } }, new RegionEntry { name = "B", adjacent = new List<string> { "C" } },
        new RegionEntry { name = "C", adjacent = new List<string> { "D" } }, new RegionEntry { name = "D", adjacent = new List<string> { "E" } },
        new RegionEntry { name = "E", adjacent = new List<string> { "F" } }, new RegionEntry { name = "F", adjacent = new List<string> { "G" } },
        new RegionEntry { name = "G" }, new RegionEntry { name = "Island" }
    },
    landAliases = new List<LandAlias> { new LandAlias { land = "Old A", region = "A" } }
});
Check(map.AreAdjacent("B", "A") && !map.AreAdjacent("A", "C") && map.Distance("A", "D") == 3 && map.Distance("A", "A") == 0 && map.Distance("A", "Island") == -1, "Map distances wrong");
Check(map.Route("A", "C").SequenceEqual(new[] { "A", "B", "C" }) && map.RegionOfLand("Old A") == "A" && map.RegionOfLand("B") == "B", "Route or alias wrong");
Check(RegionMap.Journey(0) == 1 && RegionMap.Journey(-1) == 1 && RegionMap.Journey(3) == 3 && RegionMap.Journey(9) == 5, "Journey clamp wrong");
var r9 = Fresh(); r9.Map = map;
var startTown = Town("Start", "A", CardData.FreePeople, ""); r9.Players[0].Settlements.Add(startTown); r9.StartAt(0, startTown);
var farTown = Town("Far", "G", CardData.FreePeople, ""); var nearTown = Town("Near", "A", CardData.FreePeople, ""); var midTown = Town("Mid", "C", CardData.FreePeople, "");
r9.Players[0].Settlements.AddRange(new[] { farTown, nearTown, midTown });
foreach (var land in new[] { "Old A", "C", "G" }) r9.Players[0].Field.Add(new MatchRules.Unit { Card = Card(land, "Land"), Owner = 0 });
var deckLand = Card("Deck Land", "Land"); var deckEvent = Card("Deck Event", "Event"); var deckAction = Card("Deck Action", "Action");
var deckEncounter = Card("Deck Encounter", "Encounter"); var deckArmy = Card("Deck Army", "Army"); var deckCharacter = Card("Deck Character", "Character"); var deckArmy2 = Card("Deck Army 2", "Army");
var filler = Enumerable.Range(0, 6).Select(i => Card("Filler " + i, "Object")).ToList();
r9.Players[0].Deck.AddRange(filler); r9.Players[0].Deck.AddRange(new[] { deckArmy2, deckCharacter, deckArmy, deckEncounter, deckAction, deckEvent, deckLand });
r9.Begin(0);
Check(r9.Players[0].Hand.Count == 5 && r9.Players[0].Hand.All(c => c.name.StartsWith("Filler")), "Replenish must draw from the top");
r9.Next(); r9.Next(); Check(r9.Stage == MatchStage.Destination, "Destination stage not reached");
Check(r9.DestinationChoices(0).Contains(nearTown) && r9.DestinationChoices(0).Contains(midTown) && r9.DestinationChoices(0).Contains(farTown), "An alias land must open its region");
// Ranking for the picker: most playable hand cards, then own side before neutral before hostile, then the
// shortest road, ties in pool order; a land the opponent holds opens its region for this company too.
var darkC = Town("Dark C", "C", CardData.DarkServants, ""); var neutralC = Town("Neutral C", "C", CardData.NeutralAlignment, ""); r9.Players[0].Foreign.AddRange(new[] { darkC, neutralC });
var wantsFar = Card("Wants Far", "Character"); wantsFar.startingPC = "Far"; r9.Players[0].Hand.Add(wantsFar);
var ranked = r9.RankedDestinations(0).ToList();
Check(ranked.SequenceEqual(new[] { farTown, startTown, nearTown, midTown, neutralC, darkC }), "Ranking wrong: " + string.Join(", ", ranked.Select(c => c.name)));
var darkE = Town("Dark E", "E", CardData.DarkServants, ""); r9.Players[0].Foreign.Add(darkE);
Check(!r9.DestinationChoices(0).Contains(darkE), "A town with no land down must not be a choice");
r9.Players[1].Field.Add(new MatchRules.Unit { Card = Card("E", "Land"), Owner = 1 });
Check(r9.DestinationChoices(0).Contains(darkE) && r9.RankedDestinations(0).Last() == darkE, "The opponent's land must open its region, ranked after the nearer hostile town");
r9.Players[0].Hand.Remove(wantsFar);
Check(r9.Stops(0, nearTown).SequenceEqual(new[] { "A" }) && r9.Rewards(0, nearTown) == 1, "Same region: one stop");
Check(r9.Stops(0, midTown).SequenceEqual(new[] { "B", "C" }) && r9.Rewards(0, midTown) == 2, "Two borders: two stops");
Check(r9.Stops(0, farTown).Count == 6 && r9.Rewards(0, farTown) == 5, "Six borders: six stops, five rewards");
Check(MatchRules.RewardAt(1) == TravelReward.Land && MatchRules.RewardAt(2) == TravelReward.EventOrAction && MatchRules.RewardAt(3) == TravelReward.Encounter && MatchRules.RewardAt(4) == TravelReward.Army && MatchRules.RewardAt(5) == TravelReward.Character, "Reward order wrong");
Check(r9.ChooseDestination(farTown), "Long journey refused");
r9.Next(); Check(r9.Stage == MatchStage.Travel && r9.Travel.Stops.Count == 6 && r9.Travel.Stop == 0 && r9.Travel.Region == "B", "The road must start at the first region entered");
Check(r9.Players[0].Hand.Contains(deckLand), "The first stop must draw the land at once");
int leaves = 0; while (r9.Stage == MatchStage.Travel) { r9.Next(); leaves++; }
Check(leaves == 6 && r9.Stage == MatchStage.Muster && r9.Players[0].Destination.Card == farTown, "Six stops must each be left before arriving: " + leaves);
var drawn = r9.Players[0].Hand.Where(c => !c.name.StartsWith("Filler")).ToList();
Check(drawn.Count == 5 && drawn.Contains(deckLand) && (drawn.Contains(deckEvent) || drawn.Contains(deckAction)) && drawn.Contains(deckEncounter) && (drawn.Contains(deckArmy) || drawn.Contains(deckArmy2)) && drawn.Contains(deckCharacter), "Five stops must draw a land, an event/action, an encounter, an army and a character: " + string.Join(", ", drawn.Select(c => c.name)));
// With no character left in the deck the fifth stop is another army, and with nothing of a kind the top card.
var r10 = Fresh(); r10.Map = map;
var s10 = Town("Start", "A", CardData.FreePeople, ""); r10.Players[0].Settlements.Add(s10); r10.StartAt(0, s10);
var f10 = Town("Far", "G", CardData.FreePeople, ""); r10.Players[0].Settlements.Add(f10);
r10.Players[0].Field.Add(new MatchRules.Unit { Card = Card("G", "Land"), Owner = 0 });
var top = Card("Top", "Object"); var armyA = Card("Army A", "Army"); var armyB = Card("Army B", "Army");
r10.Players[0].Deck.AddRange(new[] { top, armyA, armyB });
r10.Players[0].HandLimit = 0;
r10.Begin(0); r10.Next(); r10.Next(); Check(r10.ChooseDestination(f10), "Journey refused"); r10.Next(); while (r10.Stage == MatchStage.Travel) r10.Next();
Check(r10.Players[0].Hand.Contains(armyA) && r10.Players[0].Hand.Contains(armyB) && r10.Players[0].Hand.Contains(top) && r10.Players[0].Deck.Count == 0, "Fallbacks: armies stand in for the character, the top card for missing kinds");
// --- The road: attacks and defences gated by terrain ----------------------------------------------------------
var r12 = Fresh(); r12.Map = map;
r12.ResolveTerrain = region => region == "A" ? TerrainEnum.Plains : region == "B" ? TerrainEnum.Forest : TerrainEnum.Hills;
var from12 = Town("Start", "A", CardData.FreePeople, ""); var to12 = Town("Hillfort", "C", CardData.FreePeople, "");
r12.Players[0].Settlements.AddRange(new[] { from12, to12 }); r12.StartAt(0, from12);
r12.Players[0].Field.Add(new MatchRules.Unit { Card = Card("C", "Land"), Owner = 0 });
CardData Troop(string name, string ground, int attack, int defense) { var c = Card(name, "Army", attack, defense); c.terrain = ground; return c; }
var horsemen = new MatchRules.Unit { Card = Troop("Horsemen", "Plains", 3, 2), Owner = 1, EnteredTurn = -1 };
var rangers = new MatchRules.Unit { Card = Troop("Rangers", "Forest", 2, 2), Owner = 1, EnteredTurn = -1 };
var warlord = new MatchRules.Unit { Card = Card("Warlord", "Character", 3, 3), Owner = 1, EnteredTurn = -1 };
r12.Players[1].Field.AddRange(new[] { horsemen, rangers, warlord });
var shieldwall = new MatchRules.Unit { Card = Troop("Shieldwall", "Forest", 1, 3), Owner = 0, EnteredTurn = -1 };
var pikes = new MatchRules.Unit { Card = Troop("Pikes", "Hills", 2, 2), Owner = 0, EnteredTurn = -1 };
var captain = new MatchRules.Unit { Card = Card("Captain", "Character", 2, 4), Owner = 0, EnteredTurn = -1 };
r12.Players[0].Field.AddRange(new[] { shieldwall, pikes, captain });
r12.Begin(0); r12.Next(); r12.Next(); Check(r12.ChooseDestination(to12), "Choice failed"); r12.Next();
Check(r12.Stage == MatchStage.Travel && r12.Travel.Region == "B" && r12.Ground == TerrainEnum.Forest && r12.Attacker == 1, "First stop must be the forest of B");
Check(r12.CanAttack(rangers) && r12.CanAttack(warlord) && !r12.CanAttack(horsemen) && r12.AttackBlockReason(horsemen).Contains("Plains"), "Only forest armies and characters may attack in the forest");
Check(r12.Raiders().Count() == 2 && r12.HasLegalAction(), "Raiders must list what can fall on the company");
Check(r12.Attack(rangers) && r12.Attack(warlord) && rangers.Tapped, "Road attacks failed");
Check(r12.Next() && r12.Phase == TravelPhase.Defend, "Declared attacks must open the defence");
var rangersStrike = r12.Attacks.First(a => a.Attacker == rangers); var warlordStrike = r12.Attacks.First(a => a.Attacker == warlord);
Check(r12.CanBlock(shieldwall, rangersStrike) && r12.CanBlock(captain, rangersStrike) && !r12.CanBlock(pikes, rangersStrike) && r12.BlockBlockReason(pikes).Contains("Hills"), "Only forest armies and characters may defend in the forest");
Check(r12.Block(shieldwall, rangersStrike), "Block failed");
int life = r12.Players[0].Life;
Check(r12.Next() && r12.Players[0].Life == life - 3, "The unblocked warlord must hit the travelling company");
Check(r12.Players[1].Field.Contains(rangers) && r12.Players[0].Field.Contains(shieldwall) && !shieldwall.Wounded, "Rangers 2+3 beat the shieldwall's 1+3 by 1 under defense 3: it is merely tapped");
Check(r12.Fights.Count == 1 && r12.Fights[0].Loser == shieldwall && r12.Fights[0].Blow == Blow.Tapped && r12.Fights[0].DefenderRoll == 3, "Road duel log wrong");
Check(r12.Stage == MatchStage.Travel && r12.Travel.Region == "C" && r12.Ground == TerrainEnum.Hills && r12.Phase == TravelPhase.Attack, "The road must go on to the hills of C");
Check(!r12.CanAttack(rangers) && !r12.CanAttack(warlord) && !r12.CanAttack(horsemen), "Tapped raiders and wrong-ground armies must sit out the second stop");
Check(!r12.HasLegalAction() && r12.Next() && r12.Stage == MatchStage.Muster && r12.Players[0].Destination.Card == to12, "An unopposed last stop must arrive");
Check(r12.Players[1].Field.Contains(horsemen) && !horsemen.Tapped, "Armies that could not fight stay ready");
// --- Recruits enter tapped; hand cards raid the road and ride back into the deck; standing fast --------------
var r13 = Fresh(); r13.Map = map;
r13.ResolveTerrain = region => region == "B" ? TerrainEnum.Forest : TerrainEnum.Plains;
var from13 = Town("Start", "A", CardData.FreePeople, ""); var to13 = Town("Woodhall", "B", CardData.NeutralAlignment, "Garrison");
r13.Players[0].Settlements.Add(from13); r13.Players[0].Foreign.Add(to13); r13.StartAt(0, from13);
r13.Players[0].Field.Add(new MatchRules.Unit { Card = Card("B", "Land"), Owner = 0 });
var guard = new MatchRules.Unit { Card = Troop("Guard", "Forest", 2, 4), Owner = 0, EnteredTurn = -1 }; r13.Players[0].Field.Add(guard);
var handWolves = Troop("Wolves", "Forest", 3, 2); var handRiders = Troop("Riders", "Plains", 3, 2); var handSeer = Card("Seer", "Character", 1, 1);
r13.Players[1].Hand.AddRange(new[] { handWolves, handRiders, handSeer });
var recruit = Card("Recruit", "Army", 2, 2); var rider = Card("Outrider", "Army", 2, 2); rider.specialAbilities.Add(ObjectCharacterArmySpecialAbilityEnum.Mounted);
r13.Players[0].Hand.AddRange(new[] { recruit, rider });
r13.Begin(0); r13.Next(); r13.Next(); Check(r13.ChooseDestination(to13), "Choice failed"); r13.Next();
Check(r13.Stage == MatchStage.Travel && r13.Ground == TerrainEnum.Forest, "Forest stop expected");
Check(r13.CanAttackWith(handWolves) && r13.CanAttackWith(handSeer) && !r13.CanAttackWith(handRiders) && r13.AttackWithBlockReason(handRiders).Contains("Plains"), "Hand raiders must follow the terrain rule");
Check(r13.HandRaiders().Count() == 2 && r13.HasLegalAction(), "Hand raiders must count as a legal action");
Check(r13.AttackWith(handWolves) && !r13.Players[1].Hand.Contains(handWolves) && r13.Players[1].Field.Any(u => u.Card == handWolves && u.Roadside), "A hand raider takes the field for the fight");
Check(r13.Next() && r13.Phase == TravelPhase.Defend, "Defence expected");
var wolvesStrike = r13.Attacks.Single();
Check(r13.Block(guard, wolvesStrike, standFast: true) && !guard.Tapped && wolvesStrike.BlockerStats[guard] == (0, 2), "Standing fast must keep the unit untapped at -2/-2");
int life13 = r13.Players[0].Life;
Check(r13.Next() && r13.Players[0].Life == life13, "A blocked raid must not touch life");
Check(!r13.Players[0].Field.Contains(guard) && r13.Players[0].Discard.Contains(guard.Card), "Wolves 3+3 beat a guard standing fast (0+3) by 3 over defense 2: the guard falls");
Check(!r13.Players[1].Field.Any(u => u.Card == handWolves) && r13.Players[1].Deck.Contains(handWolves) && !r13.Players[1].Hand.Contains(handWolves), "The hand raider must ride back into the deck");
Check(r13.Stage == MatchStage.Muster && r13.Players[0].Destination.Card == to13, "Arrival expected");
Check(r13.Play(recruit) && r13.Players[0].Field.First(u => u.Card == recruit).Tapped, "A recruit must enter tapped");
Check(r13.Play(rider) && !r13.Players[0].Field.First(u => u.Card == rider).Tapped, "Mounted troops must enter ready");
var riderUnit = r13.Players[0].Field.First(u => u.Card == rider);
Check(r13.CanSecure(riderUnit) && !r13.CanSecure(r13.Players[0].Field.First(u => u.Card == recruit)), "Only a ready unit can face the dwellers");
// A defender that taps fights at full strength.
var r14 = Fresh();
var camp14 = Town("Camp", "Vale", CardData.FreePeople, ""); r14.Players[0].Settlements.Add(camp14); r14.StartAt(0, camp14);
var wall = new MatchRules.Unit { Card = Card("Wall", "Army", 6, 4), Owner = 0, EnteredTurn = -1 }; r14.Players[0].Field.Add(wall);
var raiders14 = new MatchRules.Unit { Card = Card("Raiders", "Army", 3, 2), Owner = 1, EnteredTurn = -1 }; r14.Players[1].Field.Add(raiders14);
r14.Begin(0); r14.Next(); r14.Next(); r14.Next(); Check(r14.Stage == MatchStage.Travel && r14.Attack(raiders14) && r14.Next(), "Raid setup failed");
Check(r14.Block(wall, r14.Attacks[0]) && wall.Tapped && r14.Attacks[0].BlockerStats[wall] == (6, 4), "A tapping defender fights at full strength");
Check(r14.Next() && r14.Players[0].Field.Contains(wall) && !r14.Players[1].Field.Contains(raiders14), "Wall 6+3 beats raiders 3+3 by 3 over defense 2: the raiders fall");
// A tie is a stand-off, and a wounded army mends at home like a character.
var r15 = Fresh();
var camp15 = Town("Camp", "Vale", CardData.FreePeople, ""); r15.Players[0].Settlements.Add(camp15); r15.StartAt(0, camp15);
var line15 = new MatchRules.Unit { Card = Card("Line", "Army", 3, 1), Owner = 0, EnteredTurn = -1 }; r15.Players[0].Field.Add(line15);
var foe15 = new MatchRules.Unit { Card = Card("Foe", "Army", 3, 2), Owner = 1, EnteredTurn = -1 }; r15.Players[1].Field.Add(foe15);
r15.Begin(0); r15.Next(); r15.Next(); r15.Next(); Check(r15.Attack(foe15) && r15.Next() && r15.Block(line15, r15.Attacks[0]) && r15.Next(), "Raid setup failed");
Check(r15.Fights.Single().Loser == null && r15.Players[0].Field.Contains(line15) && r15.Players[1].Field.Contains(foe15) && !line15.Wounded, "Equal totals are a stand-off");
line15.Wounded = true; line15.Tapped = true;
while (r15.Stage != MatchStage.Spoils) r15.Next(); r15.Next();
Check(!line15.Wounded && line15.Recovering, "A wounded army resting at home must mend like a character");
// --- The hand limit at the end of the turn --------------------------------------------------------------
var r11 = Fresh();
for (int i = 0; i < 9; i++) r11.Players[0].Hand.Add(Card("Card " + i, "Army"));
r11.Begin(0);
Check(r11.Players[0].Hand.Count == 9 && !r11.CanDiscard(r11.Players[0].Hand[0]), "Discarding is only for the end of the turn");
while (r11.Stage != MatchStage.Spoils) r11.Next();
Check(r11.MustDiscard(0) && r11.HasLegalAction() && !r11.Next() && r11.Message.Contains("Discard"), "The turn must not end over the hand limit");
for (int i = 0; i < 4; i++) Check(r11.Discard(r11.Players[0].Hand[0]), "Discard refused");
Check(!r11.MustDiscard(0) && !r11.CanDiscard(r11.Players[0].Hand[0]) && r11.Players[0].Discard.Count == 4, "Discard bookkeeping wrong");
Check(r11.Next() && r11.Active == 1, "Turn must end once the hand fits");
// --- The PC face and the picker's playable line ------------------------------------------------------
var plaque = Town("Dark Hold", "MirkWood", CardData.DarkServants, "Wardens");
plaque.objectTypes.AddRange(new[] { ObjectTypeEnum.Weapon, ObjectTypeEnum.Weapon, ObjectTypeEnum.Armor });
var face = PcDescriptionBuilder.BuildBody(plaque, true).Split('\n');
Check(face.Length == 4 && face[0] == PcDescriptionBuilder.RegionLink("MirkWood") + " (Dark Servants)" && face[0].Contains("<u>Mirk Wood</u>") && face[0].Contains("card:MirkWood") && face[1].StartsWith("Dwellers: ") && face[1].Contains("Wardens") && face[1].EndsWith(".")
    && face[2] == "Allows recruiting characters born here." && face[3] == "Playable objects: " + CardData.FormatObjectTypeTag(ObjectTypeEnum.Weapon) + ", " + CardData.FormatObjectTypeTag(ObjectTypeEnum.Armor),
    "PC face must read region (side) / dwellers / recruiting / playable objects, one per line: " + string.Join(" | ", face));
Check(PcDescriptionBuilder.BuildBody(Town("Ruin", "Vale", CardData.NeutralAlignment, ""), true).Split('\n').Length == 2, "A PC without dwellers or wares must drop those lines");
Check(DestinationPicker.PlayableSummary(new CardData[0]) == "Nothing in hand can be played here.", "Empty playable line wrong");
Check(DestinationPicker.PlayableSummary(new[] { Card("A", "Character"), Card("B", "Character") }) == "Allows you playing " + PcDescriptionBuilder.CardLink("A", "A") + ", " + PcDescriptionBuilder.CardLink("B", "B") + ".", "Two playable names wrong: " + DestinationPicker.PlayableSummary(new[] { Card("A", "Character"), Card("B", "Character") }));
Check(DestinationPicker.PlayableSummary(new[] { Card("A", "Character"), Card("B", "Character"), Card("B", "Character"), Card("C", "Object"), Card("D", "Object") }).EndsWith(PcDescriptionBuilder.CardLink("C", "C") + ", among others."), "Playable line past three must say among others");
var quest = Card("Old Road", "Encounter"); quest.birthplaces.Add("Dark Hold");
Check(quest.GetEncounterDescription() == "Face this encounter at " + PcDescriptionBuilder.CardLink("Dark Hold") + " with your company, or force your opponent's company at " + PcDescriptionBuilder.CardLink("Dark Hold") + " into it.", "Encounter face wrong: " + quest.GetEncounterDescription());
quest.birthplaces.Add("Ruin");
Check(quest.GetEncounterDescription().EndsWith("or force your opponent's company at one of them into it."), "Encounter face with several homes wrong: " + quest.GetEncounterDescription());
return "PASS: standing by home/side, dwellers, retention and hostile fights that enter the town, wounds that heal to tapped and sit out a turn, tapped -1/-1, ambush by character or encounter and decline, two companies meeting, map routes/aliases/clamp, stop-by-stop roads with typed draws and fallbacks, terrain-gated road attacks and defences, hand raiders that return to the deck, tapped recruits, standing fast, dice duels with killed/wounded/tapped margins and stand-offs, the end-of-turn discard, the PC face lines and the picker's playable line.";
