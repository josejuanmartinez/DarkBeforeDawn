using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A shared, screen-clamped inspection surface, shown for the card under the pointer and put away
/// when the pointer leaves it. It never sticks: a click on a card acts on it or does nothing.
/// Slots never move when inspected.
/// </summary>
public sealed class BoardCardPreview : MonoBehaviour
{
    public Board board;
    private BoardSkin Skin => BoardPresentation.SkinFor(transform);
    public float preferredHeight => Skin.preview.preferredHeight;
    private BoardCardView source;
    private RectTransform panel;
    private CanvasGroup fade;
    private Vector2 lastSize;
    private float outsideSince = -1;
    public bool IsShowing => source != null && panel != null;
    // Popups (the travel popup, the destination picker, the combat screen) register their plates
    // here: while any is up no board card is inspected, and a preview already up is put away, so
    // nothing peeks out around or through them.
    static readonly System.Collections.Generic.List<RectTransform> modals = new();
    public static void RegisterModal(RectTransform plate) { if (plate != null && !modals.Contains(plate)) modals.Add(plate); }
    public static void UnregisterModal(RectTransform plate) { modals.Remove(plate); }
    public static bool AnyModalOpen { get { modals.RemoveAll(m => m == null); return modals.Count > 0; } }
    /// <summary>The transform belongs to one of the popups (is one, or sits under one).</summary>
    public static bool InsideModal(Transform target)
    {
        modals.RemoveAll(m => m == null);
        if (target == null) return false;
        foreach (var modal in modals) if (modal.gameObject.activeInHierarchy && target.IsChildOf(modal)) return true;
        return false;
    }
    public RectTransform PreviewRect => panel;

    public void Show(BoardCardView view)
    {
        if (view == null || (source == view && panel != null)) return;
        // While any popup is up the board is not inspected at all: the glow says what can be
        // clicked, and the popup's own text explains itself.
        if (AnyModalOpen) return;
        Hide();
        source = view;
        if (source.Zone is DeckVisualizer deck) deck.ResetSelection();
        Build();
    }

