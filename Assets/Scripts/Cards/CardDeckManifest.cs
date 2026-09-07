using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

// The JSON shapes the card decks are authored in. The per-card shape is still Runeboard's, so a card
// object copied from that project deserializes here unchanged; the deck shape has since diverged.
//
// Cards are stored exactly once. Each card type has a meta deck that owns every card of that type
// (Cards/Meta/*.json, isMetaDeck in the manifest); every other deck is a list of cardRefs pointing
// into those pools. A card that four factions can draw is one object referenced four times, not four
// copies that drift apart.

[Serializable]
public class CardsManifest
{
    public int deckCount = 0;
    public List<DeckManifestEntry> decks = new();
}

[Serializable]
public class DeckManifestEntry
{
    public string deckId;
    public string nation;
    public string thematic;
    public int alignment;
    public string resourcePath;
    public int cardCount;
    public bool sharedToAll;
    // A meta deck owns the card data for one card type and is never dealt to a player; every other
    // deck holds only references into the meta decks. Always paired with excluded.
    public bool isMetaDeck;
    public string deckSpriteName;
    // Orthogonal to sharedToAll: sharedToAll means "not tied to one nation", excluded means "world
    // content (artifacts, encounters), never part of any player's own drawable pool". A deck can be
    // both, or neither.
    public bool excluded;
}

[Serializable]
public class DeckData
{
    public string deckId;
    public string nation;
    public int alignment;
    public string avatarCharacter = string.Empty;
    // Exactly one of these carries content: a meta deck fills cards and leaves cardRefs empty, a
    // reference deck does the reverse. Both are always written out, because JsonUtility has no way
    // to omit a field.
    public List<CardData> cards = new();
    public List<int> cardRefs = new();
}

// Loads decks from Resources and finds cards by name or id. Replaces the lookup half of Runeboard's
// DeckManager (its draw/shuffle/consume half was gameplay and stayed behind).
//
// Expects a manifest TextAsset at Resources/Cards.json whose entries' resourcePath values name
// further TextAssets under Resources. With no manifest present every lookup returns null and the
// card face falls back to whatever CardData you build by hand.
public static partial class CardCatalog
{
    private const string ManifestResourceName = "Cards";

    // Card ids are blocked by type: an id divided by this is its CardTypeEnum ordinal, so the id
    // alone names the meta deck that owns the card. The block is far wider than the largest type
    // (Event, at a few hundred) to leave room without renumbering.
    public const int CardIdBlockSize = 10000;

    public static int CardIdBlockStart(CardTypeEnum cardType) => (int)cardType * CardIdBlockSize;

    public static CardTypeEnum CardTypeForId(int cardId) => (CardTypeEnum)(cardId / CardIdBlockSize);

    public static string MetaDeckIdFor(CardTypeEnum cardType) =>
        cardType == CardTypeEnum.Unknown ? string.Empty : $"meta_{cardType.ToString().ToLowerInvariant()}";

    [AutoStaticsCleanup]
    private static CardsManifest manifest;
    [AutoStaticsCleanup]
    private static Dictionary<int, CardData> byId;
    [AutoStaticsCleanup]
    private static Dictionary<string, CardData> byName;
    // Resolved reference decks, built on first request and kept because resolving one clones every
    // card in it.
    [AutoStaticsCleanup]
    private static Dictionary<string, List<CardData>> deckCards;

    public static bool IsLoaded => byId != null;

    // Drop the cached tables so the next lookup re-reads the JSON. Useful after editing a deck file
    // in play mode.
    public static void Invalidate()
    {
        manifest = null;
        byId = null;
        byName = null;
        deckCards = null;
    }

    public static CardsManifest GetManifest()
    {
        if (manifest != null) return manifest;

        TextAsset manifestAsset = Resources.Load<TextAsset>(ManifestResourceName);
        if (manifestAsset == null) return null;

        manifest = JsonUtility.FromJson<CardsManifest>(manifestAsset.text);
        return manifest;
    }

