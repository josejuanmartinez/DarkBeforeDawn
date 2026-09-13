using UnityEngine;
using UnityEngine.UI;

/// <summary>Live health with a trailing damage indicator and an explicit numeric value.</summary>
public sealed class AvatarHealthBar : MonoBehaviour
{
    Board board;
    int owner, last = -1;
    Image fill, trail;
    Text value;
    float displayed = 1, damage = 1, changedAt;
    Color healthy;
    public static void Create(Transform parent, Board board, int owner, Color accent)
    {
        var plate = BoardPresentation.Panel(parent, "Health bar", BoardPresentation.SkinFor(parent).colors.ink);
        BoardPresentation.Stretch(plate.rectTransform, Vector2.zero, Vector2.right);
        plate.rectTransform.pivot = new Vector2(.5f, 0);
        plate.rectTransform.sizeDelta = new Vector2(-22, 32);
        plate.rectTransform.anchoredPosition = new Vector2(0, 10);
        BoardSurface.Dress(plate, accent, false, true);
        var health = plate.gameObject.AddComponent<AvatarHealthBar>();
        health.board = board; health.owner = owner; health.healthy = accent;
        health.trail = BoardPresentation.Panel(plate.transform, "Recent damage", new Color(.94f,.67f,.30f));
        health.fill = BoardPresentation.Panel(plate.transform, "Remaining health", accent);
        foreach (var bar in new[] { health.trail, health.fill })
        {
            BoardPresentation.Stretch(bar.rectTransform, Vector2.zero, Vector2.one);
            bar.rectTransform.offsetMin = new Vector2(4, 4); bar.rectTransform.offsetMax = new Vector2(-4, -4);
        }
        var shine = BoardPresentation.Panel(health.fill.transform, "Polished edge", new Color(1,1,1,.22f));
        BoardPresentation.Stretch(shine.rectTransform, new Vector2(0,.82f), Vector2.one);
        health.value = BoardPresentation.TextLabel(plate.transform, "", board.interfaceFont, 14, Color.white,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        health.value.fontStyle = FontStyle.Bold;
        var shadow = health.value.gameObject.AddComponent<Shadow>(); shadow.effectDistance = new Vector2(1,-1); shadow.effectColor = Color.black;
        health.Update();
    }
    void Update()
    {
        int maximum = Mathf.Max(1,board.Match != null ? board.Match.startingLife : 20);
        int life = board.Match?.Rules?.Players[owner].Life ?? maximum;
        float target = Mathf.Clamp01((float)life / maximum);
        if (life != last)
        {
            changedAt = Time.unscaledTime;
            value.text = Mathf.Max(0,life) + " / " + maximum + "  HEALTH";
            if (last < 0) displayed = damage = target;
            last = life;
        }
        displayed = Mathf.MoveTowards(displayed, target, Time.unscaledDeltaTime * 1.8f);
        if (Time.unscaledTime - changedAt > .5f) damage = Mathf.MoveTowards(damage, target, Time.unscaledDeltaTime * .5f);
        damage = Mathf.Max(damage, displayed);
        fill.rectTransform.anchorMax = new Vector2(displayed,1);
        trail.rectTransform.anchorMax = new Vector2(damage,1);
        fill.gameObject.SetActive(displayed > .02f); trail.gameObject.SetActive(damage > .02f);
        fill.color = life <= 5 ? new Color(.72f,.17f,.14f) : healthy;
    }
}
