using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The deck/card authoring window, ported from Runeboard's DeckExplorerWindow
// (Assets/Editor/DeckExplorerWindow.cs, "Tools/Runeboard/Deck Explorer"). Three panes — decks,
// cards, detail — with a live render of the real Card.prefab beside the fields that feed it, so a
// rename or a re-quote can be judged on the card face rather than in JSON.
//
// What changed on the way over, and why:
//   * Reference cards are gone. Runeboard let a subdeck hold a stub pointing at a base-deck card
//     (referenceDeckId/referenceCardId) and resolved it at load. DBD's CardData carries no such
//     fields and CardCatalog does no such resolution, so the import flattened all 689 of them into
//     full cards. Copy/Move here therefore deep-copy instead of leaving a stub behind.
//   * Abilities follow DBD's split enums: an Army card takes any number of
//     CharacterAndArmySpecialAbilityEnum, a Character card takes the union of that and
//     CharacterOnlySpecialAbilityEnum (CardData.characterAbilities).
//   * The gameplay lookups Runeboard's version made — Board.selectedCharacter, the Leader's
//     resource piles, ActionsManager/CharacterAction, the Colors scene singleton, the Illustrations
//     Addressables singleton — have no counterpart here. Each now goes through the CardServices
//     seam, which is exactly what that seam exists for, and degrades to a plain message when
//     nothing is installed.
//   * Object fields are only the ones DBD's CardData kept; the combat-effect table, passive-effect
//     hooks and status-effect modifiers went with the dropped gameplay half.
public class DeckManagerWindow : EditorWindow
{
    private const string MenuPath = "Tools/Cards/Deck Manager";
    private const string CardPrefabPath = "Assets/Prefabs/Card.prefab";
    private const string CardArtLibraryPath = "Assets/Art/CardArtLibrary.asset";
    private const string ManifestResourceName = "Cards";

    private class DeckEntryView
    {
        public DeckManifestEntry manifest;
        public DeckData deckData;

        // What the card pane shows for this deck. A meta deck lists the cards it owns; a reference
        // deck lists the very same CardData instances its cardRefs point at — not copies — so an
        // edit made while browsing Gandalf's deck is the same edit as one made in the meta deck.
        public readonly List<CardData> cards = new();

        public bool IsMeta => manifest != null && manifest.isMetaDeck;
    }

    private readonly List<DeckEntryView> deckViews = new();
    private readonly List<CardData> filteredCards = new();

    // Which meta deck owns each card, and how many reference decks point at it. Both are rebuilt by
    // RefreshData and keyed by card id, which is unique across every deck.
    private readonly Dictionary<int, DeckEntryView> ownerByCardId = new();
    private readonly Dictionary<int, int> referenceCountByCardId = new();

    private Vector2 deckScroll;
    private Vector2 cardScroll;
    private Vector2 detailScroll;

    private string searchText = string.Empty;
    private int selectedDeckIndex;
    private int selectedCardIndex;
    private bool onlyShowCardsWithActions;
    private bool sortCardsByTypeThenName = true;
    private bool showDeckEditor;
    private bool showRawJsonEditor;
    private string copyTargetResourcePath;

    // Edited-field mirror. Everything below is re-read from the selected card whenever the
    // selection changes (see SyncEditableCardFields) and written back by the section Save buttons,
    // so a half-typed value never leaks into the deck file.
    private string editedCardKey;
    private string editedName = string.Empty;
    private string editedQuote = string.Empty;
    private string editedDescription = string.Empty;
    private string editedActionEffect = string.Empty;
    private string editedRequirementsText = string.Empty;
    private string editedHistoryText = string.Empty;
    private string editedRegion = string.Empty;
    private string editedTags = string.Empty;
    private string editedActionRef = string.Empty;
    private string editedSpriteName = string.Empty;
    private string editedPortraitName = string.Empty;
    private string editedDeckSpriteName = string.Empty;
    private string editedCharacterGroup = string.Empty;
    private RacesEnum editedRace;
    private SexEnum editedSex;
    private int editedAlignment;
    private int editedDifficulty;

    private CardTypeEnum editedCardType;
    private CardSituationEnum editedSituation;
    private CardSituationEnum editedSituation2;

    private int editedCommanderSkillRequired;
    private int editedAgentSkillRequired;
    private int editedEmissarySkillRequired;
    private int editedMageSkillRequired;
    private int editedLeatherRequired;
    private int editedMountsRequired;
    private int editedTimberRequired;
    private int editedIronRequired;
    private int editedSteelRequired;
    private int editedMithrilRequired;
    private int editedGoldRequired;
    private int editedJokerRequired;

    private int editedLeatherGranted;
    private int editedMountsGranted;
    private int editedTimberGranted;
    private int editedIronGranted;
    private int editedSteelGranted;
    private int editedMithrilGranted;
    private int editedGoldGranted;
    private bool editedIsUnderground;

    private TroopsTypeEnum editedTroopType;
    private int editedProcChance;
    private ObjectCharacterArmySpecialAbilityEnum editedSharedAbilityToAdd;
    private CharacterOnlySpecialAbilityEnum editedCharacterAbilityToAdd;

    private int editedCharacterCommander;
    private int editedCharacterAgent;
    private int editedCharacterEmissary;
    private int editedCharacterMage;
    private int editedAttack;
    private int editedDefense;
    private string editedStartingPC = string.Empty;

    private bool editedObjectHidden;
    private ObjectTypeEnum editedObjectType;
    private ObjectTypeEnum editedPcObjectTypeToAdd = ObjectTypeEnum.Weapon;
    private string editedBirthplaceToAdd = string.Empty;
    private bool editedObjectTransferable;
    private int editedObjectCopies;
    private int editedObjectCommanderBonus;
    private int editedObjectAgentBonus;
    private int editedObjectEmmissaryBonus;
    private int editedObjectMageBonus;
    private int editedObjectHealPerTurn;
    private int editedObjectMovementBonus;
    private bool editedObjectIgnoreTerrainMovementPenalty;
    private bool editedObjectGrantsHasteAtSea;
    private bool editedObjectGrantsEnvironmentalImmunity;
    private int editedObjectAutoScoutRadius;
    private int editedObjectDetectionEvasion;
    private int editedObjectRecruitBonusMenAtArms;
    private int editedObjectScryAreaBonus;
    private int editedObjectScryObjectBonus;

    private string editedRawJson = string.Empty;

    // Deck-level mirror, synced the same way off the selected deck's manifest entry.
    private string editedDeckKey;
    private string editedDeckId = string.Empty;
    private string editedDeckNation = string.Empty;
    private string editedDeckThematic = string.Empty;
    private string editedDeckSprite = string.Empty;
    private int editedDeckAlignment;
    private bool editedDeckSharedToAll;
    private bool editedDeckExcluded;
    private string editedDeckAvatar = string.Empty;

    private const float PreviewCardW = 275f;
    private const float PreviewCardH = 325f;
    private const float PreviewPad = 15f;
    private const float PreviewCanvasW = PreviewCardW + PreviewPad * 2f;
    private const float PreviewCanvasH = PreviewCardH + PreviewPad * 2f;

    // The preview box is much larger than the card's own canvas rect, and on a scaled editor every
    // GUI point is more than one physical pixel — so a render texture sized to the canvas units gets
    // magnified two to four times on its way to the screen. That magnification, not a stale font, is
    // what makes TMP's SDF text look soft and jagged here. Rasterize at the size the texture is
    // actually drawn at, supersampled so the SDF shader's screen-space antialiasing has room to work.
    private const int PreviewSupersample = 2;
    private const int PreviewMaxTextureSize = 4096;

    // Every button that rewrites a deck file queues itself here instead of acting inline. Saving
    // reparses the deck and rebuilds the card list, and doing that halfway through OnGUI leaves
    // IMGUI's Layout and Repaint passes disagreeing about how many controls exist — which Unity
    // reports as a mismatched-LayoutGroup error and which makes the window unusable until reopened.
    private Action pendingAction;

