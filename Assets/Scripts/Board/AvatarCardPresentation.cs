using System.Linq;
using UnityEngine;

/// <summary>Avatar identity stays visible while its playable character is outside the field.</summary>
public sealed class AvatarCardPresentation : MonoBehaviour
{
    Card card;
    AvatarZoneVisualizer zone;
    bool? previous;
    public void Initialize(Card face, AvatarZoneVisualizer owner)
    {
        card = face;
        zone = owner;
        LateUpdate();
    }
    void LateUpdate()
    {
        if (card == null || zone == null) return;
        bool played = zone.board.Match?.Rules?.Players[zone.Owner].Field.Any(
            u => u.Card.cardId == card.cardData.cardId && u.Card.name == card.cardData.name) ?? false;
        if (previous == played) return;
        previous = played;
        card.SetAvatarUnlocked(played);
    }
}
