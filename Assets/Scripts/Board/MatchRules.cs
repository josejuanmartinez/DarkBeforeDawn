using System;
using System.Collections.Generic;
using System.Linq;

public enum MatchStage { Draw = 1, Realm, Mana, Muster, Events, Attack, Defend, Spoils }

/// <summary>Match state independent of Unity views. Card instances retain identity through every zone.</summary>
public sealed class MatchRules
{
    public sealed class Unit
    {
        public CardData Card;
        public int Owner, EnteredTurn;
        public bool Tapped;
        public readonly List<CardData> Objects = new();
        public bool IsCharacter => Card.GetCardType() == CardTypeEnum.Character;
        public bool IsCombatant => IsCharacter || Card.GetCardType() == CardTypeEnum.Army;
    }
    public sealed class Player
    {
        public int Life = 20, HandLimit = 5;
        public readonly List<CardData> Deck = new(), Hand = new(), Discard = new();
        public readonly List<Unit> Field = new();
        public readonly PlayerMaterials Mana = new();
    }
    public sealed class Strike
    {
        public Unit Attacker, Target;
        public readonly List<Unit> Blockers = new();
    }
    public sealed class Loot { public CardData Card; public int Owner; public bool Offered; }
    public readonly Player[] Players = { new(), new() };
    public readonly List<Strike> Attacks = new();
    public readonly Queue<Loot> Spoils = new();
    public int Active { get; private set; }
    public int Turn { get; private set; }
    public int Winner { get; private set; } = -1;
    public MatchStage Stage { get; private set; }
    public string Message { get; private set; }
    public event Action<CardData, int> Drawn;
    // Explicit extension seams for future card abilities; no inference from flavour text.
    public Func<Unit, bool> CanChooseTarget = u => u.Card.HasTag("ChooseTarget");
    public Action<CardData, int, MatchRules> ResolveEvent;
    public void Begin(int first) { Turn = 0; Winner = -1; StartTurn(first); }
    void StartTurn(int player)
    {
        Active = player; Turn++; Stage = MatchStage.Draw; Attacks.Clear();
        foreach (var p in Players) p.Mana.Clear();
        foreach (var u in Players[player].Field)
            if (!u.Card.statusEffects.Contains(StatusEffects.Halted)) u.Tapped = false;
        var current = Players[player];
        while (current.Hand.Count < current.HandLimit && current.Deck.Count > 0)
        {
            var card = current.Deck[0]; current.Deck.RemoveAt(0); current.Hand.Add(card); Drawn?.Invoke(card, player);
        }
        Message = "Hand replenished. Halted cards remain tapped.";
    }
    bool Reject(string message) { Message = message; return false; }
    public bool CanPlay(CardData card) => PlayBlockReason(card) == null;
    public string PlayBlockReason(CardData card, Unit recipient = null, bool choosingRecipient = true)
    {
        var p = Players[Active];
        if (card == null || Winner >= 0 || !p.Hand.Contains(card)) return "That card is not in the active hand.";
        var type = card.GetCardType();
        bool realm = type == CardTypeEnum.Land || type == CardTypeEnum.PC || type == CardTypeEnum.Environmental;
        bool muster = type == CardTypeEnum.Character || type == CardTypeEnum.Army || type == CardTypeEnum.Object;
        if (!(Stage == MatchStage.Realm && realm || Stage == MatchStage.Muster && muster || Stage == MatchStage.Events && type == CardTypeEnum.Event))
            return "This card cannot be played during " + Stage + ".";
        if (type == CardTypeEnum.PC && !p.Field.Any(u => u.Card.GetCardType() == CardTypeEnum.Land && Same(u.Card.name, card.region)))
            return "First play the land: " + card.region + ".";
        if (type == CardTypeEnum.Character && !p.Field.Any(u => u.Card.GetCardType() == CardTypeEnum.PC && Same(u.Card.name, card.startingPC)))
            return "Required starting PC: " + card.startingPC + ".";
        if (type == CardTypeEnum.Object && !(recipient != null && recipient.IsCharacter && p.Field.Contains(recipient)) &&
            !(recipient == null && choosingRecipient && p.Field.Any(u => u.IsCharacter)))
            return "Select one of your characters to carry this object.";
        if (type == CardTypeEnum.Event && ResolveEvent == null)
            return "This event needs a registered effect before it can be played.";
        return p.Mana.CanAfford(card) ? null : "Not enough mana/materials.";
    }
    public bool Play(CardData card, Unit recipient = null)
    {
        var reason = PlayBlockReason(card, recipient, false);
        if (reason != null) return Reject(reason);
        var p = Players[Active]; var type = card.GetCardType();
        if (!p.Mana.TrySpend(card)) return Reject("Not enough mana/materials.");
        p.Hand.Remove(card);
        if (type == CardTypeEnum.Object) recipient.Objects.Add(card);
        else if (type == CardTypeEnum.Event) { ResolveEvent(card, Active, this); p.Discard.Add(card); }
        else p.Field.Add(new Unit { Card = card, Owner = Active, EnteredTurn = Turn });
        Message = card.name + " played."; return true;
    }
    static bool Same(string a, string b) => !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    public bool CanTapLand(Unit unit) => unit != null && Winner < 0 && Stage == MatchStage.Mana && unit.Owner == Active &&
        !unit.Tapped && Players[Active].Field.Contains(unit) && unit.Card.GetCardType() == CardTypeEnum.Land;
    public bool TapLand(Unit unit)
    {
        if (!CanTapLand(unit))
            return Reject("Only your ready lands can produce mana in stage 3.");
        unit.Tapped = true; Players[Active].Mana.Grant(unit.Card); Message = unit.Card.name + " produced mana."; return true;
    }
    public bool IsNewUnit(Unit unit) => unit != null && unit.IsCombatant && unit.EnteredTurn == Turn &&
        !unit.Card.specialAbilities.Contains(ObjectCharacterArmySpecialAbilityEnum.Mounted);
    public bool IsEnemyTarget(Unit target) => target != null && target.Owner == 1 - Active && target.IsCombatant && Players[1 - Active].Field.Contains(target);
    public bool CanAttack(Unit unit) => AttackBlockReason(unit) == null;
    public string AttackBlockReason(Unit unit, Unit target = null, bool choosingTarget = true)
    {
        if (unit == null || Winner >= 0 || Stage != MatchStage.Attack || unit.Owner != Active || !Players[Active].Field.Contains(unit) || !unit.IsCombatant || unit.Tapped || unit.Card.statusEffects.Contains(StatusEffects.Fear))
            return "Choose a ready character or army to attack.";
        if (IsNewUnit(unit)) return "New unit: can defend now; can attack next turn (unless Mounted).";
        if (CanChooseTarget(unit) && !IsEnemyTarget(target) &&
            !(target == null && choosingTarget && Players[1 - Active].Field.Any(IsEnemyTarget)))
            return "Select an enemy character or army as the target.";
        if (!CanChooseTarget(unit) && target != null) return "This unit cannot choose a target.";
        return null;
    }
    public bool Attack(Unit unit, Unit target = null)
    {
        var reason = AttackBlockReason(unit, target, false);
        if (reason != null) return Reject(reason);
        unit.Tapped = true; Attacks.Add(new Strike { Attacker = unit, Target = target });
        Message = unit.Card.name + " attacks."; return true;
    }
    public bool CanBlock(Unit defender, Strike strike) => defender != null && Winner < 0 && Stage == MatchStage.Defend &&
        Attacks.Contains(strike) && defender.Owner == 1 - Active && !defender.Tapped && defender.IsCombatant && Players[1 - Active].Field.Contains(defender);
    public bool HasLegalAction() => Winner < 0 && (Stage switch
    {
        MatchStage.Realm or MatchStage.Muster or MatchStage.Events => Players[Active].Hand.Any(CanPlay),
        MatchStage.Mana => Players[Active].Field.Any(CanTapLand),
        MatchStage.Attack => Players[Active].Field.Any(CanAttack),
        MatchStage.Defend => Players[1 - Active].Field.Any(u => Attacks.Any(a => CanBlock(u, a))),
        MatchStage.Spoils => Spoils.Count > 0,
        _ => false
    });
    public bool Block(Unit defender, Strike strike)
    {
        if (!CanBlock(defender, strike))
            return Reject("Select an untapped defender, then an attacking unit.");
        defender.Tapped = true; strike.Blockers.Add(defender); Message = defender.Card.name + " defends against " + strike.Attacker.Card.name + "."; return true;
    }
    public bool Next()
    {
        if (Winner >= 0) return false;
        if (Stage == MatchStage.Spoils)
        {
            if (Spoils.Count > 0) return Reject("Resolve the remaining objects first.");
            CheckWinner(); if (Winner < 0) StartTurn(1 - Active); return true;
        }
        Stage++;
        if (Stage == MatchStage.Realm)
            foreach (var player in Players)
                foreach (var environment in player.Field.Where(u => u.Card.GetCardType() == CardTypeEnum.Environmental).ToArray())
                { player.Field.Remove(environment); player.Discard.Add(environment.Card); }
        if (Stage == MatchStage.Spoils) Combat();
        Message = "Stage " + (int)Stage + " / " + Stage; return true;
    }
    void Combat()
    {
        var damage = new Dictionary<Unit, int>();
        void Hit(Unit u, int amount) { if (!damage.ContainsKey(u)) damage[u] = 0; damage[u] += Math.Max(0, amount); }
        foreach (var strike in Attacks)
        {
            int power = strike.Attacker.Card.GetCombatStats().attack;
            var defenders = strike.Blockers.ToList();
            if (defenders.Count == 0 && strike.Target != null) defenders.Add(strike.Target);
            if (defenders.Count == 0) Players[1-Active].Life -= power;
            foreach (var defender in defenders)
            {
                int assigned = Math.Min(power, defender.Card.GetCombatStats().defense);
                Hit(defender, assigned); power -= assigned; Hit(strike.Attacker, defender.Card.GetCombatStats().attack);
            }
        }
        foreach (var pair in damage)
            if (pair.Value >= pair.Key.Card.GetCombatStats().defense)
            {
                var dead = pair.Key; Players[dead.Owner].Field.Remove(dead); Players[dead.Owner].Discard.Add(dead.Card);
                foreach (var item in dead.Objects) Spoils.Enqueue(new Loot { Card = item, Owner = dead.Owner });
                dead.Objects.Clear();
            }
        if (Spoils.Count == 0) CheckWinner();
    }
    public IEnumerable<Unit> Recipients()
    {
        if (Spoils.Count == 0) return Enumerable.Empty<Unit>();
        var loot = Spoils.Peek();
        return Players[loot.Offered ? 1-loot.Owner : loot.Owner].Field.Where(u => u.IsCharacter);
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
