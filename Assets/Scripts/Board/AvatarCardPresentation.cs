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
        // A champion is a portrait with life and identity, so use the space formerly occupied
        // by the empty rules panel for the illustration.
        var real = card.transform.Find("RealCard");
        var art = real?.Find("Image") as RectTransform;
        var health = card.transform.Find("Health bar") as RectTransform;
        var description = real?.Find("DescriptionBackground") as RectTransform;
        var skin = BoardPresentation.SkinFor(card.transform);
        if (art != null && health != null)
        {
            float top = health.anchoredPosition.y - health.sizeDelta.y * .5f - 8;
            float bottom = -skin.cards.size.y * .5f + 36;
            art.sizeDelta = new Vector2(art.sizeDelta.x, top - bottom);
            art.anchoredPosition = new Vector2(art.anchoredPosition.x, (top + bottom) * .5f);
            if (description != null) {
                description.sizeDelta = new Vector2(description.sizeDelta.x, 28);
                description.anchoredPosition = new Vector2(0, -skin.cards.size.y * .5f + 19);
            }
        }
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
