using System;
using System.Collections.Generic;
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
    [SerializeField] private List<CardData> cards = new();
    protected readonly List<BoardCardView> views = new();
    public IReadOnlyList<CardData> Cards => cards.AsReadOnly();
    public int Count => cards.Count;
    private Vector2 previousSize;
    protected RectTransform Area => (RectTransform)transform;

    protected virtual void Start() { Rebuild(); }
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
        cards = replacement;
        Rebuild();
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
        if (!cards.Remove(card)) return false;
        Rebuild();
        return true;
    }

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
            if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
        views.Clear();
    }

    protected BoardCardView AddView(CardData data)
    {
        var go = new GameObject("Card - " + data.name, typeof(RectTransform), typeof(BoardCardView));
        go.transform.SetParent(transform, false);
        var view = go.GetComponent<BoardCardView>();
        view.Initialize(this, data, layout != BoardCardLayout.FullRow);
        views.Add(view);
        return view;
    }

    public void Arrange()
    {
        previousSize = Area.rect.size;
        int n = views.Count;
        if (n == 0 || previousSize.x <= 0 || previousSize.y <= 0) return;
        Vector2 natural = views[0].NaturalSize;
        float width = previousSize.x, height = previousSize.y;
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
            var rect = views[i].Rect;
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
