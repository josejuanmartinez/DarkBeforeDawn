using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns match input, opponent decisions and presentation; the authored board remains the layout source.</summary>
[DefaultExecutionOrder(100)]
public sealed class TowerMatchController : MonoBehaviour
{
    public bool diceDecideFirstPlayer = true;
    public int cinematicRendererIndex = -1;
    public Material towerStoneMaterial, towerTrimMaterial, towerInkMaterial, dieLightMaterial, dieDarkMaterial, towerAccentMaterial;
    public Shader diceSurfaceShader;
    public string humanDeckId, opponentDeckId;
    public int startingLife = 20;
    [Tooltip("Build a playable opening from the chosen catalog deck while event effects are being authored.")]
    public bool useStarterDeck = true;
    public MatchRules Rules { get; private set; }
    public bool Busy { get; private set; } = true;
    public int Floor { get; private set; }
    Board board;
    Text headline, hint, nextText, deckLabel;
    Button next, offer, decline, undo;
    Text stanceText;
    RectTransform deckAnchor;
    MatchCinematic cinematic;
    DestinationPicker picker;
    CardData pendingObject;
    // A hand card raiding the road that still needs its target.
    CardData pendingRaid;
    MatchRules.Unit pendingAttacker, pendingDefender;
    // The human's choice for the next defender: tap at full strength, or stand fast untapped at -2/-2.
    bool standFast;
    Button stance;
    readonly Queue<(CardData card, int player)> draws = new();
    readonly List<GameObject> ownedUI = new();
    bool animating;
    float aiAt;
    float autoAt;
    string selectionHint;
    readonly string[] stages = { "", "REPLENISH", "BUILD YOUR REALM", "SELECT DESTINATION", "TRAVEL", "MUSTER", "EVENTS", "RECOVER OBJECTS" };
    TravelBanner banner;
    /// <summary>The human declares attacks on the road: the opponent's company is travelling.</summary>
    public bool HumanRaids => Rules != null && Rules.Stage == MatchStage.Travel && Rules.Phase == TravelPhase.Attack && Rules.Attacker == 0;
    /// <summary>The human assigns defenders: its own company is under attack on the road.</summary>
    public bool HumanDefends => Rules != null && Rules.Stage == MatchStage.Travel && Rules.Phase == TravelPhase.Defend && Rules.Active == 0;

