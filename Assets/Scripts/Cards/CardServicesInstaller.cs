using UnityEngine;

// Drop this on any GameObject in a scene to install the assets it references. Saves writing a
// bootstrap script just to hand CardServices its art library and palette.
[DefaultExecutionOrder(-100)]
public class CardServicesInstaller : MonoBehaviour
{
    [Tooltip("Sprite index the card face looks art up in. Build one via Tools > Cards > Rebuild Card Art Library.")]
    [SerializeField] private CardArtLibrary artLibrary;
    [Tooltip("Optional. Leave empty to use the built-in palette, which carries Runeboard's colors.")]
    [SerializeField] private CardPalette cardPalette;

    private void Awake()
    {
        if (artLibrary != null) CardServices.Art = artLibrary;
        if (cardPalette != null) CardServices.Palette = cardPalette;
    }
}
