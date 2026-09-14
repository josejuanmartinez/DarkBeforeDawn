// Run with: unity command eval_file --file Tests/DestinationChecks.cs
void Check(bool ok, string message) { if (!ok) throw new System.Exception(message); }
// Runs out the active player's turn; at Recover Objects the newest cards are shed down to the hand limit.
void EndTurn(MatchRules rules) { int who = rules.Active; while (rules.Active == who) { if (rules.Stage == MatchStage.Spoils) while (rules.MustDiscard(who)) rules.Discard(rules.Players[who].Hand[rules.Players[who].Hand.Count - 1]); rules.Next(); } }
CardData Card(string name, string type) => new CardData { name = name, type = type, attack = 2, defense = 2 };
var r = new MatchRules();
var vale = Card("Vale", "Land"); var hills = Card("Hills", "Land");
var home = Card("Home", "PC"); home.region = "Vale"; home.objectTypes.Add(ObjectTypeEnum.Weapon);
var market = Card("Market", "PC"); market.region = "Vale"; market.objectTypes.Add(ObjectTypeEnum.Ring); market.objectTypes.Add(ObjectTypeEnum.Remedy);
var far = Card("Far Hold", "PC"); far.region = "Hills";
r.Players[0].Settlements.AddRange(new[] { home, market, far });
var hero = Card("Hero", "Character"); hero.startingPC = "Home";
var stranger = Card("Stranger", "Character"); stranger.startingPC = "Far Hold";
var sword = Card("Sword", "Object"); sword.objectType = ObjectTypeEnum.Weapon;
var ring = Card("Ring", "Object"); ring.objectType = ObjectTypeEnum.Ring;
var beast = Card("Beast", "Army");
var wight = Card("Wight", "Encounter"); wight.birthplaces.Add("Home");
r.Players[0].Hand.AddRange(new[] { vale, hero, stranger, sword, ring, beast, wight });
// A ready unit to walk into town with: nothing can be played at a settlement until someone enters it.
var guide = new MatchRules.Unit { Card = Card("Guide", "Army"), Owner = 0 }; r.Players[0].Field.Add(guide);
var spare = Card("Spare Land", "Land"); r.Players[0].Deck.Add(spare);
r.Begin(0);
Check(!r.DestinationChoices(0).Any(), "Destinations offered before any land");
r.Next(); Check(r.Stage == MatchStage.Realm && r.Play(vale), "Land rejected in Realm");
Check(!r.Players[0].Hand.Contains(home) && r.PlayBlockReason(home) != null, "A settlement must never be a hand card");
r.Next(); Check(r.Stage == MatchStage.Destination, "Realm must go to Select Destination");
Check(r.DestinationChoices(0).SequenceEqual(new[] { home, market }), "Choices must be the settlements whose land is down");
Check(r.HasLegalAction() && !r.CanChooseDestination(far) && r.CanChooseDestination(home), "Destination legality wrong");
Check(r.DestinationDemand(0, home) == 3 && r.DestinationDemand(0, market) == 1 && r.DestinationDemand(0, far) == 1, "Demand count wrong: " + r.DestinationDemand(0, home));
Check(r.PreferredDestination(0) == home, "Preferred destination should be the one most hand cards want");
Check(!r.ChooseDestination(far) && r.ChooseDestination(home), "Choosing failed");
Check(!r.ChooseDestination(market) && r.Players[0].Travelled && !r.HasLegalAction(), "One journey per turn: re-choosing must be refused");
Check(r.Players[0].Destination == null && r.Players[0].Bound == home, "The choice is only a heading until the road is walked");
r.Next(); Check(r.Stage == MatchStage.Travel && r.Travel != null && r.Travel.Stops.Count == 1, "Realm must go to Travel with a one-stop road");
r.Next(); Check(r.Stage == MatchStage.Arrival, "A company with a ready unit and cards for the town must get its Arrival");
var destination = r.Players[0].Destination;
Check(r.NeedsEntering() && r.HasLegalAction() && !r.CanPlay(hero) && r.PlayBlockReason(hero).Contains("entered"), "Nothing plays at a town before it is entered");
Check(!r.CanEnter(null) && r.CanEnter(guide) && r.Enter(guide) && guide.Tapped && destination.Tapped && destination.Entered && destination.Garrison == guide, "Entering must tap the unit and the town");
Check(!r.CanEnter(guide) && !r.NeedsEntering() && !r.HasLegalAction(), "An entered town needs no more entering");
r.Next(); Check(r.Stage == MatchStage.Muster, "Arrival must go on to Muster");
Check(destination != null && destination.Card == home && r.Players[0].Field.Count(u => u.Card.GetCardType() == CardTypeEnum.PC) == 1, "Only the destination sits on the field");
Check(!r.CanChooseDestination(home) && !r.CanChooseDestination(market), "Nothing is selectable after travelling");
Check(r.Players[0].Hand.Contains(spare) && r.Players[0].Hand.Count == 7, "A one-stop journey draws one card, a land: " + r.Players[0].Hand.Count);
Check(r.CanPlay(beast) && r.CanPlay(hero) && r.CanPlay(wight) && !r.CanPlay(stranger) && !r.CanPlay(sword) && !r.CanPlay(ring), "Muster eligibility at destination wrong");
Check(r.PlayBlockReason(stranger).Contains("Far Hold") && r.PlayBlockReason(ring).Contains("Ring"), "Block reasons should name what is needed");
Check(r.Play(hero) && destination.Tapped, "Playing at the entered destination failed");
Check(r.CanPlay(wight), "An entered destination must keep hosting plays for the turn");
Check(r.CanPlay(beast), "Armies must not need the destination");
Check(r.Undo() && destination.Tapped && r.Players[0].Hand.Contains(hero), "Undo must return the card and leave the town entered");
Check(r.Play(wight) && r.Players[0].Discard.Contains(wight) && destination.Tapped, "Encounter not investigated");
Check(r.Undo() && r.Players[0].Hand.Contains(wight) && !r.Players[0].Discard.Contains(wight) && destination.Tapped, "Encounter undo failed");
Check(r.Play(hero), "Hero replay failed");
var heroUnit = r.Players[0].Field.First(u => u.Card == hero);
Check(r.PlayBlockReason(sword, heroUnit, false) == null, "Object refused at an entered destination");
EndTurn(r);
EndTurn(r);
Check(r.Stage == MatchStage.Draw && r.Players[0].Destination == destination && !destination.Tapped, "Destination must persist and untap on the next turn");
r.Next(); r.Next(); Check(r.Stage == MatchStage.Destination, "Second turn skipped destination");
r.Next(); Check(r.Stage == MatchStage.Travel && !r.Travel.Moving && r.Travel.Stops.Count == 1, "Staying is still a one-stop road");
r.Next(); Check(r.Stage == MatchStage.Arrival && r.Players[0].Destination == destination && !destination.Tapped, "Staying changed the destination, or the town did not untap");
Check(!heroUnit.Tapped && r.Enter(heroUnit) && destination.Entered, "The recruit must have readied and be able to enter");
r.Next(); Check(r.Stage == MatchStage.Muster, "Arrival must go on to Muster");
Check(r.Play(sword, heroUnit) && heroUnit.Objects.Contains(sword) && destination.Tapped, "Weapon not equipped at a settlement trading in weapons");
Check(!r.CanPlay(ring), "Ring accepted where nobody trades in rings");
// Opponent scoring: the most cards waiting wins, current destination on a tie.
var o = r.Players[1]; o.Field.Add(new MatchRules.Unit { Card = Card("Vale", "Land"), Owner = 1 });
var a = Card("A", "PC"); a.region = "Vale"; var b = Card("B", "PC"); b.region = "Vale"; o.Settlements.AddRange(new[] { a, b });
var wantsB = Card("Wants B", "Character"); wantsB.startingPC = "B"; o.Hand.Add(wantsB);
EndTurn(r);
r.Next(); r.Next(); Check(r.Active == 1 && r.Stage == MatchStage.Destination, "Opponent destination stage not reached");
Check(r.PreferredDestination(1) == b && r.ChooseDestination(b), "Opponent should travel where its hand cards wait");
r.Next(); while (r.Stage == MatchStage.Travel) r.Next(); Check(r.Players[1].Destination.Card == b, "Opponent did not arrive");
EndTurn(r); EndTurn(r); r.Next(); r.Next(); Check(r.Active == 1 && r.Stage == MatchStage.Destination, "Second opponent destination stage not reached");
o.Hand.Clear(); var wantsA = Card("Wants A", "Character"); wantsA.startingPC = "A"; var wantsB2 = Card("Wants B too", "Character"); wantsB2.startingPC = "B"; o.Hand.AddRange(new[] { wantsA, wantsB2 });
// A one-stop hop still draws a card, so on a demand tie the opponent travels rather than stays.
Check(r.PreferredDestination(1) == a, "Tie in demand: the journey reward decides for travelling");
var hostile = Card("Dark Hold", "PC"); hostile.region = "Vale"; hostile.settlementAlignment = CardData.DarkServants; o.Foreign.Add(hostile);
foreach (var n in new[] { "Wants Dark Hold", "Wants Dark Hold too" }) { var wants = Card(n, "Character"); wants.startingPC = "Dark Hold"; o.Hand.Add(wants); }
Check(r.DestinationChoices(1).Contains(hostile), "A foreign settlement whose land is down must be a choice");
Check(r.PreferredDestination(1) != hostile, "The opponent must not travel to hostile ground with nothing ready to fight");
Check(!r.DestinationChoices(0).Contains(far), "Far Hold offered before any Hills land");
o.Field.Add(new MatchRules.Unit { Card = Card("Hills", "Land"), Owner = 1 });
Check(r.DestinationChoices(0).Contains(far), "A land the opponent holds must open its region for the player too");
return "PASS: settlement pool, land-gated choices, one journey per turn with its draw, one destination on the field, demand scoring, arrival and entering with a ready unit, destination-gated characters/encounters/objects, undo, persistence across turns, foreign choices and opponent travel preference.";
