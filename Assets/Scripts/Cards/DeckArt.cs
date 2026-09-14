using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The back of a deck's cards: one emblem per deck, lifted from Runeboard's Assets/Art/Decks and
/// filed under Resources/DeckBacks by this project's deck id (orren_the_kindled.png, ...). Three
/// more stand in for the nations, for a card that came from a shared pool rather than a deck.
/// </summary>
public static class DeckArt
{
    const string Root = "DeckBacks/";
    static readonly Dictionary<string, Sprite> cache = new();

    /// <summary>The back for a deck id, or null when the deck has none filed.</summary>
    public static Sprite Back(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId)) return null;
        if (!cache.TryGetValue(deckId, out var sprite)) cache[deckId] = sprite = Resources.Load<Sprite>(Root + deckId);
        return sprite;
    }

    /// <summary>The nation's emblem: Free Realms, Shadow Dominion, or the Tower Council for everything between.</summary>
    public static Sprite NationBack(int alignment)
        => Back(alignment == CardData.FreePeople ? "nation_free_realms" : alignment == CardData.DarkServants ? "nation_shadow_dominion" : "nation_tower_council");

    /// <summary>The back a card would show face down: its deck's, else its nation's.</summary>
    public static Sprite BackFor(CardData card)
    {
        if (card == null) return null;
        return Back(card.deckId) ?? NationBack(card.alignment);
    }

    /// <summary>The deck badge on a card face: the art library's sprite for its deckSpriteName when it has one, else the deck back.</summary>
    public static Sprite Badge(CardData card)
    {
        if (card == null) return null;
        Sprite sprite = null;
        if (!string.IsNullOrWhiteSpace(card.deckSpriteName)) CardServices.Art?.TryGetSprite(card.deckSpriteName, false, out sprite);
        return sprite != null ? sprite : BackFor(card);
    }
}
