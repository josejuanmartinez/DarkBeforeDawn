using Unity.Scripting.LifecycleManagement;
using UnityEngine;

// The palette Runeboard used for card-type borders, as a plain asset instead of a scene singleton.
// Create one via Assets > Create > Cards > Card Palette to override the defaults.
[CreateAssetMenu(fileName = "CardPalette", menuName = "Cards/Card Palette")]
public partial class CardPalette : ScriptableObject, ICardPalette
{
    // Values carried over from Runeboard's Colors component. object/environmental keep the explicit
    // fallbacks that file hardcoded; the rest were authored in the scene, so these are the shipped
    // values read off that component.
    public Color pc = new(0.35f, 0.55f, 0.80f, 1f);
    public Color land = new(0.45f, 0.65f, 0.35f, 1f);
    public Color character = new(0.85f, 0.75f, 0.45f, 1f);
    public Color army = new(0.70f, 0.30f, 0.25f, 1f);
    public Color @event = new(0.80f, 0.60f, 0.25f, 1f);
    public Color action = new(0.55f, 0.55f, 0.75f, 1f);
    public Color spell = new(0.60f, 0.40f, 0.75f, 1f);
    public Color @object = new(0.62f, 0.36f, 0.14f, 1f);
    public Color encounter = new(0.75f, 0.70f, 0.55f, 1f);
    public Color environmental = new(0.42f, 0.67f, 0.42f, 1f);

    [AutoStaticsCleanup]
    private static CardPalette defaultPalette;

    // Used when nothing was installed. Built in memory, never saved as an asset.
    public static CardPalette Default
    {
        get
        {
            if (defaultPalette == null)
            {
                defaultPalette = CreateInstance<CardPalette>();
                defaultPalette.name = "CardPalette (built-in defaults)";
                defaultPalette.hideFlags = HideFlags.HideAndDontSave;
            }
            return defaultPalette;
        }
    }

    public Color GetCardTypeColor(CardTypeEnum cardType) => cardType switch
    {
        CardTypeEnum.PC => pc,
        CardTypeEnum.Land => land,
        CardTypeEnum.Character => character,
        CardTypeEnum.Army => army,
        CardTypeEnum.Event => @event,
        CardTypeEnum.Action => action,
        CardTypeEnum.Spell => spell,
        CardTypeEnum.Object => @object,
        CardTypeEnum.Encounter => encounter,
        CardTypeEnum.Environmental => environmental,
        _ => Color.clear
    };
}
