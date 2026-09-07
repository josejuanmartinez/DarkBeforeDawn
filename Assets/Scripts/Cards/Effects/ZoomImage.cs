using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class ZoomImage : MonoBehaviour
{
    [Min(0.01f)]
    public float zoomFactor = 1.2f;
    public float verticalOffset = 0f;

    public bool autoAssignMaterial = true;

    [Header("Hover Motion")]
    [SerializeField] private bool animateOnHover;
    [Tooltip("Extra crop applied while the card is hovered. The card frame itself never scales.")]
    [SerializeField, Range(1f, 1.5f)] private float hoverZoom = 1.22f;
    [Tooltip("How far the artwork wanders within its own frame, in UV space.")]
    [SerializeField, Range(0f, 0.1f)] private float driftAmount = 0.035f;
    [Tooltip("How quickly the artwork moves through its looping camera path.")]
    [SerializeField, Min(0.01f)] private float motionSpeed = 0.42f;
    [SerializeField, Range(0f, 0.12f)] private float zoomPulse = 0.035f;
    [SerializeField, Min(0.01f)] private float blendSpeed = 8f;

    private static readonly int ZoomId = Shader.PropertyToID("_Zoom");
    private static readonly int SpriteUvId = Shader.PropertyToID("_SpriteUV");
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");

    private Image image;
    private Sprite lastSprite;
    private Material materialInstance;
    private bool hovered;
    private float hoverBlend;

    private void Awake()
    {
        image = GetComponent<Image>();
        ApplyZoom();
    }

    private void OnEnable()
    {
        if (image == null) image = GetComponent<Image>();
        ApplyZoom();
    }

    private void Update()
    {
        if (image == null) return;
        if (image.sprite != lastSprite)
        {
            ApplyZoom();
        }
        if (!Application.isPlaying || !animateOnHover) return;

        hoverBlend = Mathf.MoveTowards(hoverBlend, hovered ? 1f : 0f, Time.unscaledDeltaTime * blendSpeed);
        if (hoverBlend <= 0f && !hovered)
        {
            ApplyMaterial(zoomFactor, new Vector2(0f, verticalOffset));
            return;
        }

        float eased = hoverBlend * hoverBlend * (3f - 2f * hoverBlend);
        float time = Time.unscaledTime * motionSpeed;

        // A slow, non-repeating-feeling camera path: the different frequencies keep the art
        // travelling through the frame rather than simply wobbling left and right.
        Vector2 drift = new Vector2(
            Mathf.Sin(time * 1.11f) * 0.72f + Mathf.Sin(time * 0.47f + 1.7f) * 0.28f,
            Mathf.Cos(time * 0.83f) * 0.70f + Mathf.Sin(time * 0.31f + 0.6f) * 0.30f)
            * (driftAmount * eased);
        float pulse = 1f + Mathf.Sin(time * 1.37f + 0.4f) * zoomPulse * eased;
        float zoom = Mathf.Lerp(zoomFactor, zoomFactor * hoverZoom * pulse, eased);
        ApplyMaterial(zoom, new Vector2(0f, verticalOffset) + drift);
    }

    /// <summary>Enables the subtle art-only hover zoom used by board card views.</summary>
    public void EnableHoverMotion() => animateOnHover = true;

    public void SetHovering(bool value) => hovered = value;

    private void OnValidate()
    {
        if (image == null) image = GetComponent<Image>();
        ApplyZoom();
    }

    private void OnDestroy()
    {
        if (materialInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(materialInstance);
            }
            else
            {
                DestroyImmediate(materialInstance);
            }
        }
    }

    private void ApplyZoom()
    {
        if (image == null) return;

        if (autoAssignMaterial)
        {
            Shader shader = Shader.Find("UI/Zoom");
            if (shader != null)
            {
                if (materialInstance == null || materialInstance.shader != shader)
                {
                    materialInstance = new Material(shader);
                }
                image.material = materialInstance;
            }
        }

        Sprite sprite = image.sprite;
        lastSprite = sprite;
        if (sprite == null || sprite.texture == null) return;

        ApplyMaterial(zoomFactor, new Vector2(0f, verticalOffset));
    }

    private void ApplyMaterial(float zoom, Vector2 offset)
    {
        Material target = image != null ? image.material : null;
        if (target == null) return;
        target.SetFloat(ZoomId, Mathf.Max(0.01f, zoom));
        if (lastSprite != null && lastSprite.texture != null)
        {
            Rect rect = lastSprite.textureRect;
            float texW = lastSprite.texture.width;
            float texH = lastSprite.texture.height;
            target.SetVector(SpriteUvId, new Vector4(rect.x / texW, rect.y / texH, rect.width / texW, rect.height / texH));
        }
        target.SetVector(OffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
    }
}
