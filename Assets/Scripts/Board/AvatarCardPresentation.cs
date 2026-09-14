using System.Linq;
using UnityEngine;

/// <summary>
/// Avatar identity stays visible while its playable character is outside the field. The champion's
/// card is a portrait: it never shows its cost, keeps its levels and combat numbers hidden until the
/// character has been played, and carries the company's life as a bar across its artwork.
/// </summary>
public sealed class AvatarCardPresentation : MonoBehaviour
{
    Card card;
    AvatarZoneVisualizer zone;
    bool? previous;
    public void Initialize(Card face, AvatarZoneVisualizer owner)
    {
        card = face;
        zone = owner;
        if (card.transform.Find("Health bar") == null) AvatarHealthBar.CreateOnCard(card, zone.board, zone.Owner);
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
