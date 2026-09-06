using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>One screen-clamped preview shared by every board zone.</summary>
public sealed class BoardCardPreview : MonoBehaviour
{
    public Board board;
    [Min(100)] public float preferredHeight = 520;
    private BoardCardView source;
    private RectTransform panel, visual;
    private Button previous, next;
    private Vector2 lastSize;
    private float outsideSince = -1;
    public bool IsShowing => source != null && panel != null;
    public RectTransform PreviewRect => panel;

    public void Show(BoardCardView view)
    {
        if (source == view && panel != null) return;
        Hide();
        source = view;
        if (source.Zone is DeckVisualizer deck) deck.ResetSelection();
        Build();
    }

    private void Build()
    {
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        var go = new GameObject("Hover preview", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        panel = (RectTransform)go.transform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
        var background = go.GetComponent<Image>();
        background.color = new Color(.04f, .055f, .07f, .98f);
        var deck = source.Zone as DeckVisualizer;
        background.raycastTarget = deck != null;
        var holder = new GameObject("Full card", typeof(RectTransform));
        holder.transform.SetParent(panel, false);
        visual = (RectTransform)holder.transform;
        var natural = BoardCardView.BuildVisual(board, deck != null ? deck.SelectedCard : source.Data, false, visual);
        visual.sizeDelta = natural;
        var area = ((RectTransform)transform).rect;
        float footer = deck != null ? 40 : 0;
        float scale = Mathf.Min(preferredHeight / natural.y, Mathf.Max(1, area.height - footer - 16) / natural.y,
            Mathf.Max(1, area.width - 16) / natural.x);
        visual.localScale = Vector3.one * scale;
        visual.anchoredPosition = new Vector2(0, footer * .5f);
        panel.sizeDelta = new Vector2(natural.x * scale, natural.y * scale + footer);
        if (deck != null)
        {
            previous = Arrow("Previous", "<", -1, deck);
            next = Arrow("Next", ">", 1, deck);
            previous.interactable = deck.SelectedIndex > 0;
            next.interactable = deck.SelectedIndex < deck.Count - 1;
        }
        Position();
    }

    private Button Arrow(string name, string label, int direction, DeckVisualizer deck)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(panel, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(direction < 0 ? .25f : .75f, 0);
        rect.pivot = new Vector2(.5f, 0); rect.sizeDelta = new Vector2(70, 38);
        go.GetComponent<Image>().color = new Color(.16f, .20f, .24f);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => deck.Browse(direction));
        var textObject = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(go.transform, false);
        var tr = (RectTransform)textObject.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
        var text = textObject.GetComponent<Text>();
        text.font = board.interfaceFont; text.fontSize = 26; text.text = label;
        text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
        return button;
    }

    private void Position()
    {
        var root = (RectTransform)transform;
        Vector3 center = root.InverseTransformPoint(source.Rect.TransformPoint(source.Rect.rect.center));
        Vector2 half = panel.sizeDelta * .5f;
        panel.anchoredPosition = new Vector2(
            Mathf.Clamp(center.x, root.rect.xMin + half.x + 4, root.rect.xMax - half.x - 4),
            Mathf.Clamp(center.y, root.rect.yMin + half.y + 4, root.rect.yMax - half.y - 4));
        lastSize = root.rect.size;
    }

    public void RefreshDeck(DeckVisualizer deck)
    {
        if (source != null && source.Zone == deck) Build();
    }

    private void Update()
    {
        if (source == null || !source.gameObject.activeInHierarchy)
        {
            if (panel != null) Hide();
            return;
        }
        if (lastSize != ((RectTransform)transform).rect.size) Build();
        if (Mouse.current == null) return;
        Vector2 pointer = Mouse.current.position.ReadValue();
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(source.Rect, pointer, camera);
        if (source.Zone is DeckVisualizer)
            inside |= RectTransformUtility.RectangleContainsScreenPoint(panel, pointer, camera);
        // A short grace period allows pointer travel from a source to a clamped deck preview.
        if (inside) outsideSince = -1;
        else if (outsideSince < 0) outsideSince = Time.unscaledTime;
        else if (Time.unscaledTime - outsideSince > .15f) Hide();
    }

    public void HideFor(CardZoneVisualizer zone) { if (source != null && source.Zone == zone) Hide(); }
    public void Hide()
    {
        source = null; outsideSince = -1;
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        panel = null;
    }
    private void OnDisable() { Hide(); }
}
