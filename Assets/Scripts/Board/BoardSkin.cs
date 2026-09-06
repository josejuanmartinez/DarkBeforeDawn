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
    public Palette colors = new();
    public Typography typography = new();
    public Chrome chrome = new();
    public CardStyle cards = new();
    public TokenStyle tokens = new();
    public PileStyle piles = new();
    public PreviewStyle preview = new();
    public ZoneStyle[] zones = DefaultZones();
    [NonSerialized] private int revision;
    public int Revision => revision;
    public static BoardSkin Default => Resources.Load<BoardSkin>("Skins/Default");
    private void OnValidate() { revision++; }
    public Color ColorFor(SkinColorRole role) => role switch {
        SkinColorRole.Ink => colors.ink, SkinColorRole.Gold => colors.gold,
        SkinColorRole.Ivory => colors.ivory, SkinColorRole.Teal => colors.teal, _ => colors.muted
    };

    [Serializable] public class Palette
    {
        public Color ink = new(.035f,.052f,.060f,.96f), gold = new(.70f,.56f,.33f), ivory = new(.93f,.89f,.78f), muted = new(.57f,.63f,.62f), teal = new(.39f,.71f,.66f);
        public Color atmosphere = new(.018f,.033f,.039f,.80f), zoneSurface = new(.035f,.055f,.06f,.96f);
        public Color tokenBorder = new(.7f,.56f,.33f,.6f), stackBorder = new(.34f,.31f,.23f);
        public Color requirementRibbon = new(.025f,.035f,.04f,.92f), button = new(.14f,.18f,.18f), disabledButton = new(.3f,.3f,.3f);
        public Color cardShadow = new(0,0,0,.6f), previewShadow = new(0,0,0,.65f), emptyPile = new(1,1,1,.035f);
        [Tooltip("Optional palette for full-card type accents; the existing card palette is used when empty.")]
        public CardPalette cardTypes;
    }
    [Serializable] public class Typography
    {
        public Font interfaceFont, mastheadFont;
        public TMP_FontAsset readingFont;
        [Min(1)] public int zoneHeadingSize = 15, countSize = 12, previewLabelSize = 12, buttonSize = 19;
        public Vector2 titleFontRange = new(14,21), descriptionFontRange = new(9,14);
        public FontStyles titleStyle = FontStyles.Bold;
        [Min(1)] public float requirementsSize = 12;
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
        public LabelStyle[] footerLabels = {
            new("YOUR REALM",15,SkinColorRole.Teal,new(.022f,.11f,.125f,.16f)),
            new("Inspect a card\nto read its story.",13,SkinColorRole.Muted,new(.022f,.04f,.125f,.11f)),
            new("CARD INSPECTION",12,SkinColorRole.Gold,new(.88f,.11f,.98f,.16f)),
            new("Click to pin\nEsc to close",13,SkinColorRole.Muted,new(.88f,.04f,.98f,.11f))
        };
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
        public Vector2 shadowOffset = new(3,-5), titleInsetMin = new(3,0), titleInsetMax = new(20,0);
        public Vector4 descriptionMargin = new(4,5,4,5), requirementMargin = new(6,4,6,4);
        public Vector2 requirementHeightRange = new(24,76);
        [Range(0,1)] public float typeBorderBlend=.55f;
    }
    [Serializable] public class TokenStyle
    {
        [Min(0)] public float captionHeight=24, captionInset=3;
        [Min(1)] public int captionMinSize=11, captionMaxSize=16;
        [Min(.1f)] public float highlightFadeSpeed=10;
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
        new ZoneStyle(BoardZoneId.OpponentLands,"LANDS",.145f,.818f,.495f,.93f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.OpponentSettlements,"SETTLEMENTS",.505f,.818f,.855f,.93f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.OpponentArmies,"OPPONENT  /  ARMIES & CHARACTERS",.145f,.678f,.855f,.808f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.OpponentVictory,"VICTORY",.018f,.678f,.13f,.93f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.OpponentDiscard,"DISCARD",.87f,.678f,.982f,.93f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.Environment,"THE WORLD  /  ENVIRONMENT",.145f,.572f,.855f,.667f,SkinColorRole.Gold),
        new ZoneStyle(BoardZoneId.HumanArmies,"YOUR REALM  /  ARMIES & CHARACTERS",.145f,.43f,.855f,.56f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.HumanLands,"LANDS",.145f,.296f,.495f,.42f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.HumanSettlements,"SETTLEMENTS",.505f,.296f,.855f,.42f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.HumanVictory,"VICTORY",.018f,.296f,.13f,.56f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.HumanDiscard,"DISCARD",.87f,.296f,.982f,.56f,SkinColorRole.Teal),
        new ZoneStyle(BoardZoneId.Hand,"YOUR HAND",.145f,.012f,.855f,.284f,SkinColorRole.Teal)
    };
}
