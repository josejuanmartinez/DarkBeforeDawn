using UnityEngine;

/// <summary>
/// Resolves a card by name and applies its CardData to the Card on this GameObject. Drop it on a
/// card instance, type a name, and the card renders — the quickest way to see art and layout without
/// writing any wiring code. The custom inspector exposes Apply for prefab/edit-time previews.
///
/// Ported from Runeboard; the only change is that it resolves through CardCatalog (Resources JSON)
/// instead of a live DeckManager singleton.
/// </summary>
[RequireComponent(typeof(Card))]
[DisallowMultipleComponent]
public sealed class CardDataProvider : MonoBehaviour
{
    [Header("Card Data")]
    public string cardName;
    public bool startAsToken = true;
    [Tooltip("Optional deck whose badge should be shown at the card's top right. Leave empty to use the card's own deck.")]
    public string deckId;

    [Header("Runtime")]
    [Tooltip("Initialize the Card from the serialized fields when this component starts in Play Mode.")]
    public bool initializeOnStart;

    [Header("Presentation")]
    public bool suppressHoverEffects;
    public bool useCardArtFolderOnly;
    public bool showRequirementWarnings = true;
    public bool showCloseIcon = true;

    private Card card;

    public Card Card
    {
        get
        {
            if (card == null) card = GetComponent<Card>();
            return card;
        }
    }

    private void Start()
    {
        if (initializeOnStart) Apply();
    }

    /// <summary>Applies the card name and token setting currently stored on the component.</summary>
    public bool Apply()
    {
        return Initialize(cardName, startAsToken);
    }

    /// <summary>Looks up card data by display name and passes it to Card.Initialize.</summary>
    public bool Initialize(string requestedCardName, bool showAsToken)
    {
        if (string.IsNullOrWhiteSpace(requestedCardName))
        {
            Debug.LogWarning($"CardDataProvider on '{name}' needs a card name.", this);
            return false;
        }

        Card target = Card;
        if (target == null)
        {
            Debug.LogError($"CardDataProvider on '{name}' could not find its required Card component.", this);
            return false;
        }

        CardData sourceData = CardCatalog.FindCardByName(requestedCardName.Trim());
        if (sourceData == null)
        {
            Debug.LogWarning($"CardDataProvider on '{name}' could not find card data named '{requestedCardName}'.", this);
            return false;
        }

        // Clone so per-instance presentation state (the deck badge override below, the one-shot
        // typewriter flag) never writes back into the shared catalog entry.
        CardData data = sourceData.Clone();
        if (!string.IsNullOrWhiteSpace(deckId))
        {
            if (CardCatalog.TryResolveDeck(deckId.Trim(), out DeckManifestEntry deck))
            {
                // The catalog hands back the card as its meta deck stores it, which is neutral and
                // unbadged. Naming a deck here re-stamps it the way CardCatalog.GetDeckCards would.
                data.deckId = deck.deckId;
                data.alignment = deck.alignment;
                data.deckSpriteName = deck.deckSpriteName;
            }
            else
            {
                Debug.LogWarning($"CardDataProvider on '{name}' could not find deck '{deckId}'.", this);
            }
        }

        cardName = data.name;
        startAsToken = showAsToken;
        target.SuppressHoverEffects = suppressHoverEffects;
        target.UseCardArtFolderOnly = useCardArtFolderOnly;
        target.ShowRequirementWarnings = showRequirementWarnings;
        target.ShowCloseIcon = showCloseIcon;
        target.Initialize(data, showAsToken);
        return true;
    }
}
