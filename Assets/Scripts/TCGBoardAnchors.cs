using System;
using UnityEngine;

/// <summary>Card placement containers in the TCG board's canvas coordinates.</summary>
public sealed class TCGBoardAnchors : MonoBehaviour
{
    [Serializable]
    public sealed class PlayerAnchors
    {
        public RectTransform armiesAndCharacters;
        public RectTransform lands;
        public RectTransform populationCenters;
        public RectTransform victoryPointsDeck;
        public RectTransform discardedDeck;
    }

    public PlayerAnchors opponent = new PlayerAnchors();
    public PlayerAnchors human = new PlayerAnchors();
    public RectTransform environmentalDecks;
    public RectTransform currentHand;
}
