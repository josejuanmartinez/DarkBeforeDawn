using UnityEngine;
using UnityEngine.UI;

/// <summary>One visible full card; browsing never changes the collection's order.</summary>
public sealed class DeckVisualizer : CardZoneVisualizer
{
    public int SelectedIndex { get; private set; } = -1;
    private Text counter;
    private Image empty;
    public CardData SelectedCard => SelectedIndex >= 0 && SelectedIndex < Count ? Cards[SelectedIndex] : null;

    protected override void Rebuild()
    {
        ClearViews();
        SelectedIndex = Count - 1;
        if (SelectedCard != null) AddView(SelectedCard);
        if (counter == null)
        {
            var placeholder = new GameObject("Empty pile", typeof(RectTransform), typeof(Image));
            placeholder.transform.SetParent(transform, false);
            empty = placeholder.GetComponent<Image>();
            empty.color = new Color(1, 1, 1, .035f);
            empty.raycastTarget = false;
            var er = (RectTransform)placeholder.transform;
            er.anchorMin = new Vector2(.2f, .2f); er.anchorMax = new Vector2(.8f, .8f);
            er.offsetMin = er.offsetMax = Vector2.zero;
            var go = new GameObject("Card count", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            counter = go.GetComponent<Text>(); counter.font = board.interfaceFont;
            counter.fontSize = 18; counter.alignment = TextAnchor.MiddleCenter;
            counter.color = Color.white; counter.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0); rt.sizeDelta = new Vector2(55, 26);
        }
        empty.gameObject.SetActive(Count == 0);
        counter.text = Count.ToString(); counter.transform.SetAsLastSibling();
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
