using UnityEngine;

/// <summary>Board collections, player avatars and material payment entry points.</summary>
[DefaultExecutionOrder(-50)]
public sealed class Board : MonoBehaviour
{
    [Min(0)] public int defaultHandSize = 5;
    [Min(1)] public int maximumHandSize = 7;
    public GameObject fullCardPrefab;
    public GameObject tokenCardPrefab;
    public Font interfaceFont;
    public BoardCardPreview preview;
    public CardZoneVisualizer hand;
    public CardZoneVisualizer environmental;
    public CardZoneVisualizer opponentArmies, opponentLands, opponentPopulationCenters;
    public CardZoneVisualizer humanArmies, humanLands, humanPopulationCenters;
    public DeckVisualizer opponentVictoryPoints, opponentDiscard, humanVictoryPoints, humanDiscard;
    [Tooltip("Catalog card name displayed as your persistent avatar.")]
    public string humanAvatarCardName;
    [Tooltip("Catalog card name displayed as the opponent's persistent avatar.")]
    public string opponentAvatarCardName;
    private readonly PlayerMaterials humanMaterials = new(), opponentMaterials = new();
    public TowerMatchController Match => GetComponent<TowerMatchController>();
    public PlayerMaterials HumanMaterials => Match != null && Match.Rules != null ? Match.Rules.Players[0].Mana : humanMaterials;
    public PlayerMaterials OpponentMaterials => Match != null && Match.Rules != null ? Match.Rules.Players[1].Mana : opponentMaterials;
    public bool IsOpponentTurn { get; private set; }
    public string ActionStatus { get; private set; } = "Click a land to tap it. Click a hand card to play it.";

    public bool IsTapped(BoardCardView view) => Match != null ? Match.IsTapped(view) : view != null && view.Zone != null && view.Zone.IsTapped(view);
    public bool CanTap(BoardCardView view) => view != null && view.Zone != null &&
        (view.Zone == humanLands || view.Zone == opponentLands) &&
        view.gameObject.activeInHierarchy && view.Data.GetCardType() == CardTypeEnum.Land && !IsTapped(view) &&
        (Match == null || Match.CanTap(view));

    public bool TryTap(BoardCardView view)
    {
        if (Match != null) return Match.Tap(view);
        if (!CanTap(view)) return false;
        view.Zone.Tap(view);
        (IsOpponentZone(view.Zone) ? OpponentMaterials : HumanMaterials).Grant(view.Data);
        ActionStatus = view.Data.name + " tapped: materials added.";
        return true;
    }

    public bool TryPlay(BoardCardView view)
    {
        if (Match != null) return Match.Play(view);
        if (view == null || view.Zone != hand || !view.gameObject.activeInHierarchy) return false;
        var data = view.Data;
        CardZoneVisualizer destination = data.GetCardType() switch {
            CardTypeEnum.Land => humanLands, CardTypeEnum.PC => humanPopulationCenters,
            CardTypeEnum.Army or CardTypeEnum.Character or CardTypeEnum.Encounter => humanArmies,
            CardTypeEnum.Environmental => environmental,
            CardTypeEnum.Action or CardTypeEnum.Event or CardTypeEnum.Spell or CardTypeEnum.Object => humanDiscard,
            _ => null
        };
        if (destination == null) { ActionStatus = "No destination configured for this card."; return false; }
        if (!HumanMaterials.TrySpend(data)) { ActionStatus = "Not enough materials to play " + data.name + "."; return false; }
        hand.Remove(data);
        destination.TryAdd(data);
        ActionStatus = data.name + " played; material cost paid.";
        return true;
    }

    /// <summary>Ready the next player's lands while retaining both material pools.</summary>
    public void BeginTurn(bool opponent)
    {
        IsOpponentTurn = opponent;
        (opponent ? opponentLands : humanLands)?.ReadyLands();
        ActionStatus = opponent ? "Opponent lands readied." : "Your lands readied.";
    }

    /// <summary>Manual board control until a match controller owns the opponent's turn.</summary>
    public void EndTurn()
    {
        if (Match != null) { Match.Advance(); return; }
        var lands = IsOpponentTurn ? opponentLands : humanLands;
        if (lands != null)
            foreach (var view in lands.GetComponentsInChildren<BoardCardView>()) TryTap(view);
        BeginTurn(!IsOpponentTurn);
    }

    public void SetMatchStatus(bool opponent, string message) { IsOpponentTurn = opponent; ActionStatus = message; }

    /// <summary>Ownership is not stored on a zone; it is which of the fields above the zone is.</summary>
    public bool IsOpponentZone(CardZoneVisualizer zone) => zone != null &&
        (zone == opponentArmies || zone == opponentLands || zone == opponentPopulationCenters
         || zone == opponentVictoryPoints || zone == opponentDiscard);

    private void OnValidate()
    {
        maximumHandSize = Mathf.Max(1, maximumHandSize);
        defaultHandSize = Mathf.Clamp(defaultHandSize, 0, maximumHandSize);
    }
}
