using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Reads TMP geometry without adding raycast targets to the card's text. Card clicks and flips
// continue to belong to the root card / board slot, and the popup never intercepts the pointer.
public sealed class CardKeywordHover : MonoBehaviour
{
    private TMP_Text[] labels;
    private readonly List<RaycastResult> hits = new();
    private GameObject popupCanvas;
    private RectTransform panel;
    private TextMeshProUGUI popupText;
    private string currentId;
    // Non-text targets: the card's deck badge, the terrain marks on a map -- rects with nothing in
    // the glossary to key on, each carrying its own explanation.
    private readonly List<(RectTransform rect, string title, string body)> badges = new();
    // A "card:Name" link (the settlement's dwellers) expands into the named card itself rather than
    // a glossary line. The board it is built with is looked up once; without one the text falls back.
    private RectTransform cardHolder;
    private Board board;
    private bool boardLooked;

    public void RefreshTargets() => labels = GetComponentsInChildren<TMP_Text>(true);

    /// <summary>The one badge of a card face; replaces any earlier one.</summary>
    public void SetBadge(RectTransform rect, string title, string body)
    {
        badges.Clear();
        AddBadge(rect, title, body);
    }

    /// <summary>Another hoverable rect under this hover, explained with the given title and body.</summary>
    public void AddBadge(RectTransform rect, string title, string body)
    {
        if (rect != null) badges.Add((rect, title, body));
    }

    public static bool TryResolve(TMP_Text label, Vector2 pointer, Camera camera, out string id)
    {
        id = null;
        if (label == null || !label.isActiveAndEnabled || label.canvasRenderer.GetInheritedAlpha() < .05f)
            return false;
        int character = TMP_TextUtilities.FindIntersectingCharacter(label, pointer, camera, true);
        if (character < 0 || character >= label.maxVisibleCharacters) return false;
        var info = label.textInfo.characterInfo[character];
        if (!info.isVisible) return false;
        // Links win over icon names: the mounts resource icon in a Mounted ability must explain
        // Mounted, and the light icon in the status row must explain Blessed.
        for (int i = 0; i < label.textInfo.linkCount; i++)
        {
            var link = label.textInfo.linkInfo[i];
            if (character < link.linkTextfirstCharacterIndex || character >= link.linkTextfirstCharacterIndex + link.linkTextLength)
                continue;
            string candidate = link.GetLinkID();
            if (CardKeywordGlossary.TryGet(candidate, out _, out _) || IsCardLink(candidate)) { id = candidate; return true; }
        }
        if (info.elementType != TMP_TextElementType.Sprite || !(info.textElement is TMP_SpriteCharacter spriteCharacter)) return false;
        string sprite = spriteCharacter.name;
        id = "icon:" + sprite;
        return CardKeywordGlossary.TryGet(id, out _, out _);
    }