    private CardsManifest cardsManifest;
    private GameObject cardPrefabAsset;
    private GameObject previewRoot;
    private GameObject previewCanvasRoot;
    private GameObject previewCardObject;
    private Card previewCardComponent;
    private Camera previewCamera;
    private RenderTexture previewRenderTexture;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        GetWindow<DeckManagerWindow>("Deck Manager");
    }

    private void OnEnable()
    {
        RefreshData();
    }

    private void OnDisable()
    {
        ClearPreviewInstance();
    }

    private void OnGUI()
    {
        GUI.enabled = true;
        EditorGUI.showMixedValue = false;
        DrawToolbar();

        if (deckViews.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No deck data found. Expected a manifest at Assets/Resources/Cards.json whose entries' " +
                "resourcePath values name TextAssets under Assets/Resources.",
                MessageType.Warning);
            if (GUILayout.Button("Refresh")) Defer(RefreshData);
            RunPendingAction();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawDeckPane();
        DrawCardPane();
        DrawDetailPane();
        EditorGUILayout.EndHorizontal();

        RunPendingAction();
    }

    private void Defer(Action action) => pendingAction = action;

    // Runs on Repaint, once the frame has been fully laid out and drawn, so the mutation lands
    // between frames rather than inside one.
    private void RunPendingAction()
    {
        if (pendingAction == null || Event.current.type != EventType.Repaint) return;

        Action action = pendingAction;
        pendingAction = null;
        action();
        Repaint();
    }

    // --- Toolbar ---------------------------------------------------------------------------------

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            Defer(RefreshData);
        }

        bool newOnlyActions = GUILayout.Toggle(onlyShowCardsWithActions, "Actions only", EditorStyles.toolbarButton, GUILayout.Width(90));
        if (newOnlyActions != onlyShowCardsWithActions)
        {
            onlyShowCardsWithActions = newOnlyActions;
            RebuildFilteredCards();
        }

        bool newSort = GUILayout.Toggle(sortCardsByTypeThenName, "Sort by type", EditorStyles.toolbarButton, GUILayout.Width(85));
        if (newSort != sortCardsByTypeThenName)
        {
            sortCardsByTypeThenName = newSort;
            RebuildFilteredCards();
        }

        showDeckEditor = GUILayout.Toggle(showDeckEditor, "Edit deck", EditorStyles.toolbarButton, GUILayout.Width(70));

        GUILayout.Space(8);
        GUILayout.Label("Search", GUILayout.Width(45));
        string newSearch = GUILayout.TextField(searchText, EditorStyles.toolbarTextField, GUILayout.MinWidth(180));
        if (!string.Equals(newSearch, searchText, StringComparison.Ordinal))
        {
            searchText = newSearch;
            RebuildFilteredCards();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label($"{deckViews.Count} decks / {CountAllCards()} cards", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    // --- Deck pane -------------------------------------------------------------------------------

    private void DrawDeckPane()
    {
        GUILayout.BeginVertical(GUILayout.Width(360));
        EditorGUILayout.LabelField("Decks", EditorStyles.boldLabel);

        deckScroll = EditorGUILayout.BeginScrollView(deckScroll, GUILayout.Width(360));
        for (int i = 0; i < deckViews.Count; i++)
        {
            DeckEntryView view = deckViews[i];
            if (view?.manifest == null) continue;

            bool selected = i == selectedDeckIndex;
            GUIStyle style = selected ? EditorStyles.helpBox : EditorStyles.label;
            if (GUILayout.Toggle(selected, BuildDeckLabel(view), style) && selectedDeckIndex != i)
            {
                selectedDeckIndex = i;
                selectedCardIndex = 0;
                editedDeckKey = null;
                RebuildFilteredCards();
            }
        }
        EditorGUILayout.EndScrollView();

        if (showDeckEditor) DrawDeckEditor();

        GUILayout.EndVertical();
    }

    // Renaming a deck is a three-place edit — the manifest entry, the deck file's own deckId, and,
    // for a meta deck, the deckId stamped on every card it owns. Doing it by hand in JSON is what
    // this section exists to avoid. resourcePath stays read-only because changing it means moving
    // the file, which belongs in the Project window; so does Meta, because which cards a meta deck
    // owns follows from their type.
    private void DrawDeckEditor()
    {
        DeckEntryView view = GetSelectedDeckView();
        if (view?.manifest == null) return;

        SyncEditableDeckFields(view);

        GUILayout.Space(6);
        EditorGUILayout.LabelField("Selected Deck", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Resource Path", view.manifest.resourcePath ?? string.Empty);
        EditorGUILayout.LabelField("Kind", view.IsMeta
            ? "Meta deck — owns the card data for one card type"
            : "Reference deck — lists card ids owned by the meta decks");

        editedDeckId = EditorGUILayout.TextField("Deck Id", editedDeckId);
        editedDeckNation = EditorGUILayout.TextField("Nation", editedDeckNation);
        editedDeckSprite = EditorGUILayout.TextField("Deck Sprite", editedDeckSprite);
        editedDeckAlignment = EditorGUILayout.IntField("Alignment", editedDeckAlignment);
        editedDeckSharedToAll = EditorGUILayout.Toggle(
            new GUIContent("Shared To All", "Not tied to one nation."), editedDeckSharedToAll);
        editedDeckExcluded = EditorGUILayout.Toggle(
            new GUIContent("Excluded", "World content that is never part of a player's drawable pool."), editedDeckExcluded);
        if (!view.IsMeta)
        {
            List<string> avatars = new() { "None" };
            avatars.AddRange(view.cards.Where(c => c != null && c.GetCardType() == CardTypeEnum.Character).Select(c => c.name).OrderBy(n => n));
            int avatarIndex = Mathf.Max(0, avatars.IndexOf(string.IsNullOrWhiteSpace(editedDeckAvatar) ? "None" : editedDeckAvatar));
            editedDeckAvatar = avatars[EditorGUILayout.Popup("Avatar", avatarIndex, avatars.ToArray())];
            if (editedDeckAvatar == "None") editedDeckAvatar = string.Empty;
        }

        EditorGUILayout.LabelField("Thematic");
        editedDeckThematic = EditorGUILayout.TextArea(
            editedDeckThematic, new GUIStyle(EditorStyles.textArea) { wordWrap = true }, GUILayout.MinHeight(60));

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save deck", GUILayout.Width(110))) Defer(() => SaveDeckFields(view));
        EditorGUILayout.EndHorizontal();
    }

    // --- Card pane -------------------------------------------------------------------------------

    private void DrawCardPane()
    {
        GUILayout.BeginVertical(GUILayout.Width(320));
        EditorGUILayout.LabelField("Cards", EditorStyles.boldLabel);

        DeckEntryView deckView = GetSelectedDeckView();
        if (deckView == null)
        {
            EditorGUILayout.HelpBox("Select a deck.", MessageType.Info);
            GUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{filteredCards.Count} card(s)");
        if (GUILayout.Button(
                new GUIContent("+ New", deckView.IsMeta
                    ? "Create a card in this meta deck."
                    : $"Create a card in the Action meta deck and reference it from {deckView.manifest.deckId}."),
                EditorStyles.miniButton, GUILayout.Width(55)))
        {
            Defer(() => AddCard(deckView));
        }
        EditorGUILayout.EndHorizontal();

        cardScroll = EditorGUILayout.BeginScrollView(cardScroll, GUILayout.Width(320));
        for (int i = 0; i < filteredCards.Count; i++)
        {
            CardData card = filteredCards[i];
            if (card == null) continue;

            bool selected = i == selectedCardIndex;
            string prefix = IsCardFinalized(card) ? "✔ " : string.Empty;
            string label = $"{prefix}{FormatCardTitle(card.name)}  [{FormatCardTypeLabel(card.GetCardType())}]";
            if (GUILayout.Toggle(selected, label, CreateRichTextStyle(selected ? EditorStyles.helpBox : EditorStyles.label))
                && selectedCardIndex != i)
            {
                selectedCardIndex = i;
                Repaint();
            }
        }
        EditorGUILayout.EndScrollView();

        GUILayout.EndVertical();
    }

    // --- Detail pane -----------------------------------------------------------------------------

    private void DrawDetailPane()
    {
        GUI.enabled = true;
        EditorGUI.showMixedValue = false;
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        CardData card = GetSelectedCard();
        if (card == null)
        {
            EditorGUILayout.HelpBox("Select a card to inspect its preview.", MessageType.Info);
            GUILayout.EndVertical();
            return;
        }

        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

        DrawSharingNotice(card);

        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        DrawCardPreview(card);

        GUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        bool finalized = EditorGUILayout.ToggleLeft("Finalized", IsCardFinalized(card), GUILayout.Width(90));
        if (EditorGUI.EndChangeCheck()) SetCardFinalized(card, finalized);
        GUILayout.FlexibleSpace();
        DrawCopyToDeckControls(card);
        GUILayout.Space(6);
        if (GUILayout.Button("Reload Card", GUILayout.Width(100))) Defer(ReloadSelectedCard);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Card Data", EditorStyles.boldLabel);
        DrawCardDetails(card);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Editable Requirements", EditorStyles.boldLabel);
        DrawEditableRequirements(card);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);
        DrawActionDetails(card);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Playability", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(BuildPlayabilityReport(card), EditorStyles.textArea, GUILayout.MinHeight(70));

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Raw Fields", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(BuildRawSummary(card), EditorStyles.textArea, GUILayout.MinHeight(110));

        GUILayout.Space(10);
        showRawJsonEditor = EditorGUILayout.Foldout(showRawJsonEditor, "Raw JSON Editor (all fields)", true);
        if (showRawJsonEditor) DrawRawJsonEditor(card);

        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    // There is one copy of each card, so an edit here is an edit everywhere it is referenced. Say so
    // before someone tunes a card for Gandalf and quietly changes Sauron's too.
    private void DrawSharingNotice(CardData card)
    {
        DeckEntryView owner = GetOwnerDeck(card);
        if (owner == null)
        {
            EditorGUILayout.HelpBox(
                $"No meta deck owns card id {card.cardId}, so edits cannot be saved. Check that its type has a meta deck in Cards.json.",
                MessageType.Error);
            return;
        }

        int references = GetReferenceCount(card);
        string where = references switch
        {
            0 => "No deck references it yet.",
            1 => $"Referenced by 1 deck: {string.Join(", ", DecksReferencing(card.cardId).Select(v => v.manifest.deckId))}.",
            _ => $"Referenced by {references} decks: {string.Join(", ", DecksReferencing(card.cardId).Select(v => v.manifest.deckId))}.",
        };
        EditorGUILayout.HelpBox(
            $"Owned by {owner.manifest.deckId} (id {card.cardId}). {where}"
            + (references > 1 ? "\nEditing this card changes it in all of them." : string.Empty),
            references > 1 ? MessageType.Warning : MessageType.Info);
    }

    private void DrawCardPreview(CardData card)
    {
        Rect box = GUILayoutUtility.GetRect(0f, 540f, GUILayout.ExpandWidth(true));
        GUI.Box(box, GUIContent.none, EditorStyles.helpBox);

        Rect inner = new(box.x + 8f, box.y + 8f, box.width - 16f, box.height - 16f);
        try
        {
            UpdateLivePreview(card, inner);
        }
        catch (Exception ex)
        {
            EditorGUI.HelpBox(inner, $"Preview failed: {ex.Message}", MessageType.Warning);
        }
    }

    private void DrawCardDetails(CardData card)
    {
        SyncEditableCardFields(card);

        EditorGUILayout.LabelField("Name", FormatCardTitle(card.name));
        EditorGUILayout.LabelField("Type", FormatCardTypeLabel(card.GetCardType()), CreateRichTextStyle(EditorStyles.label));
        EditorGUILayout.LabelField("Deck", card.deckId ?? string.Empty);
        EditorGUILayout.LabelField("Card Id", card.cardId.ToString(CultureInfo.InvariantCulture));
        EditorGUILayout.LabelField("Gold Cost", card.GetTotalGoldCost().ToString(CultureInfo.InvariantCulture));
        EditorGUILayout.LabelField("Costs", BuildCostSummary(card));
        if (card.IsMissingCost())
        {
            EditorGUILayout.HelpBox($"{card.GetCardType()} cards are paid from hand and must print a cost.", MessageType.Warning);
        }
        EditorGUILayout.LabelField("Grants", BuildGrantSummary(card));

        if (card.GetCardType() == CardTypeEnum.Land && !string.IsNullOrWhiteSpace(card.name))
        {
            EditorGUILayout.LabelField(
                $"Allows travelling to Population Centers from {PcDescriptionBuilder.FormatDisplayRegionName(card.name)}.",
                EditorStyles.wordWrappedLabel);
        }

        if (card.GetCardType() == CardTypeEnum.PC && !string.IsNullOrWhiteSpace(card.name))
        {
            EditorGUILayout.LabelField($"As the destination: characters and encounters born in {card.name}, and objects of the kinds it trades in.", EditorStyles.wordWrappedLabel);
        }

        if (GUILayout.Button("Remove Card", GUILayout.Width(120))) Defer(() => RemoveCard(card));

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Editable Fields", EditorStyles.boldLabel);
        GUIStyle textAreaStyle = new(EditorStyles.textArea) { wordWrap = true };

        editedName = EditorGUILayout.TextField("Name", editedName);
        editedRegion = EditorGUILayout.TextField("Region", editedRegion);
        editedTags = EditorGUILayout.TextField(new GUIContent("Tags", "Comma-separated list"), editedTags);
        editedActionRef = EditorGUILayout.TextField(new GUIContent("Action Ref", "Action name this card resolves to"), editedActionRef);
        editedSpriteName = EditorGUILayout.TextField("Sprite Name", editedSpriteName);
        editedPortraitName = EditorGUILayout.TextField("Portrait Name", editedPortraitName);
        editedDeckSpriteName = EditorGUILayout.TextField("Deck Sprite Name", editedDeckSpriteName);
        editedCharacterGroup = EditorGUILayout.TextField("Character Group", editedCharacterGroup);
        editedRace = (RacesEnum)EditorGUILayout.EnumPopup("Race", editedRace);
        editedSex = (SexEnum)EditorGUILayout.EnumPopup("Sex", editedSex);
        editedAlignment = EditorGUILayout.IntField("Alignment", editedAlignment);
        editedDifficulty = EditorGUILayout.IntField("Difficulty", editedDifficulty);

        GUILayout.Space(4);
        EditorGUILayout.LabelField("Quote");
        editedQuote = EditorGUILayout.TextArea(editedQuote, textAreaStyle, GUILayout.MinHeight(40));
        EditorGUILayout.LabelField("Action Effect");
        editedActionEffect = EditorGUILayout.TextArea(editedActionEffect, textAreaStyle, GUILayout.MinHeight(60));
        EditorGUILayout.LabelField("Description");
        editedDescription = EditorGUILayout.TextArea(editedDescription, textAreaStyle, GUILayout.MinHeight(60));
        EditorGUILayout.LabelField("Requirements Text");
        editedRequirementsText = EditorGUILayout.TextArea(editedRequirementsText, textAreaStyle, GUILayout.MinHeight(40));
        EditorGUILayout.LabelField("History Text");
        editedHistoryText = EditorGUILayout.TextArea(editedHistoryText, textAreaStyle, GUILayout.MinHeight(40));

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save card fields", GUILayout.Width(140))) Defer(() => SaveCardFields(card));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCopyToDeckControls(CardData card)
    {
        DeckEntryView currentDeck = GetSelectedDeckView();
        List<DeckEntryView> targets = GetAllTransferTargets(currentDeck);
        if (targets.Count == 0)
        {
            EditorGUILayout.LabelField("To deck", "No deck targets");
            return;
        }

        int targetIndex = GetCopyTargetIndex(targets);
        string[] options = targets.Select(BuildDeckLabel).ToArray();

        EditorGUILayout.BeginHorizontal(GUILayout.Width(460));
        EditorGUILayout.LabelField("To deck", GUILayout.Width(55));
        int newIndex = EditorGUILayout.Popup(targetIndex, options, GUILayout.Width(230));
        if (newIndex != targetIndex) copyTargetResourcePath = targets[newIndex].manifest.resourcePath;

        DeckEntryView target = targets[newIndex];
        if (GUILayout.Button(new GUIContent("Add ref", "Reference this one card from the chosen deck. Nothing is copied."), GUILayout.Width(62)))
        {
            Defer(() => TransferCard(card, currentDeck, target, move: false));
        }

        // A meta deck has to keep the card it owns, so there is nothing to move away from it.
        using (new EditorGUI.DisabledScope(currentDeck == null || currentDeck.IsMeta))
        {
            if (GUILayout.Button(new GUIContent("Move ref", "Reference it from the chosen deck and stop referencing it here."), GUILayout.Width(66)))
            {
                Defer(() => TransferCard(card, currentDeck, target, move: true));
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditableRequirements(CardData card)
    {
        if (card == null) return;
        SyncEditableCardFields(card);

        EditorGUILayout.BeginHorizontal();
        editedCardType = (CardTypeEnum)EditorGUILayout.EnumPopup("Card Type", editedCardType);
        if (GUILayout.Button("Save type", GUILayout.Width(90))) Defer(() => SaveCardType(card));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        editedSituation = (CardSituationEnum)EditorGUILayout.EnumPopup("Situation", editedSituation);
        editedSituation2 = (CardSituationEnum)EditorGUILayout.EnumPopup(editedSituation2);
        if (GUILayout.Button("Save", GUILayout.Width(60))) Defer(() => SaveSituations(card));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        editedCommanderSkillRequired = EditorGUILayout.IntField("Commander", editedCommanderSkillRequired);
        editedAgentSkillRequired = EditorGUILayout.IntField("Agent", editedAgentSkillRequired);
        editedEmissarySkillRequired = EditorGUILayout.IntField("Emissary", editedEmissarySkillRequired);
        editedMageSkillRequired = EditorGUILayout.IntField("Mage", editedMageSkillRequired);

        GUILayout.Space(4);
        editedLeatherRequired = EditorGUILayout.IntField("Leather", editedLeatherRequired);
        editedTimberRequired = EditorGUILayout.IntField("Timber", editedTimberRequired);
        editedMountsRequired = EditorGUILayout.IntField("Mounts", editedMountsRequired);
        editedIronRequired = EditorGUILayout.IntField("Iron", editedIronRequired);
        editedSteelRequired = EditorGUILayout.IntField("Steel", editedSteelRequired);
        editedMithrilRequired = EditorGUILayout.IntField("Mithril", editedMithrilRequired);
        editedJokerRequired = EditorGUILayout.IntField("Joker", editedJokerRequired);

        // A Character's gold cost is derived from its skill points (GetAdditionalGoldCost), so an
        // authored goldRequired on one would be silently ignored by the card face.
        if (card.GetCardType() != CardTypeEnum.Character)
        {
            editedGoldRequired = EditorGUILayout.IntField("Gold", editedGoldRequired);
        }

        if (card.GetCardType() == CardTypeEnum.Army)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Army", EditorStyles.boldLabel);
            DrawEditableArmyType(card);
            DrawEditableAbilities(card, includeCharacterOnly: false);
        }

        if (card.GetCardType() == CardTypeEnum.Character)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Character Stats", EditorStyles.boldLabel);
            DrawEditableCharacterStats(card);
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Character Abilities", EditorStyles.boldLabel);
            DrawEditableAbilities(card, includeCharacterOnly: true);
        }

        if (card.GetCardType() == CardTypeEnum.Object)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Object Effects", EditorStyles.boldLabel);
            DrawEditableObjectStats(card);
        }

        if (card.GetCardType() == CardTypeEnum.Land)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Resource Grants", EditorStyles.boldLabel);
            DrawEditableGrants(card);
        }

        if (card.GetCardType() == CardTypeEnum.PC)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Trades In", EditorStyles.boldLabel);
            DrawEditableObjectTypes(card);
        }

        if (card.GetCardType() == CardTypeEnum.Encounter)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Birthplaces", EditorStyles.boldLabel);
            DrawEditableBirthplaces(card);
        }

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save new requirements", GUILayout.Width(180))) Defer(() => SaveNewRequirements(card));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditableGrants(CardData card)
    {
        if (card == null) return;
        SyncEditableCardFields(card);

        editedLeatherGranted = EditorGUILayout.IntField("Leather", editedLeatherGranted);
        editedTimberGranted = EditorGUILayout.IntField("Timber", editedTimberGranted);
        editedMountsGranted = EditorGUILayout.IntField("Mounts", editedMountsGranted);
        editedIronGranted = EditorGUILayout.IntField("Iron", editedIronGranted);
        editedSteelGranted = EditorGUILayout.IntField("Steel", editedSteelGranted);
        editedMithrilGranted = EditorGUILayout.IntField("Mithril", editedMithrilGranted);
        editedGoldGranted = EditorGUILayout.IntField("Gold", editedGoldGranted);

        if (card.GetCardType() == CardTypeEnum.PC)
        {
            GUILayout.Space(4);
            editedIsUnderground = EditorGUILayout.Toggle(
                new GUIContent("Underground", "The founded PC marks its hex as an entrance to the Underground."),
                editedIsUnderground);
        }

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save new grants", GUILayout.Width(160))) Defer(() => SaveNewGrants(card));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditableArmyType(CardData card)
    {
        SyncEditableCardFields(card);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Troop Type", GUILayout.Width(75));
        editedTroopType = (TroopsTypeEnum)EditorGUILayout.EnumPopup(editedTroopType);
        if (GUILayout.Button("Save type", GUILayout.Width(90))) Defer(() => SaveArmyType(card));
        EditorGUILayout.EndHorizontal();
    }

    // An Army card takes any number of CharacterAndArmySpecialAbilityEnum. A Character card takes
    // the union: the same shared list, plus CharacterOnlySpecialAbilityEnum. Two lists rather than
    // one because the enums overlap in ordinals — a single list of ints could not say which enum a
    // value came from.
    private void DrawEditableAbilities(CardData card, bool includeCharacterOnly)
    {
        if (card == null) return;
        card.specialAbilities ??= new List<ObjectCharacterArmySpecialAbilityEnum>();
        card.characterAbilities ??= new List<CharacterOnlySpecialAbilityEnum>();
        SyncEditableCardFields(card);

        bool changed = DrawAbilityList(
            card.specialAbilities,
            includeCharacterOnly ? "Shared abilities" : "Abilities",
            ref editedSharedAbilityToAdd);

        if (includeCharacterOnly)
        {
            GUILayout.Space(4);
            changed |= DrawAbilityList(card.characterAbilities, "Character-only abilities", ref editedCharacterAbilityToAdd);
        }

        GUILayout.Space(4);
        editedProcChance = EditorGUILayout.IntSlider(
            new GUIContent("Proc Chance", "Shown after every ability on the card face, e.g. \"Poisoning 40%\"."),
            editedProcChance, 1, 100);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        // Draw the button unconditionally — short-circuiting it behind `changed` would emit a
        // different control count on Layout than on Repaint, which Unity reports as a layout error.
        bool savePressed = GUILayout.Button("Save abilities", GUILayout.Width(130));
        EditorGUILayout.EndHorizontal();

        // An enum popup has no commit moment of its own, so an add/remove/change saves immediately.
        if (changed || savePressed) Defer(() => SaveAbilities(card));
    }

    // Returns true when the list changed, so the caller can save immediately — an enum popup has no
    // "commit" moment of its own.
    private static bool DrawAbilityList<T>(List<T> abilities, string header, ref T toAdd) where T : struct, Enum
    {
        EditorGUILayout.LabelField(header, EditorStyles.miniBoldLabel);

        bool changed = false;
        int toRemove = -1;
        for (int i = 0; i < abilities.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            T updated = (T)(object)EditorGUILayout.EnumPopup(abilities[i]);
            if (!Equals(updated, abilities[i]))
            {
                abilities[i] = updated;
                changed = true;
            }
            if (GUILayout.Button("-", GUILayout.Width(25))) toRemove = i;
            EditorGUILayout.EndHorizontal();
        }

        if (toRemove >= 0)
        {
            abilities.RemoveAt(toRemove);
            changed = true;
        }

        EditorGUILayout.BeginHorizontal();
        toAdd = (T)(object)EditorGUILayout.EnumPopup("Add", toAdd);
        if (GUILayout.Button("+", GUILayout.Width(25)) && !abilities.Contains(toAdd))
        {
            abilities.Add(toAdd);
            changed = true;
        }
        EditorGUILayout.EndHorizontal();

        return changed;
    }

    // A settlement lists the kinds of object that can be played while it is the destination. Saved on
    // change, like the ability lists: an enum popup has no commit moment of its own.
    private void DrawEditableObjectTypes(CardData card)
    {
        if (card == null) return;
        card.objectTypes ??= new List<ObjectTypeEnum>();
        EditorGUILayout.HelpBox("Objects of these kinds can be equipped here when this settlement is the destination.", MessageType.None);
        if (DrawAbilityList(card.objectTypes, "Object kinds", ref editedPcObjectTypeToAdd))
            Defer(() => SaveObjectTypes(card));
    }

    // An encounter is investigated at one of its birthplaces; each deck that holds the encounter
    // needs one of them among its own settlements.
    private void DrawEditableBirthplaces(CardData card)
    {
        if (card == null) return;
        card.birthplaces ??= new List<string>();
        bool changed = false;
        int toRemove = -1;
        for (int i = 0; i < card.birthplaces.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(card.birthplaces[i]);
            if (GUILayout.Button("-", GUILayout.Width(25))) toRemove = i;
            EditorGUILayout.EndHorizontal();
        }
        if (toRemove >= 0) { card.birthplaces.RemoveAt(toRemove); changed = true; }

        List<string> pcNames = GetAvailablePcNames();
        EditorGUILayout.BeginHorizontal();
        int index = Mathf.Max(0, pcNames.IndexOf(editedBirthplaceToAdd));
        index = EditorGUILayout.Popup("Add", index, pcNames.ToArray());
        editedBirthplaceToAdd = pcNames[Mathf.Clamp(index, 0, pcNames.Count - 1)];
        if (GUILayout.Button("+", GUILayout.Width(25)) && !string.IsNullOrWhiteSpace(editedBirthplaceToAdd) && !card.birthplaces.Contains(editedBirthplaceToAdd))
        {
            card.birthplaces.Add(editedBirthplaceToAdd);
            changed = true;
        }
        EditorGUILayout.EndHorizontal();
        if (changed) Defer(() => SaveBirthplaces(card));
    }

    private void DrawEditableCharacterStats(CardData card)
    {
        SyncEditableCardFields(card);

        EditorGUILayout.LabelField("Gold Cost", card.GetTotalGoldCost().ToString(CultureInfo.InvariantCulture));
        editedCharacterCommander = EditorGUILayout.IntField("Commander", editedCharacterCommander);
        editedCharacterAgent = EditorGUILayout.IntField("Agent", editedCharacterAgent);
        editedCharacterEmissary = EditorGUILayout.IntField("Emissary", editedCharacterEmissary);
        editedCharacterMage = EditorGUILayout.IntField("Mage", editedCharacterMage);
        editedAttack = Mathf.Clamp(EditorGUILayout.IntField("Attack", editedAttack), 1, 6);
        editedDefense = Mathf.Clamp(EditorGUILayout.IntField("Defense", editedDefense), 1, 6);

        GUILayout.Space(4);
        List<string> pcNames = GetAvailablePcNames();
        int currentPcIndex = Mathf.Max(0, pcNames.IndexOf(editedStartingPC));
        int newPcIndex = EditorGUILayout.Popup("Starting PC", currentPcIndex, pcNames.ToArray());
        editedStartingPC = pcNames[Mathf.Clamp(newPcIndex, 0, pcNames.Count - 1)];

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save character stats", GUILayout.Width(180))) Defer(() => SaveCharacterStats(card));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditableObjectStats(CardData card)
    {
        SyncEditableCardFields(card);

        editedObjectType = (ObjectTypeEnum)EditorGUILayout.EnumPopup(
            new GUIContent("Kind", "Decides which settlements the object can be played at: a destination must trade in this kind."), editedObjectType);
        editedObjectHidden = EditorGUILayout.Toggle(
            new GUIContent("Hidden", "Not revealed to its owner until found or granted."), editedObjectHidden);
        editedObjectTransferable = EditorGUILayout.Toggle(
            new GUIContent("Transferable", "Can be handed off between characters."), editedObjectTransferable);
        editedObjectCopies = Mathf.Max(1, EditorGUILayout.IntField(
            new GUIContent("Copies", "How many instances seed the hidden-object pool. Unique items stay at 1."),
            editedObjectCopies));

        GUILayout.Space(6);
        EditorGUILayout.LabelField("Skill Bonuses", EditorStyles.boldLabel);
        editedObjectCommanderBonus = EditorGUILayout.IntField("Commander", editedObjectCommanderBonus);
        editedObjectAgentBonus = EditorGUILayout.IntField("Agent", editedObjectAgentBonus);
        editedObjectEmmissaryBonus = EditorGUILayout.IntField("Emissary", editedObjectEmmissaryBonus);
        editedObjectMageBonus = EditorGUILayout.IntField("Mage", editedObjectMageBonus);
        editedObjectRecruitBonusMenAtArms = EditorGUILayout.IntField("Recruit Bonus (Men-at-Arms)", editedObjectRecruitBonusMenAtArms);

        GUILayout.Space(6);
        EditorGUILayout.LabelField("Utility", EditorStyles.boldLabel);
        editedObjectHealPerTurn = EditorGUILayout.IntField("Heal Per Turn", editedObjectHealPerTurn);
        editedObjectMovementBonus = EditorGUILayout.IntField("Movement Bonus", editedObjectMovementBonus);
        editedObjectIgnoreTerrainMovementPenalty = EditorGUILayout.Toggle("Ignore Terrain Penalty", editedObjectIgnoreTerrainMovementPenalty);
        editedObjectGrantsHasteAtSea = EditorGUILayout.Toggle("Grants Haste At Sea", editedObjectGrantsHasteAtSea);
        editedObjectGrantsEnvironmentalImmunity = EditorGUILayout.Toggle("Environmental Immunity", editedObjectGrantsEnvironmentalImmunity);
        editedObjectAutoScoutRadius = EditorGUILayout.IntField("Auto-Scout Radius", editedObjectAutoScoutRadius);
        editedObjectDetectionEvasion = EditorGUILayout.IntField("Detection Evasion", editedObjectDetectionEvasion);
        editedObjectScryAreaBonus = EditorGUILayout.IntField("Scry Area Bonus", editedObjectScryAreaBonus);
        editedObjectScryObjectBonus = EditorGUILayout.IntField("Scry Object Bonus", editedObjectScryObjectBonus);

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save object effects", GUILayout.Width(180))) Defer(() => SaveObjectStats(card));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawActionDetails(CardData card)
    {
        string actionRef = card.GetActionRef();
        EditorGUILayout.LabelField("Action Ref", string.IsNullOrWhiteSpace(actionRef) ? "(none)" : actionRef);
        EditorGUILayout.LabelField("Action Class Name", card.actionClassName ?? string.Empty);
        EditorGUILayout.LabelField("Situation", card.GetSituation().ToString());
        EditorGUILayout.LabelField("Situation 2", card.GetSecondarySituation().ToString());
        EditorGUILayout.LabelField("Required Skills", BuildSkillSummary(card));
    }

    private void DrawRawJsonEditor(CardData card)
    {
        if (card == null) return;

        EditorGUILayout.HelpBox(
            "Edits every serialized field of the card. Load the JSON, edit it, then apply.",
            MessageType.Info);

        if (GUILayout.Button("Load card JSON", GUILayout.Width(130)))
        {
            editedRawJson = JsonUtility.ToJson(card, true);
            GUI.FocusControl(null);
        }

        if (string.IsNullOrWhiteSpace(editedRawJson)) return;

        editedRawJson = EditorGUILayout.TextArea(
            editedRawJson, new GUIStyle(EditorStyles.textArea) { wordWrap = false }, GUILayout.MinHeight(220));

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Discard", GUILayout.Width(80)))
        {
            editedRawJson = string.Empty;
            GUI.FocusControl(null);
        }
        if (GUILayout.Button("Apply && save JSON", GUILayout.Width(150))) Defer(() => SaveRawJson(card));
        EditorGUILayout.EndHorizontal();
    }

    // --- Editable-field sync ---------------------------------------------------------------------

    private void SyncEditableCardFields(CardData card)
    {
        string key = GetEditableCardKey(card);
        if (string.Equals(editedCardKey, key, StringComparison.Ordinal)) return;

        editedCardKey = key;
        editedName = card.name ?? string.Empty;
        editedQuote = card.quote ?? string.Empty;
        editedDescription = card.description ?? string.Empty;
        editedActionEffect = card.actionEffect ?? string.Empty;
        editedRequirementsText = card.requirementsText ?? string.Empty;
        editedHistoryText = card.historyText ?? string.Empty;
        editedRegion = card.region ?? string.Empty;
        editedTags = card.tags != null ? string.Join(", ", card.tags) : string.Empty;
        editedActionRef = card.GetActionRef() ?? string.Empty;
        editedSpriteName = card.spriteName ?? string.Empty;
        editedPortraitName = card.portraitName ?? string.Empty;
        editedDeckSpriteName = card.deckSpriteName ?? string.Empty;
        editedCharacterGroup = card.characterGroup ?? string.Empty;
        editedRace = card.race;
        editedSex = card.sex;
        editedAlignment = card.alignment;
        editedDifficulty = card.difficulty;

        editedCardType = card.GetCardType();
        editedSituation = card.GetSituation();
        editedSituation2 = card.GetSecondarySituation();

        editedCommanderSkillRequired = Mathf.Max(0, card.commanderSkillRequired);
        editedAgentSkillRequired = Mathf.Max(0, card.agentSkillRequired);
        editedEmissarySkillRequired = Mathf.Max(0, card.emissarySkillRequired);
        editedMageSkillRequired = Mathf.Max(0, card.mageSkillRequired);
        editedLeatherRequired = Mathf.Max(0, card.leatherRequired);
        editedTimberRequired = Mathf.Max(0, card.timberRequired);
        editedMountsRequired = Mathf.Max(0, card.mountsRequired);
        editedIronRequired = Mathf.Max(0, card.ironRequired);
        editedSteelRequired = Mathf.Max(0, card.steelRequired);
        editedMithrilRequired = Mathf.Max(0, card.mithrilRequired);
        editedGoldRequired = Mathf.Max(0, card.goldRequired);
        editedJokerRequired = Mathf.Max(0, card.jokerRequired);

        editedLeatherGranted = Mathf.Max(0, card.leatherGranted);
        editedTimberGranted = Mathf.Max(0, card.timberGranted);
        editedMountsGranted = Mathf.Max(0, card.mountsGranted);
        editedIronGranted = Mathf.Max(0, card.ironGranted);
        editedSteelGranted = Mathf.Max(0, card.steelGranted);
        editedMithrilGranted = Mathf.Max(0, card.mithrilGranted);
        editedGoldGranted = Mathf.Max(0, card.goldGranted);
        editedIsUnderground = card.isUnderground;

        editedTroopType = card.troopType;
        editedProcChance = Mathf.Clamp(card.procChance <= 0 ? 100 : card.procChance, 1, 100);

        editedCharacterCommander = Mathf.Max(0, card.commander);
        editedCharacterAgent = Mathf.Max(0, card.agent);
        editedCharacterEmissary = Mathf.Max(0, card.emmissary);
        editedCharacterMage = Mathf.Max(0, card.mage);
        editedAttack = Mathf.Clamp(card.attack, 1, 6);
        editedDefense = Mathf.Clamp(card.defense, 1, 6);
        editedStartingPC = card.startingPC ?? string.Empty;

        editedObjectHidden = card.hidden;
        editedObjectType = card.objectType;
        editedObjectTransferable = card.transferable;
        editedObjectCopies = Mathf.Max(1, card.copies);
        editedObjectCommanderBonus = card.commanderBonus;
        editedObjectAgentBonus = card.agentBonus;
        editedObjectEmmissaryBonus = card.emmissaryBonus;
        editedObjectMageBonus = card.mageBonus;
        editedObjectHealPerTurn = card.healPerTurn;
        editedObjectMovementBonus = card.movementBonus;
        editedObjectIgnoreTerrainMovementPenalty = card.ignoreTerrainMovementPenalty;
        editedObjectGrantsHasteAtSea = card.grantsHasteAtSea;
        editedObjectGrantsEnvironmentalImmunity = card.grantsEnvironmentalImmunity;
        editedObjectAutoScoutRadius = card.autoScoutRadius;
        editedObjectDetectionEvasion = card.detectionEvasion;
        editedObjectRecruitBonusMenAtArms = card.recruitBonusMenAtArms;
        editedObjectScryAreaBonus = card.scryAreaBonus;
        editedObjectScryObjectBonus = card.scryObjectBonus;

        editedRawJson = string.Empty;
    }

    private void SyncEditableDeckFields(DeckEntryView view)
    {
        string key = view?.manifest?.resourcePath ?? string.Empty;
        if (string.Equals(editedDeckKey, key, StringComparison.Ordinal)) return;

        editedDeckKey = key;
        editedDeckId = view.manifest.deckId ?? string.Empty;
        editedDeckNation = view.manifest.nation ?? string.Empty;
        editedDeckThematic = view.manifest.thematic ?? string.Empty;
        editedDeckSprite = view.manifest.deckSpriteName ?? string.Empty;
        editedDeckAlignment = view.manifest.alignment;
        editedDeckSharedToAll = view.manifest.sharedToAll;
        editedDeckExcluded = view.manifest.excluded;
        editedDeckAvatar = view.deckData?.avatarCharacter ?? string.Empty;
    }

    private static string GetEditableCardKey(CardData card)
    {
        if (card == null) return string.Empty;
        string deckId = string.IsNullOrWhiteSpace(card.deckId) ? "unknownDeck" : card.deckId.Trim();
        string cardName = string.IsNullOrWhiteSpace(card.name) ? "unknownCard" : card.name.Trim();
        return $"{deckId}:{card.cardId}:{cardName}";
    }

    // --- Saving ------------------------------------------------------------------------------------

    private void SaveCardFields(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;

        target.name = editedName?.Trim() ?? string.Empty;
        target.quote = editedQuote ?? string.Empty;
        target.actionEffect = editedActionEffect ?? string.Empty;
        target.description = editedDescription ?? string.Empty;
        target.requirementsText = editedRequirementsText ?? string.Empty;
        target.historyText = editedHistoryText ?? string.Empty;
        target.region = editedRegion?.Trim() ?? string.Empty;
        target.tags = SplitCommaList(editedTags);
        target.spriteName = editedSpriteName?.Trim() ?? string.Empty;
        target.portraitName = editedPortraitName?.Trim() ?? string.Empty;
        target.deckSpriteName = editedDeckSpriteName?.Trim() ?? string.Empty;
        target.characterGroup = editedCharacterGroup?.Trim() ?? string.Empty;
        target.race = editedRace;
        target.sex = editedSex;
        target.alignment = editedAlignment;
        target.difficulty = Mathf.Max(0, editedDifficulty);

        // GetActionRef() prefers `action` and falls back to `actionClassName`; write back to
        // whichever of the two it is actually reading, or the edit would appear to do nothing.
        string newActionRef = editedActionRef?.Trim() ?? string.Empty;
        if (!string.Equals(newActionRef, target.GetActionRef() ?? string.Empty, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(target.action)) target.action = newActionRef;
            else target.actionClassName = newActionRef;
        }

        editedCardKey = null;
        CommitDeck(deckView, $"card fields for '{target.name}'");
    }

    private void SaveNewRequirements(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;

        target.commanderSkillRequired = Mathf.Max(0, editedCommanderSkillRequired);
        target.agentSkillRequired = Mathf.Max(0, editedAgentSkillRequired);
        target.emissarySkillRequired = Mathf.Max(0, editedEmissarySkillRequired);
        target.mageSkillRequired = Mathf.Max(0, editedMageSkillRequired);
        target.leatherRequired = Mathf.Max(0, editedLeatherRequired);
        target.timberRequired = Mathf.Max(0, editedTimberRequired);
        target.mountsRequired = Mathf.Max(0, editedMountsRequired);
        target.ironRequired = Mathf.Max(0, editedIronRequired);
        target.steelRequired = Mathf.Max(0, editedSteelRequired);
        target.mithrilRequired = Mathf.Max(0, editedMithrilRequired);
        target.goldRequired = Mathf.Max(0, editedGoldRequired);
        target.jokerRequired = Mathf.Max(0, editedJokerRequired);

        CommitDeck(deckView, $"requirements for '{target.name}'");
    }

    private void SaveNewGrants(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;

        target.leatherGranted = Mathf.Max(0, editedLeatherGranted);
        target.timberGranted = Mathf.Max(0, editedTimberGranted);
        target.mountsGranted = Mathf.Max(0, editedMountsGranted);
        target.ironGranted = Mathf.Max(0, editedIronGranted);
        target.steelGranted = Mathf.Max(0, editedSteelGranted);
        target.mithrilGranted = Mathf.Max(0, editedMithrilGranted);
        target.goldGranted = Mathf.Max(0, editedGoldGranted);
        if (target.GetCardType() == CardTypeEnum.PC) target.isUnderground = editedIsUnderground;

        CommitDeck(deckView, $"grants for '{target.name}'");
    }

    private void SaveAbilities(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;

        target.specialAbilities = card.specialAbilities != null
            ? new List<ObjectCharacterArmySpecialAbilityEnum>(card.specialAbilities)
            : new List<ObjectCharacterArmySpecialAbilityEnum>();
        target.characterAbilities = card.characterAbilities != null
            ? new List<CharacterOnlySpecialAbilityEnum>(card.characterAbilities)
            : new List<CharacterOnlySpecialAbilityEnum>();

        // procChance only means anything next to an ability, and a stray value on an ability-less
        // card would still render as "... 40%" the moment one were added back.
        bool hasAny = target.specialAbilities.Count > 0 || target.characterAbilities.Count > 0;
        target.procChance = hasAny ? Mathf.Clamp(editedProcChance <= 0 ? 100 : editedProcChance, 1, 100) : 0;

        CommitDeck(deckView, $"abilities for '{target.name}'");
    }

    private void SaveArmyType(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;
        target.troopType = editedTroopType;
        CommitDeck(deckView, $"troop type for '{target.name}'");
    }

    private void SaveCharacterStats(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;

        target.commander = Mathf.Max(0, editedCharacterCommander);
        target.agent = Mathf.Max(0, editedCharacterAgent);
        target.emmissary = Mathf.Max(0, editedCharacterEmissary);
        target.mage = Mathf.Max(0, editedCharacterMage);
        target.attack = Mathf.Clamp(editedAttack, 1, 6);
        target.defense = Mathf.Clamp(editedDefense, 1, 6);
        target.startingPC = editedStartingPC ?? string.Empty;

        CommitDeck(deckView, $"character stats for '{target.name}'");
    }

    private void SaveObjectTypes(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;
        target.objectTypes = card.objectTypes != null ? new List<ObjectTypeEnum>(card.objectTypes.Distinct()) : new List<ObjectTypeEnum>();
        CommitDeck(deckView, $"object kinds for '{target.name}'");
    }

    private void SaveBirthplaces(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;
        target.birthplaces = card.birthplaces != null ? new List<string>(card.birthplaces.Distinct()) : new List<string>();
        CommitDeck(deckView, $"birthplaces for '{target.name}'");
    }

    private void SaveObjectStats(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;

        target.hidden = editedObjectHidden;
        target.objectType = editedObjectType;
        target.transferable = editedObjectTransferable;
        target.copies = Mathf.Max(1, editedObjectCopies);
        target.commanderBonus = editedObjectCommanderBonus;
        target.agentBonus = editedObjectAgentBonus;
        target.emmissaryBonus = editedObjectEmmissaryBonus;
        target.mageBonus = editedObjectMageBonus;
        target.healPerTurn = editedObjectHealPerTurn;
        target.movementBonus = editedObjectMovementBonus;
        target.ignoreTerrainMovementPenalty = editedObjectIgnoreTerrainMovementPenalty;
        target.grantsHasteAtSea = editedObjectGrantsHasteAtSea;
        target.grantsEnvironmentalImmunity = editedObjectGrantsEnvironmentalImmunity;
        target.autoScoutRadius = editedObjectAutoScoutRadius;
        target.detectionEvasion = editedObjectDetectionEvasion;
        target.recruitBonusMenAtArms = editedObjectRecruitBonusMenAtArms;
        target.scryAreaBonus = editedObjectScryAreaBonus;
        target.scryObjectBonus = editedObjectScryObjectBonus;

        CommitDeck(deckView, $"object effects for '{target.name}'");
    }

    // Changing a card's type moves it to a different meta deck, which also changes its id — ids are
    // blocked by type. Every deck that referenced the old id has to be repointed at the new one.
    private void SaveCardType(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView oldOwner, out CardData target)) return;

        CardTypeEnum newType = editedCardType;
        if (newType == target.GetCardType())
        {
            return;
        }
        if (newType == CardTypeEnum.Unknown)
        {
            EditorUtility.DisplayDialog(
                "Type required",
                "A card's type decides which meta deck owns it, so it cannot be cleared.",
                "OK");
            return;
        }

        DeckEntryView newOwner = GetMetaDeck(newType);
        if (newOwner?.deckData?.cards == null)
        {
            EditorUtility.DisplayDialog(
                "No meta deck",
                $"There is no '{CardCatalog.MetaDeckIdFor(newType)}' deck to move '{target.name}' into. Add one to Cards.json first.",
                "OK");
            return;
        }

        List<DeckEntryView> referencing = DecksReferencing(target.cardId).ToList();
        int oldId = target.cardId;
        int newId = GetNextCardId(newType);
        if (!EditorUtility.DisplayDialog(
                "Change Card Type",
                $"Retype '{target.name}' as {newType}?\n\nIt moves from {oldOwner.manifest.deckId} to {newOwner.manifest.deckId} and its id changes from {oldId} to {newId}."
                + (referencing.Count == 0 ? string.Empty : $"\n\n{referencing.Count} deck(s) referencing it will be repointed."),
                "Retype", "Cancel"))
        {
            return;
        }

        oldOwner.deckData.cards.RemoveAll(c => c != null && c.cardId == oldId);
        target.type = newType.ToString();
        target.cardId = newId;
        target.deckId = newOwner.deckData.deckId;
        target.alignment = newOwner.deckData.alignment;
        target.deckSpriteName = newOwner.manifest?.deckSpriteName ?? string.Empty;
        newOwner.deckData.cards.Add(target);

        foreach (DeckEntryView view in new[] { oldOwner, newOwner })
        {
            if (WriteDeckFile(view)) view.manifest.cardCount = view.deckData.cards.Count;
        }
        foreach (DeckEntryView view in referencing)
        {
            for (int i = 0; i < view.deckData.cardRefs.Count; i++)
            {
                if (view.deckData.cardRefs[i] == oldId) view.deckData.cardRefs[i] = newId;
            }
            WriteDeckFile(view);
        }

        editedCardKey = null;
        SaveCardsManifest();
        AssetDatabase.Refresh();
        CardCatalog.Invalidate();
        RefreshPreservingSelection(newId, target.name);
        Debug.Log($"{nameof(DeckManagerWindow)}: retyped '{target.name}' as {newType} and moved it to '{newOwner.manifest.deckId}'.");
    }

    private void SaveSituations(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out CardData target)) return;
        target.situation = editedSituation == CardSituationEnum.None ? string.Empty : editedSituation.ToString();
        target.situation2 = editedSituation2 == CardSituationEnum.None ? string.Empty : editedSituation2.ToString();
        CommitDeck(deckView, $"situations for '{target.name}'");
    }

    private void SaveRawJson(CardData card)
    {
        if (!TryGetSaveTarget(card, out DeckEntryView deckView, out _)) return;
        if (string.IsNullOrWhiteSpace(editedRawJson)) return;

        CardData parsed;
        try
        {
            parsed = JsonUtility.FromJson<CardData>(editedRawJson);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Invalid JSON", $"Could not parse card JSON:\n{ex.Message}", "OK");
            return;
        }

        if (parsed == null)
        {
            EditorUtility.DisplayDialog("Invalid JSON", "Could not parse card JSON.", "OK");
            return;
        }

        // Identity is not editable here. The id is what every reference deck points at, and it and
        // the owning deck both follow from the card's type — use the Type dropdown, which moves the
        // card and repoints its references, rather than retyping it in the JSON.
        if (parsed.GetCardType() != card.GetCardType())
        {
            EditorUtility.DisplayDialog(
                "Type change ignored",
                $"'{card.name}' stays a {card.GetCardType()} card. Changing type moves the card to another meta deck and renumbers it, so use the Type dropdown under Card Data.",
                "OK");
            parsed.type = card.type;
        }
        parsed.cardId = card.cardId;
        parsed.deckId = card.deckId;
        parsed.alignment = card.alignment;
        parsed.deckSpriteName = card.deckSpriteName;

        int index = deckView.deckData.cards.FindIndex(c => c != null && c.cardId == card.cardId);
        if (index < 0)
        {
            Debug.LogWarning($"{nameof(DeckManagerWindow)}: could not find card '{card.name}' in deck to apply raw JSON.");
            return;
        }
        deckView.deckData.cards[index] = parsed;

        editedCardKey = null;
        editedRawJson = string.Empty;
        CommitDeck(deckView, $"raw JSON for '{parsed.name}'");
    }

    private void SaveDeckFields(DeckEntryView view)
    {
        if (view?.manifest == null || view.deckData == null) return;

        string oldId = view.manifest.deckId ?? string.Empty;
        string newId = editedDeckId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newId))
        {
            EditorUtility.DisplayDialog("Deck Id required", "A deck needs an id — it is what the reference decks point at.", "OK");
            return;
        }

        bool idChanged = !string.Equals(oldId, newId, StringComparison.Ordinal);

        // A meta deck is found by name from its card type, so its id is not free-form. Renaming one
        // would leave new cards of that type with nowhere to go.
        if (idChanged && view.IsMeta)
        {
            CardTypeEnum ownedType = view.deckData.cards?.FirstOrDefault(c => c != null)?.GetCardType() ?? CardTypeEnum.Unknown;
            string requiredId = CardCatalog.MetaDeckIdFor(ownedType);
            if (!string.IsNullOrEmpty(requiredId) && !string.Equals(requiredId, newId, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Meta deck id is fixed",
                    $"A meta deck's id follows its card type, so the deck holding {ownedType} cards has to stay '{requiredId}'.",
                    "OK");
                return;
            }
        }

        if (idChanged && deckViews.Any(v => v != view && string.Equals(v.manifest?.deckId, newId, StringComparison.OrdinalIgnoreCase)))
        {
            EditorUtility.DisplayDialog("Deck Id in use", $"Another deck already uses the id '{newId}'.", "OK");
            return;
        }

        view.manifest.deckId = newId;
        view.manifest.nation = editedDeckNation?.Trim() ?? string.Empty;
        view.manifest.thematic = editedDeckThematic ?? string.Empty;
        view.manifest.deckSpriteName = editedDeckSprite?.Trim() ?? string.Empty;
        view.manifest.alignment = editedDeckAlignment;
        view.manifest.sharedToAll = editedDeckSharedToAll;
        view.manifest.excluded = editedDeckExcluded;

        view.deckData.deckId = newId;
        view.deckData.nation = view.manifest.nation;
        view.deckData.alignment = view.manifest.alignment;
        view.deckData.avatarCharacter = editedDeckAvatar ?? string.Empty;

        // A meta deck's cards carry its own stamp, so they have to move with it or they go stale on
        // disk. A reference deck owns no cards; the loader stamps its identity onto resolved copies
        // at load time, so there is nothing here to rewrite.
        foreach (CardData c in view.deckData.cards ?? new List<CardData>())
        {
            if (c == null) continue;
            c.deckId = newId;
            c.alignment = view.manifest.alignment;
            c.deckSpriteName = view.manifest.deckSpriteName;
        }

        editedDeckKey = null;
        editedCardKey = null;
        WriteDeckFile(view);
        SaveCardsManifest();
        AssetDatabase.Refresh();
        CardCatalog.Invalidate();
        RefreshData();
        SelectDeckByResourcePath(view.manifest.resourcePath);
        Debug.Log($"{nameof(DeckManagerWindow)}: saved deck '{newId}'.");
    }

    // A card edit always lands on the meta deck that owns it, whichever deck you were browsing when
    // you opened the card — that is what makes one edit reach every deck referencing it. The card
    // pane hands out the owner's own instances, so the target is the card itself.
    private bool TryGetSaveTarget(CardData card, out DeckEntryView deckView, out CardData target)
    {
        deckView = GetOwnerDeck(card);
        target = card;
        if (card == null) return false;

        if (deckView?.deckData?.cards == null)
        {
            Debug.LogWarning($"{nameof(DeckManagerWindow)}: no meta deck owns card id {card.cardId} ('{card.name}'), so there is nowhere to save it.");
            return false;
        }
        return true;
    }

    // One write path for every section Save, so the file, the manifest card count, the runtime
    // catalog cache and the pane selection all stay in step. deckView here is always the meta deck
    // that owns the edited card, never the deck you happened to be browsing.
    private void CommitDeck(DeckEntryView deckView, string what)
    {
        if (!WriteDeckFile(deckView)) return;

        if (deckView.manifest != null && deckView.deckData?.cards != null)
        {
            deckView.manifest.cardCount = deckView.deckData.cards.Count;
        }

        AssetDatabase.Refresh();
        CardCatalog.Invalidate();
        ReloadSelectedCard();
        EditorUtility.SetDirty(this);
        Debug.Log($"{nameof(DeckManagerWindow)}: saved {what}.");
    }

    private static bool WriteDeckFile(DeckEntryView deckView)
    {
        if (deckView?.manifest == null || deckView.deckData == null) return false;

        string assetPath = GetDeckAssetPath(deckView.manifest.resourcePath);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            Debug.LogWarning($"{nameof(DeckManagerWindow)}: could not resolve a file path for resourcePath '{deckView.manifest.resourcePath}'.");
            return false;
        }

        File.WriteAllText(assetPath, JsonUtility.ToJson(deckView.deckData, true));
        AssetDatabase.ImportAsset(ToAssetPath(assetPath), ImportAssetOptions.ForceUpdate);
        return true;
    }

    private void SaveCardsManifest()
    {
        if (cardsManifest == null) return;

        cardsManifest.deckCount = cardsManifest.decks?.Count ?? 0;
        string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Cards.json"));
        File.WriteAllText(manifestPath, JsonUtility.ToJson(cardsManifest, true));
        AssetDatabase.ImportAsset(ToAssetPath(manifestPath), ImportAssetOptions.ForceUpdate);
    }

    // --- Ownership -----------------------------------------------------------------------------
    //
    // Card data lives in exactly one place: the meta deck for its type. Every other deck is a list
    // of card ids. So "which file do I write when this card changes?" is always "its meta deck",
    // whichever deck you happened to be browsing.

    private DeckEntryView GetOwnerDeck(CardData card) =>
        card != null && ownerByCardId.TryGetValue(card.cardId, out DeckEntryView owner) ? owner : null;

    private DeckEntryView GetMetaDeck(CardTypeEnum cardType)
    {
        string metaId = CardCatalog.MetaDeckIdFor(cardType);
        return string.IsNullOrEmpty(metaId)
            ? null
            : deckViews.FirstOrDefault(v => v != null && v.IsMeta && string.Equals(v.manifest?.deckId, metaId, StringComparison.OrdinalIgnoreCase));
    }

    private int GetReferenceCount(CardData card) =>
        card != null && referenceCountByCardId.TryGetValue(card.cardId, out int count) ? count : 0;

    private IEnumerable<DeckEntryView> DecksReferencing(int cardId) =>
        deckViews.Where(v => v != null && !v.IsMeta && v.deckData?.cardRefs != null && v.deckData.cardRefs.Contains(cardId));

    // --- Card lifecycle ----------------------------------------------------------------------------

    // A new card is born in the meta deck for its type. If you were browsing a reference deck it
    // also gains a reference there, so it appears where you asked for it.
    private void AddCard(DeckEntryView deckView)
    {
        const CardTypeEnum newCardType = CardTypeEnum.Action;
        DeckEntryView metaView = GetMetaDeck(newCardType);
        if (metaView?.deckData == null)
        {
            EditorUtility.DisplayDialog(
                "No meta deck",
                $"There is no '{CardCatalog.MetaDeckIdFor(newCardType)}' deck to hold the new card. Add one to Cards.json first.",
                "OK");
            return;
        }

        metaView.deckData.cards ??= new List<CardData>();
        CardData card = new()
        {
            cardId = GetNextCardId(newCardType),
            name = "New Card",
            type = newCardType.ToString(),
            deckId = metaView.deckData.deckId,
            alignment = metaView.deckData.alignment,
            deckSpriteName = metaView.manifest?.deckSpriteName ?? string.Empty,
        };
        metaView.deckData.cards.Add(card);
        if (!WriteDeckFile(metaView))
        {
            RefreshData();
            return;
        }
        metaView.manifest.cardCount = metaView.deckData.cards.Count;

        if (deckView != null && !deckView.IsMeta && deckView.deckData != null)
        {
            deckView.deckData.cardRefs ??= new List<int>();
            deckView.deckData.cardRefs.Add(card.cardId);
            if (WriteDeckFile(deckView)) deckView.manifest.cardCount = deckView.deckData.cardRefs.Count;
        }

        SaveCardsManifest();
        AssetDatabase.Refresh();
        CardCatalog.Invalidate();

        editedCardKey = null;
        RefreshData();
        selectedCardIndex = Mathf.Max(0, FindCardIndexInFilteredCards(card.cardId, card.name));
        Repaint();
    }

    // From a reference deck this drops the reference and leaves the card alone. From a meta deck it
    // deletes the card outright, which means pulling the reference out of every deck that had one.
    private void RemoveCard(CardData card)
    {
        DeckEntryView deckView = GetSelectedDeckView();
        if (card == null || deckView?.deckData == null) return;

        if (!deckView.IsMeta)
        {
            RemoveReference(card, deckView);
            return;
        }

        List<DeckEntryView> referencing = DecksReferencing(card.cardId).ToList();
        string alsoFrom = referencing.Count == 0
            ? "No other deck references it."
            : $"It will also be removed from {referencing.Count} deck(s): {string.Join(", ", referencing.Select(v => v.manifest.deckId))}.";
        if (!EditorUtility.DisplayDialog(
                "Delete Card",
                $"Delete '{card.name}' from {deckView.manifest.deckId}?\n\n{alsoFrom}\n\nThis cannot be undone.",
                "Delete", "Cancel"))
        {
            return;
        }

        if (deckView.deckData.cards.RemoveAll(c => c != null && c.cardId == card.cardId) <= 0)
        {
            Debug.LogWarning($"{nameof(DeckManagerWindow)}: could not find card '{card.name}' to delete from '{deckView.manifest.deckId}'.");
            return;
        }
        if (!WriteDeckFile(deckView))
        {
            RefreshData();
            return;
        }
        deckView.manifest.cardCount = deckView.deckData.cards.Count;

        foreach (DeckEntryView other in referencing)
        {
            other.deckData.cardRefs.RemoveAll(id => id == card.cardId);
            if (WriteDeckFile(other)) other.manifest.cardCount = other.deckData.cardRefs.Count;
        }

        editedCardKey = null;
        SaveCardsManifest();
        AssetDatabase.Refresh();
        CardCatalog.Invalidate();
        RefreshData();
    }

    private void RemoveReference(CardData card, DeckEntryView deckView)
    {
        if (deckView?.deckData?.cardRefs == null) return;

        DeckEntryView owner = GetOwnerDeck(card);
        string ownerId = owner?.manifest?.deckId ?? "its meta deck";
        if (!EditorUtility.DisplayDialog(
                "Remove Reference",
                $"Remove '{card.name}' from {deckView.manifest.deckId}?\n\nThe card itself stays in {ownerId} and in every other deck that references it.",
                "Remove", "Cancel"))
        {
            return;
        }

        // Only the first reference goes: a deck may legitimately list the same card twice.
        int index = deckView.deckData.cardRefs.IndexOf(card.cardId);
        if (index < 0)
        {
            Debug.LogWarning($"{nameof(DeckManagerWindow)}: '{deckView.manifest.deckId}' has no reference to card id {card.cardId} to remove.");
            return;
        }
        deckView.deckData.cardRefs.RemoveAt(index);

        editedCardKey = null;
        if (WriteDeckFile(deckView))
        {
            deckView.manifest.cardCount = deckView.deckData.cardRefs.Count;
            SaveCardsManifest();
            AssetDatabase.Refresh();
            CardCatalog.Invalidate();
            RefreshData();
        }
    }

    // Nothing is copied any more — the target deck gains a reference to the one card, which is the
    // whole point of the meta decks. "Move" additionally drops the reference from the source, and is
    // only offered when the source is a reference deck; a meta deck cannot give its card away.
    private void TransferCard(CardData sourceCard, DeckEntryView sourceDeckView, DeckEntryView targetDeckView, bool move)
    {
        if (sourceCard == null || targetDeckView?.deckData == null || targetDeckView.IsMeta) return;

        string targetDeckId = targetDeckView.manifest?.deckId?.Trim() ?? string.Empty;
        targetDeckView.deckData.cardRefs ??= new List<int>();
        if (targetDeckView.deckData.cardRefs.Contains(sourceCard.cardId)
            && !EditorUtility.DisplayDialog(
                "Already referenced",
                $"{targetDeckId} already references '{sourceCard.name}'. Add a second reference to it?",
                "Add anyway", "Cancel"))
        {
            return;
        }

        move &= sourceDeckView != null && !sourceDeckView.IsMeta && sourceDeckView.deckData?.cardRefs != null;
        if (move && !EditorUtility.DisplayDialog(
                "Move Reference",
                $"Move '{sourceCard.name}' to {targetDeckId}? {sourceDeckView.manifest.deckId} will stop referencing it; the card itself is untouched.",
                "Move", "Cancel"))
        {
            return;
        }

        targetDeckView.deckData.cardRefs.Add(sourceCard.cardId);
        if (!WriteDeckFile(targetDeckView))
        {
            RefreshData();
            return;
        }
        targetDeckView.manifest.cardCount = targetDeckView.deckData.cardRefs.Count;

        if (move)
        {
            int sourceIndex = sourceDeckView.deckData.cardRefs.IndexOf(sourceCard.cardId);
            if (sourceIndex < 0)
            {
                // The reference is already on disk in the target, so take it back rather than
                // leaving the card listed in both decks.
                Debug.LogWarning($"{nameof(DeckManagerWindow)}: '{sourceDeckView.manifest.deckId}' has no reference to '{sourceCard.name}'; rolling back.");
                targetDeckView.deckData.cardRefs.RemoveAt(targetDeckView.deckData.cardRefs.Count - 1);
                targetDeckView.manifest.cardCount = targetDeckView.deckData.cardRefs.Count;
                WriteDeckFile(targetDeckView);
                RefreshData();
                return;
            }

            sourceDeckView.deckData.cardRefs.RemoveAt(sourceIndex);
            sourceDeckView.manifest.cardCount = sourceDeckView.deckData.cardRefs.Count;
            if (!WriteDeckFile(sourceDeckView))
            {
                Debug.LogWarning($"{nameof(DeckManagerWindow)}: could not save the source deck; rolling back the move.");
                sourceDeckView.deckData.cardRefs.Insert(sourceIndex, sourceCard.cardId);
                sourceDeckView.manifest.cardCount = sourceDeckView.deckData.cardRefs.Count;
                targetDeckView.deckData.cardRefs.RemoveAt(targetDeckView.deckData.cardRefs.Count - 1);
                targetDeckView.manifest.cardCount = targetDeckView.deckData.cardRefs.Count;
                WriteDeckFile(targetDeckView);
                RefreshData();
                return;
            }
        }

        SaveCardsManifest();
        AssetDatabase.Refresh();
        CardCatalog.Invalidate();

        copyTargetResourcePath = targetDeckView.manifest.resourcePath;
        editedCardKey = null;
        RefreshData();
        SelectDeckByResourcePath(targetDeckView.manifest.resourcePath);
        selectedCardIndex = Mathf.Max(0, FindCardIndexInFilteredCards(sourceCard.cardId, sourceCard.name));
        Repaint();

        Debug.Log($"{nameof(DeckManagerWindow)}: {(move ? "moved" : "added")} a reference to '{sourceCard.name}' in '{targetDeckId}'.");
    }

    // Ids are unique across every deck and blocked by card type, so allocation has to look at the
    // whole catalog rather than at one deck's list.
    private int GetNextCardId(CardTypeEnum cardType)
    {
        int block = CardCatalog.CardIdBlockStart(cardType);
        int highest = block;
        foreach (int cardId in ownerByCardId.Keys)
        {
            if (cardId >= block && cardId < block + CardCatalog.CardIdBlockSize && cardId > highest) highest = cardId;
        }
        return highest + 1;
    }

    // --- Data loading --------------------------------------------------------------------------------

    private void RefreshData()
    {
        deckViews.Clear();
        filteredCards.Clear();
        selectedCardIndex = 0;

        TextAsset manifestAsset = Resources.Load<TextAsset>(ManifestResourceName);
        if (manifestAsset == null)
        {
            cardsManifest = null;
            Repaint();
            return;
        }

        cardsManifest = JsonUtility.FromJson<CardsManifest>(manifestAsset.text);
        if (cardsManifest?.decks == null)
        {
            Repaint();
            return;
        }

        ownerByCardId.Clear();
        referenceCountByCardId.Clear();

        foreach (DeckManifestEntry entry in cardsManifest.decks.Where(x => x != null))
        {
            DeckEntryView view = new() { manifest = entry, deckData = LoadDeckData(entry.resourcePath) };
            deckViews.Add(view);
        }

        // Meta decks first: they own the card objects everything else points at, so the pool has to
        // exist before any reference can be resolved.
        foreach (DeckEntryView view in deckViews.Where(v => v != null && v.IsMeta && v.deckData?.cards != null))
        {
            foreach (CardData card in view.deckData.cards.Where(c => c != null))
            {
                if (ownerByCardId.ContainsKey(card.cardId))
                {
                    Debug.LogWarning($"{nameof(DeckManagerWindow)}: card id {card.cardId} ('{card.name}') is claimed by more than one meta deck; keeping the first.");
                    continue;
                }
                ownerByCardId[card.cardId] = view;
                view.cards.Add(card);
            }
            view.manifest.cardCount = view.cards.Count;
        }

        foreach (DeckEntryView view in deckViews.Where(v => v != null && !v.IsMeta && v.deckData?.cardRefs != null))
        {
            foreach (int cardId in view.deckData.cardRefs)
            {
                if (!ownerByCardId.TryGetValue(cardId, out DeckEntryView owner))
                {
                    Debug.LogWarning($"{nameof(DeckManagerWindow)}: deck '{view.manifest.deckId}' references card id {cardId}, which no meta deck owns.");
                    continue;
                }
                view.cards.Add(owner.deckData.cards.First(c => c != null && c.cardId == cardId));
                referenceCountByCardId.TryGetValue(cardId, out int count);
                referenceCountByCardId[cardId] = count + 1;
            }
            view.manifest.cardCount = view.deckData.cardRefs.Count;
        }

        selectedDeckIndex = deckViews.Count > 0 ? Mathf.Clamp(selectedDeckIndex, 0, deckViews.Count - 1) : 0;
        editedDeckKey = null;
        RebuildFilteredCards();
        Repaint();
    }

    // Editing one card touches its meta deck, and every reference deck shows that same instance, so
    // there is no cheap partial reload to do — everything is re-read and the selection restored.
    private void ReloadSelectedCard()
    {
        CardData selectedCard = GetSelectedCard();
        if (selectedCard == null) return;
        RefreshPreservingSelection(selectedCard.cardId, selectedCard.name);
    }

    private void RefreshPreservingSelection(int cardId, string cardName)
    {
        string deckResourcePath = GetSelectedDeckView()?.manifest?.resourcePath;

        RefreshData();

        if (!string.IsNullOrWhiteSpace(deckResourcePath)) SelectDeckByResourcePath(deckResourcePath);
        int matchIndex = FindCardIndexInFilteredCards(cardId, cardName);
        if (matchIndex >= 0) selectedCardIndex = matchIndex;
        Repaint();
    }

    // Reads the file rather than the imported TextAsset, so a save made moments ago is visible even
    // before Unity finishes reimporting it.
    private static DeckData LoadDeckData(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return null;
        try
        {
            string assetPath = GetDeckAssetPath(resourcePath);
            if (!string.IsNullOrWhiteSpace(assetPath) && File.Exists(assetPath))
            {
                return JsonUtility.FromJson<DeckData>(File.ReadAllText(assetPath));
            }

            TextAsset deckAsset = Resources.Load<TextAsset>(resourcePath);
            return deckAsset != null ? JsonUtility.FromJson<DeckData>(deckAsset.text) : null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{nameof(DeckManagerWindow)}: failed to load deck '{resourcePath}': {ex.Message}");
            return null;
        }
    }

    private static string GetDeckAssetPath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return string.Empty;
        string normalized = resourcePath.Trim().Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", $"{normalized}.json"));
    }

    private static string ToAssetPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;
        string dataPath = Path.GetFullPath(Application.dataPath);
        string normalized = Path.GetFullPath(fullPath);
        if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return string.Empty;

        string relative = normalized.Substring(dataPath.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return $"Assets/{relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/')}";
    }

    private void RebuildFilteredCards()
    {
        filteredCards.Clear();

        DeckEntryView deckView = GetSelectedDeckView();
        if (deckView == null) return;

        IEnumerable<CardData> cards = deckView.cards.Where(c => c != null);
        if (onlyShowCardsWithActions) cards = cards.Where(c => !string.IsNullOrWhiteSpace(c.GetActionRef()));

        string query = searchText?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(query)) cards = cards.Where(card => MatchesSearch(card, query));

        if (sortCardsByTypeThenName)
        {
            cards = cards.OrderBy(c => c.GetCardType().ToString()).ThenBy(c => c.name, StringComparer.OrdinalIgnoreCase);
        }

        filteredCards.AddRange(cards);
        selectedCardIndex = filteredCards.Count == 0 ? 0 : Mathf.Clamp(selectedCardIndex, 0, filteredCards.Count - 1);
        Repaint();
    }

    private static bool MatchesSearch(CardData card, string query)
    {
        if (card == null || string.IsNullOrWhiteSpace(query)) return true;

        const StringComparison cmp = StringComparison.OrdinalIgnoreCase;
        return (card.name != null && card.name.Contains(query, cmp))
            || (card.quote != null && card.quote.Contains(query, cmp))
            || (card.actionEffect != null && card.actionEffect.Contains(query, cmp))
            || (card.description != null && card.description.Contains(query, cmp))
            || (card.requirementsText != null && card.requirementsText.Contains(query, cmp))
            || (card.action != null && card.action.Contains(query, cmp))
            || (card.actionClassName != null && card.actionClassName.Contains(query, cmp))
            || (card.deckId != null && card.deckId.Contains(query, cmp))
            || (card.region != null && card.region.Contains(query, cmp))
            || (card.tags != null && card.tags.Any(tag => tag != null && tag.Contains(query, cmp)));
    }

    private DeckEntryView GetSelectedDeckView()
    {
        if (deckViews.Count == 0) return null;
        selectedDeckIndex = Mathf.Clamp(selectedDeckIndex, 0, deckViews.Count - 1);
        return deckViews[selectedDeckIndex];
    }

    private CardData GetSelectedCard()
    {
        if (filteredCards.Count == 0) return null;
        selectedCardIndex = Mathf.Clamp(selectedCardIndex, 0, filteredCards.Count - 1);
        return filteredCards[selectedCardIndex];
    }

    private int FindCardIndexInFilteredCards(int cardId, string cardName)
    {
        for (int i = 0; i < filteredCards.Count; i++)
        {
            CardData candidate = filteredCards[i];
            if (candidate == null) continue;
            if (cardId > 0 && candidate.cardId == cardId) return i;
            if (!string.IsNullOrWhiteSpace(cardName) && string.Equals(candidate.name, cardName, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private void SelectDeckByResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return;

        for (int i = 0; i < deckViews.Count; i++)
        {
            if (!string.Equals(deckViews[i]?.manifest?.resourcePath, resourcePath, StringComparison.OrdinalIgnoreCase)) continue;
            selectedDeckIndex = i;
            selectedCardIndex = 0;
            editedDeckKey = null;
            RebuildFilteredCards();
            return;
        }
    }

    // Meta decks are not targets: which one holds a card is decided by its type, not by hand.
    private List<DeckEntryView> GetAllTransferTargets(DeckEntryView currentDeck)
    {
        return deckViews
            .Where(view => view?.manifest != null
                && view.deckData != null
                && !view.IsMeta
                && !string.IsNullOrWhiteSpace(view.manifest.resourcePath)
                && (currentDeck == null || !string.Equals(view.manifest.resourcePath, currentDeck.manifest.resourcePath, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private int GetCopyTargetIndex(List<DeckEntryView> targets)
    {
        if (targets == null || targets.Count == 0) return 0;

        if (!string.IsNullOrWhiteSpace(copyTargetResourcePath))
        {
            int matchIndex = targets.FindIndex(view =>
                string.Equals(view?.manifest?.resourcePath, copyTargetResourcePath, StringComparison.OrdinalIgnoreCase));
            if (matchIndex >= 0) return matchIndex;
        }

        copyTargetResourcePath = targets[0].manifest.resourcePath;
        return 0;
    }

    // A Character's startingPC names a PC card. The character itself is shared by every deck that
    // references it, so the choice cannot be narrowed to one deck's PCs any more — the whole PC meta
    // deck is the candidate list.
    private List<string> GetAvailablePcNames()
    {
        List<string> names = new() { string.Empty };
        CollectPcNames(GetMetaDeck(CardTypeEnum.PC), names);

        // The card's own value may name a PC that has since been deleted; keep it in the list so
        // opening the card does not silently retarget it.
        if (!string.IsNullOrWhiteSpace(editedStartingPC) && !names.Contains(editedStartingPC))
        {
            names.Add(editedStartingPC);
        }
        return names;
    }

    private static void CollectPcNames(DeckEntryView deckView, List<string> names)
    {
        if (deckView?.deckData?.cards == null) return;
        foreach (CardData c in deckView.deckData.cards)
        {
            if (c?.GetCardType() == CardTypeEnum.PC && !string.IsNullOrWhiteSpace(c.name) && !names.Contains(c.name))
            {
                names.Add(c.name);
            }
        }
    }

    // Only the meta decks own cards, so this is the count of distinct cards in the game rather than
    // the number of deck slots they fill.
    private int CountAllCards() => deckViews.Where(v => v != null && v.IsMeta).Sum(v => v.deckData?.cards?.Count ?? 0);

    // --- Live preview ----------------------------------------------------------------------------------

    // Renders the real Card.prefab through Card.InitializePreview — the same entry point the
    // in-game centered preview uses, which fills every field in without starting the one-shot
    // typewriter coroutine (coroutines do not run in edit mode, so a typewritten description would
    // simply never appear).
    private void UpdateLivePreview(CardData card, Rect rect)
    {
        if (card == null)
        {
            EditorGUI.HelpBox(rect, "No card selected.", MessageType.Info);
            return;
        }

        EnsureArtSourceInstalled();
        EnsurePreviewObjects(ResolvePreviewTextureSize(rect));
        if (previewCardComponent == null || previewCamera == null || previewRenderTexture == null)
        {
            EditorGUI.HelpBox(rect, $"Card preview could not be created. Expected a prefab at {CardPrefabPath}.", MessageType.Warning);
            return;
        }

        previewCardObject.SetActive(true);
        previewCanvasRoot.SetActive(true);

        // Preview a clone: Initialize writes presentation state onto the CardData it is handed, and
        // that instance is the one the deck file will be written from.
        CardData previewData = card.Clone();
        previewData.hasShownHandAnimation = true;
        if (string.IsNullOrWhiteSpace(previewData.deckSpriteName))
        {
            previewData.deckSpriteName = GetSelectedDeckView()?.manifest?.deckSpriteName ?? string.Empty;
        }

        previewCardComponent.SuppressHoverEffects = true;
        previewCardComponent.ShowCloseIcon = false;
        previewCardComponent.InitializePreview(previewData);

        // TMP computes its mesh lazily; without forcing it here the ContentSizeFitters read stale
        // preferred heights and pivot-Y=0 containers collapse or overflow in the editor.
        foreach (TextMeshProUGUI tmp in previewCardObject.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            tmp.ForceMeshUpdate();
        }

        Canvas.ForceUpdateCanvases();
        RectTransform cardRect = previewCardObject.GetComponent<RectTransform>();
        if (cardRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);

        previewCamera.aspect = PreviewCanvasW / PreviewCanvasH;
        previewCamera.targetTexture = previewRenderTexture;
        previewCamera.Render();
        previewCamera.targetTexture = null;

        GUI.DrawTexture(rect, previewRenderTexture, ScaleMode.ScaleToFit, false);
    }

    // CardServices.Art is normally installed by CardServicesInstaller at scene Awake, which never
    // runs for an editor window. Without it every preview would render art-less.
    private static void EnsureArtSourceInstalled()
    {
        if (CardServices.Art != null) return;
        CardArtLibrary library = AssetDatabase.LoadAssetAtPath<CardArtLibrary>(CardArtLibraryPath);
        if (library != null) CardServices.Art = library;
    }

    private void EnsurePreviewObjects(Vector2Int textureSize)
    {
        if (cardPrefabAsset == null) cardPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (cardPrefabAsset == null) return;

        if (previewRoot == null)
        {
            previewRoot = new GameObject("DeckManagerPreviewRoot") { hideFlags = HideFlags.HideAndDontSave, layer = 5 };
            previewCamera = previewRoot.AddComponent<Camera>();
            previewCamera.hideFlags = HideFlags.HideAndDontSave;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 6f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.black;
            previewCamera.cullingMask = 1 << 5;      // UI layer only, so nothing in the open scene leaks in
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 100f;
            previewCamera.transform.position = new Vector3(0f, 0f, -10f);
            previewCamera.transform.rotation = Quaternion.identity;
        }

        if (previewCanvasRoot == null)
        {
            // A world-space canvas at a fixed card-sized rect, so the preview does not change shape
            // with the editor pane.
            previewCanvasRoot = new GameObject("DeckManagerPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster))
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = 5,
            };
            previewCanvasRoot.transform.SetParent(previewRoot.transform, false);
            previewCanvasRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            float worldScale = previewCamera.orthographicSize * 2f / PreviewCanvasH;
            RectTransform canvasRect = previewCanvasRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(PreviewCanvasW, PreviewCanvasH);
            canvasRect.localPosition = new Vector3(0f, 0f, 10f);
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * worldScale;
        }

        if (previewCardObject == null)
        {
            previewCardObject = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefabAsset);
            if (previewCardObject == null) return;

            previewCardObject.transform.SetParent(previewCanvasRoot.transform, false);
            previewCardObject.SetActive(true);

            RectTransform cardRect = previewCardObject.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.sizeDelta = new Vector2(PreviewCardW, PreviewCardH);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.localScale = Vector3.one;
            }

            SetHideFlagsRecursive(previewCardObject.transform);
            SetLayerRecursive(previewCardObject.transform, 5);
            previewCardComponent = previewCardObject.GetComponent<Card>();
        }

        ConfigurePreviewTextMeshPro();
        EnsureRenderTexture(textureSize);
    }

    // A prefab authored without an explicit font or sprite asset renders as empty boxes offscreen,
    // because nothing has pushed TMP's project defaults onto it outside of play mode.
    private void ConfigurePreviewTextMeshPro()
    {
        if (previewCardObject == null) return;

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        TMP_SpriteAsset defaultSpriteAsset = TMP_Settings.defaultSpriteAsset;

        foreach (TextMeshProUGUI tmp in previewCardObject.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font == null && defaultFont != null) tmp.font = defaultFont;
            if (tmp.spriteAsset == null && defaultSpriteAsset != null) tmp.spriteAsset = defaultSpriteAsset;
            tmp.richText = true;
        }
    }

    // Physical pixels the preview will occupy once ScaleToFit has letterboxed it into rect, times a
    // supersample factor. The scale is quantized so dragging the pane edge does not reallocate the
    // texture on every repaint, and the canvas aspect is preserved so the camera framing is unchanged.
    private static Vector2Int ResolvePreviewTextureSize(Rect rect)
    {
        float fit = Mathf.Min(rect.width / PreviewCanvasW, rect.height / PreviewCanvasH);
        if (float.IsNaN(fit) || float.IsInfinity(fit) || fit <= 0f) fit = 1f;

        float scale = fit * Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint) * PreviewSupersample;
        scale = Mathf.Ceil(Mathf.Max(1f, scale) * 4f) / 4f;
        scale = Mathf.Min(scale, PreviewMaxTextureSize / Mathf.Max(PreviewCanvasW, PreviewCanvasH));

        return new Vector2Int(
            Mathf.RoundToInt(PreviewCanvasW * scale),
            Mathf.RoundToInt(PreviewCanvasH * scale));
    }

    private void EnsureRenderTexture(Vector2Int size)
    {
        int width = Mathf.Max(1, size.x);
        int height = Mathf.Max(1, size.y);
        if (previewRenderTexture != null && previewRenderTexture.width == width && previewRenderTexture.height == height) return;

        if (previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            DestroyImmediate(previewRenderTexture);
        }

        previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false,
        };
    }

    private static void SetHideFlagsRecursive(Transform root)
    {
        if (root == null) return;
        root.gameObject.hideFlags = HideFlags.HideAndDontSave;
        for (int i = 0; i < root.childCount; i++) SetHideFlagsRecursive(root.GetChild(i));
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) SetLayerRecursive(root.GetChild(i), layer);
    }

    private void ClearPreviewInstance()
    {
        if (previewCardObject != null)
        {
            DestroyImmediate(previewCardObject);
            previewCardObject = null;
        }
        if (previewCanvasRoot != null)
        {
            DestroyImmediate(previewCanvasRoot);
            previewCanvasRoot = null;
        }
        if (previewRoot != null)
        {
            DestroyImmediate(previewRoot);
            previewRoot = null;
            previewCamera = null;
        }
        if (previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            DestroyImmediate(previewRenderTexture);
            previewRenderTexture = null;
        }

        previewCardComponent = null;
        cardPrefabAsset = null;
    }

    // --- Summaries ---------------------------------------------------------------------------------------

    private static string BuildSkillSummary(CardData data)
    {
        if (data == null) return string.Empty;
        List<string> reqs = new();
        if (data.commanderSkillRequired > 0) reqs.Add($"Commander {data.commanderSkillRequired}");
        if (data.agentSkillRequired > 0) reqs.Add($"Agent {data.agentSkillRequired}");
        if (data.emissarySkillRequired > 0) reqs.Add($"Emissary {data.emissarySkillRequired}");
        if (data.mageSkillRequired > 0) reqs.Add($"Mage {data.mageSkillRequired}");
        return reqs.Count == 0 ? "None" : string.Join(", ", reqs);
    }

    private static string BuildCostSummary(CardData data)
    {
        if (data == null) return string.Empty;
        List<string> parts = new();
        if (data.goldRequired > 0) parts.Add($"gold {data.goldRequired}");
        if (data.leatherRequired > 0) parts.Add($"leather {data.leatherRequired}");
        if (data.timberRequired > 0) parts.Add($"timber {data.timberRequired}");
        if (data.mountsRequired > 0) parts.Add($"mounts {data.mountsRequired}");
        if (data.ironRequired > 0) parts.Add($"iron {data.ironRequired}");
        if (data.steelRequired > 0) parts.Add($"steel {data.steelRequired}");
        if (data.mithrilRequired > 0) parts.Add($"mithril {data.mithrilRequired}");
        if (data.jokerRequired > 0) parts.Add($"joker {data.jokerRequired}");
        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    private static string BuildGrantSummary(CardData data)
    {
        if (data == null) return string.Empty;
        List<string> parts = new();
        if (data.leatherGranted > 0) parts.Add($"leather +{data.leatherGranted}");
        if (data.timberGranted > 0) parts.Add($"timber +{data.timberGranted}");
        if (data.mountsGranted > 0) parts.Add($"mounts +{data.mountsGranted}");
        if (data.ironGranted > 0) parts.Add($"iron +{data.ironGranted}");
        if (data.steelGranted > 0) parts.Add($"steel +{data.steelGranted}");
        if (data.mithrilGranted > 0) parts.Add($"mithril +{data.mithrilGranted}");
        if (data.goldGranted > 0) parts.Add($"gold +{data.goldGranted}");
        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    private static string BuildRawSummary(CardData card)
    {
        if (card == null) return string.Empty;

        StringBuilder sb = new();
        sb.AppendLine($"cardId: {card.cardId}");
        sb.AppendLine($"name: {card.name}");
        sb.AppendLine($"type: {card.type}");
        sb.AppendLine($"action: {card.GetActionRef()}");
        sb.AppendLine($"spriteName: {card.spriteName}");
        sb.AppendLine($"portraitName: {card.portraitName}");
        sb.AppendLine($"deckSpriteName: {card.deckSpriteName}");
        sb.AppendLine($"region: {card.region}");
        sb.AppendLine($"requirementsText: {card.requirementsText}");
        sb.AppendLine($"quote: {card.quote}");
        sb.AppendLine($"actionEffect: {card.actionEffect}");
        sb.AppendLine($"historyText: {card.historyText}");
        sb.AppendLine($"tags: {(card.tags != null ? string.Join(", ", card.tags) : string.Empty)}");
        sb.AppendLine($"specialAbilities: {FormatAbilityNames(card.specialAbilities)}");
        sb.AppendLine($"characterAbilities: {FormatAbilityNames(card.characterAbilities)}");
        return sb.ToString().TrimEnd();
    }

    private static string FormatAbilityNames<T>(List<T> abilities)
    {
        return abilities == null || abilities.Count == 0 ? "(none)" : string.Join(", ", abilities);
    }

    // Runeboard read the selected Character's skills and the Leader's resource piles straight out of
    // the scene here. This project routes all of that through ICardPlayabilitySource, so the window
    // reports whatever the installed source says — and says so plainly when nothing is installed.
    private static string BuildPlayabilityReport(CardData card)
    {
        if (card == null) return string.Empty;

        ICardPlayabilitySource source = CardServices.Playability;
        if (source == null)
        {
            return "No ICardPlayabilitySource is installed, so every card reads as playable and the "
                 + "card face shows no requirement warnings.\n"
                 + $"Printed cost: {BuildCostSummary(card)}\n"
                 + $"Printed skills: {BuildSkillSummary(card)}";
        }

        CardPlayabilityResult result;
        try
        {
            result = source.Evaluate(card.Clone());
        }
        catch (Exception ex)
        {
            return $"Playability source threw: {ex.Message}";
        }

        if (result == null) return "Playability source returned nothing.";
        if (result.isPlayable && (result.messages == null || result.messages.Count == 0)) return "Playable. No requirement errors.";
        return string.Join("\n", (result.messages ?? new List<string>()).Distinct());
    }

    private string BuildDeckLabel(DeckEntryView view)
    {
        if (view?.manifest == null) return string.Empty;

        string deckId = string.IsNullOrWhiteSpace(view.manifest.deckId) ? "(no id)" : view.manifest.deckId;
        string displayName = !view.IsMeta && !string.IsNullOrWhiteSpace(view.deckData?.avatarCharacter)
            ? view.deckData.avatarCharacter.Trim() : deckId;
        string nation = string.IsNullOrWhiteSpace(view.manifest.nation) ? "(no nation)" : view.manifest.nation;
        string kind = view.IsMeta ? "owns" : "refs";
        return $"{nation} / {displayName} ({kind} {view.manifest.cardCount})";
    }

    // --- Finalized flag ------------------------------------------------------------------------------------

    // An authoring bookmark, not card data: it marks which cards have been reviewed and is kept in
    // EditorPrefs so it never reaches the deck files or a build.
    private string GetFinalizedPreferenceKey(CardData card)
    {
        if (card == null) return null;
        string deckId = !string.IsNullOrWhiteSpace(card.deckId)
            ? card.deckId.Trim()
            : GetSelectedDeckView()?.manifest?.deckId?.Trim() ?? "unknownDeck";
        return $"DarkBeforeDawn.DeckManager.Finalized.{deckId}.{card.cardId}";
    }

    private bool IsCardFinalized(CardData card)
    {
        string key = GetFinalizedPreferenceKey(card);
        return !string.IsNullOrWhiteSpace(key) && EditorPrefs.GetBool(key, false);
    }

    private void SetCardFinalized(CardData card, bool finalized)
    {
        string key = GetFinalizedPreferenceKey(card);
        if (!string.IsNullOrWhiteSpace(key)) EditorPrefs.SetBool(key, finalized);
    }

    // --- Formatting ----------------------------------------------------------------------------------------

    private static List<string> SplitCommaList(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
    }

    // Mirrors Card.FormatCardTitle so a name reads in this window exactly as it will on the card.
    private static string FormatCardTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        List<char> chars = new(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            if (ShouldInsertWordSpace(value, i)) chars.Add(' ');
            chars.Add(value[i]);
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(new string(chars.ToArray()).Trim().ToLowerInvariant());
    }

    private static bool ShouldInsertWordSpace(string value, int index)
    {
        if (index <= 0 || index >= value.Length) return false;

        char current = value[index];
        if (!char.IsUpper(current)) return false;

        char previous = value[index - 1];
        if (char.IsWhiteSpace(previous)) return false;
        if (char.IsLower(previous) || char.IsDigit(previous)) return true;
        if (!char.IsUpper(previous)) return false;

        // "PCFounding" -> "PC Founding": break a run of capitals only before the last one.
        return index + 1 < value.Length && char.IsLower(value[index + 1]);
    }

    private static string FormatCardTypeLabel(CardTypeEnum cardType)
    {
        string label = cardType switch
        {
            CardTypeEnum.PC => "PC",
            CardTypeEnum.Land => "Land",
            CardTypeEnum.Character => "Character",
            CardTypeEnum.Army => "Army",
            CardTypeEnum.Event => "Event",
            CardTypeEnum.Action => "Action",
            CardTypeEnum.Spell => "Spell",
            CardTypeEnum.Encounter => "Encounter",
            CardTypeEnum.Environmental => "Environmental",
            CardTypeEnum.Object => "Object",
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(label)) return string.Empty;

        Color c = CardServices.Palette?.GetCardTypeColor(cardType) ?? Color.clear;
        return c.a < 0.01f ? label : $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{label}</color>";
    }

    private static GUIStyle CreateRichTextStyle(GUIStyle baseStyle) => new(baseStyle) { richText = true };
}
