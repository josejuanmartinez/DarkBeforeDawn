using UnityEngine;

/// <summary>A visible popup chip takes precedence over the board card it represents.</summary>
public sealed class BattleVfxAnchor : MonoBehaviour
{
    public CardData Card { get; private set; }
    BoardBattleVfx effects;
    public static void Bind(Board board,CardData card,RectTransform chip)
    {
        var anchor=chip.gameObject.AddComponent<BattleVfxAnchor>();
        anchor.Card=card;anchor.effects=BoardBattleVfx.For(board);anchor.effects.Register(anchor);
    }
    void OnDestroy(){if(effects!=null)effects.Unregister(this);}
}
