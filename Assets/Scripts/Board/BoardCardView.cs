using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A stable layout slot; the preview never moves or resizes this slot.</summary>
public sealed class BoardCardView : MonoBehaviour, IPointerEnterHandler
{
    public CardData Data { get; private set; }
    public CardZoneVisualizer Zone { get; private set; }
    public Vector2 NaturalSize { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    public void Initialize(CardZoneVisualizer zone, CardData data, bool token)
    {
        Zone = zone;
        Data = data;
        NaturalSize = BuildVisual(zone.board, data, token, transform);
        var hit = gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Zone != null && Zone.board.preview != null) Zone.board.preview.Show(this);
    }

    // The reusable prefab has a 200x250 root but 390x455 artwork and an opaque
    // border ordered above it. Normalize only the generated instance for board use.
    private static void NormalizeFullCard(GameObject instance)
    {
        var root = (RectTransform)instance.transform;
        root.sizeDelta = new Vector2(300, 420);
        var real = instance.transform.Find("RealCard") as RectTransform;
        if (real == null) return;
        Fit(real, 0, 0, 1, 1);
        Fit(real.Find("Border") as RectTransform, 0, 0, 1, 1);
        var border = real.Find("Border");
        if (border != null) border.SetAsFirstSibling();
        Fit(real.Find("Image") as RectTransform, .025f, .29f, .975f, .88f);
        Fit(real.Find("TitleBackground") as RectTransform, .025f, .89f, .975f, .98f);
        Fit(real.Find("DescriptionBackground") as RectTransform, .025f, .02f, .975f, .28f);
        Fit(real.Find("TypeBackground") as RectTransform, .86f, .80f, .97f, .88f);
        foreach (var rect in instance.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name == "DisabledImage") rect.gameObject.SetActive(false);
            if (rect.name == "Title" || rect.name == "Description")
            {
                Fit(rect, .025f, .025f, .975f, .975f);
                var fitter = rect.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false;
                var text = rect.GetComponent<TMPro.TMP_Text>();
                if (text != null)
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 8; text.fontSizeMax = rect.name == "Title" ? 23 : 15;
                }
            }
        }
    }

    private static void Fit(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        if (rect == null) return;
        var fitter = rect.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static Vector2 BuildVisual(Board board, CardData data, bool token, Transform parent)
    {
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
            if (visual != null) visual.SetActive(true);
            instance.SetActive(false);
            Destroy(instance);
        }
        else
        {
            card.InitializePreview(data);
            card.ShowRealCard();
            var rect = (RectTransform)instance.transform;
            NormalizeFullCard(instance);
            size = rect.rect.size;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
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