    private void LateUpdate()
    {
        if (Hover.SuppressAll || Mouse.current == null || EventSystem.current == null) { Hide(); return; }
        // While a popup is up (travel, destination picker, combat screen) only its own text explains
        // itself: nothing on the board under or beside it is hovered.
        if (BoardCardPreview.AnyModalOpen && !BoardCardPreview.InsideModal(transform)) { Hide(); return; }
        Vector2 pointer = Mouse.current.position.ReadValue();
        hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, hits);
        if (hits.Count == 0) { Hide(); return; }
        Transform hit = hits[0].gameObject.transform;
        if (!hit.IsChildOf(transform) && !transform.IsChildOf(hit)) { Hide(); return; }
        if (labels == null) RefreshTargets();
        TMP_FontAsset font = null;
        foreach (var label in labels)
        {
            if (label == null) continue;
            font ??= label.font;
            var canvas = label.canvas != null ? label.canvas.rootCanvas : null;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!TryResolve(label, pointer, camera, out string id)) continue;
            if (IsCardLink(id)) { ShowCard(id, pointer, label.font); return; }
            CardKeywordGlossary.TryGet(id, out string title, out string body);
            Show(id, title, body, pointer, label.font);
            return;
        }
        for (int i = 0; i < badges.Count; i++)
        {
            var (rect, title, body) = badges[i];
            if (rect == null || !rect.gameObject.activeInHierarchy || rect.GetComponent<Image>()?.enabled != true) continue;
            var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, pointer, camera)) continue;
            Show("badge:" + i, title, body, pointer, font);
            return;
        }
        Hide();
    }

    public static bool IsCardLink(string id) => !string.IsNullOrEmpty(id) && id.StartsWith(PcDescriptionBuilder.CardLinkPrefix, System.StringComparison.Ordinal);

    // The named card, drawn full size beside the pointer. Falls back to its text when the catalog
    // does not know it or there is no board to build the visual with.
    private void ShowCard(string id, Vector2 pointer, TMP_FontAsset font)
    {
        string name = id.Substring(PcDescriptionBuilder.CardLinkPrefix.Length);
        var data = CardCatalog.FindCardByName(name);
        if (!boardLooked) { board = FindAnyObjectByType<Board>(); boardLooked = true; }
        if (data == null || board == null)
        {
            Show(id, name, data != null ? data.GetRenderedDescription() : "No card of this name is in the catalog.", pointer, font);
            return;
        }
        bool fresh = id != currentId;
        Show(id, string.Empty, string.Empty, pointer, font);
        if (fresh)
        {
            var holder = new GameObject("Card", typeof(RectTransform));
            holder.transform.SetParent(panel, false);
            cardHolder = holder.GetComponent<RectTransform>();
            cardHolder.anchorMin = cardHolder.anchorMax = cardHolder.pivot = Vector2.one * .5f;
            var natural = BoardCardView.BuildVisual(board, data, false, cardHolder);
            BoardCardView.EnablePreviewArtworkMotion(cardHolder);
            cardHolder.sizeDelta = natural;
            float scale = Mathf.Min(1, Screen.height * .55f / natural.y);
            cardHolder.localScale = Vector3.one * scale;
            panel.sizeDelta = natural * scale + new Vector2(24, 24);
            popupText.gameObject.SetActive(false);
        }
        Place(pointer, panel.sizeDelta.x, panel.sizeDelta.y);
    }

    private void Show(string id, string title, string body, Vector2 pointer, TMP_FontAsset font)
    {
        if (popupCanvas == null)
        {
            popupCanvas = new GameObject("Card keyword popup", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = popupCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var group = popupCanvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var background = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(popupCanvas.transform, false);
            background.GetComponent<Image>().color = new Color(.045f, .055f, .075f, .98f);
            background.GetComponent<Image>().raycastTarget = false;
            panel = background.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            var text = new GameObject("Explanation", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(panel, false);
            popupText = text.GetComponent<TextMeshProUGUI>();
            popupText.rectTransform.anchorMin = Vector2.zero; popupText.rectTransform.anchorMax = Vector2.one;
            popupText.rectTransform.offsetMin = new Vector2(14, 12); popupText.rectTransform.offsetMax = new Vector2(-14, -12);
            popupText.fontSize = 18; popupText.color = new Color(.96f, .94f, .88f);
            popupText.raycastTarget = false; popupText.richText = true;
            // The card icon sheet, so a terrain or material glyph in an explanation draws as on the face.
            if (!boardLooked) { board = FindAnyObjectByType<Board>(); boardLooked = true; }
            popupText.spriteAsset = BoardPresentation.SpriteAssetFor(board);
        }
        popupCanvas.SetActive(true);
        if (id != currentId)
        {
            currentId = id;
            if (cardHolder != null) { Destroy(cardHolder.gameObject); cardHolder = null; }
            popupText.gameObject.SetActive(true);
            if (font != null) popupText.font = font;
            popupText.text = "<b><color=#E8C681>" + title + "</color></b>\n" + body;
        }
        // A card popup sizes and places itself once its visual exists.
        if (cardHolder != null || IsCardLink(id) && string.IsNullOrEmpty(title)) return;
        float width = Mathf.Min(340, Screen.width - 16);
        float height = popupText.GetPreferredValues(popupText.text, width - 28, Mathf.Infinity).y + 24;
        panel.sizeDelta = new Vector2(width, height);
        Place(pointer, width, height);
    }

    private void Place(Vector2 pointer, float width, float height)
    {
        float x = pointer.x + 18, y = pointer.y - height - 18;
        if (x + width > Screen.width - 8) x = pointer.x - width - 18;
        if (y < 8) y = pointer.y + 18;
        panel.anchoredPosition = new Vector2(Mathf.Clamp(x, 8, Mathf.Max(8, Screen.width - width - 8)),
            Mathf.Clamp(y, 8, Mathf.Max(8, Screen.height - height - 8)));
    }

    private void Hide()
    {
        if (popupCanvas != null) popupCanvas.SetActive(false);
        if (cardHolder != null) { Destroy(cardHolder.gameObject); cardHolder = null; }
        currentId = null;
    }
    private void OnDisable() => Hide();
    private void OnDestroy() { if (popupCanvas != null) Destroy(popupCanvas); }
}
