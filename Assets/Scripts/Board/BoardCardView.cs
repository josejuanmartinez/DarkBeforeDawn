using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>A stable layout slot; the preview never moves or resizes this slot.</summary>
public sealed class BoardCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private CanvasGroup highlight;
    private CanvasGroup actionHighlight;
    public bool ActionHighlighted { get; private set; }
    private float targetHighlight;
    private readonly List<ZoomImage> artworkMotion = new();
    private Text tappedLabel;
    public CardData Data { get; private set; }
    public CardZoneVisualizer Zone { get; private set; }
    public Vector2 NaturalSize { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    public void SetStatusEffects(IEnumerable<StatusEffects> effects)
    {
        if (Data == null || (Data.GetCardType() != CardTypeEnum.Character && Data.GetCardType() != CardTypeEnum.Army)) return;
        Data.statusEffects = effects != null ? new List<StatusEffects>(effects) : new();
        // Tokens are stripped visual clones, so update their retained TMP row directly.
        foreach (var text in GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (text.name != "StatusEffects") continue;
            bool compact = text.GetComponentInParent<Card>()?.IsTokenOnlyPresentation ?? true;
            text.text = Data.GetStatusEffectsText(compact);
            text.gameObject.SetActive(!string.IsNullOrEmpty(text.text));
        }
        foreach (var card in GetComponentsInChildren<Card>(true)) card.SetStatusEffects(Data.statusEffects);
        Zone?.board?.preview?.RefreshCard(this);
    }

    public void Initialize(CardZoneVisualizer zone, CardData data, bool token)
    {
        var skin = BoardPresentation.SkinFor(transform);
        Zone = zone;
        Data = data;
        NaturalSize = BuildVisual(zone.board, data, token, transform, zone);
        ConfigureArtworkMotion();
        if (zone is DeckVisualizer deck && deck.Count > 1)
        {
            var face = GetComponentInChildren<Card>();
            if (face != null)
            {
                face.transform.localScale = Vector3.one * skin.piles.faceScale;
                face.transform.localPosition = (Vector3)skin.piles.faceOffset;
            }
            for (int i = 1; i <= Mathf.Min(skin.piles.visibleStackEdges, deck.Count - 1); i++)
            {
                var stock = BoardPresentation.Panel(transform, "Stack edge", skin.colors.ink);
                stock.rectTransform.sizeDelta = NaturalSize * skin.piles.faceScale;
                stock.rectTransform.anchoredPosition = skin.piles.stackOffset * i;
                BoardPresentation.Border(stock.rectTransform, skin.colors.stackBorder);
                stock.transform.SetAsFirstSibling();
            }
        }
        var frame = BoardPresentation.Border(Rect, skin.colors.ivory);
        highlight = frame.gameObject.AddComponent<CanvasGroup>();
        highlight.alpha = 0; highlight.blocksRaycasts = false;
        var readyFrame = BoardPresentation.Border(Rect, new Color(.3f,1f,.78f), 3);
        readyFrame.name = "Legal action highlight";
        actionHighlight = readyFrame.gameObject.AddComponent<CanvasGroup>();
        actionHighlight.alpha = 0; actionHighlight.blocksRaycasts = false;
        var hit = gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        if (data.GetCardType() == CardTypeEnum.Land || data.GetCardType() == CardTypeEnum.Character || data.GetCardType() == CardTypeEnum.Army)
        {
            tappedLabel = BoardPresentation.TextLabel(transform, "TAPPED", zone.board.interfaceFont, 16,
                skin.colors.ivory, new Vector2(0,.4f), new Vector2(1,.65f), TextAnchor.MiddleCenter);
            tappedLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
        }
        // Recreated views start in their actual pose. Only subsequent state changes animate.
        if (zone.board.Match != null) transform.localRotation = Quaternion.Euler(0, 0, zone.board.IsTapped(this) ? -90 : 0);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetHighlight = 1;
        SetArtworkHover(true);
        if (Zone != null && Zone.board.preview != null) Zone.board.preview.Show(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetHighlight = 0;
        SetArtworkHover(false);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && Zone != null && Zone.board.Match != null && Zone.board.Match.Select(this)) return;
        if (eventData.button == PointerEventData.InputButton.Left && Zone != null && Zone.board.preview != null)
            Zone.board.preview.Pin(this);
    }
    private void Update()
    {
        if (tappedLabel != null)
        {
            var unit = Zone.board.Match?.Unit(this);
            string objects = unit != null && unit.Objects.Count > 0 ? "\n" + unit.Objects.Count + " OBJECTS" : "";
            string state = Zone.board.IsTapped(this) ? "TAPPED" : Zone.board.Match != null && Zone.board.Match.Rules != null && Zone.board.Match.Rules.IsNewUnit(unit)
                ? "NEW\nATTACK NEXT TURN" : "";
            tappedLabel.text = state + objects;
            tappedLabel.gameObject.SetActive(tappedLabel.text.Length > 0);
        }
        if (Zone != null && Zone.board.Match != null && Zone != Zone.board.hand)
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                Quaternion.Euler(0, 0, Zone.board.IsTapped(this) ? -90 : 0), Time.unscaledDeltaTime * 12);
        if (highlight != null) highlight.alpha = Mathf.MoveTowards(highlight.alpha, targetHighlight, Time.unscaledDeltaTime * BoardPresentation.SkinFor(transform).tokens.highlightFadeSpeed);
        ActionHighlighted = Zone != null && Zone.board.Match != null && Zone.board.Match.IsActionable(this);
        if (actionHighlight != null) actionHighlight.alpha = ActionHighlighted ? .75f + .25f * Mathf.Sin(Time.unscaledTime * 3) : 0;
    }

    private void SetArtworkHover(bool hovered)
    {
        foreach (var effect in artworkMotion)
            if (effect != null) effect.SetHovering(hovered);
    }

    private void AddArtworkMotion(Image image)
    {
        var effect = EnsureArtworkMotion(image);
        if (effect == null) return;
        artworkMotion.Add(effect);
    }

    /// <summary>
    /// Makes the art inside an enlarged inspection card use the same in-frame camera motion as
    /// the source card. The outer card transform remains untouched.
    /// </summary>
    public static void EnablePreviewArtworkMotion(Transform cardRoot)
    {
        if (cardRoot == null) return;
        foreach (var image in cardRoot.GetComponentsInChildren<Image>(true))
        {
            if (image.name != "Image" && image.name != "TokenedImage") continue;
            var effect = EnsureArtworkMotion(image);
            if (effect != null) effect.SetHovering(true);
        }
    }

    private static ZoomImage EnsureArtworkMotion(Image image)
    {
        if (image == null) return null;
        var effect = image.GetComponent<ZoomImage>();
        if (effect == null) effect = image.gameObject.AddComponent<ZoomImage>();
        effect.EnableHoverMotion();
        return effect;
    }

    private void ConfigureArtworkMotion()
    {
        foreach (var image in GetComponentsInChildren<Image>(true))
            if (image.name == "Image" || image.name == "TokenedImage") AddArtworkMotion(image);
    }

    // The prefab composes a card out of pieces that deliberately overflow RealCard's 200x200 box —
    // the border alone is 300x350 at 1.3 scale — so the root's authored 200x250 understates the card
    // and a layout slot sized from it clips. Size the root to the border instead and leave the
    // composition untouched: the border is already centred on the root, so nothing has to move.
    //
    // This used to re-fit every piece to hand-picked fractions of the root, which is what lost the
    // type-coloured border (refitted flush and pushed behind the art) and opened gaps between the
    // title, art and description. A card on the board now looks exactly like Card.prefab does.
    private static Vector2 FitFullCardRoot(Card card)
    {
        var root = (RectTransform)card.transform;
        // Authored active and opaque black over the art; nothing in Card ever toggles it. A skin
        // that wants it can say so rather than having the board decide.
        // SkinOrNull, not SkinFor: this also runs from the editor's authoring preview, where a card
        // may sit outside any board, and "no skin" has an obvious answer here rather than a throw.
        var disabled = root.Find("RealCard/Image/DisabledImage");
        var skin = BoardPresentation.SkinOrNull(card.transform);
        if (disabled != null)
            disabled.gameObject.SetActive(skin != null && skin.cards.keepDisabledOverlay);
        Vector2 footprint = card.CardFootprint;
        root.sizeDelta = footprint;
        return footprint;
    }

    // Presentation setup shared by the runtime build below and the editor's preview of authored
    // cards, so a zone filled in by dragging prefabs into the scene renders the way the one Play
    // mode builds does. Returns the footprint a layout slot should reserve.
    public static Vector2 PrepareForBoard(Card card, bool token)
    {
        card.SuppressHoverEffects = true;
        card.TypewriterEffect = false;
        card.ShowCloseIcon = false;
        card.ShowRequirementWarnings = false;
        if (token)
        {
            card.ShowToken();
            card.CompactTokenInPlace();
            return card.TokenFootprint;
        }
        card.ShowRealCard();
        Vector2 authoredSize = FitFullCardRoot(card);
        var skin = BoardPresentation.SkinOrNull(card.transform);
        if (skin == null) return authoredSize;
        // Hierarchy refreshes must keep the frame matched to the already styled face.
        // The prefab's larger authored border is not the board skin's footprint.
        ((RectTransform)card.transform).sizeDelta = skin.cards.size;
        return skin.cards.size;
    }

    public static Vector2 BuildVisual(Board board, CardData data, bool token, Transform parent, CardZoneVisualizer zone = null)
    {
        var skin = BoardPresentation.SkinFor(parent);
        var instance = Instantiate(token ? board.tokenCardPrefab : board.fullCardPrefab, parent, false);
        instance.SetActive(true);
        var provider = instance.GetComponent<CardDataProvider>();
        if (provider != null) provider.enabled = false;
        var card = instance.GetComponent<Card>();
        card.SuppressHoverEffects = true;
        card.TypewriterEffect = false;
        card.ShowCloseIcon = false;
        card.ShowRequirementWarnings = false;
        Vector2 size;
        if (token)
        {
            card.InitializeTokenVisualOnly(data);
            // Before the clone: the source card is destroyed below, so the clone is what has to
            // carry the colour.
            card.ApplyCompactInfoMaterial(BoardPresentation.StatMaterialFor(skin, zone));
            // Scaled before the clone, so the clone inherits it: the source is destroyed below.
            // The clone reports the prefab's own measurement, so the skin's footprint replaces it.
            card.ScaleTokenVisual(BoardPresentation.TokenScaleFor(skin, card, out Vector2 tokenSize));
            var visual = card.CreateTokenVisualClone(parent, out size);
            size = tokenSize;
            if (visual != null)
            {
                visual.AddComponent<CardKeywordHover>().RefreshTargets();
                visual.SetActive(true);
                visual.transform.localPosition = new Vector3(0, skin.tokens.captionHeight * .5f, 0);
                var backing = BoardPresentation.Panel(parent, "Token stock", skin.colors.ink);
                BoardPresentation.Stretch(backing.rectTransform, Vector2.zero, Vector2.one);
                backing.transform.SetAsFirstSibling();
                BoardPresentation.Border(backing.rectTransform, skin.colors.tokenBorder, skin.tokens.borderWidth);
                string caption = System.Text.RegularExpressions.Regex.Replace(data.name ?? "", "(?<=[a-z])(?=[A-Z])", " ");
                var label = BoardPresentation.TextLabel(parent, caption, board.interfaceFont, skin.tokens.captionMaxSize,
                    skin.colors.ivory, Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
                label.rectTransform.pivot = new Vector2(.5f, 0);
                label.rectTransform.sizeDelta = new Vector2(-skin.tokens.captionInset * 2, skin.tokens.captionHeight);
                label.resizeTextForBestFit = true; label.resizeTextMinSize = skin.tokens.captionMinSize; label.resizeTextMaxSize = skin.tokens.captionMaxSize;
                size.y += skin.tokens.captionHeight;
            }
            instance.SetActive(false);
            Destroy(instance);
        }
        else
        {
            card.InitializePreview(data);
            card.ShowRealCard();
            var rect = (RectTransform)instance.transform;
            // Called for its side effects only -- the disabled-overlay decision and sizing the root
            // to the authored composition before StyleFullCard re-lays it out. Its measurement is
            // deliberately not the answer here: on this path the skin's card size is, and the root
            // is resized to it two lines down.
            FitFullCardRoot(card);
            BoardPresentation.StyleFullCard(card);
            size = skin.cards.size;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var group in instance.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }
        return new Vector2(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    }
}
