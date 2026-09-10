using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BoardCardLayout { FullRow, TokenGrid, TokenRow }

/// <summary>Fits an ordered card collection into a responsive anchor without pagination.</summary>
public class CardZoneVisualizer : MonoBehaviour
{
    public Board board;
    public BoardCardLayout layout;
    [Min(0)] public float gap = 8;
    [Range(.25f, 1f)] public float minimumVisibleFraction = .65f;
    public bool isHand;
    // Deliberately not serialized: a zone is authored by dropping card prefabs onto the anchor,
    // never by filling in card data field by field in the inspector. This list is runtime state.
    private readonly List<CardData> cards = new();
    private readonly System.Collections.Generic.HashSet<int> tapped = new();
    public bool IsTapped(BoardCardView view) => tapped.Contains(views.IndexOf(view));
    public void Tap(BoardCardView view) { int index = views.IndexOf(view); if (index >= 0) tapped.Add(index); }
    public void ReadyLands() => tapped.Clear();
    protected readonly List<BoardCardView> views = new();
    public IReadOnlyList<CardData> Cards => cards.AsReadOnly();
    public int Count => cards.Count;
    private Vector2 previousSize;
    protected RectTransform Area => (RectTransform)transform;

    /// <summary>True where the zone shows readable card faces: the hand, and the deck piles.</summary>
    /// <remarks>
    /// Every other zone is a token board. This is the whole token/full rule, derived rather than
    /// serialized so no anchor can be set to a presentation its prefabs do not match. `layout` is
    /// only ever an arrangement strategy — it has no say in which prefab a zone takes.
    /// </remarks>
    public virtual bool UsesFullCards => isHand;

    /// <summary>The one prefab this zone accepts, for messages and for validation.</summary>
    public string AcceptedPrefabName => UsesFullCards ? "Card.prefab" : "TokenCard.prefab";

    protected virtual void Start()
    {
        AdoptAuthoredChildren();
        Rebuild();
    }

