using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The combat screen: every resolution (a stop's fight on the road, a town's dwellers, an ambush,
/// two companies meeting) is laid out as "COMBAT AT {settlement}" or "COMBAT IN {region}", one row
/// per attacker with who stood against it, the dice, and the blow that landed. Rows are dealt one by
/// one and the dice spin before they settle; a click or key deals everything at once, and the next
/// one dismisses the screen. The match waits (TowerMatchController.CombatShowing) until it is read.
/// </summary>
public sealed class CombatResultPanel : MonoBehaviour
{
    Board board;
    int lastSerial;
    GameObject panel;
    CanvasGroup fade;
    bool closing, dealt;
    float openedAt;
    readonly List<(CanvasGroup group, RectTransform rect, float at)> rows = new();
    readonly List<(Text label, int final, float settleAt, Color color)> dice = new();
    float dealtAt;
    const float RowStagger = .5f, DiceSpin = .7f, AutoClose = 25f;
    public bool Showing => panel != null && !closing;
    public void Initialize(Board source) => board = source;

    void Update()
    {
        var battle = board?.Match?.Rules?.LastBattle;
        if (battle != null && battle.Serial != lastSerial) { lastSerial = battle.Serial; Show(battle); }
        if (panel == null) return;
        float now = Time.unscaledTime;
        if (closing)
        {
            fade.alpha = Mathf.MoveTowards(fade.alpha, 0, Time.unscaledDeltaTime * 5);
            if (fade.alpha <= 0) { BoardCardPreview.UnregisterModal((RectTransform)panel.transform); Destroy(panel); panel = null; closing = false; }
            return;
        }
        fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * 5);
        if (panel.transform.GetSiblingIndex() != panel.transform.parent.childCount - 1) panel.transform.SetAsLastSibling();
        // Deal the rows: each slides in from the left as its moment comes.
        bool allDealt = true;
        foreach (var row in rows)
        {
            float t = Mathf.Clamp01((now - row.at) / .35f);
            if (t < 1) allDealt = false;
            float eased = 1 - (1 - t) * (1 - t);
            row.group.alpha = eased;
            row.rect.anchoredPosition = new Vector2(Mathf.Lerp(-60, 0, eased), row.rect.anchoredPosition.y);
        }
        // The dice tumble through random faces until they settle on the real roll, with a small pop.
        foreach (var die in dice)
        {
            float remaining = die.settleAt - now;
            if (remaining > 0) { allDealt = false; die.label.text = Random.Range(1, 7).ToString(); die.label.color = Color.Lerp(die.color, Color.white, .5f); die.label.transform.localScale = Vector3.one; }
            else
            {
                die.label.text = die.final.ToString(); die.label.color = die.color;
                float pop = Mathf.Clamp01(-remaining / .25f);
                die.label.transform.localScale = Vector3.one * (1 + .5f * (1 - pop) * Mathf.Sin(pop * Mathf.PI));
            }
        }
        if (allDealt && !dealt) { dealt = true; dealtAt = now; }
        var footer = panel.transform.Find("Footer")?.GetComponent<Text>();
        if (footer != null) footer.text = dealt ? "CLICK OR PRESS ANY KEY TO CONTINUE" : "CLICK TO SHOW EVERYTHING AT ONCE";
        bool pressed = now - openedAt > .4f && (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame
            || Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame));
        if (pressed)
        {
            if (!dealt) DealEverything(); else closing = true;
        }
        else if (dealt && now - dealtAt > AutoClose) closing = true;
    }

    void DealEverything()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < rows.Count; i++) rows[i] = (rows[i].group, rows[i].rect, Mathf.Min(rows[i].at, now - 1));
        for (int i = 0; i < dice.Count; i++) dice[i] = (dice[i].label, dice[i].final, Mathf.Min(dice[i].settleAt, now), dice[i].color);
    }

    void Show(MatchRules.Battle battle)
    {
        if (panel != null) { panel.SetActive(false); Destroy(panel); }
        rows.Clear(); dice.Clear(); dealt = false; closing = false; openedAt = Time.unscaledTime;
        var skin = BoardPresentation.SkinFor(transform);
        var rules = board.Match.Rules;
        // A veil over the whole board that swallows clicks, and the plate on it.
        var veil = BoardPresentation.Panel(transform, "Combat screen", new Color(.01f, .015f, .02f, .8f));
        panel = veil.gameObject; veil.raycastTarget = true;
        BoardPresentation.Stretch(veil.rectTransform, Vector2.zero, Vector2.one);
        fade = panel.AddComponent<CanvasGroup>(); fade.alpha = 0; fade.blocksRaycasts = true;
        BoardCardPreview.RegisterModal(veil.rectTransform);
        board.preview?.Hide();
        var plate = BoardPresentation.Panel(veil.transform, "Battle plate", skin.colors.ink);
        BoardPresentation.Stretch(plate.rectTransform, new Vector2(.13f, .1f), new Vector2(.87f, .92f));
        BoardSurface.Dress(plate, skin.colors.gold, true, true);
        var shadow = plate.gameObject.AddComponent<Shadow>(); shadow.effectColor = skin.colors.previewShadow; shadow.effectDistance = skin.preview.shadowOffset * 2;

        // Where and what.
        string where = battle.Settlement != null ? "COMBAT AT " + battle.Settlement.name.ToUpperInvariant()
            : "COMBAT IN " + PcDescriptionBuilder.FormatDisplayRegionName(battle.Region ?? "the wild").ToUpperInvariant();
        var title = Label(plate.transform, where, 26, skin.colors.gold, new Vector2(.03f, .89f), new Vector2(.97f, .985f));
        if (skin.typography.mastheadFont != null) title.font = skin.typography.mastheadFont;
        Label(plate.transform, Subtitle(battle), 13, skin.colors.ivory, new Vector2(.03f, .83f), new Vector2(.97f, .89f));
        BoardPresentation.Rule(plate.transform, skin.colors.gold, new Vector2(.06f, .825f), new Vector2(.94f, .825f));
        var footer = Label(plate.transform, "", 11, skin.colors.muted, new Vector2(.03f, .015f), new Vector2(.97f, .06f));
        footer.name = "Footer";

        // One row per attacker. Rows share the space between the header and the footer.
        int count = Mathf.Max(1, battle.Clashes.Count);
        float top = .81f, bottom = .07f, gap = .012f;
        float rowHeight = Mathf.Min(.24f, (top - bottom - gap * (count - 1)) / count);
        for (int i = 0; i < battle.Clashes.Count; i++)
        {
            float rowTop = top - i * (rowHeight + gap);
            var row = BoardPresentation.Panel(plate.transform, "Clash " + (i + 1), new Color(1, 1, 1, .035f));
            BoardPresentation.Stretch(row.rectTransform, new Vector2(.03f, rowTop - rowHeight), new Vector2(.97f, rowTop));
            var group = row.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
            rows.Add((group, row.rectTransform, openedAt + .25f + i * RowStagger));
            BuildClash(row.rectTransform, battle, battle.Clashes[i], rules, skin, openedAt + .25f + i * RowStagger);
        }
        if (battle.Clashes.Count == 0) Label(plate.transform, "No blows were struck.", 16, skin.colors.muted, new Vector2(.1f, .4f), new Vector2(.9f, .5f));
        panel.transform.SetAsLastSibling();
    }

    string Subtitle(MatchRules.Battle battle)
    {
        string region = PcDescriptionBuilder.FormatDisplayRegionName(battle.Region ?? "");
        string ground = battle.Ground != TerrainEnum.None ? "  ·  " + battle.Ground.ToString().ToUpperInvariant() + " GROUND" : "";
        string who = battle.Player == 0 ? "YOUR COMPANY" : "THE ENEMY COMPANY";
        switch (battle.Kind)
        {
            case MatchRules.BattleKind.Road:
                var travel = board.Match.Rules.Travel;
                string road = travel != null && travel.Moving ? "ON THE ROAD TO " + travel.Destination.name.ToUpperInvariant() : battle.Settlement != null ? "AT THE GATES OF " + battle.Settlement.name.ToUpperInvariant() : "HOLDING STILL";
                return region.ToUpperInvariant() + ground + "  ·  " + who + " " + road + "  ·  STOP " + battle.Stop + " OF " + battle.Stops;
            case MatchRules.BattleKind.Dwellers:
                return region.ToUpperInvariant() + "  ·  " + who + " FACES THE DWELLERS" + (battle.Neutral ? "  ·  A RETENTION ATTACK: NO BLOOD SPILT" : "  ·  A NORMAL ATTACK") + (battle.Led ? "  ·  THE CHAMPION LEADS, NOTHING ELSE READY" : "");
            case MatchRules.BattleKind.Ambush:
                return region.ToUpperInvariant() + "  ·  " + (battle.Player == 0 ? "YOU AMBUSH THE ENEMY COMPANY" : "THE ENEMY AMBUSHES YOUR COMPANY") + " FROM THE HAND";
            default:
                return region.ToUpperInvariant() + "  ·  BOTH COMPANIES HOLD THE TOWN: THE CHAMPIONS MEET";
        }
    }

    void BuildClash(RectTransform row, MatchRules.Battle battle, MatchRules.Clash clash, MatchRules rules, BoardSkin skin, float dealtAt)
    {
        var attacker = clash.Attacker;
        // The attacker on the left: portrait, name, and what it fought with.
        Combatant(row, attacker, new Vector2(.01f, .06f), new Vector2(.22f, .94f), skin, clash.FromHand ? "FROM HAND" : null, AttackerStats(clash, rules));
        var arrow = Label(row, "➤", 30, skin.colors.muted, new Vector2(.22f, .2f), new Vector2(.27f, .8f));
        if (clash.Unblocked > 0)
        {
            Label(row, "UNBLOCKED", 18, new Color(1f, .45f, .3f), new Vector2(.28f, .5f), new Vector2(.75f, .92f));
            Label(row, (battle.Player == 0 ? "Your company" : "The enemy company") + " takes " + clash.Unblocked + " damage", 14, skin.colors.ivory, new Vector2(.28f, .08f), new Vector2(.75f, .5f));
            var hit = Label(row, "-" + clash.Unblocked, 34, new Color(1f, .35f, .25f), new Vector2(.76f, .1f), new Vector2(.98f, .9f));
            hit.name = "Life lost";
            return;
        }
        // Each blocker it met, left to right, with the duel between them.
        int cells = Mathf.Max(1, clash.Blockers.Count);
        float left = .28f, width = (.99f - left) / cells;
        for (int i = 0; i < clash.Blockers.Count; i++)
        {
            var defender = clash.Blockers[i];
            var fight = clash.Fights.FirstOrDefault(f => f.Defender == defender || f.Attacker == defender);
            float x = left + i * width;
            var cell = BoardPresentation.Panel(row, "Duel", new Color(0, 0, 0, .18f));
            BoardPresentation.Stretch(cell.rectTransform, new Vector2(x + .004f, .04f), new Vector2(x + width - .004f, .96f));
            string tag = clash.StoodFast.Contains(defender) ? "STANDS FAST" : defender.Owner != battle.Player && battle.Kind == MatchRules.BattleKind.Dwellers ? "DWELLERS" : null;
            if (fight == null)
            {
                Combatant(cell.rectTransform, defender, new Vector2(.55f, .06f), new Vector2(.99f, .94f), skin, tag, null);
                Label(cell.rectTransform, "SPARED\nthe attacker was already down", 11, skin.colors.muted, new Vector2(.02f, .1f), new Vector2(.54f, .9f));
                continue;
            }
            var defenderStats = fight.Defender == defender ? fight.DefenderStats : fight.AttackerStats;
            Combatant(cell.rectTransform, defender, new Vector2(.62f, .06f), new Vector2(.99f, .94f), skin, tag, defenderStats.attack + "/" + defenderStats.defense);
            // The dice: attacker's total on top, defender's below, each as attack + die = total.
            var attackerColor = attacker.Owner == 0 ? skin.colors.teal : skin.colors.gold;
            var defenderColor = defender.Owner == 0 ? skin.colors.teal : skin.colors.gold;
            DiceLine(cell.rectTransform, fight.AttackerStats.attack, fight.AttackerRoll, fight.AttackerTotal, attackerColor, new Vector2(.02f, .62f), new Vector2(.6f, .95f), dealtAt + .2f + i * .15f, skin);
            Label(cell.rectTransform, "vs", 11, skin.colors.muted, new Vector2(.02f, .5f), new Vector2(.6f, .62f));
            DiceLine(cell.rectTransform, fight.DefenderStats.attack, fight.DefenderRoll, fight.DefenderTotal, defenderColor, new Vector2(.02f, .3f), new Vector2(.6f, .5f), dealtAt + .35f + i * .15f, skin);
            string verdict = fight.Loser == null ? "STAND-OFF" : (fight.Loser == defender ? defender.Card.name : attacker.Card.name).ToUpperInvariant() + ": " +
                (fight.Blow == Blow.Killed ? (fight.Loser.IsCharacter ? "STRUCK DOWN" : "KILLED") : fight.Blow == Blow.Wounded ? "WOUNDED" : "TAPPED") + "  (" + fight.Margin + " vs " + (fight.Loser == fight.Attacker ? fight.AttackerStats.defense : fight.DefenderStats.defense) + " def)";
            var color = fight.Loser == null ? skin.colors.muted : fight.Blow == Blow.Killed ? new Color(1f, .4f, .3f) : fight.Blow == Blow.Wounded ? new Color(1f, .7f, .35f) : skin.colors.ivory;
            var verdictLabel = Label(cell.rectTransform, verdict, 12, color, new Vector2(.02f, .04f), new Vector2(.6f, .3f));
            verdictLabel.fontStyle = FontStyle.Bold;
            // The verdict lands with the second die.
            var reveal = verdictLabel.gameObject.AddComponent<CanvasGroup>(); reveal.alpha = 0;
            StartCoroutine(RevealAt(reveal, dealtAt + .35f + i * .15f + DiceSpin));
        }
    }

    System.Collections.IEnumerator RevealAt(CanvasGroup group, float at)
    {
        while (group != null && (Time.unscaledTime < at && !dealt)) yield return null;
        while (group != null && group.alpha < 1) { group.alpha = Mathf.MoveTowards(group.alpha, 1, Time.unscaledDeltaTime * 6); yield return null; }
    }

    static string AttackerStats(MatchRules.Clash clash, MatchRules rules)
    {
        var fight = clash.Fights.FirstOrDefault();
        if (fight != null) return fight.AttackerStats.attack + "/" + fight.AttackerStats.defense;
        var stats = rules.Stats(clash.Attacker);
        return stats.attack + "/" + stats.defense;
    }

    void Combatant(RectTransform parent, MatchRules.Unit unit, Vector2 min, Vector2 max, BoardSkin skin, string tag, string stats)
    {
        var accent = unit.Owner == 0 ? skin.colors.teal : skin.colors.gold;
        var frame = BoardPresentation.Panel(parent, "Combatant", skin.colors.ink);
        BoardPresentation.Stretch(frame.rectTransform, min, max);
        var portrait = BoardPresentation.Panel(frame.transform, "Portrait", Color.white);
        BoardPresentation.Stretch(portrait.rectTransform, new Vector2(0, .3f), new Vector2(1, 1));
        portrait.rectTransform.offsetMin = new Vector2(3, 2); portrait.rectTransform.offsetMax = new Vector2(-3, -3);
        portrait.sprite = TravelBanner.Artwork(unit.Card); portrait.preserveAspect = true;
        if (portrait.sprite == null) portrait.color = new Color(.2f, .22f, .25f);
        BoardPresentation.Border(frame.rectTransform, accent, 2);
        var name = Label(frame.transform, unit.Card.name, 12, skin.colors.ivory, new Vector2(.02f, .14f), new Vector2(.98f, .3f));
        name.fontStyle = FontStyle.Bold;
        string line = (stats != null ? stats : "") + (tag != null ? (stats != null ? "  ·  " : "") + tag : "");
        if (line.Length > 0) Label(frame.transform, line, 10, tag != null ? new Color(1f, .8f, .45f) : skin.colors.muted, new Vector2(.02f, .0f), new Vector2(.98f, .14f));
        if (unit.Owner == 0 == (board.Match.Rules.Active == 0) && tag == null) { }
    }

    void DiceLine(RectTransform parent, int attack, int roll, int total, Color color, Vector2 min, Vector2 max, float settleAt, BoardSkin skin)
    {
        float w = max.x - min.x;
        Label(parent, attack.ToString(), 15, skin.colors.ivory, new Vector2(min.x, min.y), new Vector2(min.x + w * .18f, max.y));
        Label(parent, "+", 13, skin.colors.muted, new Vector2(min.x + w * .18f, min.y), new Vector2(min.x + w * .28f, max.y));
        // The die: a small carved square whose face tumbles until it settles.
        var die = BoardPresentation.Panel(parent, "Die", new Color(.92f, .86f, .7f));
        BoardPresentation.Stretch(die.rectTransform, new Vector2(min.x + w * .3f, min.y + .08f), new Vector2(min.x + w * .5f, max.y - .08f));
        BoardPresentation.Border(die.rectTransform, new Color(.4f, .3f, .15f), 2);
        var face = Label(die.transform, roll.ToString(), 15, new Color(.1f, .08f, .06f), Vector2.zero, Vector2.one);
        face.fontStyle = FontStyle.Bold;
        dice.Add((face, roll, settleAt + DiceSpin, new Color(.1f, .08f, .06f)));
        Label(parent, "=", 13, skin.colors.muted, new Vector2(min.x + w * .52f, min.y), new Vector2(min.x + w * .62f, max.y));
        var sum = Label(parent, total.ToString(), 20, color, new Vector2(min.x + w * .64f, min.y), new Vector2(min.x + w, max.y));
        sum.fontStyle = FontStyle.Bold;
        var reveal = sum.gameObject.AddComponent<CanvasGroup>(); reveal.alpha = 0;
        StartCoroutine(RevealAt(reveal, settleAt + DiceSpin));
    }

    Text Label(Transform parent, string text, int size, Color color, Vector2 min, Vector2 max)
    {
        var label = BoardPresentation.TextLabel(parent, text, board.interfaceFont, size, color, min, max, TextAnchor.MiddleCenter);
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 7; label.resizeTextMaxSize = size;
        return label;
    }

    void OnDisable() { if (panel != null) { BoardCardPreview.UnregisterModal((RectTransform)panel.transform); Destroy(panel); } panel = null; }
}
