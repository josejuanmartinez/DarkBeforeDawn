using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime board chrome. Authored cards and match collections remain the source of truth.</summary>
[ExecuteAlways, DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public sealed class BoardPresentation : MonoBehaviour
{
    public SkinManager skinManager;
    public BoardSkin Skin => skinManager != null ? skinManager.ActiveSkin : BoardSkin.Default;
    private readonly List<(CardZoneVisualizer zone, Text label)> counts = new();
    private readonly List<GameObject> generated = new();
    private Board board;
    private Font font;
    private bool started;

    private void Awake()
    {
        board = GetComponent<Board>();
        if (skinManager == null) skinManager = GetComponent<SkinManager>();
    }
    private void OnEnable()
    {
        if (skinManager != null) skinManager.SkinChanged += ApplySkin;
        ApplySkin(Skin);
    }
    private void Start() { started = true; }
    private void OnDisable() { if (skinManager != null) skinManager.SkinChanged -= ApplySkin; }

    public static BoardSkin SkinFor(Transform target)
    {
        var presentation = target.GetComponentInParent<BoardPresentation>();
        var skin = presentation != null ? presentation.Skin : BoardSkin.Default;
        if (skin == null) throw new System.InvalidOperationException("Assign a BoardSkin to SkinManager or create Resources/Skins/Default.");
        return skin;
    }

    private T Track<T>(T component) where T : Component
    {
        // Board chrome is rebuilt from the active skin whenever the scene loads.  Do not serialize
        // these transient objects into the scene: that avoids stale generated UI after a script
        // reload and keeps the authored hierarchy free of helper components.
        if (!Application.isPlaying) component.gameObject.hideFlags |= HideFlags.DontSaveInEditor;
        generated.Add(component.gameObject);
        return component;
    }

    private void ApplySkin(BoardSkin skin)
    {
        if (board == null || skin == null) return;
        board.preview?.Hide();
        ClearStaleGeneratedBackdrops();
        // Clear cached labels before immediate edit-mode destruction, so LateUpdate never tries
        // to write to a label that belonged to the previous generated chrome.
        counts.Clear();
        foreach (var go in generated)
        {
            if (go == null) continue;
            DestroyGenerated(go);
        }
        generated.Clear();
        font = skin.typography.interfaceFont != null ? skin.typography.interfaceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        board.interfaceFont = font;
        var shade = Track(Panel(transform, "Atmosphere veil", skin.colors.atmosphere));
        Stretch(shade.rectTransform, Vector2.zero, Vector2.one);
        shade.transform.SetAsFirstSibling();
        var header = Track(Panel(transform, "Board masthead", skin.colors.ink));
        Stretch(header.rectTransform, skin.chrome.header.min, skin.chrome.header.max);
        foreach (var label in skin.chrome.headerLabels) StyledLabel(header.transform, label);
        Rule(header.transform, skin.colors.gold, Vector2.zero, Vector2.right);
        foreach (var style in skin.zones) if (style != null) Zone(ResolveZone(style.zone), style);
        if (board.hand != null) board.hand.gap = skin.chrome.handGap;
        foreach (var label in skin.chrome.footerLabels) StyledLabel(transform, label);
        if (board.preview != null) board.preview.transform.SetAsLastSibling();
        // Runtime zones own generated BoardCardViews. In Edit mode the authored prefab instances
        // are laid out by CardZoneVisualizerEditor instead, so rebuilding here would leave them
        // out of sync with the scene authoring surface.
        if (Application.isPlaying && started)
            foreach (var zone in board.GetComponentsInChildren<CardZoneVisualizer>()) zone.RefreshSkin();
    }

    private static void DestroyGenerated(GameObject go)
    {
        go.SetActive(false);
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // Earlier edit-mode rebuilds did not retain their generated-object tracking after a domain
    // reload. Remove those old full-screen/panel roots by their unique generated names before
    // creating the current skin, otherwise their alpha values stack into an opaque black board.
    private void ClearStaleGeneratedBackdrops()
    {
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (image == null) continue;
            if (image.name != "Atmosphere veil" && image.name != "Board masthead" && image.name != "Zone surface") continue;
            DestroyGenerated(image.gameObject);
        }
    }

    private void StyledLabel(Transform parent, BoardSkin.LabelStyle style)
    {
        if (style == null) return;
        var text = Label(parent, style.text, style.size, Skin.ColorFor(style.color), style.bounds.min, style.bounds.max, style.alignment);
        if (style.mastheadFont && Skin.typography.mastheadFont != null) text.font = Skin.typography.mastheadFont;
    }

    public CardZoneVisualizer ResolveZone(BoardZoneId id) => id switch {
        BoardZoneId.OpponentLands => board.opponentLands, BoardZoneId.OpponentSettlements => board.opponentPopulationCenters,
        BoardZoneId.OpponentArmies => board.opponentArmies, BoardZoneId.OpponentVictory => board.opponentVictoryPoints,
        BoardZoneId.OpponentDiscard => board.opponentDiscard, BoardZoneId.Environment => board.environmental,
        BoardZoneId.HumanArmies => board.humanArmies, BoardZoneId.HumanLands => board.humanLands,
        BoardZoneId.HumanSettlements => board.humanPopulationCenters, BoardZoneId.HumanVictory => board.humanVictoryPoints,
        BoardZoneId.HumanDiscard => board.humanDiscard, _ => board.hand
    };

    private void Zone(CardZoneVisualizer zone, BoardSkin.ZoneStyle style)
    {
        if (zone == null) return;
        var skin = Skin; var chrome = skin.chrome;
        Color accent = skin.ColorFor(style.accent);
        var panel = (RectTransform)zone.transform.parent;
        Stretch(panel, style.bounds.min, style.bounds.max);
        foreach (var old in panel.GetComponentsInChildren<Graphic>())
            if (!old.transform.IsChildOf(zone.transform)) old.enabled = false;
        var surface = Track(Panel(panel, "Zone surface", skin.colors.zoneSurface));
        Stretch(surface.rectTransform, Vector2.zero, Vector2.one);
        surface.transform.SetAsFirstSibling();
        Border(surface.rectTransform, new Color(accent.r, accent.g, accent.b, chrome.zoneBorderOpacity));
        Rule(surface.transform, new Color(accent.r, accent.g, accent.b, chrome.zoneInlayOpacity), Vector2.up, new Vector2(chrome.zoneInlayFraction, 1));
        var heading = Label(panel, style.title, skin.typography.zoneHeadingSize, accent, Vector2.up, Vector2.one);
        heading.rectTransform.pivot = new Vector2(.5f, 1);
        heading.rectTransform.sizeDelta = new Vector2(-chrome.headingInset.x * 2, chrome.headingHeight);
        heading.rectTransform.anchoredPosition = new Vector2(0, -chrome.headingInset.y);
        var count = Label(panel, "", skin.typography.countSize, skin.colors.muted, Vector2.one, Vector2.one, TextAnchor.MiddleRight);
        count.rectTransform.pivot = Vector2.one;
        count.rectTransform.sizeDelta = new Vector2(chrome.countWidth, chrome.headingHeight);
        count.rectTransform.anchoredPosition = -chrome.headingInset;
        counts.Add((zone, count));
        var area = (RectTransform)zone.transform;
        Stretch(area, Vector2.zero, Vector2.one);
        area.offsetMin = chrome.contentInsetMin;
        area.offsetMax = -chrome.contentInsetMax;
        zone.gap = chrome.zoneGap;
    }

    private void LateUpdate()
    {
        foreach (var entry in counts)
        {
            if (entry.zone == null || entry.label == null) continue;
            var value = entry.zone.isHand ? $"{entry.zone.Count} / {board.maximumHandSize}" : entry.zone.Count.ToString("00");
            if (entry.label.text != value) entry.label.text = value;
        }
    }

    private Text Label(Transform parent, string value, int size, Color color, Vector2 min, Vector2 max, TextAnchor alignment = TextAnchor.MiddleLeft)
        => Track(TextLabel(parent, value, font, size, color, min, max, alignment));

    public static Text TextLabel(Transform parent, string value, Font font, int size, Color color, Vector2 min, Vector2 max, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var go = new GameObject(!string.IsNullOrEmpty(value) ? value : "Count", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.color = color; text.text = value;
        text.alignment = alignment; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        Stretch(text.rectTransform, min, max);
        return text;
    }

    public static Image Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image;
    }

    public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = Vector2.one * .5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public static void Rule(Transform parent, Color color, Vector2 min, Vector2 max)
    {
        var line = Panel(parent, "Inlay", color);
        Stretch(line.rectTransform, min, max);
        line.rectTransform.sizeDelta = new Vector2(0, SkinFor(parent).chrome.lineWidth);
    }

    public static Image Border(RectTransform parent, Color color)
    {
        var frame = Panel(parent, "Frame", Color.clear);
        Stretch(frame.rectTransform, Vector2.zero, Vector2.one);
        // Four thin strips avoid an opaque centre and remain crisp at different scales.
        Rule(frame.transform, color, Vector2.zero, Vector2.right);
        Rule(frame.transform, color, Vector2.up, Vector2.one);
        foreach (float x in new[] { 0f, 1f })
        {
            var edge = Panel(frame.transform, "Edge", color);
            Stretch(edge.rectTransform, new Vector2(x, 0), new Vector2(x, 1));
            edge.rectTransform.sizeDelta = new Vector2(SkinFor(parent).chrome.lineWidth, 0);
        }
        return frame;
    }

    public static void StyleFullCard(Card card)
    {
        var skin = SkinFor(card.transform);
        var style = skin.cards;
        var root = (RectTransform)card.transform;
        var real = root.Find("RealCard") as RectTransform;
        if (real == null) return;
        ClearGeneratedCardChrome(root, real);
        real.anchorMin = real.anchorMax = Vector2.one * .5f;
        real.sizeDelta = style.size;
        real.anchoredPosition = Vector2.zero;
        var backing = Panel(root, "Obsidian card stock", skin.colors.ink);
        Stretch(backing.rectTransform, Vector2.zero, Vector2.one);
        backing.transform.SetAsFirstSibling();
        var shadow = backing.gameObject.AddComponent<Shadow>();
        shadow.effectColor = skin.colors.cardShadow; shadow.effectDistance = style.shadowOffset;
        SetPiece(real, "Image", style.art.size, style.art.position, skin.colors.ink);
        SetPiece(real, "TitleBackground", style.title.size, style.title.position, skin.colors.ink);
        SetPiece(real, "DescriptionBackground", style.description.size, style.description.position, skin.colors.ink);
        SetPiece(real, "TypeBackground", style.badge.size, style.badge.position, skin.colors.ink);
        var border = real.Find("Border");
        if (border != null) border.gameObject.SetActive(false);
        foreach (var hover in root.GetComponentsInChildren<Hover>(true)) hover.gameObject.SetActive(false);
        var title = real.Find("TitleBackground/Title")?.GetComponent<TMP_Text>();
        var readingFont = skin.typography.readingFont != null ? skin.typography.readingFont : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        foreach (var text in real.GetComponentsInChildren<TMP_Text>(true))
        {
            if (readingFont != null) { text.font = readingFont; text.fontSharedMaterial = readingFont.material; }
            text.fontStyle = FontStyles.Normal;
            text.outlineWidth = 0;
        }
        if (title != null)
        {
            Stretch(title.rectTransform, Vector2.zero, Vector2.one);
            title.rectTransform.offsetMin = style.titleInsetMin; title.rectTransform.offsetMax = -style.titleInsetMax;
            title.text = title.text.Replace("<u>", "").Replace("</u>", "");
            title.color = skin.colors.ivory; title.enableAutoSizing = true; title.fontSizeMin = skin.typography.titleFontRange.x; title.fontSizeMax = skin.typography.titleFontRange.y;
            title.fontStyle = skin.typography.titleStyle;
            title.outlineWidth = 0;
        }
        var description = real.Find("DescriptionBackground/Description")?.GetComponent<TMP_Text>();
        if (description != null)
        {
            Stretch(description.rectTransform, Vector2.zero, Vector2.one);
            description.margin = style.descriptionMargin;
            description.color = skin.colors.ivory; description.enableAutoSizing = true;
            description.fontSizeMin = skin.typography.descriptionFontRange.x; description.fontSizeMax = skin.typography.descriptionFontRange.y;
            description.outlineWidth = 0;
        }
        var requirements = real.Find("Image/Requirements") as RectTransform;
        if (requirements != null)
        {
            Stretch(requirements, new Vector2(0, 1), new Vector2(1, 1));
            var requirementText = requirements.GetComponent<TMP_Text>();
            float height = style.requirementHeightRange.x;
            if (requirementText != null && !string.IsNullOrWhiteSpace(requirementText.text))
            {
                requirementText.fontSize = skin.typography.requirementsSize;
                height = Mathf.Clamp(requirementText.GetPreferredValues(requirementText.text, Mathf.Max(1, style.art.size.x - style.requirementMargin.x - style.requirementMargin.z), Mathf.Infinity).y + style.requirementMargin.y + style.requirementMargin.w, style.requirementHeightRange.x, style.requirementHeightRange.y);
                requirementText.margin = style.requirementMargin;
                requirementText.color = skin.colors.ivory;
                var band = Panel(requirements.parent, "Requirement ribbon", skin.colors.requirementRibbon);
                Stretch(band.rectTransform, Vector2.up, Vector2.one);
                band.rectTransform.pivot = new Vector2(.5f, 1);
                band.rectTransform.sizeDelta = new Vector2(0, height);
                band.transform.SetSiblingIndex(requirements.GetSiblingIndex());
            }
            requirements.pivot = new Vector2(.5f, 1); requirements.sizeDelta = new Vector2(0, height);
        }
        var accent = (skin.colors.cardTypes != null ? skin.colors.cardTypes : CardPalette.Default)
            .GetCardTypeColor(card.cardData != null ? card.cardData.GetCardType() : CardTypeEnum.Unknown);
        Border(root, Color.Lerp(skin.colors.gold, accent, style.typeBorderBlend));
        Rule(root, accent, Vector2.zero, Vector2.right);
    }

    private static void ClearGeneratedCardChrome(RectTransform root, RectTransform real)
    {
        ClearChildrenNamed(root, "Obsidian card stock", "Frame", "Inlay");
        for (int i = real.childCount - 1; i >= 0; i--)
        {
            var child = real.GetChild(i);
            foreach (var ribbon in child.GetComponentsInChildren<Transform>(true))
                if (ribbon.name == "Requirement ribbon")
                {
                    if (Application.isPlaying) Destroy(ribbon.gameObject);
                    else DestroyImmediate(ribbon.gameObject);
                }
        }
    }

    private static void ClearChildrenNamed(Transform parent, params string[] names)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (!System.Array.Exists(names, name => child.name == name)) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    /// <summary>Styles an authored token card as the same framed, captioned token used at runtime.</summary>
    public static Vector2 StyleTokenCard(Card card, Font interfaceFont)
    {
        var skin = SkinFor(card.transform);
        var root = (RectTransform)card.transform;
        ClearGeneratedTokenChrome(root);
        card.ShowToken();
        card.CompactTokenInPlace();
        Vector2 tokenSize = card.TokenFootprint;
        root.sizeDelta = tokenSize + Vector2.up * skin.tokens.captionHeight;
        card.SetTokenPreviewOffset(Vector2.up * skin.tokens.captionHeight * .5f);
        var backing = Panel(root, "Token stock", skin.colors.ink);
        Stretch(backing.rectTransform, Vector2.zero, Vector2.one);
        backing.transform.SetAsFirstSibling();
        Border(backing.rectTransform, skin.colors.tokenBorder);
        string caption = System.Text.RegularExpressions.Regex.Replace(card.cardData?.name ?? string.Empty, "(?<=[a-z])(?=[A-Z])", " ");
        var label = TextLabel(root, caption, interfaceFont, skin.tokens.captionMaxSize, skin.colors.ivory,
            Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
        label.name = "Token caption";
        label.rectTransform.pivot = new Vector2(.5f, 0);
        label.rectTransform.sizeDelta = new Vector2(-skin.tokens.captionInset * 2, skin.tokens.captionHeight);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = skin.tokens.captionMinSize;
        label.resizeTextMaxSize = skin.tokens.captionMaxSize;
        return root.sizeDelta;
    }

    private static void ClearGeneratedTokenChrome(RectTransform root)
    {
        ClearChildrenNamed(root, "Token stock", "Token caption");
    }

    private static void SetPiece(RectTransform real, string name, Vector2 size, Vector2 position, Color background)
    {
        var piece = real.Find(name) as RectTransform;
        if (piece == null) return;
        foreach (var layout in piece.GetComponents<Behaviour>())
            if (layout is LayoutGroup || layout is ContentSizeFitter) layout.enabled = false;
        piece.anchorMin = piece.anchorMax = piece.pivot = Vector2.one * .5f;
        piece.sizeDelta = size; piece.anchoredPosition = position; piece.localScale = Vector3.one;
        var image = piece.GetComponent<Image>();
        if (image != null && name.EndsWith("Background")) { image.sprite = null; image.color = background; }
    }
}
