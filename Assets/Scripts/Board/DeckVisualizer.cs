using UnityEngine;
using UnityEngine.UI;

/// <summary>One visible full card; browsing never changes the collection's order.</summary>
public sealed class DeckVisualizer : CardZoneVisualizer
{
    /// <summary>A pile shows the face of its top card, so it takes full cards, never tokens.</summary>
    public override bool UsesFullCards => true;

    public int SelectedIndex { get; private set; } = -1;
    private Text counter;
    private Image empty;
    public CardData SelectedCard => SelectedIndex >= 0 && SelectedIndex < Count ? Cards[SelectedIndex] : null;

    public override void RefreshSkin()
    {
        int selection = SelectedIndex;
        Rebuild();
        SelectedIndex = Count == 0 ? -1 : Mathf.Clamp(selection, 0, Count - 1);
    }

    protected override void Rebuild()
    {
        var skin = BoardPresentation.SkinFor(transform);
        ClearViews();
        SelectedIndex = Count - 1;
        if (SelectedCard != null) AddView(SelectedCard);
        if (counter == null)
        {
            // Old scene versions serialized generated placeholders. Remove those before creating
            // the live pair; otherwise empty piles leave several pale rectangles on the table.
            for (int i=transform.childCount-1;i>=0;i--)
            {
                var child=transform.GetChild(i);
                if (child.name != "Empty pile" && child.name != "Card count") continue;
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            var placeholder = new GameObject("Empty pile", typeof(RectTransform), typeof(Image));
            placeholder.transform.SetParent(transform, false);
            empty = placeholder.GetComponent<Image>();
            empty.raycastTarget = false;
            var er = (RectTransform)placeholder.transform;
            er.offsetMin = er.offsetMax = Vector2.zero;
            var go = new GameObject("Card count", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            counter = go.GetComponent<Text>(); counter.font = board.interfaceFont;
            counter.alignment = TextAnchor.MiddleCenter;
            counter.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
        }
        var engraving = empty.GetComponentInChildren<BoardSurface>();
        if (engraving == null)
        {
            empty.color = skin.colors.ink;
            BoardSurface.Dress(empty, skin.colors.muted, true, true);
            BoardPresentation.TextLabel(empty.transform, "\u2014", board.interfaceFont, 14, skin.colors.muted,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        }
        else
        {
            engraving.surface = skin.colors.ink;
            engraving.accent = skin.colors.muted;
            engraving.SetVerticesDirty();
        }
        BoardPresentation.Stretch(empty.rectTransform, skin.piles.emptyBounds.min, skin.piles.emptyBounds.max);
        counter.font = board.interfaceFont;
        counter.fontSize = skin.piles.counterFontSize;
        counter.color = skin.colors.ivory;
        counter.rectTransform.sizeDelta = skin.piles.counterSize;
        empty.gameObject.SetActive(Count == 0 && !skin.openTable);
        counter.text = board.GetComponent<BoardPresentation>() != null ? "" : Count.ToString();
        counter.transform.SetAsLastSibling();
        Arrange();
    }

    public void ResetSelection() { SelectedIndex = Count - 1; }
    public void Browse(int direction)
    {
        if (Count == 0) return;
        SelectedIndex = Mathf.Clamp(SelectedIndex + direction, 0, Count - 1);
        if (board.preview != null) board.preview.RefreshDeck(this);
    }
}
