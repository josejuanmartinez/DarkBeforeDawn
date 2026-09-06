using UnityEngine;

/// <summary>Presentation configuration and entry points for the board's card collections.</summary>
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

    private void OnValidate()
    {
        maximumHandSize = Mathf.Max(1, maximumHandSize);
        defaultHandSize = Mathf.Clamp(defaultHandSize, 0, maximumHandSize);
    }
}