    public static CardData FindCardByName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName)) return null;
        EnsureLoaded();
        return byName != null && byName.TryGetValue(CardNameUtility.Normalize(cardName), out CardData card)
            ? card
            : null;
    }

    // The reliable lookup: unlike a name, a cardId picks out exactly one card. A handful of names are
    // deliberately shared by cards of different types — Athelas is both an Object and an Event — and
    // FindCardByName can only ever return one of them.
    public static CardData FindCardById(int cardId)
    {
        EnsureLoaded();
        return byId != null && byId.TryGetValue(cardId, out CardData card) ? card : null;
    }

    // Every distinct card in the game, each appearing once however many decks reference it.
    public static IEnumerable<CardData> AllCards()
    {
        EnsureLoaded();
        return byId != null ? byId.Values : System.Linq.Enumerable.Empty<CardData>();
    }

    // The cards a deck holds. For a meta deck that is the pool itself; for a reference deck it is a
    // clone of each referenced card, stamped with this deck's identity — the same card drawn from
    // Gandalf's deck and from Sauron's differs only in the badge it wears.
    //
    // The returned list is the catalog's own and is reused across calls, so treat it as read-only.
    public static IReadOnlyList<CardData> GetDeckCards(string deckId)
    {
        EnsureLoaded();
        if (deckCards == null || string.IsNullOrWhiteSpace(deckId)) return Array.Empty<CardData>();
        if (deckCards.TryGetValue(deckId, out List<CardData> cached)) return cached;

        if (!TryResolveDeck(deckId, out DeckManifestEntry entry)) return Array.Empty<CardData>();

        List<CardData> resolved = new();
        DeckData deck = LoadDeck(entry);

        if (entry.isMetaDeck)
        {
            // A meta deck already holds its cards, and they are the pool everything else clones
            // from, so they are handed back as they are rather than re-stamped.
            if (deck?.cards != null)
            {
                foreach (CardData card in deck.cards)
                {
                    if (card != null && byId.TryGetValue(card.cardId, out CardData owned)) resolved.Add(owned);
                }
            }
            deckCards[entry.deckId] = resolved;
            return resolved;
        }

        if (deck?.cardRefs != null)
        {
            foreach (int cardId in deck.cardRefs)
            {
                if (!byId.TryGetValue(cardId, out CardData source))
                {
                    Debug.LogWarning($"{nameof(CardCatalog)}: deck '{entry.deckId}' references card id {cardId}, which no meta deck owns.");
                    continue;
                }

                CardData copy = source.Clone();
                copy.deckId = entry.deckId;
                copy.alignment = entry.alignment;
                copy.deckSpriteName = entry.deckSpriteName;
                resolved.Add(copy);
            }
        }

        deckCards[entry.deckId] = resolved;
        return resolved;
    }

    public static bool TryResolveDeck(string deckId, out DeckManifestEntry deck)
    {
        deck = null;
        if (string.IsNullOrWhiteSpace(deckId)) return false;

        CardsManifest loaded = GetManifest();
        if (loaded?.decks == null) return false;

        foreach (DeckManifestEntry candidate in loaded.decks)
        {
            if (candidate != null && string.Equals(candidate.deckId, deckId, StringComparison.OrdinalIgnoreCase))
            {
                deck = candidate;
                return true;
            }
        }
        return false;
    }

    // Only the meta decks are read here. Reference decks hold no card data of their own, so there is
    // nothing to load until something actually asks for one via GetDeckCards.
    private static void EnsureLoaded()
    {
        if (byId != null) return;
        byId = new Dictionary<int, CardData>();
        byName = new Dictionary<string, CardData>();
        deckCards = new Dictionary<string, List<CardData>>(StringComparer.OrdinalIgnoreCase);

        CardsManifest loaded = GetManifest();
        if (loaded?.decks == null) return;

        foreach (DeckManifestEntry entry in loaded.decks)
        {
            if (entry == null || !entry.isMetaDeck) continue;

            DeckData deck = LoadDeck(entry);
            if (deck?.cards == null) continue;

            foreach (CardData card in deck.cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.name)) continue;

                if (byId.ContainsKey(card.cardId))
                {
                    Debug.LogWarning($"{nameof(CardCatalog)}: card id {card.cardId} ('{card.name}') is claimed by more than one meta deck; keeping the first.");
                    continue;
                }
                byId[card.cardId] = card;

                // A name can be shared by cards of different types, so the first meta deck listed
                // wins and the rest stay reachable only by id.
                string key = CardNameUtility.Normalize(card.name);
                if (!byName.ContainsKey(key)) byName[key] = card;
            }
        }
    }

    private static DeckData LoadDeck(DeckManifestEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.resourcePath)) return null;

        TextAsset deckAsset = Resources.Load<TextAsset>(entry.resourcePath);
        return deckAsset != null ? JsonUtility.FromJson<DeckData>(deckAsset.text) : null;
    }
}
