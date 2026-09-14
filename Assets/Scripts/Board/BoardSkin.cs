using System;
using TMPro;
using UnityEngine;

public enum BoardZoneId { OpponentLands, OpponentSettlements, OpponentArmies, OpponentVictory, OpponentDiscard, Environment, HumanArmies, HumanLands, HumanSettlements, HumanVictory, HumanDiscard, Hand }
public enum SkinColorRole { Ink, Gold, Ivory, Muted, Teal }

/// <summary>All board presentation parameters. Duplicate Default to author another skin.</summary>
[CreateAssetMenu(fileName = "New Skin", menuName = "Cards/Board Skin")]
public sealed class BoardSkin : ScriptableObject
{
    public string displayName = "Default";
    [Tooltip("Full bleed illustration beneath the board furniture. Leave empty to show the scene camera.")]
    public Texture2D backdrop;
    [Tooltip("Open battlefield, floating cards and a fanned hand instead of framed dashboard lanes.")]
    public bool openTable;
    public Palette colors = new();
    public Typography typography = new();
    public Chrome chrome = new();
    public CardStyle cards = new();
    public TokenStyle tokens = new();
    public FaceStyle face = new();
    public PileStyle piles = new();
    public PreviewStyle preview = new();
    public PlayerStyle players = new();
    public ZoneStyle[] zones = DefaultZones();
    [NonSerialized] private int revision;
    public int Revision => revision;
    public static BoardSkin Default => Resources.Load<BoardSkin>("Skins/Default");
    private void OnValidate() { revision++; }

    /// <summary>
    /// The card-type palette this skin wants installed, or null to leave whatever is already in
    /// CardServices alone. Assigning one here makes the skin the single authority: the card face
    /// (background tint, token ring, type label) and the board chrome (card border, stat plaque)
    /// then read the same asset instead of drifting apart.
    /// </summary>
    public CardPalette CardTypePalette => colors.cardTypes;
    public Color ColorFor(SkinColorRole role) => role switch {
        SkinColorRole.Ink => colors.ink, SkinColorRole.Gold => colors.gold,
        SkinColorRole.Ivory => colors.ivory, SkinColorRole.Teal => colors.teal, _ => colors.muted
    };

