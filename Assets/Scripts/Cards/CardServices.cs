using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

// The seam that replaces Runeboard's direct coupling.
//
// In Runeboard, Card.cs reached straight for Game.Instance, Board.Instance, DeckManager.Instance,
// ActionsManager, Illustrations, Colors, CursorManager, Sounds and HexPathRenderer. That is what made
// the card face impossible to lift out on its own: two thirds of the codebase came with it.
//
// Here the face asks CardServices instead. Every hook is optional and every one has a working
// default, so the prefabs render correctly in a project that installs nothing at all — art and
// palette come from assets, every card reads as playable, clicks do nothing. Install a hook when
// this project grows real rules for it.
public static partial class CardServices
{
    // --- Art -------------------------------------------------------------------------------------
    // Replaces Illustrations (which streamed ~1000 sprites through Addressables). Assign a built
    // CardArtLibrary asset, or install any ICardArtSource of your own.
    [AutoStaticsCleanup]
    public static ICardArtSource Art { get; set; }

    // --- Palette ---------------------------------------------------------------------------------
    // Replaces Colors.GetColorByName("action" / "spell" / ...). Falls back to CardPalette.Default,
    // which carries the same hues Runeboard shipped, so borders are correctly colored out of the box.
    [AutoStaticsCleanup]
    private static ICardPalette palette;
    public static ICardPalette Palette
    {
        get => palette ??= CardPalette.Default;
        set => palette = value;
    }

    // --- Face style ------------------------------------------------------------------------------
    // Style for the pieces the card face builds itself at refresh time -- the requirement-failure
    // messages -- which board chrome cannot reach afterwards.
    // BoardPresentation installs the active BoardSkin's section here, so the skin stays the single
    // authority without Card ever referencing BoardSkin. Falls back to the values the face shipped.
    [AutoStaticsCleanup]
    private static ICardFaceStyle faceStyle;
    public static ICardFaceStyle FaceStyle
    {
        get => faceStyle ??= DefaultCardFaceStyle.Instance;
        set => faceStyle = value;
    }

    // --- Playability -----------------------------------------------------------------------------
    // Replaces CardData.EvaluatePlayability + ActionsManager.ResolveActionByRef + the Leader's
    // resource piles. Null means "everything is playable", which is the right answer for a project
    // with no rules yet.
    [AutoStaticsCleanup]
    public static ICardPlayabilitySource Playability { get; set; }

    // --- Interaction -----------------------------------------------------------------------------
    // Replaces TryPlayCard/Discard's walk through Game, DeckManager and the action pipeline.
    [AutoStaticsCleanup]
    public static ICardInteractionHandler Interaction { get; set; }

    // --- Feedback --------------------------------------------------------------------------------
    // Replaces Sounds.Instance and CursorManager. Optional; null is silent and leaves the cursor be.
    [AutoStaticsCleanup]
    public static ICardFeedback Feedback { get; set; }

    // Clears every installed hook. Domain reload does this for free in the editor; call it from a
    // test teardown, where it does not.
    public static void ResetAll()
    {
        Art = null;
        palette = null;
        faceStyle = null;
        Playability = null;
        Interaction = null;
        Feedback = null;
    }
}

public interface ICardArtSource
{
    // Looks up by any of the card's candidate names (spriteName, portraitName, name, action ref).
    // cardArtOnly mirrors Runeboard's UseCardArtFolderOnly: restrict to the Art/Cards folder so a
    // same-named sprite from a UI or animation folder can't win (see Card.ResolveCardArtwork).
    bool TryGetSprite(string name, bool cardArtOnly, out Sprite sprite);
}

public interface ICardPalette
{
    // Return a color with alpha 0 to mean "no color for this type" — the face then leaves the
    // authored border color alone rather than painting it transparent.
    Color GetCardTypeColor(CardTypeEnum cardType);
}

public interface ICardFaceStyle
{
    // Colour of the "Need 3<sprite name="gold">" lines under the description.
    Color RequirementsMessageColor { get; }
}

// What the face used before any of this was skinnable. Kept as the no-install default so the
// prefabs still render correctly in a project that never sets up a board.
public sealed class DefaultCardFaceStyle : ICardFaceStyle
{
    // Immutable and stateless, so there is nothing for the auto cleanup to reset to.
    [NoAutoStaticsCleanup]
    public static readonly DefaultCardFaceStyle Instance = new();
    public Color RequirementsMessageColor => Color.red;
}

public interface ICardPlayabilitySource
{
    // Fill in and return the card's playability. The face renders result.messages verbatim, in red,
    // beneath the description, and dims the card when isPlayable is false.
    CardPlayabilityResult Evaluate(CardData data);
}

public interface ICardInteractionHandler
{
    // Called on a left click on a playable card. Return true if the card was consumed, which lets
    // the face run its discard/fly-out animation.
    bool TryPlay(Card card, CardData data);

    // Called by the card's own discard button.
    bool TryDiscard(Card card, CardData data);
}

public interface ICardFeedback
{
    void OnCardHoverEnter(CardData data, bool isPlayable);
    void OnCardHoverExit(CardData data);
}

// A ready-made playability source for the common case: the project has costs but no characters yet.
// Reports a card unplayable when a supplied purse can't cover its printed resource requirements,
// and writes the same sprite-tagged "Need 3<sprite name="gold">" lines Runeboard showed.
public class SimpleResourcePlayability : ICardPlayabilitySource
{
    private readonly IReadOnlyDictionary<string, int> purse;

    public SimpleResourcePlayability(IReadOnlyDictionary<string, int> purse)
    {
        this.purse = purse;
    }

    public CardPlayabilityResult Evaluate(CardData data)
    {
        CardPlayabilityResult result = data?.playability ?? new CardPlayabilityResult();
        result.Reset();
        if (data == null) return result;

        List<string> missing = new();
        Require(missing, "leather", data.leatherRequired);
        Require(missing, "timber", data.timberRequired);
        Require(missing, "mounts", data.mountsRequired);
        Require(missing, "iron", data.ironRequired);
        Require(missing, "steel", data.steelRequired);
        Require(missing, "mithril", data.mithrilRequired);
        Require(missing, "gold", data.GetTotalGoldCost());

        if (missing.Count > 0)
        {
            result.failsResourceRequirements = true;
            result.isPlayable = false;
            result.messages.Add($"<sprite name=\"error\">Need {string.Join(string.Empty, missing)}");
        }

        return result;
    }

    private void Require(List<string> missing, string resource, int required)
    {
        if (required <= 0) return;
        int held = purse != null && purse.TryGetValue(resource, out int amount) ? amount : 0;
        if (held < required) missing.Add($"{required}<sprite name=\"{resource}\">");
    }
}