    private void Build()
    {
        if (source == null) return;
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        var background = BoardPresentation.Panel(transform, "Hover preview", Skin.colors.ink);
        panel = background.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
        var deck = source.Zone as DeckVisualizer;
        background.raycastTarget = true;
        var shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Skin.colors.previewShadow; shadow.effectDistance = Skin.preview.shadowOffset;
        BoardSurface.Dress(background, Skin.colors.gold, true);
        fade = panel.gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0;
        var holder = new GameObject("Full card", typeof(RectTransform));
        holder.transform.SetParent(panel, false);
        var visual = (RectTransform)holder.transform;
        var data = deck != null ? deck.SelectedCard : source.Data;
        var natural = BoardCardView.BuildVisual(board, data, false, visual, source.Zone);
        // The inspection card is a separate visual clone, so explicitly start the same
        // art-only pan/zoom that is active on the board card beneath the pointer.
        BoardCardView.EnablePreviewArtworkMotion(visual);
        visual.sizeDelta = natural;
        var area = ((RectTransform)transform).rect;
        float header = Skin.preview.header, footer = Skin.preview.footer, padding = Skin.preview.padding;
        float margin = Skin.preview.screenMargin * 2 + 4;
        float scale = Mathf.Min(preferredHeight / natural.y, Mathf.Max(1, area.height - header - footer - margin) / natural.y,
            Mathf.Max(1, area.width - padding * 2 - margin) / natural.x);
        visual.localScale = Vector3.one * scale;
        visual.anchoredPosition = new Vector2(0, (footer - header) * .5f);
        panel.sizeDelta = new Vector2(natural.x * scale + padding * 2, natural.y * scale + header + footer);
        var title = BoardPresentation.TextLabel(panel, "INSPECT  /  " + (data.type ?? "CARD").ToUpperInvariant(),
            board.interfaceFont, Skin.typography.previewLabelSize, Skin.colors.gold, Vector2.up, Vector2.one);
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(-Skin.preview.labelInset * 2, header);
        // The footer says why the card cannot act, or what a click would do; nothing when neither applies.
        string hint = deck != null ? $"{deck.SelectedIndex + 1} / {deck.Count}" : "";
        var unit = board.Match?.Unit(source);
        if (board.Match != null)
        {
            var action = board.Match.ActionLabel(source);
            hint = board.Match.InspectionHint(source) ?? (action != null ? "CLICK TO " + action : hint);
        }
        else if (source.Zone == board.hand) hint = "CLICK TO PLAY";
        else if (board.CanTap(source)) hint = "CLICK TO TAP LAND";
        if (unit != null && unit.Objects.Count > 0) hint = "Objects: " + string.Join(", ", unit.Objects.ConvertAll(c => c.name));
        var status = BoardPresentation.TextLabel(panel, hint, board.interfaceFont, Skin.typography.previewLabelSize, Skin.colors.muted,
            Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
        status.rectTransform.pivot = new Vector2(.5f, 0); status.rectTransform.sizeDelta = new Vector2(-Skin.preview.statusInset * 2, footer);
        if (deck != null)
        {
            var previous = Arrow("Previous", "<", -1, deck);
            var next = Arrow("Next", ">", 1, deck);
            previous.interactable = deck.SelectedIndex > 0;
            next.interactable = deck.SelectedIndex < deck.Count - 1;
        }
        Position();
    }

    private Button Arrow(string name, string label, int direction, DeckVisualizer deck)
    {
        var button = MakeButton(name, label, panel, new Vector2(direction < 0 ? 0 : 1, 0),
            new Vector2(direction < 0 ? Skin.preview.arrowInset.x : -Skin.preview.arrowInset.x, Skin.preview.arrowInset.y), Skin.preview.arrowSize);
        button.onClick.AddListener(() => deck.Browse(direction));
        return button;
    }

    private Button MakeButton(string name, string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var image = BoardPresentation.Panel(parent, name, Skin.colors.button);
        image.raycastTarget = true;
        var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Vector2.one * .5f; rect.sizeDelta = size; rect.anchoredPosition = position;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = Skin.colors.gold;
        colors.pressedColor = Skin.colors.teal; colors.disabledColor = Skin.colors.disabledButton; button.colors = colors;
        BoardPresentation.TextLabel(rect, label, board.interfaceFont, Skin.typography.buttonSize, Skin.colors.ivory,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        return button;
    }

    private void Position()
    {
        var root = (RectTransform)transform;
        Vector3 center = root.InverseTransformPoint(source.Rect.TransformPoint(source.Rect.rect.center));
        var corners = new Vector3[4]; source.Rect.GetWorldCorners(corners);
        float right = root.InverseTransformPoint(corners[2]).x;
        float left = root.InverseTransformPoint(corners[0]).x;
        Vector2 half = panel.sizeDelta * .5f;
        // Open beside the card wherever possible, preserving sight of the inspected token.
        float x = center.x <= 0 ? right + half.x + Skin.preview.sourceGap : left - half.x - Skin.preview.sourceGap;
        panel.anchoredPosition = new Vector2(
            Mathf.Clamp(x, root.rect.xMin + half.x + Skin.preview.screenMargin, root.rect.xMax - half.x - Skin.preview.screenMargin),
            Mathf.Clamp(center.y, root.rect.yMin + half.y + Skin.preview.screenMargin, root.rect.yMax - half.y - Skin.preview.screenMargin));
        lastSize = root.rect.size;
    }

    public void RefreshDeck(DeckVisualizer deck) { if (source != null && source.Zone == deck) Build(); }
    public void RefreshCard(BoardCardView view) { if (source == view && panel != null) Build(); }

    private void Update()
    {
        if (source == null || !source.gameObject.activeInHierarchy) { if (panel != null) Hide(); return; }
        if (lastSize != ((RectTransform)transform).rect.size) Build();
        if (fade != null) fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * Skin.preview.fadeSpeed);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Hide(); return; }
        if (AnyModalOpen) { Hide(); return; }
        if (Mouse.current == null) return;
        Vector2 pointer = Mouse.current.position.ReadValue();
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(source.Rect, pointer, camera);
        inside |= RectTransformUtility.RectangleContainsScreenPoint(panel, pointer, camera);
        if (inside) outsideSince = -1;
        else if (outsideSince < 0) outsideSince = Time.unscaledTime;
        else if (Time.unscaledTime - outsideSince > Skin.preview.exitGrace) Hide();
    }

    public void HideFor(CardZoneVisualizer zone) { if (source != null && source.Zone == zone) Hide(); }
    public void Hide()
    {
        source = null; outsideSince = -1;
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        panel = null; fade = null;
    }
    private void OnDisable() { Hide(); }
}
