using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Select Destination popup: the settlements the human can travel to, as a pile showing one face
/// at a time with arrows to browse, and a button to travel there or to stay. Owned and opened by
/// TowerMatchController while its stage is on; nothing else on the board offers the choice.
/// </summary>
public sealed class DestinationPicker : MonoBehaviour
{
    Board board;
    TowerMatchController match;
    RectTransform panel;
    CanvasGroup fade;
    readonly List<CardData> choices = new();
    int index;
    BoardSkin Skin => BoardPresentation.SkinFor(transform);
    public bool IsOpen => panel != null;
    public CardData Shown => choices.Count > 0 ? choices[Mathf.Clamp(index, 0, choices.Count - 1)] : null;

    public void Initialize(Board board, TowerMatchController match) { this.board = board; this.match = match; }

    /// <summary>Opens on the human's choices, or closes when there is nothing to choose. Rebuilds only when the pile changed.</summary>
    public void Sync(IEnumerable<CardData> candidates)
    {
        var list = candidates?.ToList() ?? new List<CardData>();
        if (list.Count == 0) { Close(); return; }
        if (IsOpen && list.SequenceEqual(choices)) return;
        choices.Clear(); choices.AddRange(list);
        // Open on the place the player is already at, so a glance says "stay" before "travel".
        var current = match.Rules.Players[0].Destination?.Card;
        index = Mathf.Max(0, choices.IndexOf(current));
        Build();
    }

    public void Close()
    {
        choices.Clear();
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        panel = null; fade = null;
    }

    public void Browse(int direction)
    {
        if (choices.Count == 0) return;
        index = Mathf.Clamp(index + direction, 0, choices.Count - 1);
        Build();
    }

    void Build()
    {
        if (choices.Count == 0) return;
        bool fresh = panel == null;
        float alpha = fade != null ? fade.alpha : 0;
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        var skin = Skin;
        var background = BoardPresentation.Panel(transform, "Destination picker", skin.colors.ink);
        panel = background.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
        background.raycastTarget = true;
        var shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = skin.colors.previewShadow; shadow.effectDistance = skin.preview.shadowOffset;
        BoardPresentation.Border(panel, skin.colors.teal);
        fade = panel.gameObject.AddComponent<CanvasGroup>(); fade.alpha = fresh ? 0 : alpha;

        var data = Shown;
        var rules = match.Rules;
        bool current = rules.IsDestination(0, data);
        float header = skin.preview.header + 6, footer = skin.preview.footer + 40, padding = skin.preview.padding + 40;
        var holder = new GameObject("Settlement", typeof(RectTransform));
        holder.transform.SetParent(panel, false);
        var visual = (RectTransform)holder.transform;
        var natural = BoardCardView.BuildVisual(board, data, false, visual, board.humanPopulationCenters);
        BoardCardView.EnablePreviewArtworkMotion(visual);
        visual.sizeDelta = natural;
        var area = ((RectTransform)transform).rect;
        float scale = Mathf.Min(skin.preview.preferredHeight / natural.y, Mathf.Max(1, area.height * .8f - header - footer) / natural.y);
        visual.localScale = Vector3.one * scale;
        visual.anchoredPosition = new Vector2(0, (footer - header) * .5f);
        // The pile: the settlements behind the shown one peek out as stacked edges, the same way the
        // discard and victory piles read on the board.
        for (int i = 1; i <= Mathf.Min(skin.piles.visibleStackEdges, choices.Count - 1); i++)
        {
            var edge = BoardPresentation.Panel(panel, "Stack edge", skin.colors.ink);
            edge.rectTransform.sizeDelta = natural * scale;
            edge.rectTransform.anchoredPosition = visual.anchoredPosition + skin.piles.stackOffset * i * 3;
            BoardPresentation.Border(edge.rectTransform, skin.colors.stackBorder);
            edge.transform.SetSiblingIndex(holder.transform.GetSiblingIndex());
        }
        panel.sizeDelta = new Vector2(natural.x * scale + padding * 2, natural.y * scale + header + footer);

        var title = BoardPresentation.TextLabel(panel, "SELECT DESTINATION  /  " + (index + 1) + " OF " + choices.Count,
            board.interfaceFont, skin.typography.previewLabelSize + 2, skin.colors.teal, Vector2.up, Vector2.one, TextAnchor.MiddleCenter);
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(-skin.preview.labelInset * 2, header);

        int demand = rules.DestinationDemand(0, data);
        string status = current ? "You are here now." : demand == 1 ? "1 card in hand can be played here." : demand + " cards in hand can be played here.";
        var hint = BoardPresentation.TextLabel(panel, status, board.interfaceFont, skin.typography.previewLabelSize, skin.colors.muted,
            Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
        hint.rectTransform.pivot = new Vector2(.5f, 0); hint.rectTransform.sizeDelta = new Vector2(-skin.preview.statusInset, 22);
        hint.rectTransform.anchoredPosition = new Vector2(0, skin.preview.footer - 2);

        var previous = Arrow("Previous", "<", -1);
        var next = Arrow("Next", ">", 1);
        previous.interactable = index > 0;
        next.interactable = index < choices.Count - 1;

        float buttonY = skin.preview.footer * .5f;
        var travel = MakeButton("Travel", current ? "CURRENT" : "TRAVEL HERE", new Vector2(.5f, 0), new Vector2(-100, buttonY), new Vector2(180, skin.preview.footer - 8));
        travel.interactable = !current;
        var chosen = data;
        travel.onClick.AddListener(() => match.Travel(chosen));
        var destination = rules.Players[0].Destination;
        var stay = MakeButton("Stay", destination == null ? "SKIP" : "STAY AT " + destination.Card.name.ToUpperInvariant(),
            new Vector2(.5f, 0), new Vector2(100, buttonY), new Vector2(180, skin.preview.footer - 8));
        stay.onClick.AddListener(match.Advance);
        panel.SetAsLastSibling();
    }

    Button Arrow(string name, string label, int direction)
    {
        var button = MakeButton(name, label, new Vector2(direction < 0 ? 0 : 1, .5f),
            new Vector2(direction < 0 ? Skin.preview.arrowInset.x : -Skin.preview.arrowInset.x, 0), Skin.preview.arrowSize);
        button.onClick.AddListener(() => Browse(direction));
        return button;
    }

    Button MakeButton(string name, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var skin = Skin;
        var image = BoardPresentation.Panel(panel, name, skin.colors.button);
        image.raycastTarget = true;
        var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Vector2.one * .5f; rect.sizeDelta = size; rect.anchoredPosition = position;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = skin.colors.gold;
        colors.pressedColor = skin.colors.teal; colors.disabledColor = skin.colors.disabledButton; button.colors = colors;
        BoardPresentation.TextLabel(rect, label, board.interfaceFont, skin.typography.buttonSize - 4, skin.colors.ivory,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        return button;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (fade != null) fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * Skin.preview.fadeSpeed);
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.leftArrowKey.wasPressedThisFrame) Browse(-1);
        else if (keyboard.rightArrowKey.wasPressedThisFrame) Browse(1);
    }

    void OnDisable() { Close(); }
}