    IEnumerator Start()
    {
        board = GetComponent<Board>();
        yield return null; // Zones first adopt their authored content.
        BuildUI();
        cinematic = gameObject.AddComponent<MatchCinematic>();
        cinematic.Initialize(board);
        yield return cinematic.ShowTower(Floor);
        PrepareMatch();
        yield return RollInitiative();
    }
    void PrepareMatch()
    {
        Rules = new MatchRules { Map = RegionMap.Load() };
        var world = CardCatalog.AllCards().Where(c => c.GetCardType() == CardTypeEnum.PC).ToList();
        for (int i = 0; i < 2; i++)
        {
            var player = Rules.Players[i];
            player.Life = startingLife;
            player.HandLimit = board.defaultHandSize;
            string id = i == 0 ? humanDeckId : opponentDeckId;
            var avatar = CardCatalog.FindCardByName(i == 0 ? board.humanAvatarCardName : board.opponentAvatarCardName);
            if (string.IsNullOrWhiteSpace(id)) id = avatar?.deckId;
            var cards = CardCatalog.GetDeckCards(id).Select(c => c.Clone()).ToList();
            if (avatar != null && !cards.Any(c => c.cardId == avatar.cardId && c.name == avatar.name))
                cards.Add(avatar.Clone());
            if (cards.Count == 0) { selectionHint = "Assign both match deck IDs on TowerMatchController."; Busy = true; return; }
            // The company's side decides which foreign settlements welcome it.
            player.Alignment = CardCatalog.TryResolveDeck(id, out var deck) ? deck.alignment : avatar?.alignment ?? CardData.NeutralAlignment;
            for (int n = cards.Count - 1; n > 0; n--) { int j = Random.Range(0, n + 1); (cards[n], cards[j]) = (cards[j], cards[n]); }
            // Settlements are never drawn: the whole pool waits on the table for its land, and one of
            // them is picked as the destination each turn. Every other settlement in the world is
            // reachable too, once its land is down, but its dwellers hold it against the company.
            player.Settlements.AddRange(cards.Where(c => c.GetCardType() == CardTypeEnum.PC).OrderBy(c => c.name));
            cards.RemoveAll(c => c.GetCardType() == CardTypeEnum.PC);
            var own = player.Settlements.Select(c => c.cardId).ToHashSet();
            player.Foreign.AddRange(world.Where(c => !own.Contains(c.cardId)).Select(c => c.Clone()).OrderBy(c => c.name));
            if (useStarterDeck) cards = StarterDeck(cards);
            player.Deck.AddRange(cards);
            // Each company sets out from one of its own towns.
            if (player.Settlements.Count > 0) Rules.StartAt(i, player.Settlements[Random.Range(0, player.Settlements.Count)]);
        }
        Rules.Drawn += (card, player) => draws.Enqueue((card, player));
        Sync();
    }
    static List<CardData> StarterDeck(List<CardData> catalog)
    {
        // Preserve printed costs and identities; select only the implemented deployment types.
        var supported = catalog.Where(c => c.GetCardType() == CardTypeEnum.Land || c.GetCardType() == CardTypeEnum.Character ||
            c.GetCardType() == CardTypeEnum.Army || c.GetCardType() == CardTypeEnum.Object || c.GetCardType() == CardTypeEnum.Environmental ||
            c.GetCardType() == CardTypeEnum.Encounter).ToList();
        var lands = supported.Where(c => c.GetCardType() == CardTypeEnum.Land).ToList();
        var opening = lands.Take(3).ToList();
        var army = supported.Where(c => c.GetCardType() == CardTypeEnum.Army).OrderBy(c => c.GetTotalMaterialCost()).FirstOrDefault();
        if (army != null) opening.Add(army);
        foreach(var c in opening) supported.Remove(c);
        opening.AddRange(supported);
        return opening;
    }
    IEnumerator RollInitiative()
    {
        if (Rules == null || Rules.Players.Any(p => p.Deck.Count == 0)) yield break;
        int a, b;
        do
        {
            a = Random.Range(1, 7); b = Random.Range(1, 7);
            yield return cinematic.Roll(a, b);
            headline.text = a == b ? "A TIE — ROLL AGAIN" : (a > b ? "ORREN" : "THE SLEEPLESS EYE") + " WINS INITIATIVE";
            yield return new WaitForSecondsRealtime(1.2f);
        } while (a == b);
        cinematic.Hide();
        Rules.Begin(diceDecideFirstPlayer && b > a ? 1 : 0);
        Busy = false; Sync();
    }
    void BuildUI()
    {
        var bar = BoardPresentation.Panel(transform, "Match stages", new Color(.035f,.045f,.06f,.97f));
        ownedUI.Add(bar.gameObject);
        BoardPresentation.Stretch(bar.rectTransform, new Vector2(.15f,.91f), new Vector2(.87f,.995f));
        BoardPresentation.Border(bar.rectTransform, new Color(.72f,.53f,.26f));
        headline = Label(bar.transform, "THE ASCENT", 20, new Vector2(.02f,.46f), new Vector2(.62f,.98f));
        hint = Label(bar.transform, "Orren vs The Sleepless Eye", 12, new Vector2(.02f,.02f), new Vector2(.62f,.46f));
        undo = Button(bar.transform, "\u2190 UNDO", new Vector2(.63f,.15f), new Vector2(.76f,.85f), Undo);
        undo.gameObject.SetActive(false);
        next = Button(bar.transform, "CONTINUE", new Vector2(.77f,.15f), new Vector2(.98f,.85f), Advance);
        nextText = next.GetComponentInChildren<Text>();
        var old = transform.Find("Your materials/End turn"); if (old != null) old.gameObject.SetActive(false);
        var deck = BoardPresentation.Panel(transform, "Your draw deck", new Color(.06f,.09f,.12f,.98f));
        ownedUI.Add(deck.gameObject); deckAnchor = deck.rectTransform;
        if (board.humanVictoryPoints != null)
        {
            deck.transform.SetParent(board.humanVictoryPoints.transform.parent, false);
            BoardPresentation.Stretch(deckAnchor, new Vector2(.03f,.04f), new Vector2(.97f,.76f));
            foreach (var label in deck.transform.parent.GetComponentsInChildren<Text>())
                if (label.text == "VICTORY") label.text = "DRAW DECK";
                else if (label.transform.parent == deck.transform.parent) label.enabled = false;
        }
        else BoardPresentation.Stretch(deckAnchor, new Vector2(.88f,.20f), new Vector2(.985f,.32f));
        BoardPresentation.Border(deckAnchor, new Color(.7f,.52f,.25f));
        deckLabel = Label(deck.transform, "ORREN\nDRAW DECK", 16, Vector2.zero, Vector2.one);
        offer = Button(transform, "OFFER TO ENEMY", new Vector2(.35f,.565f), new Vector2(.65f,.60f), () => { Rules.OfferLoot(); selectionHint = null; Sync(); });
        ownedUI.Add(offer.gameObject); offer.gameObject.SetActive(false);
        // Same spot as the loot offer: one belongs to Recover Objects, the other to an ambush during Muster.
        decline = Button(transform, "LET THEM PASS", new Vector2(.35f,.565f), new Vector2(.65f,.60f), () => { Rules.DeclineAmbush(); selectionHint = null; Sync(); });
        ownedUI.Add(decline.gameObject); decline.gameObject.SetActive(false);
        // While defending on the road: how the next defender meets its attacker.
        stance = Button(transform, "DEFENDERS TAP", new Vector2(.35f,.565f), new Vector2(.65f,.60f), () => { standFast = !standFast; UpdateHUD(); });
        stanceText = stance.GetComponentInChildren<Text>();
        ownedUI.Add(stance.gameObject); stance.gameObject.SetActive(false);
        picker = gameObject.AddComponent<DestinationPicker>(); picker.Initialize(board, this);
        banner = gameObject.AddComponent<TravelBanner>(); banner.Initialize(board, this);
    }
    Text Label(Transform root, string value, int size, Vector2 min, Vector2 max) => BoardPresentation.TextLabel(root, value,
        board.interfaceFont != null ? board.interfaceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), size,
        new Color(.94f,.88f,.73f), min, max, TextAnchor.MiddleCenter);
    Button Button(Transform root, string title, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        var panel = BoardPresentation.Panel(root, title, new Color(.25f,.18f,.095f));
        BoardPresentation.Stretch(panel.rectTransform, min, max); panel.raycastTarget = true;
        BoardPresentation.Border(panel.rectTransform, new Color(.8f,.61f,.3f));
        Label(panel.transform, title, 13, Vector2.zero, Vector2.one);
        var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel; button.onClick.AddListener(action); return button;
    }
    public MatchRules.Unit Unit(BoardCardView view) => view == null || Rules == null ? null : Rules.Players.SelectMany(p => p.Field).FirstOrDefault(u => ReferenceEquals(u.Card, view.Data));
    public bool IsTapped(BoardCardView view) => Unit(view)?.Tapped ?? false;
    public bool CanInteract => !Busy && !animating && draws.Count == 0 && Rules != null && Rules.Winner < 0;
    public bool CanPlay(BoardCardView view) => CanInteract && Rules.Active == 0 && view != null && view.Zone == board.hand && Rules.CanPlay(view.Data);
    public bool CanTap(BoardCardView view) => CanInteract && Rules.Active == 0 && Rules.CanTapLand(Unit(view));
    public bool IsDestination(BoardCardView view) => view != null && Rules != null && view.Zone != board.hand &&
        Rules.IsDestination(board.IsOpponentZone(view.Zone) ? 1 : 0, view.Data);
    /// <summary>The human is being asked to answer the opponent's tap with a card born there.</summary>
    public bool HumanAmbushPending => Rules != null && Rules.PendingAmbush != null && Rules.PendingAmbush.Defender == 0;
    public bool CanDiscard(BoardCardView view) => CanInteract && Rules.Active == 0 && view != null && view.Zone == board.hand && Rules.CanDiscard(view.Data);
    public bool IsActionable(BoardCardView view)
    {
        if (!CanInteract || view == null) return false;
        var unit = Unit(view);
        if (HumanAmbushPending) return view.Zone == board.hand && Rules.CanAmbush(view.Data);
        if (Rules.PendingAmbush != null) return false;
        if (pendingObject != null) return unit != null && unit.Owner == 0 && unit.IsCharacter && Rules.PlayBlockReason(pendingObject, unit, false) == null;
        if (pendingAttacker != null) return Rules.AttackBlockReason(pendingAttacker, unit, false) == null;
        if (pendingRaid != null) return Rules.AttackWithBlockReason(pendingRaid, unit, false) == null;
        if (Rules.Stage == MatchStage.Spoils) return unit != null && Rules.Recipients().Contains(unit) || CanDiscard(view);
        if (HumanRaids) return view.Zone == board.hand ? Rules.CanAttackWith(view.Data) : Rules.CanAttack(unit);
        if (HumanDefends)
            return unit != null && (Rules.Attacks.Any(a => Rules.CanBlock(unit, a)) ||
                pendingDefender != null && Rules.Attacks.Any(a => a.Attacker == unit && Rules.CanBlock(pendingDefender, a)));
        return CanPlay(view) || CanTap(view) || Rules.Active == 0 && Rules.CanSecure(unit);
    }
    public string ActionLabel(BoardCardView view)
    {
        if (!IsActionable(view)) return null;
        if (HumanAmbushPending) return view.Data.GetCardType() == CardTypeEnum.Encounter ? "SPRING ENCOUNTER" : "AMBUSH";
        if (CanDiscard(view)) return "DISCARD";
        if (pendingObject != null || pendingAttacker != null || pendingRaid != null || Rules.Stage == MatchStage.Spoils) return "SELECT";
        if (HumanRaids && view.Zone == board.hand) return "STRIKE FROM HAND";
        if (view.Zone == board.hand) return "PLAY CARD";
        if (CanTap(view)) return "TAP LAND";
        if (Rules.Stage == MatchStage.Muster) return "FIGHT DWELLERS";
        return HumanRaids ? "ATTACK" : "DEFEND";
    }
    public string InspectionHint(BoardCardView view)
    {
        if (Rules == null || view == null) return null;
        var unit = Unit(view);
        if (IsDestination(view))
        {
            int owner = board.IsOpponentZone(view.Zone) ? 1 : 0;
            string standing = Rules.StandingAt(owner, view.Data) switch
            {
                Standing.Hostile => "Hostile ground: the dwellers must be beaten in a normal attack before acting here. ",
                Standing.Neutral => "Neutral ground: a retention attack on the dwellers opens it for the turn. ",
                _ => ""
            };
            string state = unit == null ? "" : unit.Wounded ? "" : unit.Tapped ? (unit.Secured ? "Used this turn. " : "Closed this turn: the dwellers held. ") : unit.Secured ? "" : "Held by its dwellers. ";
            return (state + standing + "Characters and encounters born here, and objects it trades in, are played here.").Trim();
        }
        if (unit != null && unit.Wounded) return "Wounded: out of action until healed by an object that heals or a night in one of your own settlements. Heals to tapped.";
        if (unit != null && unit.Recovering) return "Healing: back on its feet, but sits out this turn.";
        if (HumanAmbushPending && view.Zone == board.hand) return Rules.CanAmbush(view.Data) ? "Born at " + Rules.PendingAmbush.Settlement.name + ": may answer the tap." : "Waiting on your ambush choice.";
        if (HumanRaids && view.Zone == board.hand) return Rules.AttackWithBlockReason(view.Data) ?? "Can strike the travelling company from the hand, then shuffles back into the deck.";
        if (unit != null && unit.Roadside) return "Striking from the hand: goes back into the deck after this fight.";
        if (unit != null && unit.Tapped && unit.IsCombatant && unit.EnteredTurn == Rules.Turn && unit.Owner == Rules.Active) return "Mustering: a recruit readies at the start of its next turn (Mounted troops excepted).";
        if (view.Zone == board.hand && Rules.Active == 0) return Rules.CanDiscard(view.Data) ? "Hand over its limit: discard " + (Rules.Players[0].Hand.Count - Rules.Players[0].HandLimit) + "." : Rules.PlayBlockReason(view.Data);
        if (unit != null && Rules.Stage == MatchStage.Travel && unit.IsCombatant)
        {
            if (unit.Owner == Rules.Attacker) return Rules.Phase == TravelPhase.Attack ? Rules.AttackBlockReason(unit) : (Rules.Attacks.Any(a => a.Attacker == unit) ? "Attacking the travelling company." : null);
            return Rules.Phase == TravelPhase.Defend ? Rules.BlockBlockReason(unit) : (unit.Card.FightsOn(Rules.Ground) ? "Can defend on " + Rules.Ground + " ground." : unit.Card.name + " cannot fight on " + Rules.Ground + " ground (" + unit.Card.GetTerrain() + ").");
        }
        if (unit != null && Rules.Stage == MatchStage.Muster && Rules.Active == 0 && unit.Owner == 0 && unit.IsCombatant && Rules.NeedsSecuring())
            return Rules.SecureBlockReason(unit) ?? "Ready to face the dwellers of " + Rules.Players[0].Destination.Card.name + " (" + Rules.Stats(unit).attack + "/" + Rules.Stats(unit).defense + ").";
        return null;
    }
    public void PerformAction(BoardCardView view) { if (view.Zone == board.hand) Play(view); else Select(view); }
    public bool Tap(BoardCardView view)
    {
        if (!CanTap(view)) return false;
        bool result = Rules.TapLand(Unit(view)); selectionHint = null; Sync(); return result;
    }
    public bool Play(BoardCardView view)
    {
        if (!CanInteract || view == null || view.Zone != board.hand) return false;
        // A hand card can also answer an ambush or be shed at the end of the turn.
        if (HumanAmbushPending)
        {
            bool answered = Rules.AmbushWith(view.Data);
            selectionHint = answered ? null : Rules.Message; Sync(); return answered;
        }
        if (CanDiscard(view)) { Rules.Discard(view.Data); selectionHint = null; Sync(); return true; }
        if (HumanRaids)
        {
            if (!Rules.CanAttackWith(view.Data)) { selectionHint = Rules.AttackWithBlockReason(view.Data); UpdateHUD(); return false; }
            if (view.Data.HasTag("ChooseTarget")) { pendingRaid = view.Data; selectionHint = "Select a travelling enemy unit as the target for " + view.Data.name; UpdateHUD(); return true; }
            bool struck = Rules.AttackWith(view.Data); selectionHint = struck ? null : Rules.Message; Sync(); return struck;
        }
        if (!CanPlay(view)) { selectionHint = InspectionHint(view); UpdateHUD(); return false; }
        if (view.Data.GetCardType() == CardTypeEnum.Object && Rules.Stage == MatchStage.Muster)
        { pendingObject = view.Data; selectionHint = "Select your character to carry " + pendingObject.name + "."; board.preview?.Hide(); UpdateHUD(); return true; }
        bool result = Rules.Play(view.Data); selectionHint = null; Sync(); return result;
    }
    /// <summary>The human travels to a settlement for the turn; the choice made, the stage moves on.</summary>
    public bool Travel(CardData settlement)
    {
        if (!CanInteract || Rules.Active != 0 || Rules.Stage != MatchStage.Destination) return false;
        if (!Rules.ChooseDestination(settlement)) { selectionHint = Rules.Message; UpdateHUD(); return false; }
        selectionHint = null; Rules.Next(); Sync(); return true;
    }
    /// <summary>Takes back the human's last play of this stage. A stage change forgets the stack.</summary>
    public void Undo()
    {
        if (!CanInteract || Rules.Active != 0 || !Rules.CanUndo) return;
        pendingObject = null; selectionHint = null;
        Rules.Undo(); Sync();
    }
    // While a play can still be taken back the stage waits for NEXT STAGE, otherwise the automatic
    // advance would quietly wipe the undo half a second after the last card was played.
    bool HoldingForUndo => Rules.Active == 0 && Rules.CanUndo;
    public bool Select(BoardCardView view)
    {
        if (!CanInteract || view == null) return false;
        if (view.Zone == board.hand) return (HumanAmbushPending || CanDiscard(view) || HumanRaids) && Play(view);
        if (Rules.PendingAmbush != null) return false;
        if (CanTap(view)) return Tap(view);
        var unit = Unit(view);
        if (Rules.Stage == MatchStage.Muster && Rules.Active == 0 && unit != null && unit.Owner == 0 && unit.IsCombatant && Rules.NeedsSecuring() && pendingObject == null)
        {
            if (Rules.Secure(unit)) selectionHint = null; else selectionHint = Rules.Message;
            Sync(); return true;
        }
        if (pendingObject != null)
        {
            if (Rules.Play(pendingObject, unit)) { pendingObject = null; selectionHint = null; }
            else selectionHint = Rules.Message;
            Sync(); return true;
        }
        if (unit == null) return false;
        if (Rules.Stage == MatchStage.Spoils && Rules.Spoils.Count > 0)
        { Rules.Transfer(unit); selectionHint = null; Sync(); return true; }
        if (HumanRaids)
        {
            if (pendingRaid != null)
            {
                if (Rules.AttackWith(pendingRaid, unit)) { pendingRaid = null; selectionHint = null; } else selectionHint = Rules.Message;
                Sync(); return true;
            }
            if (pendingAttacker == null && !Rules.CanAttack(unit)) { selectionHint = Rules.AttackBlockReason(unit); UpdateHUD(); return false; }
            if (pendingAttacker != null)
            { if (Rules.Attack(pendingAttacker, unit)) { pendingAttacker = null; selectionHint = null; } else selectionHint = Rules.Message; }
            else if (unit.Owner == 0 && Rules.CanChooseTarget(unit)) { pendingAttacker = unit; selectionHint = "Select a travelling enemy unit as the target for " + unit.Card.name; }
            else { Rules.Attack(unit); selectionHint = null; }
            Sync(); return true;
        }
        if (HumanDefends)
        {
            if (unit.Owner == 0 && Rules.Attacks.Any(a => Rules.CanBlock(unit, a))) { pendingDefender = unit; selectionHint = "Select the attacker " + unit.Card.name + " will meet" + (standFast ? " standing fast (-2/-2, stays ready)." : " (it taps)."); }
            else if (pendingDefender != null)
            {
                var strike = Rules.Attacks.FirstOrDefault(s => s.Attacker == unit);
                if (Rules.Block(pendingDefender, strike, standFast)) { pendingDefender = null; selectionHint = null; }
                else selectionHint = Rules.Message;
            }
            Sync(); return true;
        }
        return false;
    }
    public void Advance()
    {
        if (Busy || animating || draws.Count > 0 || Rules == null || Rules.Stage == MatchStage.Draw) return;
        if (Rules.Winner >= 0) { StartCoroutine(ReturnToTower()); return; }
        if (Rules.PendingAmbush != null) return;
        // On the opponent's turn the human only gets a say on the road, as the raider.
        if (Rules.Active == 1 && !HumanRaids && Rules.Stage != MatchStage.Spoils) return;
        if (Rules.Active == 0 && Rules.Stage == MatchStage.Travel && Rules.Phase == TravelPhase.Attack) return;
        pendingObject = null; pendingRaid = null; pendingAttacker = pendingDefender = null; selectionHint = null;
        Rules.Next(); Sync();
    }
    /// <summary>Called only after draw animations finish. One transition per tick avoids runaway empty turns.</summary>
    public bool AdvanceIfNoActions()
    {
        if (!CanInteract || Rules.HasLegalAction() || HoldingForUndo) return false;
        pendingObject = null; pendingRaid = null; pendingAttacker = pendingDefender = null; selectionHint = null;
        Rules.Next(); Sync(); return true;
    }
    IEnumerator ReturnToTower()
    {
        Busy = true;
        if (Rules.Winner == 0) Floor++;
        yield return cinematic.ShowTower(Floor);
        // Later opponents need authored decks; the tower is not populated with invented identities.
        if (Floor > 0) { selectionHint = "Floor cleared. The next opponent and deck await authoring."; cinematic.Hide(); UpdateHUD(); yield break; }
        PrepareMatch(); yield return RollInitiative();
    }
    void Update()
    {
        if (Rules == null || Busy) return;
        if (!animating && draws.Count > 0) { StartCoroutine(AnimateDraws()); return; }
        UpdateHUD();
        if (CanInteract && Time.unscaledTime >= autoAt && AdvanceIfNoActions()) return;
        if (animating || Rules.Winner >= 0 || Time.unscaledTime < aiAt) return;
        aiAt = Time.unscaledTime + .85f;
        // An ambush pauses whoever is tapping until the other side answers. The opponent answers
        // with its best-hitting character, else springs an encounter; the human answers by hand.
        if (Rules.PendingAmbush != null)
        {
            if (Rules.PendingAmbush.Defender != 1) return;
            var answer = Rules.PendingAmbush.Options.Where(c => c.GetCardType() == CardTypeEnum.Character).OrderByDescending(c => c.GetCombatStats().attack).FirstOrDefault()
                ?? Rules.PendingAmbush.Options.FirstOrDefault();
            if (answer == null || !Rules.AmbushWith(answer)) Rules.DeclineAmbush();
            Sync(); return;
        }
        if (Rules.Stage == MatchStage.Spoils && Rules.Active == 1 && Rules.MustDiscard(1))
        {
            // Over the limit at the end of its turn: the opponent sheds what it can least afford to play.
            var surplus = Rules.Players[1].Hand.OrderByDescending(c => c.GetTotalMaterialCost()).First();
            Rules.Discard(surplus); Sync(); return;
        }
        if (Rules.Stage == MatchStage.Spoils && Rules.Spoils.Count > 0)
        {
            var loot = Rules.Spoils.Peek(); int chooser = loot.Offered ? 1-loot.Owner : loot.Owner;
            if (!Rules.Recipients().Any()) { Rules.OfferLoot(); Sync(); }
            else if (chooser == 1) { Rules.Transfer(Rules.Recipients().First()); Sync(); }
            return;
        }
        if (Rules.Stage == MatchStage.Travel)
        {
            // The road: the opponent falls on the human's company with whatever can fight on this
            // ground, and shields its own company with the same when the human raids it.
            if (Rules.Phase == TravelPhase.Attack && Rules.Attacker == 1)
            {
                MatchRules.Unit Softest() => Rules.Players[0].Field.Where(Rules.IsAttackTarget).OrderBy(x => Rules.Stats(x).defense).FirstOrDefault();
                foreach (var u in Rules.Players[1].Field.Where(Rules.CanAttack).ToArray())
                    Rules.Attack(u, Rules.CanChooseTarget(u) ? Softest() : null);
                // Hand cards cost the opponent the card in hand, so it only throws in the ones that hit hard.
                foreach (var card in Rules.Players[1].Hand.Where(c => Rules.CanAttackWith(c) && c.GetCombatStats().attack >= 2).ToArray())
                    Rules.AttackWith(card, card.HasTag("ChooseTarget") ? Softest() : null);
                Rules.Next(); Sync(); return;
            }
            if (Rules.Phase == TravelPhase.Defend && Rules.Active == 1)
            {
                // It stands fast when a held town awaits it and the unit can take the -2/-2.
                var bound = Rules.Travel.Destination;
                bool wantsReady = Rules.StandingAt(1, bound) != Standing.Friendly;
                foreach (var defender in Rules.Players[1].Field.Where(u => u.IsReady).ToArray())
                {
                    var attack = Rules.Attacks.OrderBy(s => s.Blockers.Count).FirstOrDefault();
                    if (attack != null && Rules.CanBlock(defender, attack))
                        Rules.Block(defender, attack, wantsReady && Rules.Stats(defender).defense - MatchRules.StandFastPenalty > 0);
                }
                Rules.Next(); Sync(); return;
            }
            return; // the human's call
        }
        if (Rules.Active != 1) return;
        var p = Rules.Players[1];
        // The opponent travels wherever the most cards in its hand are waiting; on a tie it stays.
        if (Rules.Stage == MatchStage.Destination)
        {
            var preferred = Rules.PreferredDestination(1);
            if (preferred != null) Rules.ChooseDestination(preferred);
        }
        if ((Rules.Stage == MatchStage.Muster || Rules.Stage == MatchStage.Events) &&
            p.Hand.Any(c => !Rules.CanPlay(c) && Rules.PlayBlockReason(c, includeReadyMana: true) == null))
        {
            var land = p.Field.FirstOrDefault(Rules.CanTapLand);
            if (land != null) { Rules.TapLand(land); Sync(); return; }
        }
        // A destination held against it is fought for with the hardest hitter that can, before playing.
        if (Rules.Stage == MatchStage.Muster && Rules.NeedsSecuring())
        {
            var champion = p.Field.Where(Rules.CanSecure).OrderByDescending(u => Rules.Stats(u).attack).ThenByDescending(u => Rules.Stats(u).defense).FirstOrDefault();
            if (champion != null) { Rules.Secure(champion); Sync(); return; }
        }
        if (Rules.Stage == MatchStage.Realm || Rules.Stage == MatchStage.Muster || Rules.Stage == MatchStage.Events)
        {
            bool played;
            do
            {
                played = false;
                foreach (var card in p.Hand.ToArray())
                    if (Rules.CanPlay(card) && Rules.Play(card, p.Field.FirstOrDefault(u => u.IsCharacter && !u.Wounded) ?? p.Field.FirstOrDefault(u => u.IsCharacter))) played = true;
                // The tap woke the human's ambush: wait for the answer before playing on.
                if (Rules.PendingAmbush != null) { Sync(); return; }
            } while (played);
        }
        Rules.Next(); Sync();
    }
    void UpdateHUD()
    {
        if (headline == null || Rules == null) return;
        string player = Rules.Active == 0 ? "ORREN" : "THE SLEEPLESS EYE";
        headline.text = Rules.Winner >= 0 ? (Rules.Winner == 0 ? "VICTORY — FLOOR CLEARED" : Rules.Winner == 2 ? "DRAW" : "DEFEAT") : $"{player}  /  {(int)Rules.Stage} · {stages[(int)Rules.Stage]}";
        hint.text = selectionHint ?? StageHint();
        if (Rules.Stage == MatchStage.Spoils && Rules.Spoils.Count > 0 && selectionHint == null)
            hint.text = "Assign " + Rules.Spoils.Peek().Card.name + " to a surviving " + (Rules.Spoils.Peek().Offered ? "enemy" : "friendly") + " character.";
        deckLabel.text = $"ORREN {Rules.Players[0].Life}  /  EYE {Rules.Players[1].Life}\nDRAW DECK · {Rules.Players[0].Deck.Count}\nHAND {Rules.Players[0].Hand.Count}/{Rules.Players[0].HandLimit}";
        if (Rules.PendingAmbush != null && selectionHint == null)
            hint.text = Rules.PendingAmbush.Defender == 0
                ? "The enemy taps " + Rules.PendingAmbush.Settlement.name + ". Answer with a glowing card born there, or let them pass."
                : "The Sleepless Eye may answer your tap of " + Rules.PendingAmbush.Settlement.name + "...";
        else if (Rules.Stage == MatchStage.Spoils && Rules.Active == 0 && Rules.MustDiscard(0) && selectionHint == null)
            hint.text = "Hand over its limit: discard " + (Rules.Players[0].Hand.Count - Rules.Players[0].HandLimit) + " glowing " + (Rules.Players[0].Hand.Count - Rules.Players[0].HandLimit == 1 ? "card" : "cards") + " to end the turn.";
        bool humansCall = Rules.Stage == MatchStage.Travel ? HumanRaids || HumanDefends : Rules.Active == 0;
        bool manual = Rules.PendingAmbush == null && Rules.Stage != MatchStage.Draw && Rules.Stage != MatchStage.Spoils && (Rules.HasLegalAction() || HoldingForUndo) && humansCall;
        next.gameObject.SetActive(Rules.Winner >= 0 || manual);
        next.interactable = !Busy && !animating && draws.Count == 0 && (Rules.Winner >= 0 || manual);
        var destination = Rules.Players[0].Destination;
        nextText.text = Rules.Winner >= 0 ? "TOWER" : HumanDefends ? "RESOLVE COMBAT" : HumanRaids ? (Rules.Attacks.Count > 0 ? "ATTACK!" : "LET THEM PASS")
            : Rules.Stage == MatchStage.Destination ? (destination == null ? "SKIP" : "STAY") : "NEXT STAGE";
        banner?.Sync();
        // The popup is the whole Select Destination stage for the human; anywhere else it is closed.
        picker?.Sync(CanInteract && Rules.Active == 0 && Rules.Stage == MatchStage.Destination && Rules.HasLegalAction() ? Rules.RankedDestinations(0) : null);
        undo.gameObject.SetActive(Rules.Winner < 0 && HoldingForUndo);
        undo.interactable = CanInteract;
        offer.gameObject.SetActive(!Busy && Rules.Stage == MatchStage.Spoils && Rules.Spoils.Count > 0 && !Rules.Spoils.Peek().Offered && Rules.Spoils.Peek().Owner == 0);
        decline.gameObject.SetActive(!Busy && HumanAmbushPending);
        decline.interactable = CanInteract;
        stance.gameObject.SetActive(!Busy && HumanDefends && Rules.HasLegalAction());
        stanceText.text = standFast ? "STAND FAST  ·  -2/-2, STAYS READY" : "DEFENDERS TAP  ·  FULL STRENGTH";
        board.SetMatchStatus(Rules.Active == 1, hint.text);
    }
    string StageHint()
    {
        if (Rules.Stage == MatchStage.Draw) return "Replenishing hand... play continues automatically.";
        if (Rules.Active == 0 && Rules.Stage == MatchStage.Realm) return "Glowing hand cards can build your realm: lands and environments. Settlements unlock as destinations once their land is down.";
        if (Rules.Active == 0 && Rules.Stage == MatchStage.Destination)
            return Rules.Players[0].Destination == null ? "Browse the settlements whose land is down and travel to one for this turn. Each stop on the way draws a card."
                : "Travel to a settlement whose land is down (each stop draws a card), or stay at " + Rules.Players[0].Destination.Card.name + ".";
        if (Rules.Active == 0 && Rules.Stage == MatchStage.Muster)
        {
            var destination = Rules.Players[0].Destination;
            if (destination != null && !destination.Secured && !destination.Tapped)
            {
                var dwellers = Rules.Dwellers(destination.Card);
                return destination.Card.name + " is held by " + (dwellers != null ? dwellers.name + " (" + dwellers.GetCombatStats().attack + "/" + dwellers.GetCombatStats().defense + ")" : "its dwellers")
                    + ": click a ready unit to fight them" + (Rules.StandingAt(0, destination.Card) == Standing.Neutral ? " (retention attack: lose and both tap)." : " (a normal attack: they hit back).") + " Armies deploy anywhere regardless.";
            }
            return "Tap ready lands for mana, then deploy armies anywhere; characters, encounters and objects only at your destination, which one play taps."
                + (destination != null ? " Destination: " + destination.Card.name + (destination.Tapped && !destination.Secured ? " (closed this turn)." : ".") : " No destination this turn.");
        }
        if (Rules.Active == 0 && Rules.Stage == MatchStage.Events && Rules.ResolveEvent != null) return "Tap ready lands for mana as needed, then play events.";
        if (Rules.Stage == MatchStage.Travel && Rules.Travel != null)
        {
            string ground = Rules.Ground != TerrainEnum.None ? Rules.Ground + " ground" : "unknown ground";
            if (HumanRaids)
                return "The enemy company crosses " + Rules.Travel.Region + " (" + ground + "). Click glowing units, or hand cards, to fall on it: characters, and armies of that terrain. Hand cards shuffle back into the deck afterwards. " + Rules.Attacks.Count + " committed.";
            if (HumanDefends)
                return "Ambushed in " + Rules.Travel.Region + " (" + ground + "): choose a defender, then the attacker it meets. Only characters and " + Rules.Ground + " armies can stand. Toggle whether defenders tap or stand fast (-2/-2, stay ready for the town). " + Rules.Attacks.Count + " attacks incoming.";
            return Rules.Active == 0 ? "Your company crosses " + Rules.Travel.Region + " (" + ground + ")... the enemy weighs an ambush." : "The enemy company defends in " + Rules.Travel.Region + ".";
        }
        if (Rules.Stage == MatchStage.Events && Rules.ResolveEvent == null)
            return "No playable events. Continuing automatically...";
        return Rules.Message;
    }
    void Sync()
    {
        if (Rules == null) return;
        autoAt = Time.unscaledTime + .5f;
        board.preview?.Hide();
        void Zone(CardZoneVisualizer zone, int player, params CardTypeEnum[] types)
        { zone?.SynchronizeCards(Rules.Players[player].Field.Where(u => types.Contains(u.Card.GetCardType())).Select(u => u.Card)); }
        Zone(board.humanLands, 0, CardTypeEnum.Land); Zone(board.opponentLands, 1, CardTypeEnum.Land);
        // The settlement zone holds one token: the destination. Choosing it happens in the picker popup.
        Zone(board.humanPopulationCenters, 0, CardTypeEnum.PC); Zone(board.opponentPopulationCenters, 1, CardTypeEnum.PC);
        Zone(board.humanArmies, 0, CardTypeEnum.Character, CardTypeEnum.Army); Zone(board.opponentArmies, 1, CardTypeEnum.Character, CardTypeEnum.Army);
        board.environmental?.SynchronizeCards(Rules.Players.SelectMany(p => p.Field).Where(u => u.Card.GetCardType() == CardTypeEnum.Environmental).Select(u => u.Card));
        board.humanDiscard?.SynchronizeCards(Rules.Players[0].Discard); board.opponentDiscard?.SynchronizeCards(Rules.Players[1].Discard);
        board.humanVictoryPoints?.SynchronizeCards(System.Array.Empty<CardData>()); board.opponentVictoryPoints?.SynchronizeCards(System.Array.Empty<CardData>());
        board.maximumHandSize = Mathf.Max(board.maximumHandSize, Rules.Players[0].HandLimit);
        board.hand.SynchronizeCards(Rules.Players[0].Hand);
        UpdateHUD();
    }
    IEnumerator AnimateDraws()
    {
        animating = true;
        // Hide newly drawn faces until each travelling card arrives.
        foreach (var view in board.hand.GetComponentsInChildren<BoardCardView>())
            if (draws.Any(d => d.player == 0 && ReferenceEquals(d.card, view.Data))) view.gameObject.SetActive(false);
        while (draws.Count > 0)
        {
            var draw = draws.Dequeue();
            var panel = BoardPresentation.Panel(transform, "Drawing card", new Color(.07f,.10f,.15f));
            panel.rectTransform.sizeDelta = new Vector2(110,150); BoardPresentation.Border(panel.rectTransform, new Color(.88f,.7f,.36f));
            Label(panel.transform, "✦\nDARK BEFORE DAWN", 14, Vector2.zero, Vector2.one);
            var view = board.hand.GetComponentsInChildren<BoardCardView>(true).FirstOrDefault(v => ReferenceEquals(v.Data, draw.card));
            Vector3 start = draw.player == 0 ? deckAnchor.position : transform.TransformPoint(new Vector3(500,250,0));
            Vector3 end = draw.player == 0 && view != null ? view.transform.position : transform.TransformPoint(new Vector3(0,350,0));
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .55f)
            {
                float s = t*t*(3-2*t); panel.transform.position = Vector3.Lerp(start,end,s) + transform.up * Mathf.Sin(t*Mathf.PI)*90;
                panel.transform.localRotation = Quaternion.Euler(0,0,Mathf.Lerp(-18,0,s));
                yield return null;
            }
            if (view != null) view.gameObject.SetActive(true);
            Destroy(panel.gameObject);
        }
        animating = false;
        AdvanceIfNoActions();
    }
    void OnDestroy() { foreach (var ui in ownedUI) if (ui != null) Destroy(ui); }
}