    // The only way a zone is authored: drop this zone's prefab onto the anchor once per card and
    // name each instance on its CardDataProvider. An authored instance only says *which* card belongs
    // here — the visual the board shows is rebuilt from Board.fullCardPrefab / tokenCardPrefab by
    // AddView — so the instances are consumed once read, which is why this runs at Start and never
    // again.
    private void AdoptAuthoredChildren()
    {
        var authored = new List<CardData>();
        var consumed = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var card = transform.GetChild(i).GetComponent<Card>();
            if (card == null) continue;
            consumed.Add(card.gameObject);
            CardData data = ResolveAuthoredCard(card);
            if (data != null) authored.Add(data);
        }
        if (consumed.Count == 0) return;
        // Switched off rather than destroyed. Hiding them is all the board needs, and these are the
        // only record of how the zone was authored — destroying the scene's own authoring data to
        // save a few inactive GameObjects is a bad trade.
        foreach (var instance in consumed) instance.SetActive(false);
        // A warning rather than the exception SetCards throws: an over-full authored hand should
        // still enter Play mode, just not silently exceed the cap.
        if (isHand && board != null && authored.Count > board.maximumHandSize)
        {
            Debug.LogWarning($"'{name}' was authored with {authored.Count} cards but Board.maximumHandSize " +
                $"is {board.maximumHandSize}; the extras are dropped.", this);
            authored.RemoveRange(board.maximumHandSize, authored.Count - board.maximumHandSize);
        }
        cards.Clear();
        cards.AddRange(authored);
    }

    // A zone takes one prefab and only that one. The two are not interchangeable in either
    // direction: Card.prefab carries no token subtree and TokenCard.prefab carries no RealCard, so
    // the wrong one renders as nothing at all rather than as something merely misshapen.
    private bool IsAcceptedPrefab(Card card)
    {
        if (card.IsTokenOnlyPresentation != UsesFullCards) return true;
        Debug.LogWarning($"'{card.name}' is the wrong prefab for '{name}': this zone takes " +
            $"{AcceptedPrefabName}. Replace the instance; it is ignored.", card);
        return false;
    }

    // A CardDataProvider is the authoring surface: it carries the card name, the optional deck-badge
    // override and the presentation flags, and applying it is what fills in Card.cardData.
    private CardData ResolveAuthoredCard(Card card)
    {
        if (!IsAcceptedPrefab(card)) return null;
        var provider = card.GetComponent<CardDataProvider>();
        if (provider != null && provider.Apply()) return card.cardData;
        if (card.cardData != null) return card.cardData;
        Debug.LogWarning($"'{card.name}' under '{name}' names no card: set Card Name on its " +
            "CardDataProvider, or remove the instance.", card);
        return null;
    }

    protected virtual void OnDisable() { if (board != null && board.preview != null) board.preview.HideFor(this); }
    protected virtual void LateUpdate()
    {
        if (previousSize != Area.rect.size) Arrange();
    }

    public void SetCards(IEnumerable<CardData> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var replacement = new List<CardData>(source);
        if (replacement.Exists(c => c == null)) throw new ArgumentException("Cards cannot contain null entries.");
        if (isHand && replacement.Count > board.maximumHandSize)
            throw new ArgumentException("The hand exceeds Board.maximumHandSize.");
        cards.Clear();
        cards.AddRange(replacement);
        tapped.Clear();
        Rebuild();
    }

    /// <summary>Keep surviving slots and their animation state when match collections change.</summary>
    public void SynchronizeCards(IEnumerable<CardData> source)
    {
        var replacement = source.ToList();
        if (cards.SequenceEqual(replacement)) return;
        if (this is DeckVisualizer) { SetCards(replacement); return; }
        if (replacement.Any(c => c == null) || (isHand && replacement.Count > board.maximumHandSize))
            throw new ArgumentException("Invalid match zone contents.");
        var previous = views.ToList();
        var tappedCards = tapped.Where(i => i >= 0 && i < cards.Count).Select(i => cards[i]).ToHashSet();
        cards.Clear(); cards.AddRange(replacement); tapped.Clear(); views.Clear();
        for (int i = 0; i < cards.Count; i++)
        {
            var retained = previous.FirstOrDefault(v => ReferenceEquals(v.Data, cards[i]));
            if (retained != null) { views.Add(retained); previous.Remove(retained); }
            else AddView(cards[i]);
            if (tappedCards.Contains(cards[i])) tapped.Add(i);
        }
        foreach (var removed in previous)
        {
            board.preview?.HideFor(this);
            removed.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(removed.gameObject); else DestroyImmediate(removed.gameObject);
        }
        Arrange();
    }

    public bool TryAdd(CardData card)
    {
        if (card == null || (isHand && Count >= board.maximumHandSize)) return false;
        cards.Add(card);
        Rebuild();
        return true;
    }

    public bool Remove(CardData card)
    {
        int index = cards.IndexOf(card);
        if (index < 0) return false;
        cards.RemoveAt(index);
        var shifted = new List<int>();
        foreach (int slot in tapped) if (slot != index) shifted.Add(slot > index ? slot - 1 : slot);
        tapped.Clear();
        foreach (int slot in shifted) tapped.Add(slot);
        Rebuild();
        return true;
    }

    /// <summary>Recreate visual clones without changing the collection or authored instances.</summary>
    public virtual void RefreshSkin() { Rebuild(); }

    protected virtual void Rebuild()
    {
        ClearViews();
        foreach (var card in cards) if (card != null) AddView(card);
        Arrange();
    }

    protected void ClearViews()
    {
        if (board != null && board.preview != null) board.preview.HideFor(this);
        foreach (var view in views)
            if (view != null)
            {
                view.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(view.gameObject);
                else DestroyImmediate(view.gameObject);
            }
        views.Clear();
    }

    protected BoardCardView AddView(CardData data)
    {
        var go = new GameObject("Card - " + data.name, typeof(RectTransform), typeof(BoardCardView));
        go.transform.SetParent(transform, false);
        var view = go.GetComponent<BoardCardView>();
        view.Initialize(this, data, !UsesFullCards);
        views.Add(view);
        return view;
    }

    public void Arrange()
    {
        previousSize = Area.rect.size;
        if (views.Count == 0) return;
        var rects = new List<RectTransform>(views.Count);
        foreach (var view in views) rects.Add(view.Rect);
        LayoutSlots(rects, views[0].NaturalSize);
    }

    // Positions `slots` inside this anchor, each one `natural` units across before the uniform fit
    // scale. Split out of Arrange so the editor can preview authored children through exactly the
    // same math the board uses at runtime — what you lay out while authoring is what Play produces.
    public void LayoutSlots(IReadOnlyList<RectTransform> slots, Vector2 natural)
    {
        int n = slots.Count;
        Vector2 area = Area.rect.size;
        if (n == 0 || area.x <= 0 || area.y <= 0 || natural.x <= 0 || natural.y <= 0) return;
        float width = area.x, height = area.y;
        int columns = n, rows = 1;
        float scale;
        // Limit gaps as collections grow so even very dense layouts stay within their anchor.
        float spacing = Mathf.Min(gap, width / Mathf.Max(1, n * 2));
        if (layout == BoardCardLayout.TokenGrid)
        {
            float best = 0;
            for (int c = 1; c <= n; c++)
            {
                int r = Mathf.CeilToInt((float)n / c);
                float candidate = Mathf.Min(width / c / natural.x, height / r / natural.y);
                if (candidate > best) { best = candidate; columns = c; rows = r; }
            }
            spacing = Mathf.Min(gap, Mathf.Min(width / columns, height / rows) * .1f);
            scale = Mathf.Min((width - spacing * (columns - 1)) / columns / natural.x,
                (height - spacing * (rows - 1)) / rows / natural.y);
        }
        else
        {
            float overlap = layout == BoardCardLayout.FullRow ? minimumVisibleFraction : 1;
            scale = Mathf.Min(height / natural.y, width / (natural.x * (1 + (n - 1) * overlap)));
            if (isHand)
                scale = Mathf.Min(scale, width / (natural.x * Mathf.Max(1, board.defaultHandSize)));
        }
        float w = natural.x * scale, h = natural.y * scale;
        float step = columns == 1 ? 0 : Mathf.Min(w + spacing, (width - w) / (columns - 1));
        float contentHeight = rows * h + (rows - 1) * spacing;
        for (int i = 0; i < n; i++)
        {
            var rect = slots[i];
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = natural;
            rect.localScale = Vector3.one * scale;
            int row = i / columns, col = i % columns;
            int rowCount = Mathf.Min(columns, n - row * columns);
            rect.anchoredPosition = new Vector2((col - (rowCount - 1) * .5f) * step,
                contentHeight * .5f - h * .5f - row * (h + spacing));
        }
    }
}
