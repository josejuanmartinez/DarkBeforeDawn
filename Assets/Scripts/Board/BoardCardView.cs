using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A stable layout slot; the preview never moves or resizes this slot.</summary>
public sealed class BoardCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private CanvasGroup highlight;
    private float targetHighlight;
    public CardData Data { get; private set; }
    public CardZoneVisualizer Zone { get; private set; }
    public Vector2 NaturalSize { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    public void Initialize(CardZoneVisualizer zone, CardData data, bool token)
    {
        var skin = BoardPresentation.SkinFor(transform);
        Zone = zone;
        Data = data;
        NaturalSize = BuildVisual(zone.board, data, token, transform);
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
        var hit = gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetHighlight = 1;
        if (Zone != null && Zone.board.preview != null) Zone.board.preview.Show(this);
    }

    public void OnPointerExit(PointerEventData eventData) { targetHighlight = 0; }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && Zone != null && Zone.board.preview != null)
            Zone.board.preview.Pin(this);
    }
    private void Update()
    {
        if (highlight != null) highlight.alpha = Mathf.MoveTowards(highlight.alpha, targetHighlight, Time.unscaledDeltaTime * BoardPresentation.SkinFor(transform).tokens.highlightFadeSpeed);
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
        // Authored active and opaque black over the art; nothing in Card ever toggles it.
        var disabled = root.Find("RealCard/Image/DisabledImage");
        if (disabled != null) disabled.gameObject.SetActive(false);
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
        return FitFullCardRoot(card);
    }

    public static Vector2 BuildVisual(Board board, CardData data, bool token, Transform parent)
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
            var visual = card.CreateTokenVisualClone(parent, out size);
            if (visual != null)
            {
                visual.SetActive(true);
                visual.transform.localPosition = new Vector3(0, skin.tokens.captionHeight * .5f, 0);
                var backing = BoardPresentation.Panel(parent, "Token stock", skin.colors.ink);
                BoardPresentation.Stretch(backing.rectTransform, Vector2.zero, Vector2.one);
                backing.transform.SetAsFirstSibling();
                BoardPresentation.Border(backing.rectTransform, skin.colors.tokenBorder);
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
            size = FitFullCardRoot(card);
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
