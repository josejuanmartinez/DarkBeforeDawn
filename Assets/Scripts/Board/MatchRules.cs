using System;
using System.Collections.Generic;
using System.Linq;

public enum MatchStage { Draw = 1, Realm, Destination, Travel, Arrival, Muster, Events, Spoils }
/// <summary>Within Travel, at each stop: the other company declares its attacks, then the traveller assigns defenders.</summary>
public enum TravelPhase { Attack, Defend }
/// <summary>How a settlement receives a company: its own, or of its side (Friendly); of no side (Neutral); of the other side (Hostile).</summary>
public enum Standing { Friendly, Neutral, Hostile }
/// <summary>How a duel ends for its loser: the winner's margin against the loser's defense.</summary>
public enum Blow { None, Tapped, Wounded, Killed }
/// <summary>What each stop of a journey yields, in order. Stop five is a character while the deck still has one, else another army.</summary>
public enum TravelReward { Land, EventOrAction, Encounter, Army, Character }

/// <summary>Match state independent of Unity views. Card instances retain identity through every zone.</summary>
public sealed class MatchRules
{
    public sealed class Unit
    {
        public CardData Card;
        public int Owner, EnteredTurn;
        public bool Tapped;
        // A character that took lethal damage: out of action until healed. A healed character comes
        // back tapped and misses its next untap (Recovering), so it cannot act the turn after either.
        public bool Wounded, Recovering;
        // Settlement only. Secured: the company is allowed in, because the place is its own or the
        // dwellers were beaten. A secured settlement that is Tapped has been *entered* this turn: a
        // ready unit walked in (and tapped for it), and the company may play there until the turn
        // ends. An unsecured tapped settlement is closed: the dwellers held. Garrison: the unit that
        // entered, who answers when both companies end up in the same town.
        public bool Secured;
        public Unit Garrison;
        public bool Entered => Secured && Tapped;
        // A card that struck from the hand during the other company's travel. It stands on the field
        // only for that stop's fight, then goes back into the deck whatever happened to it.
        public bool Roadside;
        public readonly List<CardData> Objects = new();
        public bool IsCharacter => Card.GetCardType() == CardTypeEnum.Character;
        public bool IsCombatant => IsCharacter || Card.GetCardType() == CardTypeEnum.Army;
        public bool IsReady => IsCombatant && !Tapped && !Wounded;
    }
    public sealed class Player
    {
        public int Life = 20, HandLimit = 5;
        public int Alignment = CardData.NeutralAlignment;
        public readonly List<CardData> Deck = new(), Hand = new(), Discard = new();
        // Population centres are never drawn: the deck's own (Settlements, the company's homes) are on
        // the table from the first turn, and one of them is picked as the turn's destination once its
        // land has been played. Foreign holds every other settlement in the world: reachable the same
        // way, but held by its dwellers until the company earns its way in.
        public readonly List<CardData> Settlements = new(), Foreign = new();
        public readonly List<Unit> Field = new();
        public readonly PlayerMaterials Mana = new();
        /// <summary>The settlement in play this turn. Also in Field, so it taps and untaps like any unit.</summary>
        public Unit Destination;
        /// <summary>Where the company is bound this turn, chosen in Select Destination and reached at the end of Travel.</summary>
        public CardData Bound;
        /// <summary>One journey per turn: the choice cannot be re-picked within the stage.</summary>
        public bool Travelled;
        /// <summary>The company's champion, the avatar card. Leads the way into a town when no unit is ready, and takes the dwellers' blows then.</summary>
        public CardData Avatar;
    }
    /// <summary>
    /// A turn's journey: the regions entered, one stop at a time. At each stop the company draws the
    /// stop's card, then the other company may fall on it with characters and with armies of the
    /// region's terrain, and it defends under the same rule. Staying is a journey of one stop, the
    /// region the company is already in.
    /// </summary>
    public sealed class Journey
    {
        public CardData Destination;
        public bool Moving;
        public readonly List<string> Stops = new();
        public int Stop;
        public string Region => Stops[Math.Clamp(Stop, 0, Stops.Count - 1)];
        public bool Last => Stop >= Stops.Count - 1;
    }
    public sealed class Strike
    {
        public Unit Attacker, Target;
        // Taken as the attack or block is declared: attacking and blocking tap the unit, and that tap
        // is not the -1/-1 a unit fighting while already tapped suffers.
        public (int attack, int defense) Stats;
        public readonly List<Unit> Blockers = new();
        public readonly Dictionary<Unit, (int attack, int defense)> BlockerStats = new();
        public readonly HashSet<Unit> StoodFast = new();
    }
    public sealed class Loot { public CardData Card; public int Owner; public bool Offered; }
    /// <summary>
    /// One duel: each side adds a die to its attack; the higher total wins and deals the difference
    /// to the loser. Over the loser's defense kills, equal wounds, under merely taps. A tie does nothing.
    /// </summary>
    public sealed class Fight
    {
        public Unit Attacker, Defender;
        public (int attack, int defense) AttackerStats, DefenderStats;
        public int AttackerRoll, DefenderRoll;
        public int AttackerTotal => AttackerStats.attack + AttackerRoll;
        public int DefenderTotal => DefenderStats.attack + DefenderRoll;
        public Unit Loser;
        public Blow Blow;
        public int Margin => Math.Abs(AttackerTotal - DefenderTotal);
        public override string ToString()
        {
            string line = Attacker.Card.name + " " + AttackerStats.attack + "+" + AttackerRoll + "=" + AttackerTotal + " vs " + Defender.Card.name + " " + DefenderStats.attack + "+" + DefenderRoll + "=" + DefenderTotal + ": ";
            if (Loser == null) return line + "a stand-off.";
            var stats = Loser == Attacker ? AttackerStats : DefenderStats;
            return line + Loser.Card.name + " takes " + Margin + " against defense " + stats.defense + " — " + (Blow == Blow.Killed ? (Loser.IsCharacter ? "struck down, wounded." : "killed.") : Blow == Blow.Wounded ? "wounded." : "tapped.");
        }
    }
    /// <summary>The duels of the last resolution (a stop's fight, a dwellers fight, an ambush or a meeting).</summary>
    public readonly List<Fight> Fights = new();
    /// <summary>What a fight was about: a stop on the road, a town's dwellers, an ambush at a tap, or two companies meeting.</summary>
    public enum BattleKind { Road, Dwellers, Ambush, Meeting }
    /// <summary>One attacker and everything that stood against it: its blockers and the duels, or the blow that went through.</summary>
    public sealed class Clash
    {
        public Unit Attacker;
        public Unit Target;
        public readonly List<Unit> Blockers = new();
        public readonly HashSet<Unit> StoodFast = new();
        public readonly List<Fight> Fights = new();
        /// <summary>Damage that landed on the travelling company itself, no one having stood in the way.</summary>
        public int Unblocked;
        public bool FromHand => Attacker != null && Attacker.Roadside;
    }
    /// <summary>
    /// A whole resolution in one readable record, for the combat screen: where it happened, who
    /// fell on whom, and how every duel went. Fights holds the same duels flat.
    /// </summary>
    public sealed class Battle
    {
        public BattleKind Kind;
        /// <summary>The company whose turn it is; on the road, the one travelling.</summary>
        public int Player;
        public string Region;
        public CardData Settlement;
        public TerrainEnum Ground;
        public int Stop, Stops;
        public bool Neutral;
        /// <summary>The company had nothing ready, so its champion led the way (a stand-in when the avatar is not on the field).</summary>
        public bool Led;
        public readonly List<Clash> Clashes = new();
        public int Serial;
        public Battle Add(Clash clash) { Clashes.Add(clash); return this; }
        public IEnumerable<Fight> AllFights => Clashes.SelectMany(c => c.Fights);
    }
    /// <summary>The last resolution, for presentation. Serial climbs with each so a screen can tell a new one from the last.</summary>
    public Battle LastBattle { get; private set; }
    int battles;
    void Report(Battle battle) { battle.Serial = ++battles; LastBattle = battle; }
    /// <summary>A settlement being tapped while the other player holds a card born there: they may answer before play goes on.</summary>
    public sealed class Ambush
    {
        public int Tapper, Defender;
        public CardData Settlement;
        /// <summary>The tapper's unit that acted there, if any; the one an ambushing character strikes.</summary>
        public Unit Target;
        public readonly List<CardData> Options = new();
    }
    // Everything a play touched, so it can be put back. Cleared at every stage change: once the
    // stage is passed there is no way back.
    sealed class Played { public CardData Card; public int HandIndex; public Unit Unit, Recipient; public PlayerMaterials Spent; public bool Discarded; }
    readonly Stack<Played> played = new();
    readonly Random random = new();
    public readonly Player[] Players = { new(), new() };
    public readonly List<Strike> Attacks = new();
    public readonly Queue<Loot> Spoils = new();
    public int Active { get; private set; }
    public int Turn { get; private set; }
    public int Winner { get; private set; } = -1;
    public MatchStage Stage { get; private set; }
    public string Message { get; private set; }
    public Ambush PendingAmbush { get; private set; }
    /// <summary>The journey under way while the stage is Travel.</summary>
    public Journey Travel { get; private set; }
    public TravelPhase Phase { get; private set; }
    /// <summary>The company being attacked on the road is the active player's; its foe declares the attacks.</summary>
    public int Attacker => 1 - Active;
    /// <summary>The ground at the current stop, which decides which armies can fight there.</summary>
    public TerrainEnum Ground => Travel != null ? TerrainOf(Travel.Region) : TerrainEnum.None;
    /// <summary>Which regions border which, and their terrain; null means every journey is one stop on unknown ground.</summary>
    public RegionMap Map;
    /// <summary>The terrain of a region. Defaults to the map; replace to test without one.</summary>
    public Func<string, TerrainEnum> ResolveTerrain;
    public TerrainEnum TerrainOf(string region) => ResolveTerrain != null ? ResolveTerrain(region) : Map?.TerrainOf(region) ?? TerrainEnum.None;
    public event Action<CardData, int> Drawn;
    // Explicit extension seams for future card abilities; no inference from flavour text.
    public Func<Unit, bool> CanChooseTarget = u => u.Card.HasTag("ChooseTarget");
    public Action<CardData, int, MatchRules> ResolveEvent;
    // An encounter's outcome. Left null, facing it simply spends the card: the face promises
    // nothing about what happens, so an unauthored outcome is not a blocked play.
    public Action<CardData, int, MatchRules> ResolveEncounter;
    /// <summary>The army holding a settlement, from its `dwellers` name. Replace to test without the catalog.</summary>
    public Func<CardData, CardData> ResolveDwellers = pc => CardCatalog.FindCardByName(pc.dwellers);
    /// <summary>An index below the given count. Replace for deterministic tests.</summary>
    public Func<int, int> Pick;
    /// <summary>One six-sided die. Replace for deterministic tests.</summary>
    public Func<int> Roll;
    public MatchRules() { Pick = count => random.Next(count); Roll = () => random.Next(1, 7); }
    public void Begin(int first) { Turn = 0; Winner = -1; PendingAmbush = null; StartTurn(first); }
    void StartTurn(int player)
    {
        Active = player; Turn++; Stage = MatchStage.Draw; Attacks.Clear(); played.Clear(); PendingAmbush = null; Travel = null;
        var current = Players[player];
        foreach (var u in current.Field)
        {
            if (u.Wounded || u.Card.statusEffects.Contains(StatusEffects.Halted)) continue;
            // Healed last turn: stays tapped through this one, and readies normally after.
            if (u.Recovering) { u.Recovering = false; continue; }
            u.Tapped = false;
        }
        current.Travelled = false; current.Bound = null;
        if (current.Destination != null)
        {
            // Yesterday's fight does not hold the town: a company that stays somewhere it is not
            // welcome faces the dwellers again.
            current.Destination.Secured = StandingAt(player, current.Destination.Card) == Standing.Friendly;
            current.Destination.Garrison = null;
        }
        Draw(player, current.HandLimit - current.Hand.Count);
        Message = "Hand replenished. Halted and wounded cards remain tapped.";
    }
    void Draw(int player, int count)
    {
        var p = Players[player];
        for (int i = 0; i < count && p.Deck.Count > 0; i++)
        {
            var card = p.Deck[0]; p.Deck.RemoveAt(0); p.Hand.Add(card); Drawn?.Invoke(card, player);
        }
    }
    bool Reject(string message) { Message = message; return false; }
    static string Fraction((int attack, int defense) stats) => stats.attack + "/" + stats.defense;
    public bool CanPlay(CardData card) => PlayBlockReason(card) == null;
    public string PlayBlockReason(CardData card, Unit recipient = null, bool choosingRecipient = true, bool includeReadyMana = false)
    {
        var p = Players[Active];
        if (PendingAmbush != null) return "The ambush at " + PendingAmbush.Settlement.name + " must be answered first.";
        if (card == null || Winner >= 0 || !p.Hand.Contains(card)) return "That card is not in the active hand.";
        var type = card.GetCardType();
        if (type == CardTypeEnum.PC) return "Settlements are chosen as your destination, not played from the hand.";
        bool realm = type == CardTypeEnum.Land || type == CardTypeEnum.Environmental;
        bool muster = type == CardTypeEnum.Character || type == CardTypeEnum.Army || type == CardTypeEnum.Object || type == CardTypeEnum.Encounter;
        if (type == CardTypeEnum.Action || type == CardTypeEnum.Spell)
            return (type == CardTypeEnum.Action ? "Actions" : "Spells") + " have no stage in this prototype yet: set it aside during Muster.";
        // Arrival is for the gates only: a unit walks into the town (or fights for it), and the
        // recruiting waits for Muster. Nothing, armies included, is played from the hand there.
        if (Stage == MatchStage.Arrival && muster)
            return "Played during Muster" + (p.Destination != null && card.RequiresDestination() ? ", once a ready unit has entered " + p.Destination.Card.name : "") + ". Arrival is for walking into the town.";
        if (!(Stage == MatchStage.Realm && realm || Stage == MatchStage.Muster && muster || Stage == MatchStage.Events && type == CardTypeEnum.Event))
            return "This card cannot be played during " + Stage + ".";
        var destination = DestinationBlockReason(card);
        if (destination != null) return destination;
        if (type == CardTypeEnum.Object && !(recipient != null && recipient.IsCharacter && p.Field.Contains(recipient)) &&
            !(recipient == null && choosingRecipient && p.Field.Any(u => u.IsCharacter)))
            return "Select one of your characters to carry this object.";
        if (type == CardTypeEnum.Event && ResolveEvent == null)
            return "This event needs a registered effect before it can be played: set it aside during Muster.";
        if (p.Mana.CanAfford(card)) return null;
        // What the pool is short of, and whether the ready lands would make up the difference: the
        // face tells the player to tap (a click on the card gathers them itself, see GatherFor).
        var withLands = p.Mana.Copy();
        foreach (var land in p.Field.Where(CanTapLand)) withLands.Grant(land.Card);
        if (includeReadyMana && withLands.CanAfford(card)) return null;
        string short_ = p.Mana.Shortfall(card);
        if (withLands.CanAfford(card)) return "Needs " + short_ + " more: your ready lands can pay it (click the card to tap them and play it).";
        return "Not enough materials: " + short_ + " short" + (short_ != withLands.Shortfall(card) ? " (" + withLands.Shortfall(card) + " even with every ready land tapped)" : (p.Field.Any(CanTapLand) ? " (your ready lands would not make up the difference)" : "")) + ".";
    }
    /// <summary>
    /// Taps ready lands until the active pool can pay for a card, the lands that cover most of what
    /// is still owed first, and stops when the card is affordable or no ready land helps. Mana never
    /// goes to waste: every untapped land is tapped at the end of the turn anyway and the pool persists.
    /// </summary>
    public bool GatherFor(CardData card)
    {
        var p = Players[Active];
        if (card == null) return false;
        while (!p.Mana.CanAfford(card))
        {
            var land = p.Field.Where(CanTapLand).OrderByDescending(u => p.Mana.Help(u.Card, card)).FirstOrDefault();
            if (land == null || p.Mana.Help(land.Card, card) <= 0) break;
            TapLand(land);
        }
        return p.Mana.CanAfford(card);
    }
    /// <summary>Why the active destination cannot host this card, or null when it can (or the card does not care).</summary>
    public string DestinationBlockReason(CardData card)
    {
        if (card == null || !card.RequiresDestination()) return null;
        var destination = Players[Active].Destination;
        if (destination == null) return "Choose a destination first: " + DestinationWanted(card) + ".";
        if (!card.CanBePlayedAt(destination.Card)) return "Not playable at " + destination.Card.name + ". Needs " + DestinationWanted(card) + ".";
        if (!destination.Secured)
        {
            if (destination.Tapped) return destination.Card.name + " is closed to your company this turn: the dwellers held.";
            var dwellers = Dwellers(destination.Card);
            return destination.Card.name + " is held by its dwellers" + (dwellers != null ? " (" + dwellers.name + " " + Fraction(dwellers.GetCombatStats()) + ")" : "") + ": fight them with a ready unit first.";
        }
        if (!destination.Tapped) return destination.Card.name + " has not been entered: a ready character or army must walk in first.";
        return null;
    }
    static string DestinationWanted(CardData card) => card.GetCardType() == CardTypeEnum.Object
        ? "a settlement trading in " + CardData.FormatObjectTypeLabel(card.objectType)
        : CardData.JoinNames(card.GetBirthplaces().ToList());
    public bool Play(CardData card, Unit recipient = null)
    {
        var reason = PlayBlockReason(card, recipient, false);
        if (reason != null) return Reject(reason);
        var p = Players[Active]; var type = card.GetCardType();
        var before = p.Mana.Copy();
        if (!p.Mana.TrySpend(card)) return Reject("Not enough mana/materials.");
        var record = new Played { Card = card, HandIndex = p.Hand.IndexOf(card), Recipient = recipient, Spent = p.Mana.SpentSince(before) };
        p.Hand.Remove(card);
        if (type == CardTypeEnum.Object) recipient.Objects.Add(card);
        else if (type == CardTypeEnum.Event) { ResolveEvent(card, Active, this); p.Discard.Add(card); }
        else if (type == CardTypeEnum.Encounter) { ResolveEncounter?.Invoke(card, Active, this); p.Discard.Add(card); record.Discarded = true; }
        // A recruit spends the turn mustering: it enters tapped and readies with the next turn. Mounted
        // troops are the exception and can ride out at once.
        else
        {
            record.Unit = new Unit { Card = card, Owner = Active, EnteredTurn = Turn };
            record.Unit.Tapped = record.Unit.IsCombatant && !card.specialAbilities.Contains(ObjectCharacterArmySpecialAbilityEnum.Mounted);
            p.Field.Add(record.Unit);
        }
        Message = type == CardTypeEnum.Encounter ? card.name + " faced at " + p.Destination.Card.name + "." : card.name + " played.";
        // An event's effect is whatever its handler did and cannot be walked back, so nothing
        // played before it can be either. The same goes for an encounter with an authored outcome.
        bool irreversible = type == CardTypeEnum.Event || type == CardTypeEnum.Encounter && ResolveEncounter != null;
        if (irreversible) played.Clear(); else played.Push(record);
        return true;
    }
    public bool CanUndo => Winner < 0 && played.Count > 0 && PendingAmbush == null;
    public CardData LastPlayed => played.Count > 0 ? played.Peek().Card : null;
    /// <summary>Returns the last card played this stage to the hand and refunds what it cost.</summary>
    public bool Undo()
    {
        if (!CanUndo) return Reject("Nothing to take back this stage.");
        var record = played.Pop(); var p = Players[Active];
        if (record.Unit != null) p.Field.Remove(record.Unit);
        else if (record.Discarded) p.Discard.Remove(record.Card);
        else record.Recipient?.Objects.Remove(record.Card);
        p.Hand.Insert(Math.Clamp(record.HandIndex, 0, p.Hand.Count), record.Card);
        p.Mana.Refund(record.Spent);
        Message = record.Card.name + " returned to hand."; return true;
    }
    static bool Same(string a, string b) => !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    // --- Destinations ---------------------------------------------------------------------------------
    /// <summary>The region a land card opens: itself, unless the map says it is an alternate card for another.</summary>
    public string LandRegion(CardData land) => Map != null ? Map.RegionOfLand(land.name) : land.name;
    /// <summary>Every settlement a player knows of: the deck's own and the wider world.</summary>
    public IEnumerable<CardData> KnownSettlements(int player) => Players[player].Settlements.Concat(Players[player].Foreign);
    /// <summary>
    /// The settlements a player could travel to: any whose land is on the board, whether the player
    /// or the opponent holds it. A land opens its region for both companies.
    /// </summary>
    public IEnumerable<CardData> DestinationChoices(int player)
    {
        var regions = Players.SelectMany(p => p.Field).Where(u => u.Card.GetCardType() == CardTypeEnum.Land)
            .Select(u => LandRegion(u.Card)?.Trim()).Where(r => !string.IsNullOrEmpty(r)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return KnownSettlements(player).Where(pc => !string.IsNullOrWhiteSpace(pc.region) && regions.Contains(pc.region.Trim()));
    }
    /// <summary>
    /// The choices ranked for the picker, best first: the most hand cards playable there, then the
    /// warmest welcome (the company's own side before neutral before hostile), then the shortest
    /// road. Ties keep the pool's order so the pile does not shuffle between refreshes.
    /// </summary>
    public IEnumerable<CardData> RankedDestinations(int player) => DestinationChoices(player)
        .OrderByDescending(pc => DestinationDemand(player, pc)).ThenBy(pc => (int)StandingAt(player, pc)).ThenBy(pc => Stops(player, pc).Count);
    public bool IsDestination(int player, CardData pc) => pc != null && Players[player].Destination != null && ReferenceEquals(Players[player].Destination.Card, pc);
    /// <summary>A settlement of the company's own deck that it has claimed; the only kind that is safe whatever its side.</summary>
    public bool IsHome(int player, CardData pc) => pc != null && Players[player].Settlements.Any(s => ReferenceEquals(s, pc) || Same(s.name, pc.name));
    public Standing StandingAt(int player, CardData pc)
    {
        if (pc == null || IsHome(player, pc) || pc.settlementAlignment == Players[player].Alignment) return Standing.Friendly;
        return pc.IsNeutralSettlement() ? Standing.Neutral : Standing.Hostile;
    }
    public CardData Dwellers(CardData pc) => pc == null || string.IsNullOrWhiteSpace(pc.dwellers) ? null : ResolveDwellers?.Invoke(pc);
    /// <summary>Puts a company at a settlement before the first turn: each starts at a random one of its own.</summary>
    public void StartAt(int player, CardData pc)
    {
        var p = Players[player];
        if (pc == null) return;
        if (!p.Settlements.Contains(pc)) p.Settlements.Add(pc);
        if (p.Destination != null) p.Field.Remove(p.Destination);
        p.Destination = new Unit { Card = pc, Owner = player, EnteredTurn = Turn, Secured = true };
        p.Field.Add(p.Destination);
    }
    public bool CanChooseDestination(CardData pc) => Winner < 0 && Stage == MatchStage.Destination && pc != null && !Players[Active].Travelled &&
        !IsDestination(Active, pc) && DestinationChoices(Active).Contains(pc);
    // --- Journeys -------------------------------------------------------------------------------------
    /// <summary>
    /// The regions a company enters on its way to a settlement, one per stop: the shortest route on
    /// the map, or the destination's own region when it is the same, unknown, or there is no map.
    /// </summary>
    public List<string> Stops(int player, CardData pc)
    {
        var origin = Players[player].Destination?.Card.region;
        var route = Map != null && origin != null ? Map.Route(origin, pc.region) : null;
        return route == null || route.Count < 2 ? new List<string> { pc.region } : route.Skip(1).ToList();
    }
    /// <summary>How many rewards the journey pays: one per region entered, at least one and at most five.</summary>
    public int Rewards(int player, CardData pc) => RegionMap.Journey(Stops(player, pc).Count);
    public static TravelReward RewardAt(int stop) => (TravelReward)Math.Clamp(stop - 1, 0, 4);
    public static string RewardLabel(TravelReward reward) => reward switch
    {
        TravelReward.Land => "Land", TravelReward.EventOrAction => "Event or Action",
        TravelReward.Encounter => "Encounter", TravelReward.Army => "Army", _ => "Character"
    };
    static readonly CardTypeEnum[][] rewardTypes =
    {
        new[] { CardTypeEnum.Land }, new[] { CardTypeEnum.Event, CardTypeEnum.Action },
        new[] { CardTypeEnum.Encounter }, new[] { CardTypeEnum.Army }, new[] { CardTypeEnum.Character }
    };
    /// <summary>Draws one reward: a random card of the stop's kinds still in the deck, else the top card.</summary>
    CardData Claim(int player, TravelReward reward)
    {
        var p = Players[player];
        var types = rewardTypes[(int)reward];
        var pool = p.Deck.Where(c => types.Contains(c.GetCardType())).ToList();
        if (pool.Count == 0 && reward == TravelReward.Character) pool = p.Deck.Where(c => c.GetCardType() == CardTypeEnum.Army).ToList();
        var card = pool.Count > 0 ? pool[Math.Clamp(Pick(pool.Count), 0, pool.Count - 1)] : p.Deck.FirstOrDefault();
        if (card == null) return null;
        p.Deck.Remove(card); p.Hand.Add(card); Drawn?.Invoke(card, player);
        return card;
    }
    /// <summary>Sets out for a settlement: the road is walked, stop by stop, in the Travel stage. Staying is simply advancing the stage.</summary>
    public bool ChooseDestination(CardData pc)
    {
        if (!CanChooseDestination(pc)) return Reject(Players[Active].Travelled ? "The company has already chosen its road this turn." : "Choose a settlement whose land you have played, or stay where you are.");
        var p = Players[Active];
        int journey = Rewards(Active, pc);
        p.Bound = pc; p.Travelled = true;
        Message = "Bound for " + pc.name + ": " + journey + (journey == 1 ? " stop" : " stops") + " on the road.";
        return true;
    }
    // --- Travel ---------------------------------------------------------------------------------------
    /// <summary>Leaving Select Destination: lay out the road. A company with nowhere to be skips the stage.</summary>
    void BeginTravel()
    {
        var p = Players[Active];
        var target = p.Bound ?? p.Destination?.Card;
        if (target == null) { Travel = null; Stage = MatchStage.Muster; Message = "No settlement to travel to."; return; }
        bool moving = p.Bound != null && !IsDestination(Active, p.Bound);
        Travel = new Journey { Destination = target, Moving = moving, Stop = -1 };
        Travel.Stops.AddRange(moving ? Stops(Active, target) : new List<string> { p.Destination.Card.region });
        NextStop();
    }
    /// <summary>Enter the next region: draw its card, then open the floor to the other company's attacks.</summary>
    void NextStop()
    {
        Travel.Stop++; Attacks.Clear(); Phase = TravelPhase.Attack;
        int stop = Travel.Stop + 1;
        var drawn = stop <= RegionMap.MaxDistance ? Claim(Active, RewardAt(stop)) : null;
        Message = (Travel.Moving ? "On the road to " + Travel.Destination.name : "Holding at " + Travel.Destination.name) + ": stop " + stop + " of " + Travel.Stops.Count + ", " + Travel.Region
            + (Ground != TerrainEnum.None ? " (" + Ground + ")" : "") + "." + (drawn != null ? " Drew " + drawn.name + "." : "");
    }
    /// <summary>The road's end: the company takes up its new destination, welcome or not.</summary>
    void Arrive()
    {
        var p = Players[Active];
        if (Travel != null && Travel.Moving)
        {
            if (p.Destination != null) p.Field.Remove(p.Destination);
            p.Destination = new Unit { Card = Travel.Destination, Owner = Active, EnteredTurn = Turn, Secured = StandingAt(Active, Travel.Destination) == Standing.Friendly };
            p.Field.Add(p.Destination);
            Message = "Arrived at " + Travel.Destination.name + (p.Destination.Secured ? "." : ": its dwellers hold it.");
        }
        else Message = "The company holds at " + (p.Destination?.Card.name ?? "no settlement") + ".";
        p.Bound = null; Travel = null;
    }
    /// <summary>The other company's units that could fall on the traveller at this stop.</summary>
    public IEnumerable<Unit> Raiders() => Stage == MatchStage.Travel ? Players[Attacker].Field.Where(CanAttack) : Enumerable.Empty<Unit>();
    /// <summary>The cards in the raider's hand that could strike at this stop.</summary>
    public IEnumerable<CardData> HandRaiders() => Stage == MatchStage.Travel ? Players[Attacker].Hand.Where(CanAttackWith) : Enumerable.Empty<CardData>();
    /// <summary>How many cards in a player's hand could be played at a settlement. What the opponent travels by.</summary>
    public int DestinationDemand(int player, CardData pc) => Players[player].Hand.Count(c => c.RequiresDestination() && c.CanBePlayedAt(pc));
    /// <summary>Hand cards that could be played at a settlement were it the destination.</summary>
    public IEnumerable<CardData> PlayableAt(int player, CardData pc) => Players[player].Hand.Where(c => c.RequiresDestination() && c.CanBePlayedAt(pc));
    /// <summary>
    /// The opponent's pick: the most hand cards waiting, then the warmest welcome, then the longest
    /// journey; anywhere it would have to fight needs a unit ready to fight. Null to stay.
    /// </summary>
    public CardData PreferredDestination(int player)
    {
        var p = Players[player];
        var current = p.Destination?.Card;
        bool canFight = p.Field.Any(u => u.IsReady);
        int Score(CardData pc, bool travel)
        {
            var standing = StandingAt(player, pc);
            if (standing != Standing.Friendly && !canFight) return int.MinValue;
            return DestinationDemand(player, pc) * 4 + (standing == Standing.Friendly ? 3 : standing == Standing.Neutral ? 1 : -2) + (travel ? Rewards(player, pc) : 0);
        }
        CardData best = null; int bestScore = current != null ? Score(current, false) : -1;
        foreach (var pc in DestinationChoices(player))
        {
            if (ReferenceEquals(pc, current)) continue;
            int score = Score(pc, true);
            if (score > bestScore) { best = pc; bestScore = score; }
        }
        return best;
    }
    // --- Dwellers, ambushes and meetings ---------------------------------------------------------------
    /// <summary>Combat values as a unit fights now: a unit already tapped when it fights acts at -1/-1.</summary>
    public (int attack, int defense) Stats(Unit unit)
    {
        var (attack, defense) = unit.Card.GetCombatStats();
        if (unit.Tapped) { attack--; defense--; }
        return (Math.Max(0, attack), Math.Max(0, defense));
    }
    /// <summary>
    /// Rolls a duel and, unless told otherwise, lands the blow on the loser. The dwellers of a town are
    /// a unit off the field, so a blow to them is nothing; a hand raider rides back into the deck
    /// whatever happens to it.
    /// </summary>
    Fight Duel(Unit attacker, (int attack, int defense) attackerStats, Unit defender, (int attack, int defense) defenderStats, bool apply = true)
    {
        var fight = new Fight { Attacker = attacker, Defender = defender, AttackerStats = attackerStats, DefenderStats = defenderStats, AttackerRoll = Roll(), DefenderRoll = Roll() };
        if (fight.AttackerTotal != fight.DefenderTotal)
        {
            bool attackerWins = fight.AttackerTotal > fight.DefenderTotal;
            fight.Loser = attackerWins ? defender : attacker;
            int defense = (attackerWins ? defenderStats : attackerStats).defense;
            fight.Blow = fight.Margin > defense ? Blow.Killed : fight.Margin == defense ? Blow.Wounded : Blow.Tapped;
            if (apply) Land(fight.Loser, fight.Blow);
        }
        Fights.Add(fight);
        return fight;
    }
    /// <summary>Lands a blow. Armies can be killed; characters never die, a killing blow wounds them.</summary>
    void Land(Unit unit, Blow blow)
    {
        if (unit == null || unit.Roadside || !Players[unit.Owner].Field.Contains(unit)) return;
        if (blow == Blow.Killed && !unit.IsCharacter) { Fell(unit); return; }
        if (blow == Blow.Killed || blow == Blow.Wounded) unit.Wounded = true;
        if (blow != Blow.None) unit.Tapped = true;
    }
    void Fell(Unit unit)
    {
        var owner = Players[unit.Owner];
        owner.Field.Remove(unit); owner.Discard.Add(unit.Card);
        foreach (var item in unit.Objects) Spoils.Enqueue(new Loot { Card = item, Owner = unit.Owner });
        unit.Objects.Clear();
        if (owner.Destination != null && owner.Destination.Garrison == unit) owner.Destination.Garrison = null;
    }
    /// <summary>The stages in which a company deals with the town it stands at: Arrival, and Muster as the fallback.</summary>
    bool AtTheGates => Stage == MatchStage.Arrival || Stage == MatchStage.Muster;
    /// <summary>No character or army of the company stands ready: the champion has to lead the way into town.</summary>
    public bool NoneReady(int player) => !Players[player].Field.Any(u => u.IsReady);
    /// <summary>
    /// Who leads a company that has no ready unit: its champion on the field (tapped or not, but not
    /// wounded), else a stand-in built from the avatar card, whose blows land on the company's life.
    /// Null when the company has no champion at all.
    /// </summary>
    public Unit Leader(int player)
    {
        var p = Players[player];
        if (p.Avatar == null) return null;
        return p.Field.FirstOrDefault(u => u.IsCharacter && !u.Wounded && u.Card.cardId == p.Avatar.cardId && u.Card.name == p.Avatar.name)
            ?? new Unit { Card = p.Avatar, Owner = player };
    }
    /// <summary>Whether a unit is the company's champion standing on the field.</summary>
    public bool IsChampionUnit(Unit unit) => unit != null && Players[unit.Owner].Field.Contains(unit) && Players[unit.Owner].Avatar != null
        && unit.Card.cardId == Players[unit.Owner].Avatar.cardId && unit.Card.name == Players[unit.Owner].Avatar.name;
    /// <summary>A null unit asks for the champion to lead: allowed only when nothing else is ready.</summary>
    string LeaderBlockReason(Unit unit, string verb)
    {
        var p = Players[Active];
        if (unit == null)
        {
            if (!NoneReady(Active)) return "Choose a ready character or army to " + verb + ".";
            return null;
        }
        if (unit.Owner != Active || !p.Field.Contains(unit) || !unit.IsReady) return "Choose a ready character or army to " + verb + ".";
        return null;
    }
    public bool CanSecure(Unit unit) => SecureBlockReason(unit) == null;
    public string SecureBlockReason(Unit unit)
    {
        var p = Players[Active];
        if (PendingAmbush != null) return "The ambush at " + PendingAmbush.Settlement.name + " must be answered first.";
        if (Winner >= 0 || !AtTheGates) return "Choose a ready character or army to face the dwellers.";
        var lead = LeaderBlockReason(unit, "face the dwellers");
        if (lead != null) return lead;
        if (unit == null && Leader(Active) == null) return "No one can face the dwellers: the company has neither a ready unit nor a champion.";
        var destination = p.Destination;
        if (destination == null || destination.Secured) return "Nothing to secure at the destination.";
        if (destination.Tapped) return destination.Card.name + " is closed to your company this turn.";
        return null;
    }
    /// <summary>
    /// Fights the destination's dwellers to be allowed in. Neutral ground is a retention attack:
    /// losing taps the unit and closes the town for the turn, and no one bleeds. Hostile ground is a
    /// normal attack: the dwellers strike back and the unit taps. Winning either way, the unit walks
    /// in: the town is entered, as by Enter.
    /// </summary>
    public bool Secure(Unit unit)
    {
        var reason = SecureBlockReason(unit);
        if (reason != null) return Reject(reason);
        var p = Players[Active]; var destination = p.Destination;
        played.Clear();
        // With nothing ready the champion leads: on the field if played, else a stand-in whose
        // wounds are the company's own.
        bool led = unit == null;
        if (led) unit = Leader(Active);
        bool standIn = !p.Field.Contains(unit);
        var dwellers = Dwellers(destination.Card);
        if (dwellers == null)
        {
            destination.Secured = true;
            Message = destination.Card.name + " is unguarded."; Walk(standIn ? null : unit); return true;
        }
        Fights.Clear();
        var garrison = new Unit { Card = dwellers, Owner = 1 - Active };
        bool neutral = StandingAt(Active, destination.Card) == Standing.Neutral;
        // On neutral ground the dwellers only bar the gate: the duel decides, but no blow lands.
        var fight = Duel(unit, Stats(unit), garrison, dwellers.GetCombatStats(), apply: !neutral);
        bool won = fight.Loser == garrison;
        string fought = fight + " At " + destination.Card.name + ": ";
        Report(new Battle { Kind = BattleKind.Dwellers, Player = Active, Settlement = destination.Card, Region = destination.Card.region, Neutral = neutral, Led = led }
            .Add(new Clash { Attacker = unit, Blockers = { garrison }, Fights = { fight } }));
        if (neutral && !won)
        {
            unit.Tapped = true; destination.Tapped = true;
            Message = fought + "held back. " + unit.Card.name + " and " + destination.Card.name + " are tapped for the turn."; return true;
        }
        if (!neutral) unit.Tapped = true;
        // A stand-in champion cannot be wounded, so the blow that would have wounded it strikes the company.
        if (!neutral && !won && standIn && fight.Blow != Blow.None)
        {
            p.Life -= fight.Margin; CheckWinner();
            fought += "The champion is not on the field: the company takes " + fight.Margin + ". ";
        }
        if (!won) { destination.Tapped = true; Message = fought + destination.Card.name + " is closed this turn."; return true; }
        destination.Secured = true;
        Message = fought + (neutral ? "the dwellers stand aside." : "the dwellers are driven off.");
        // The winner walks in, if it is still on its feet.
        if (standIn) Walk(null);
        else if (p.Field.Contains(unit) && !unit.Wounded) Walk(unit);
        return true;
    }
    /// <summary>A destination the company is welcome at (or has beaten its way into) but has not walked into yet.</summary>
    public bool CanEnter(Unit unit) => EnterBlockReason(unit) == null;
    public string EnterBlockReason(Unit unit)
    {
        var p = Players[Active];
        if (PendingAmbush != null) return "The ambush at " + PendingAmbush.Settlement.name + " must be answered first.";
        if (Winner >= 0 || !AtTheGates) return "Choose a ready character or army to enter the settlement.";
        var lead = LeaderBlockReason(unit, "enter the settlement");
        if (lead != null) return lead;
        var destination = p.Destination;
        if (destination == null) return "The company is at no settlement.";
        if (!destination.Secured) return destination.Card.name + " is held by its dwellers: fight them first.";
        if (destination.Tapped) return destination.Card.name + " has already been entered this turn.";
        return null;
    }
    /// <summary>
    /// A ready unit walks into the destination: it taps, the town taps (entered), and for the rest of
    /// the turn characters, encounters and objects can be played there. Entering is the moment the
    /// other player may answer with a card born there, and the moment two companies in one town
    /// come to blows, so it cannot be taken back.
    /// </summary>
    public bool Enter(Unit unit)
    {
        var reason = EnterBlockReason(unit);
        if (reason != null) return Reject(reason);
        played.Clear();
        var p = Players[Active];
        if (unit == null)
        {
            // Nothing ready: the champion leads the company in. On the field it garrisons the town.
            var leader = Leader(Active);
            var champion = leader != null && p.Field.Contains(leader) ? leader : null;
            Message = (leader != null ? leader.Card.name + " leads the company into " : "The company enters ") + p.Destination.Card.name + ".";
            Walk(champion);
            return true;
        }
        Message = unit.Card.name + " enters " + p.Destination.Card.name + ".";
        Walk(unit);
        return true;
    }
    /// <summary>The company needs a unit to walk into its destination before it can play the cards waiting for it.</summary>
    public bool NeedsEntering()
    {
        var d = Players[Active].Destination;
        return d != null && d.Secured && !d.Tapped && PlayableAt(Active, d.Card).Any();
    }
    void Walk(Unit unit)
    {
        if (unit != null) unit.Tapped = true;
        TapDestination(unit);
    }
    /// <summary>
    /// Entering the destination taps it. That is the moment the other player may answer with a card
    /// born there, and the moment two companies in one town come to blows. True when something
    /// happened that a later Undo could not put back.
    /// </summary>
    bool TapDestination(Unit acting)
    {
        var p = Players[Active]; var destination = p.Destination;
        destination.Tapped = true;
        if (acting != null) destination.Garrison = acting;
        var options = Players[1 - Active].Hand.Where(c => (c.GetCardType() == CardTypeEnum.Character || c.GetCardType() == CardTypeEnum.Encounter) && c.IsBornAt(destination.Card.name)).ToList();
        if (options.Count > 0)
        {
            PendingAmbush = new Ambush { Tapper = Active, Defender = 1 - Active, Settlement = destination.Card, Target = acting ?? destination.Garrison };
            PendingAmbush.Options.AddRange(options);
            Message += " The other company has " + (options.Count == 1 ? "a card" : options.Count + " cards") + " born at " + destination.Card.name + " and may answer.";
            return true;
        }
        return Meet();
    }
    public bool CanAmbush(CardData card) => PendingAmbush != null && card != null && PendingAmbush.Options.Contains(card) && Players[PendingAmbush.Defender].Hand.Contains(card);
    /// <summary>
    /// The defender answers a tap. A character born there was home all along: it enters the field for
    /// nothing, strikes the unit that acted (or the tapper's weakest unit) and taps for the effort. An
    /// encounter born there is sprung on the tapper through ResolveEncounter and spent.
    /// </summary>
    public bool AmbushWith(CardData card)
    {
        if (!CanAmbush(card)) return Reject(PendingAmbush == null ? "No ambush is pending." : "Choose one of your cards born at " + PendingAmbush.Settlement.name + ".");
        var ambush = PendingAmbush; var defender = Players[ambush.Defender]; var tapper = Players[ambush.Tapper];
        defender.Hand.Remove(card);
        if (card.GetCardType() == CardTypeEnum.Encounter)
        {
            defender.Discard.Add(card);
            ResolveEncounter?.Invoke(card, ambush.Tapper, this);
            Message = card.name + " is sprung on the company at " + ambush.Settlement.name + ".";
        }
        else
        {
            var unit = new Unit { Card = card, Owner = ambush.Defender, EnteredTurn = Turn };
            defender.Field.Add(unit);
            var target = ambush.Target != null && tapper.Field.Contains(ambush.Target) && !ambush.Target.Wounded ? ambush.Target
                : tapper.Field.Where(u => u.IsCombatant && !u.Wounded).OrderBy(u => Stats(u).defense).FirstOrDefault();
            if (target == null) Message = card.name + " rises at " + ambush.Settlement.name + " but finds no one to strike.";
            else
            {
                Fights.Clear();
                var fight = Duel(unit, Stats(unit), target, Stats(target));
                Message = card.name + " ambushes " + target.Card.name + " at " + ambush.Settlement.name + ". " + fight;
                Report(new Battle { Kind = BattleKind.Ambush, Player = ambush.Defender, Settlement = ambush.Settlement, Region = ambush.Settlement.region }
                    .Add(new Clash { Attacker = unit, Target = target, Blockers = { target }, Fights = { fight } }));
            }
            unit.Tapped = true;
        }
        PendingAmbush = null; Meet(); return true;
    }
    public bool DeclineAmbush()
    {
        if (PendingAmbush == null) return false;
        Message = "The company at " + PendingAmbush.Settlement.name + " goes unchallenged.";
        PendingAmbush = null; Meet(); return true;
    }
    /// <summary>Who answers for a company when the two meet: its garrison at the town, else its hardest hitter.</summary>
    public Unit Champion(int player)
    {
        var p = Players[player];
        var garrison = p.Destination?.Garrison;
        if (garrison != null && p.Field.Contains(garrison) && !garrison.Wounded) return garrison;
        return p.Field.Where(u => u.IsCombatant && !u.Wounded).OrderByDescending(u => Stats(u).attack).ThenByDescending(u => Stats(u).defense).FirstOrDefault();
    }
    /// <summary>Both companies at one settlement, both having acted there: their champions trade blows at once.</summary>
    bool Meet()
    {
        var mine = Players[Active].Destination; var theirs = Players[1 - Active].Destination;
        if (mine == null || theirs == null || !mine.Tapped || !theirs.Tapped || !Same(mine.Card.name, theirs.Card.name)) return false;
        var ours = Champion(Active); var rival = Champion(1 - Active);
        if (ours == null || rival == null) return false;
        Fights.Clear();
        var fight = Duel(ours, Stats(ours), rival, Stats(rival));
        Message = (Message + " Both companies hold " + mine.Card.name + ". " + fight).Trim();
        Report(new Battle { Kind = BattleKind.Meeting, Player = Active, Settlement = mine.Card, Region = mine.Card.region }
            .Add(new Clash { Attacker = ours, Target = rival, Blockers = { rival }, Fights = { fight } }));
        return true;
    }
    // --- Environments -----------------------------------------------------------------------------------
    /// <summary>The environmental cards in play, whichever company laid them down.</summary>
    public IEnumerable<Unit> Environments() => Players.SelectMany(p => p.Field).Where(u => u.Card.GetCardType() == CardTypeEnum.Environmental);
    /// <summary>The side a unit answers to for the weather: its company's. (A card's own stamp defaults to 0, which is also Free People, so it cannot be trusted.)</summary>
    public int AlignmentOf(Unit unit) => Players[unit.Owner].Alignment;
    /// <summary>The environments whose text names this unit's side with anything but "unaffected". Characters and armies only.</summary>
    public IEnumerable<Unit> EnvironmentsAffecting(Unit unit)
    {
        if (unit == null || !unit.IsCombatant) return Enumerable.Empty<Unit>();
        int side = AlignmentOf(unit);
        return Environments().Where(e => e.Card.EnvironmentEffectFor(side) != null);
    }
    // --- Mana, attacks and defence ----------------------------------------------------------------------
    public bool CanTapLand(Unit unit) => unit != null && Winner < 0 && PendingAmbush == null && (Stage == MatchStage.Muster || Stage == MatchStage.Events) && unit.Owner == Active &&
        !unit.Tapped && Players[Active].Field.Contains(unit) && unit.Card.GetCardType() == CardTypeEnum.Land;
    public bool TapLand(Unit unit)
    {
        if (!CanTapLand(unit))
            return Reject("Tap your ready lands during Muster or Events to produce mana.");
        unit.Tapped = true; Players[Active].Mana.Grant(unit.Card); Message = unit.Card.name + " produced mana."; return true;
    }
    /// <summary>A unit of the travelling company that an attack can single out.</summary>
    public bool IsAttackTarget(Unit target) => target != null && target.Owner == Active && target.IsCombatant && Players[Active].Field.Contains(target);
    /// <summary>Whether a unit can fight at the current stop: characters anywhere, armies on their own ground.</summary>
    public bool FightsHere(Unit unit) => unit != null && unit.Card.FightsOn(Ground);
    public bool CanAttack(Unit unit) => AttackBlockReason(unit) == null;
    public string AttackBlockReason(Unit unit, Unit target = null, bool choosingTarget = true)
    {
        if (unit == null || Winner >= 0 || Stage != MatchStage.Travel || Phase != TravelPhase.Attack || unit.Owner != Attacker || !Players[Attacker].Field.Contains(unit) || !unit.IsReady || unit.Card.statusEffects.Contains(StatusEffects.Fear))
            return "Choose a ready character or army to fall on the travelling company.";
        if (!FightsHere(unit)) return unit.Card.name + " fights on " + unit.Card.GetTerrain() + " ground, not " + Ground + ".";
        if (CanChooseTarget(unit) && !IsAttackTarget(target) &&
            !(target == null && choosingTarget && Players[Active].Field.Any(IsAttackTarget)))
            return "Select a travelling character or army as the target.";
        if (!CanChooseTarget(unit) && target != null) return "This unit cannot choose a target.";
        return null;
    }
    public bool Attack(Unit unit, Unit target = null)
    {
        var reason = AttackBlockReason(unit, target, false);
        if (reason != null) return Reject(reason);
        var stats = Stats(unit);
        unit.Tapped = true; Attacks.Add(new Strike { Attacker = unit, Target = target, Stats = stats });
        Message = unit.Card.name + " attacks."; return true;
    }
    /// <summary>A card in the raider's hand that could fall on the company at this stop: an army of this ground, or any character.</summary>
    public bool CanAttackWith(CardData card) => AttackWithBlockReason(card) == null;
    public string AttackWithBlockReason(CardData card, Unit target = null, bool choosingTarget = true)
    {
        if (card == null || Winner >= 0 || Stage != MatchStage.Travel || Phase != TravelPhase.Attack || !Players[Attacker].Hand.Contains(card))
            return "Choose an army or character in your hand to fall on the travelling company.";
        var type = card.GetCardType();
        if (type != CardTypeEnum.Army && type != CardTypeEnum.Character) return "Only armies and characters strike from the hand.";
        if (!card.FightsOn(Ground)) return card.name + " fights on " + card.GetTerrain() + " ground, not " + Ground + ".";
        if (card.HasTag("ChooseTarget") && !IsAttackTarget(target) && !(target == null && choosingTarget && Players[Active].Field.Any(IsAttackTarget)))
            return "Select a travelling character or army as the target.";
        return null;
    }
    /// <summary>
    /// Strikes from the hand: the card takes the field for this stop's fight only, and afterwards is
    /// shuffled back into the deck, whatever the blows did to it. It costs the raider the card in hand.
    /// </summary>
    public bool AttackWith(CardData card, Unit target = null)
    {
        var reason = AttackWithBlockReason(card, target, false);
        if (reason != null) return Reject(reason);
        var raider = Players[Attacker];
        raider.Hand.Remove(card);
        var unit = new Unit { Card = card, Owner = Attacker, EnteredTurn = Turn, Roadside = true };
        raider.Field.Add(unit);
        Attacks.Add(new Strike { Attacker = unit, Target = target, Stats = Stats(unit) });
        unit.Tapped = true;
        Message = card.name + " strikes from the hand."; return true;
    }
    /// <summary>After a stop's fight the hand raiders leave the field and go back into the deck, which is reshuffled.</summary>
    void RecallRoadside()
    {
        foreach (var player in Players)
        {
            var roadside = player.Field.Where(u => u.Roadside).ToList();
            if (roadside.Count == 0) continue;
            foreach (var unit in roadside) { player.Field.Remove(unit); player.Deck.Add(unit.Card); }
            for (int n = player.Deck.Count - 1; n > 0; n--) { int j = Math.Clamp(Pick(n + 1), 0, n); (player.Deck[n], player.Deck[j]) = (player.Deck[j], player.Deck[n]); }
            Message += " " + string.Join(", ", roadside.Select(u => u.Card.name)) + (roadside.Count == 1 ? " rides" : " ride") + " back into the deck.";
        }
    }
    public bool CanBlock(Unit defender, Strike strike) => defender != null && Winner < 0 && Stage == MatchStage.Travel && Phase == TravelPhase.Defend &&
        Attacks.Contains(strike) && defender.Owner == Active && defender.IsReady && FightsHere(defender) && Players[Active].Field.Contains(defender);
    public string BlockBlockReason(Unit defender)
    {
        if (defender == null || Stage != MatchStage.Travel || Phase != TravelPhase.Defend || defender.Owner != Active || !defender.IsCombatant) return "Select an untapped defender, then an attacking unit.";
        if (!defender.IsReady) return defender.Card.name + " is not ready to defend.";
        if (!FightsHere(defender)) return defender.Card.name + " fights on " + defender.Card.GetTerrain() + " ground, not " + Ground + ".";
        return null;
    }
    /// <summary>The active hand is over its limit and must shed cards before the turn can end.</summary>
    public bool MustDiscard(int player) => Players[player].Hand.Count > Players[player].HandLimit;
    /// <summary>
    /// A card that no stage of this prototype can spend: an action or spell, or an event while no
    /// effect is registered. It may be set aside during Muster so it does not clog the hand for good.
    /// </summary>
    public bool IsDeadWeight(CardData card)
    {
        var type = card?.GetCardType();
        return type == CardTypeEnum.Action || type == CardTypeEnum.Spell || type == CardTypeEnum.Event && ResolveEvent == null;
    }
    public bool CanDiscard(CardData card) => card != null && Winner < 0 && PendingAmbush == null && Players[Active].Hand.Contains(card) &&
        (Stage == MatchStage.Spoils && Spoils.Count == 0 && MustDiscard(Active) || Stage == MatchStage.Muster && IsDeadWeight(card));
    public bool Discard(CardData card)
    {
        if (!CanDiscard(card)) return Reject("Discard only when your hand is over its limit at the end of the turn, or to set aside a card with no stage during Muster.");
        var p = Players[Active]; p.Hand.Remove(card); p.Discard.Add(card);
        int over = p.Hand.Count - p.HandLimit;
        Message = card.name + (Stage == MatchStage.Muster ? " set aside." : " discarded.") + (over > 0 && Stage == MatchStage.Spoils ? " Discard " + over + " more." : ""); return true;
    }
    public bool HasLegalAction() => Winner < 0 && (PendingAmbush != null || Stage switch
    {
        MatchStage.Realm => Players[Active].Hand.Any(CanPlay),
        MatchStage.Destination => DestinationChoices(Active).Any(CanChooseDestination),
        MatchStage.Arrival => GatesOpen(),
        MatchStage.Muster => Players[Active].Hand.Any(c => PlayBlockReason(c, includeReadyMana: true) == null) || Players[Active].Hand.Any(CanDiscard) || GatesOpen(),
        MatchStage.Events => Players[Active].Hand.Any(c => PlayBlockReason(c, includeReadyMana: true) == null),
        MatchStage.Travel => Phase == TravelPhase.Attack ? Players[Attacker].Field.Any(CanAttack) || Players[Attacker].Hand.Any(CanAttackWith) : Players[Active].Field.Any(u => Attacks.Any(a => CanBlock(u, a))),
        MatchStage.Spoils => Spoils.Count > 0 || MustDiscard(Active),
        _ => false
    });
    /// <summary>Something can be done at the gates: a unit (or the champion, with nothing ready) can fight the dwellers or walk in.</summary>
    public bool GatesOpen() => NeedsSecuring() && (Players[Active].Field.Any(CanSecure) || CanSecure(null))
        || NeedsEntering() && (Players[Active].Field.Any(CanEnter) || CanEnter(null));
    /// <summary>The destination is held against the company while cards in hand want to be played there.</summary>
    public bool NeedsSecuring()
    {
        var d = Players[Active].Destination;
        return d != null && !d.Secured && !d.Tapped && PlayableAt(Active, d.Card).Any();
    }
    /// <summary>What standing fast costs: a defender that keeps its feet under it fights at -2/-2.</summary>
    public const int StandFastPenalty = 2;
    /// <summary>
    /// Assigns a defender. Tapping is the usual way and fights at full strength; standing fast keeps
    /// the unit untapped, so it can still act on arrival, at -2/-2 for this fight.
    /// </summary>
    public bool Block(Unit defender, Strike strike, bool standFast = false)
    {
        if (!CanBlock(defender, strike))
            return Reject(BlockBlockReason(defender) ?? "Select an untapped defender, then an attacking unit.");
        var stats = Stats(defender);
        if (standFast) stats = (Math.Max(0, stats.attack - StandFastPenalty), Math.Max(0, stats.defense - StandFastPenalty));
        strike.BlockerStats[defender] = stats;
        if (standFast) strike.StoodFast.Add(defender);
        defender.Tapped = !standFast; strike.Blockers.Add(defender);
        Message = defender.Card.name + (standFast ? " stands fast (" + Fraction(stats) + ") against " : " defends against ") + strike.Attacker.Card.name + "."; return true;
    }
    public bool Next()
    {
        if (Winner >= 0) return false;
        if (PendingAmbush != null) return Reject("The ambush at " + PendingAmbush.Settlement.name + " must be answered first.");
        played.Clear();
        if (Stage == MatchStage.Spoils)
        {
            if (Spoils.Count > 0) return Reject("Resolve the remaining objects first.");
            if (MustDiscard(Active)) return Reject("Discard down to " + Players[Active].HandLimit + " cards first.");
            CheckWinner();
            if (Winner < 0)
            {
                var ending = Players[Active];
                foreach (var land in ending.Field.Where(u => !u.Tapped && u.Card.GetCardType() == CardTypeEnum.Land))
                {
                    land.Tapped = true;
                    ending.Mana.Grant(land.Card);
                }
                // Wounded characters mend where they can: carrying something that heals, or resting
                // the night in one of the company's own towns. They come back tapped and sit out a turn.
                bool home = ending.Destination != null && IsHome(Active, ending.Destination.Card);
                foreach (var unit in ending.Field.Where(u => u.Wounded))
                    if (home || unit.Objects.Any(o => o.healPerTurn > 0)) { unit.Wounded = false; unit.Tapped = true; unit.Recovering = true; }
                StartTurn(1 - Active);
            }
            return true;
        }
        if (Stage == MatchStage.Travel && Travel != null)
        {
            // Each stop: the attacks are declared, then defended, then the blows land; then the next
            // region, until the company arrives.
            if (Phase == TravelPhase.Attack && Attacks.Count > 0) { Phase = TravelPhase.Defend; Message = Attacks.Count + (Attacks.Count == 1 ? " attack" : " attacks") + " on the company at " + Travel.Region + ": assign defenders."; return true; }
            if (Attacks.Count > 0) Combat();
            if (Winner >= 0) return true;
            if (!Travel.Last) { NextStop(); return true; }
            Arrive();
            // At the gates: a company with a ready unit, and something to do in town, gets its
            // Arrival. One with neither goes straight on to Muster, as one with no town at all does.
            Stage = MatchStage.Arrival;
            if (!HasLegalAction()) { Stage = MatchStage.Muster; Message += " Nothing to do at the gates."; }
            return true;
        }
        Stage++;
        if (Stage == MatchStage.Realm)
            foreach (var player in Players)
                foreach (var environment in player.Field.Where(u => u.Card.GetCardType() == CardTypeEnum.Environmental).ToArray())
                { player.Field.Remove(environment); player.Discard.Add(environment.Card); }
        if (Stage == MatchStage.Travel) { BeginTravel(); return true; }
        Message = "Stage " + (int)Stage + " / " + Stage; return true;
    }
    void Combat()
    {
        Fights.Clear();
        var lines = new List<string>();
        var battle = new Battle { Kind = BattleKind.Road, Player = Active, Region = Travel.Region, Ground = Ground, Stop = Travel.Stop + 1, Stops = Travel.Stops.Count,
            Settlement = Travel.Last ? Travel.Destination : null };
        foreach (var strike in Attacks)
        {
            var clash = new Clash { Attacker = strike.Attacker, Target = strike.Target };
            battle.Add(clash);
            var defenders = strike.Blockers.ToList();
            if (defenders.Count == 0 && strike.Target != null && Players[Active].Field.Contains(strike.Target)) defenders.Add(strike.Target);
            clash.Blockers.AddRange(defenders);
            foreach (var braced in strike.StoodFast) clash.StoodFast.Add(braced);
            // Unanswered, the blow lands on the travelling company itself.
            if (defenders.Count == 0) { Players[Active].Life -= strike.Stats.attack; clash.Unblocked = strike.Stats.attack; lines.Add(strike.Attacker.Card.name + " hits the company for " + strike.Stats.attack + "."); continue; }
            // The attacker duels each defender in turn, for as long as it is still on its feet.
            foreach (var defender in defenders)
            {
                if (!Players[strike.Attacker.Owner].Field.Contains(strike.Attacker) || strike.Attacker.Wounded) break;
                if (!Players[defender.Owner].Field.Contains(defender)) continue;
                var defence = strike.BlockerStats.TryGetValue(defender, out var braced) ? braced : Stats(defender);
                var fight = Duel(strike.Attacker, strike.Stats, defender, defence);
                clash.Fights.Add(fight); lines.Add(fight.ToString());
            }
        }
        Message = string.Join(" ", lines);
        Attacks.Clear();
        Report(battle);
        RecallRoadside();
        CheckWinner();
    }
    public IEnumerable<Unit> Recipients()
    {
        if (Spoils.Count == 0) return Enumerable.Empty<Unit>();
        var loot = Spoils.Peek();
        // A wounded character is out of action and cannot pick anything up.
        return Players[loot.Offered ? 1-loot.Owner : loot.Owner].Field.Where(u => u.IsCharacter && !u.Wounded);
    }
    public bool Transfer(Unit recipient)
    {
        if (Stage != MatchStage.Spoils || Spoils.Count == 0 || !Recipients().Contains(recipient)) return Reject("Select an eligible surviving character.");
        recipient.Objects.Add(Spoils.Dequeue().Card); if (Spoils.Count == 0) CheckWinner(); return true;
    }
    public void OfferLoot()
    {
        if (Stage != MatchStage.Spoils || Spoils.Count == 0) return;
        var loot = Spoils.Peek();
        if (!loot.Offered) loot.Offered = true;
        if (!Recipients().Any()) { Players[loot.Owner].Discard.Add(loot.Card); Spoils.Dequeue(); }
        if (Spoils.Count == 0) CheckWinner();
    }
    void CheckWinner()
    {
        if (Players[0].Life <= 0 && Players[1].Life <= 0) { Winner = 2; return; }
        if (Players[0].Life <= 0) Winner = 1;
        else if (Players[1].Life <= 0) Winner = 0;
    }
}