    [Serializable] public class Palette
    {
        public Color ink = new(.035f,.052f,.060f,.96f), gold = new(.70f,.56f,.33f), ivory = new(.93f,.89f,.78f), muted = new(.57f,.63f,.62f), teal = new(.39f,.71f,.66f);
        public Color atmosphere = new(.018f,.033f,.039f,.80f), zoneSurface = new(.035f,.055f,.06f,.96f);
        public Color tokenBorder = new(.7f,.56f,.33f,.6f), stackBorder = new(.34f,.31f,.23f);
        public Color button = new(.14f,.18f,.18f), disabledButton = new(.3f,.3f,.3f);
        public Color cardShadow = new(0,0,0,.6f), previewShadow = new(0,0,0,.65f), emptyPile = new(1,1,1,.035f);
        [Tooltip("Materials for the combat/resource numerals on tokens, by side. Full cards keep the " +
                 "material authored on the card prefab.")]
        public Material ownTokenStats, opponentTokenStats;
        [Tooltip("Card-type palette. Assign one to make this skin the single authority for type " +
                 "colour -- it is installed into CardServices, so the card face and the board " +
                 "chrome read the same asset. Left empty, whatever CardServices already holds wins.")]
        public CardPalette cardTypes;
        [Tooltip("Cost/resource row on the full card. Plain white by default: the row is numerals " +
                 "beside coloured resource sprites, and a warm tint on it fights them.")]
        public Color requirements = Color.white;
    }
    [Serializable] public class PlayerStyle
    {
        public Bounds opponentMaterials = new(.008f,.477f,.145f,.632f);
        public Bounds humanMaterials = new(.008f,.31f,.145f,.465f);
        public Bounds opponentAvatar = new(.008f,.64f,.145f,.93f);
        public Bounds humanAvatar = new(.008f,.012f,.145f,.302f);
        public int headingSize = 12, amountSize = 22, materialSize = 11;
    }
    [Serializable] public class Typography
    {
        public Font interfaceFont, mastheadFont;
        public TMP_FontAsset readingFont;
        [Tooltip("Resources path loaded when readingFont is empty. Only reached by a skin that " +
                 "leaves the font unassigned.")]
        public string readingFontFallback = "Fonts & Materials/LiberationSans SDF";
        [Min(1)] public int zoneHeadingSize = 15, countSize = 12, previewLabelSize = 12, buttonSize = 19;
        public Vector2 titleFontRange = new(14,21), descriptionFontRange = new(9,14);
        public FontStyles titleStyle = FontStyles.Bold;
        [Tooltip("Cost/resource row on the full card. The glyphs are inline sprites, so they scale " +
                 "with this and nothing else -- an inline <size> would enlarge the sprite alone.")]
        [Min(1)] public float requirementsSize = 18;
    }
    [Serializable] public struct Bounds
    {
        public Vector2 min, max;
        public Bounds(float x0,float y0,float x1,float y1) { min=new(x0,y0); max=new(x1,y1); }
    }
    [Serializable] public class LabelStyle
    {
        public string text;
        [Min(1)] public int size;
        public SkinColorRole color;
        public Bounds bounds;
        public TextAnchor alignment;
        public bool mastheadFont;
        public LabelStyle(string text,int size,SkinColorRole color,Bounds bounds,TextAnchor alignment=TextAnchor.MiddleLeft,bool masthead=false)
        { this.text=text; this.size=size; this.color=color; this.bounds=bounds; this.alignment=alignment; mastheadFont=masthead; }
    }
    [Serializable] public class Chrome
    {
        [Min(.1f)] public float lineWidth = 2;
        public Bounds header = new(0,.944f,1,1);
        public LabelStyle[] headerLabels = {
            new("DARK BEFORE DAWN",24,SkinColorRole.Ivory,new(.02f,0,.44f,1),masthead:true),
            new("THE GOLDEN VALE",13,SkinColorRole.Gold,new(.44f,0,.65f,1),TextAnchor.MiddleCenter),
            new("HOVER TO INSPECT  /  CLICK TO PIN",12,SkinColorRole.Muted,new(.66f,0,.98f,1),TextAnchor.MiddleRight)
        };
        public LabelStyle[] footerLabels = Array.Empty<LabelStyle>();
        [Range(0,1)] public float zoneBorderOpacity=.23f, zoneInlayOpacity=.65f, zoneInlayFraction=.12f;
        public Vector2 headingInset = new(14,2);
        [Min(1)] public float headingHeight=26, countWidth=80;
        public Vector2 contentInsetMin=new(14,10), contentInsetMax=new(14,30);
        [Min(0)] public float zoneGap=12, handGap=18;
    }
    [Serializable] public class Piece
    {
        public Vector2 size, position;
        public Piece(Vector2 size,Vector2 position) { this.size=size; this.position=position; }
    }
    [Serializable] public class CardStyle
    {
        public Vector2 size = new(300,410);
        public Piece art = new(new(280,232),new(0,38)), title = new(new(280,38),new(0,178)), description = new(new(276,116),new(0,-137)), badge = new(new(22,22),new(130,180));
        [Tooltip("Combat plaque on the artwork's lower-right corner. Keep it inside the art: it is drawn " +
                 "over whatever it overlaps, and the description below has no room to give.")]
        public Piece stats = new(new(88,32),new(92,-58));
        public Vector2 statsFontRange = new(13,20);
        public Vector4 statsMargin = new(7,3,7,3);
        public Vector2 shadowOffset = new(3,-5), titleInsetMin = new(3,0), titleInsetMax = new(20,0);
        public Vector4 descriptionMargin = new(4,5,4,5), requirementMargin = new(6,4,6,4);
        public Vector2 requirementHeightRange = new(24,96);
        [Range(0,1)] public float typeBorderBlend=.55f;
        [Tooltip("Keep the border authored on the card prefab instead of the generated one. Off by " +
                 "default: the prefab's border is a decorative 1.3-scaled bleed sized for the " +
                 "authored layout, not for this skin's card size.")]
        public bool keepAuthoredBorder;
        [Tooltip("Keep the prefab's Hover components live on a board card. Off by default: a board " +
                 "card is inspected through the preview, and the prefab's own hover fights it.")]
        public bool keepHoverEffects;
        [Tooltip("Keep RealCard/Image/DisabledImage. Off by default -- it is authored active and " +
                 "opaque black over the art, and nothing in Card ever toggles it.")]
        public bool keepDisabledOverlay;
    }
    [Serializable] public class TokenStyle
    {
        [Tooltip("Token footprint in pixels. Leave at zero to use the prefab's own border ring, " +
                 "which is what the authored token measures. Any other value scales the token " +
                 "visual uniformly to fit, so the skin owns token size without re-authoring.")]
        public Vector2 size;
        [Min(0)] public float captionHeight=24, captionInset=3;
        [Min(1)] public int captionMinSize=11, captionMaxSize=16;
        [Tooltip("Border thickness for the generated token stock. Zero falls back to chrome.lineWidth.")]
        [Min(0)] public float borderWidth;
        [Tooltip("Shrinks the token artwork inside its stock panel, in pixels per side.")]
        [Min(0)] public float artInset;
        [Min(.1f)] public float highlightFadeSpeed=10;
    }
    /// <summary>
    /// Style the card face draws itself, which no amount of board chrome can reach: these pieces are
    /// built inside Card at refresh time. Installed into CardServices so Card keeps reading its own
    /// seam and never takes a dependency on BoardSkin.
    /// </summary>
    [Serializable] public class FaceStyle : ICardFaceStyle
    {
        [Tooltip("Unplayable-requirement messages beneath the description.")]
        public Color requirementsMessage = Color.red;
        Color ICardFaceStyle.RequirementsMessageColor => requirementsMessage;
    }
    [Serializable] public class PileStyle
    {
        [Range(.1f,1)] public float faceScale=.94f;
        public Vector2 faceOffset=new(-3,3), stackOffset=new(3,-3);
        [Range(0,6)] public int visibleStackEdges=2;
        public Bounds emptyBounds=new(.2f,.2f,.8f,.8f);
        public Vector2 counterSize=new(55,26);
        [Min(1)] public int counterFontSize=18;
    }
    [Serializable] public class PreviewStyle
    {
        [Min(100)] public float preferredHeight=610;
        [Min(0)] public float header=34, footer=46, padding=12, screenMargin=10, sourceGap=18;
        public Vector2 shadowOffset=new(7,-9), closeOffset=new(-16,-17), closeSize=new(28,26), arrowInset=new(36,23), arrowSize=new(44,30);
        [Min(0)] public float labelInset=14, statusInset=55, exitGrace=.32f;
        [Min(.1f)] public float fadeSpeed=12;
    }
    [Serializable] public class ZoneStyle
    {
        public BoardZoneId zone;
        public string title;
        public Bounds bounds;
        public SkinColorRole accent;
        public ZoneStyle(BoardZoneId zone,string title,float x0,float y0,float x1,float y1,SkinColorRole accent)
        { this.zone=zone; this.title=title; bounds=new(x0,y0,x1,y1); this.accent=accent; }
    }
    private static ZoneStyle[] DefaultZones() => new[] {
        new ZoneStyle(BoardZoneId.OpponentLands,"LANDS",0.153f,0.818f,0.70f,0.93f,SkinColorRole.Gold),
        // One token wide: the settlement zone only ever shows the destination (and, while the human
        // is choosing, the settlements that could be travelled to).
        new ZoneStyle(BoardZoneId.OpponentSettlements,"DESTINATION",0.708f,0.818f,0.863f,0.93f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.OpponentArmies,"OPPONENT  /  ARMIES & CHARACTERS",0.153f,0.678f,0.863f,0.81f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.OpponentVictory,"VICTORY",0.871f,0.628f,0.992f,0.73f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.OpponentDiscard,"DISCARD",0.871f,0.738f,0.992f,0.93f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.Environment,"THE WORLD  /  ENVIRONMENT",0.153f,0.572f,0.863f,0.67f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.HumanArmies,"YOUR REALM  /  ARMIES & CHARACTERS",0.153f,0.43f,0.863f,0.564f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.HumanLands,"LANDS",0.153f,0.302f,0.70f,0.422f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.HumanSettlements,"DESTINATION",0.708f,0.302f,0.863f,0.422f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.HumanVictory,"VICTORY",0.871f,0.302f,0.992f,0.408f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.HumanDiscard,"DISCARD",0.871f,0.416f,0.992f,0.62f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.Hand,"YOUR HAND",0.153f,0.012f,0.992f,0.294f,SkinColorRole.Teal)
    };
}
