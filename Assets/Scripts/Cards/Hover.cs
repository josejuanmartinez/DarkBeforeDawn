using TMPro;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mouse-following tooltip panel. The card face uses it for the card-type label ("Action", "Spell")
// shown beside the title. Ported from Runeboard unchanged except for the two singletons it reached
// for directly: Sounds.Instance (now the optional CardServices.Feedback hook) and PopupManager
// .IsShowing (now the static SuppressAll flag below, which anything modal can set).
public partial class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Set true while a modal dialog is up so tooltips underneath it don't pop open on hover.
    [AutoStaticsCleanup]
    public static bool SuppressAll { get; set; }

    public GameObject tooltipPanel;
    public TextMeshProUGUI textWidget;
    public Vector2 offset;
    public float exitCheckFrequency = 0.1f; // How often to check for exit

    private RectTransform tooltipRectTransform;
    private float lastExitCheckTime = 0f;
    private bool initialized = false;

    void Awake()
    {
        if (tooltipPanel != null)
        {
            tooltipRectTransform = tooltipPanel.GetComponent<RectTransform>();
            tooltipPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (!initialized || tooltipPanel == null) return;

        if (tooltipPanel.activeSelf)
        {
            // Always update position when tooltip is active
            UpdateTooltipPosition();

            // OnPointerExit is not reliable when the hovered object is destroyed or deactivated
            // under the cursor, so poll for the pointer having actually left.
            if (Time.time - lastExitCheckTime > exitCheckFrequency)
            {
                lastExitCheckTime = Time.time;
                if (!IsPointOverUI()) tooltipPanel.SetActive(false);
            }
        }
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRectTransform == null || tooltipPanel == null) return;

        Vector2 mousePosition = Input.mousePosition;

        // Get the tooltip dimensions in screen space
        Vector2 tooltipSize = tooltipRectTransform.rect.size;
        Canvas canvas = tooltipPanel.GetComponentInParent<Canvas>();
        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
        tooltipSize *= scaleFactor;

        Vector2 tooltipPosition = mousePosition + offset;

        // Keep the panel fully on screen, accounting for its pivot.
        Vector2 pivotOffset = (tooltipRectTransform.pivot - new Vector2(0.5f, 0.5f)) * tooltipSize;

        if (tooltipPosition.x - (tooltipSize.x * 0.5f) + pivotOffset.x < 0)
            tooltipPosition.x = (tooltipSize.x * 0.5f) - pivotOffset.x;

        if (tooltipPosition.x + (tooltipSize.x * 0.5f) + pivotOffset.x > Screen.width)
            tooltipPosition.x = Screen.width - (tooltipSize.x * 0.5f) - pivotOffset.x;

        if (tooltipPosition.y - (tooltipSize.y * 0.5f) + pivotOffset.y < 0)
            tooltipPosition.y = (tooltipSize.y * 0.5f) - pivotOffset.y;

        if (tooltipPosition.y + (tooltipSize.y * 0.5f) + pivotOffset.y > Screen.height)
            tooltipPosition.y = Screen.height - (tooltipSize.y * 0.5f) - pivotOffset.y;

        tooltipRectTransform.position = tooltipPosition;
    }

    public void Initialize(string text, Vector2 offset, int fontSize, TextAlignmentOptions textAlignment)
    {
        if (textWidget == null || tooltipRectTransform == null) return;
        this.offset = offset;
        textWidget.text = CreateTextWithBackground(text);
        textWidget.fontSize = fontSize;
        textWidget.alignment = textAlignment;

        // Force layout rebuild to ensure correct size calculation
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);

        initialized = true;
    }

    public void Initialize(string text, int fontSize)
    {
        if (textWidget == null || tooltipRectTransform == null) return;
        this.offset = Vector2.zero;
        textWidget.text = CreateTextWithBackground(text);
        textWidget.fontSize = fontSize;
        textWidget.alignment = TextAlignmentOptions.MidlineLeft;

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);

        initialized = true;
    }

    public void Initialize(string text)
    {
        if (textWidget == null || tooltipRectTransform == null) return;
        this.offset = Vector2.zero;
        textWidget.text = CreateTextWithBackground(text);
        textWidget.fontSize = 12;
        textWidget.alignment = TextAlignmentOptions.MidlineGeoAligned;

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);

        initialized = true;
    }

    public string CreateTextWithBackground(string text)
    {
        return $"<mark=#ffffff>{text}</mark>";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SuppressAll) return;
        if (tooltipPanel == null || tooltipRectTransform == null) return;
        tooltipPanel.SetActive(true);

        // Force layout rebuild before positioning
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);

        UpdateTooltipPosition();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    private bool IsPointOverUI()
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventDataCurrentPosition = new(EventSystem.current)
        {
            position = Input.mousePosition
        };
        System.Collections.Generic.List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        foreach (RaycastResult result in results)
            if (result.gameObject == gameObject)
                return true;

        return false;
    }
}
