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
    public string humanDeckId, opponentDeckId;
    public int startingLife = 20;
    [Tooltip("Build a playable opening from the chosen catalog deck while event effects are being authored.")]
    public bool useStarterDeck = true;
    public MatchRules Rules { get; private set; }
    public bool Busy { get; private set; } = true;
    public int Floor { get; private set; }
    Board board;
    Text headline, hint, nextText, deckLabel;
    Button next, offer;
    RectTransform deckAnchor;
    MatchCinematic cinematic;
    CardData pendingObject;
    MatchRules.Unit pendingAttacker, pendingDefender;
    readonly Queue<(CardData card, int player)> draws = new();
    readonly List<GameObject> ownedUI = new();
    bool animating;
    float aiAt;
    float autoAt;
    string selectionHint;
    readonly string[] stages = { "", "REPLENISH", "BUILD YOUR REALM", "GATHER MANA", "MUSTER", "EVENTS", "DECLARE ATTACKS", "ASSIGN DEFENDERS", "RECOVER OBJECTS" };

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
        Rules = new MatchRules();
        for (int i = 0; i < 2; i++)
        {
            Rules.Players[i].Life = startingLife;
            Rules.Players[i].HandLimit = board.defaultHandSize;
            string id = i == 0 ? humanDeckId : opponentDeckId;
            var avatar = CardCatalog.FindCardByName(i == 0 ? board.humanAvatarCardName : board.opponentAvatarCardName);
            if (string.IsNullOrWhiteSpace(id)) id = avatar?.deckId;
            var cards = CardCatalog.GetDeckCards(id).Select(c => c.Clone()).ToList();
            if (cards.Count == 0) { selectionHint = "Assign both match deck IDs on TowerMatchController."; Busy = true; return; }
            for (int n = cards.Count - 1; n > 0; n--) { int j = Random.Range(0, n + 1); (cards[n], cards[j]) = (cards[j], cards[n]); }
            if (useStarterDeck) cards = StarterDeck(cards);
            Rules.Players[i].Deck.AddRange(cards);
        }
        Rules.Drawn += (card, player) => draws.Enqueue((card, player));
        Sync();
    }
    static List<CardData> StarterDeck(List<CardData> catalog)
    {
        // Preserve printed costs and identities; select only the implemented deployment types.
        var supported = catalog.Where(c => c.GetCardType() == CardTypeEnum.Land || c.GetCardType() == CardTypeEnum.PC ||
            c.GetCardType() == CardTypeEnum.Character || c.GetCardType() == CardTypeEnum.Army || c.GetCardType() == CardTypeEnum.Object || c.GetCardType() == CardTypeEnum.Environmental).ToList();
        var lands = supported.Where(c => c.GetCardType() == CardTypeEnum.Land).ToList();
        var opening = lands.Take(3).ToList();
        var pc = supported.FirstOrDefault(c => c.GetCardType() == CardTypeEnum.PC && opening.Any(l => l.name == c.region));
        if (pc != null) opening.Add(pc);
        var army = supported.Where(c => c.GetCardType() == CardTypeEnum.Army).OrderBy(c => c.GetTotalGoldCost()+c.jokerRequired+c.ironRequired+c.steelRequired+c.mithrilRequired+c.leatherRequired+c.mountsRequired+c.timberRequired).FirstOrDefault();
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
        headline = Label(bar.transform, "THE ASCENT", 20, new Vector2(.02f,.46f), new Vector2(.76f,.98f));
        hint = Label(bar.transform, "Orren vs The Sleepless Eye", 12, new Vector2(.02f,.02f), new Vector2(.76f,.46f));
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
    public bool IsActionable(BoardCardView view)
    {
        if (!CanInteract || view == null) return false;
        var unit = Unit(view);
        if (pendingObject != null) return unit != null && unit.Owner == 0 && unit.IsCharacter && Rules.PlayBlockReason(pendingObject, unit, false) == null;
        if (pendingAttacker != null) return Rules.AttackBlockReason(pendingAttacker, unit, false) == null;
        if (Rules.Stage == MatchStage.Spoils) return unit != null && Rules.Recipients().Contains(unit);
        if (Rules.Stage == MatchStage.Defend && Rules.Active == 1)
            return unit != null && (Rules.Attacks.Any(a => Rules.CanBlock(unit, a)) ||
                pendingDefender != null && Rules.Attacks.Any(a => a.Attacker == unit && Rules.CanBlock(pendingDefender, a)));
        return CanPlay(view) || CanTap(view) || (Rules.Active == 0 && Rules.CanAttack(unit));
    }
    public string ActionLabel(BoardCardView view)
    {
        if (!IsActionable(view)) return null;
        if (pendingObject != null || pendingAttacker != null || Rules.Stage == MatchStage.Spoils) return "SELECT";
        if (view.Zone == board.hand) return "PLAY CARD";
        return Rules.Stage == MatchStage.Mana ? "TAP LAND" : Rules.Stage == MatchStage.Attack ? "ATTACK" : "DEFEND";
    }
    public string InspectionHint(BoardCardView view)
    {
        if (Rules == null || view == null) return null;
        var unit = Unit(view);
        if (Rules.IsNewUnit(unit)) return "New unit: untapped; can defend now, attack next turn (unless Mounted).";
        if (view.Zone == board.hand && Rules.Active == 0) return Rules.PlayBlockReason(view.Data);
        if (unit != null && Rules.Stage == MatchStage.Attack) return Rules.AttackBlockReason(unit);
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
        if (!CanPlay(view)) { selectionHint = InspectionHint(view); UpdateHUD(); return false; }
        if (view.Data.GetCardType() == CardTypeEnum.Object && Rules.Stage == MatchStage.Muster)
        { pendingObject = view.Data; selectionHint = "Select your character to carry " + pendingObject.name + "."; board.preview?.Hide(); UpdateHUD(); return true; }
        bool result = Rules.Play(view.Data); selectionHint = null; Sync(); return result;
    }
    public bool Select(BoardCardView view)
    {
        if (!CanInteract) return false;
        if (CanTap(view)) return Tap(view);
        var unit = Unit(view);
        if (pendingObject != null)
        {
            if (Rules.Play(pendingObject, unit)) { pendingObject = null; selectionHint = null; }
            else selectionHint = Rules.Message;
            Sync(); return true;
        }
        if (unit == null) return false;
        if (Rules.Stage == MatchStage.Spoils && Rules.Spoils.Count > 0)
        { Rules.Transfer(unit); selectionHint = null; Sync(); return true; }
        if (Rules.Stage == MatchStage.Attack && Rules.Active == 0)
        {
            if (pendingAttacker == null && !Rules.CanAttack(unit)) { selectionHint = Rules.AttackBlockReason(unit); UpdateHUD(); return false; }
            if (pendingAttacker != null)
            { if (Rules.Attack(pendingAttacker, unit)) { pendingAttacker = null; selectionHint = null; } else selectionHint = Rules.Message; }
            else if (unit.Owner == 0 && Rules.CanChooseTarget(unit)) { pendingAttacker = unit; selectionHint = "Select an enemy target for " + unit.Card.name; }
            else { Rules.Attack(unit); selectionHint = null; }
            Sync(); return true;
        }
        if (Rules.Stage == MatchStage.Defend && Rules.Active == 1)
        {
            if (unit.Owner == 0 && Rules.Attacks.Any(a => Rules.CanBlock(unit, a))) { pendingDefender = unit; selectionHint = "Select the attacker this unit will defend against."; }
            else if (pendingDefender != null)
            {
                var strike = Rules.Attacks.FirstOrDefault(s => s.Attacker == unit);
                if (Rules.Block(pendingDefender, strike)) { pendingDefender = null; selectionHint = null; }
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
        if (Rules.Active == 1 && Rules.Stage != MatchStage.Defend && Rules.Stage != MatchStage.Spoils) return;
        pendingObject = null; pendingAttacker = pendingDefender = null; selectionHint = null;
        Rules.Next(); Sync();
    }
    /// <summary>Called only after draw animations finish. One transition per tick avoids runaway empty turns.</summary>
    public bool AdvanceIfNoActions()
    {
        if (!CanInteract || Rules.HasLegalAction()) return false;
        pendingObject = null; pendingAttacker = pendingDefender = null; selectionHint = null;
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
        if (Rules.Stage == MatchStage.Spoils && Rules.Spoils.Count > 0)
        {
            var loot = Rules.Spoils.Peek(); int chooser = loot.Offered ? 1-loot.Owner : loot.Owner;
            if (!Rules.Recipients().Any()) { Rules.OfferLoot(); Sync(); }
            else if (chooser == 1) { Rules.Transfer(Rules.Recipients().First()); Sync(); }
            return;
        }
        if (Rules.Stage == MatchStage.Defend && Rules.Active == 0)
        {
            foreach (var defender in Rules.Players[1].Field.Where(u => u.IsCombatant && !u.Tapped).ToArray())
            {
                var attack = Rules.Attacks.OrderBy(s => s.Blockers.Count).FirstOrDefault();
                if (attack != null && Rules.CanBlock(defender, attack)) Rules.Block(defender, attack);
            }
            Rules.Next(); Sync(); return;
        }
        if (Rules.Active != 1 || Rules.Stage == MatchStage.Defend) return;
        var p = Rules.Players[1];
        if (Rules.Stage == MatchStage.Mana)
        {
            var land = p.Field.FirstOrDefault(Rules.CanTapLand);
            if (land != null) { Rules.TapLand(land); Sync(); return; }
        }
        if (Rules.Stage == MatchStage.Realm || Rules.Stage == MatchStage.Muster || Rules.Stage == MatchStage.Events)
        {
            bool played;
            do { played = false; foreach (var card in p.Hand.ToArray()) if (Rules.CanPlay(card) && Rules.Play(card, p.Field.FirstOrDefault(u => u.IsCharacter))) played = true; } while (played);
        }
        if (Rules.Stage == MatchStage.Attack)
            foreach (var u in p.Field.Where(Rules.CanAttack).ToArray()) Rules.Attack(u, Rules.CanChooseTarget(u) ? Rules.Players[0].Field.FirstOrDefault(x => x.IsCombatant) : null);
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
        bool manual = Rules.Stage != MatchStage.Draw && Rules.Stage != MatchStage.Spoils && Rules.HasLegalAction() &&
            (Rules.Active == 0 && Rules.Stage != MatchStage.Defend || Rules.Active == 1 && Rules.Stage == MatchStage.Defend);
        next.gameObject.SetActive(Rules.Winner >= 0 || manual);
        next.interactable = !Busy && !animating && draws.Count == 0 && (Rules.Winner >= 0 || manual);
        nextText.text = Rules.Winner >= 0 ? "TOWER" : Rules.Stage == MatchStage.Defend ? "RESOLVE COMBAT" : "NEXT STAGE";
        offer.gameObject.SetActive(!Busy && Rules.Stage == MatchStage.Spoils && Rules.Spoils.Count > 0 && !Rules.Spoils.Peek().Offered && Rules.Spoils.Peek().Owner == 0);
        board.SetMatchStatus(Rules.Active == 1, hint.text);
    }
    string StageHint()
    {
        if (Rules.Stage == MatchStage.Draw) return "Replenishing hand... play continues automatically.";
        if (Rules.Active == 0 && Rules.Stage == MatchStage.Realm) return "Glowing hand cards can build your realm: lands, PCs and environments.";
        if (Rules.Active == 0 && Rules.Stage == MatchStage.Mana) return "Click a glowing land to gather mana. Each land taps once.";
        if (Rules.Active == 0 && Rules.Stage == MatchStage.Muster) return "Glowing hand cards can deploy. New units can defend now and attack next turn.";
        if (Rules.Stage == MatchStage.Attack && Rules.Active == 0)
            return "Click ready units to attack. " + Rules.Attacks.Count + " committed. " + Rules.Message;
        if (Rules.Stage == MatchStage.Defend && Rules.Active == 1)
            return Rules.Attacks.Count == 0 ? "No enemy attacks. Resolve combat to continue." : "Choose your defender, then an enemy attacker. " + Rules.Attacks.Count + " attacks incoming.";
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
