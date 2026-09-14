using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The combat screen: every resolution (a stop's fight on the road, a town's dwellers, an ambush,
/// two companies meeting) is staged as "COMBAT AT {settlement}" or "COMBAT IN {region}" on a black
/// ground under the terrain's colour, one row per attacker: its portrait, lit and breathing, against
/// each unit that stood in its way, the pip dice tumbling between them until they settle on the
/// real roll, the totals struck up, and the blow stamped across the duel with a burst of light on
/// whoever took it. Both companies' life is kept in the corners. Rows are dealt one by one; a click
/// or key deals everything at once, and the next one dismisses the screen. The match waits
/// (TowerMatchController.CombatShowing) until it is read.
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
    readonly List<Die> dice = new();
    readonly List<(RectTransform stamp, float at, Color color)> stamps = new();
    readonly List<(ClashFlash flash, float at)> flashes = new();
    readonly List<(CanvasGroup group, float at)> reveals = new();
    float dealtAt;
    Text footer;
    const float RowStagger = .55f, DiceSpin = .8f, AutoClose = 25f;
    static readonly Color Blood = new(1f, .32f, .22f), Bruise = new(1f, .72f, .35f), Bone = new(.93f, .88f, .74f);
    public bool Showing => panel != null && !closing;
    public void Initialize(Board source) => board = source;

    /// <summary>A pip die: an ivory square whose face tumbles until it settles on the roll, then pops.</summary>
    sealed class Die
    {
        public RectTransform rect; public Image[] pips = new Image[9]; public int final; public float settleAt; public float lastFace; public bool settled;
        static readonly int[][] faces = { new[] { 4 }, new[] { 0, 8 }, new[] { 0, 4, 8 }, new[] { 0, 2, 6, 8 }, new[] { 0, 2, 4, 6, 8 }, new[] { 0, 2, 3, 5, 6, 8 } };
        public void Show(int face)
        {
            var on = faces[Mathf.Clamp(face, 1, 6) - 1];
            for (int i = 0; i < pips.Length; i++) pips[i].enabled = System.Array.IndexOf(on, i) >= 0;
        }
    }

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
            float t = Mathf.Clamp01((now - row.at) / .4f);
            if (t < 1) allDealt = false;
            float eased = 1 - (1 - t) * (1 - t) * (1 - t);
            row.group.alpha = eased;
            row.rect.anchoredPosition = new Vector2(Mathf.Lerp(-90, 0, eased), row.rect.anchoredPosition.y);
        }
        // The dice tumble through random faces, rocking, until they settle on the real roll with a pop.
        foreach (var die in dice)
        {
            float remaining = die.settleAt - now;
            if (remaining > 0)
            {
                allDealt = false;
                if (now - die.lastFace > .07f) { die.lastFace = now; die.Show(Random.Range(1, 7)); }
                die.rect.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(now * 37 + die.final) * 22);
                die.rect.localScale = Vector3.one * 1.1f;
            }
            else
            {
                if (!die.settled) { die.settled = true; die.Show(die.final); }
                float pop = Mathf.Clamp01(-remaining / .3f);
                die.rect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-6, 0, pop) * (die.final % 2 == 0 ? 1 : -1));
                die.rect.localScale = Vector3.one * (1 + .55f * (1 - pop) * Mathf.Sin(pop * Mathf.PI));
            }
        }
        // Totals and verdicts light up as their dice come to rest.
        foreach (var reveal in reveals)
            if (reveal.group != null) reveal.group.alpha = Mathf.MoveTowards(reveal.group.alpha, now >= reveal.at || dealt ? 1 : 0, Time.unscaledDeltaTime * 7);
        // The blow is stamped down hard, then breathes.
        foreach (var (stamp, at, _) in stamps)
        {
            if (stamp == null) continue;
            float t = Mathf.Clamp01((now - at) / .35f);
            var group = stamp.GetComponent<CanvasGroup>();
            group.alpha = t;
            float scale = t < 1 ? Mathf.Lerp(2.4f, 1, 1 - (1 - t) * (1 - t)) : 1 + .03f * Mathf.Sin(now * 3);
            stamp.localScale = Vector3.one * scale;
        }
        foreach (var (flash, at) in flashes) if (flash != null) flash.Tick(at, now);
        if (allDealt && !dealt) { dealt = true; dealtAt = now; }
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
        foreach (var die in dice) die.settleAt = Mathf.Min(die.settleAt, now);
        for (int i = 0; i < stamps.Count; i++) stamps[i] = (stamps[i].stamp, Mathf.Min(stamps[i].at, now), stamps[i].color);
        for (int i = 0; i < flashes.Count; i++) flashes[i] = (flashes[i].flash, Mathf.Min(flashes[i].at, now));
        for (int i = 0; i < reveals.Count; i++) reveals[i] = (reveals[i].group, Mathf.Min(reveals[i].at, now));
    }

    void Show(MatchRules.Battle battle)
    {
        if (panel != null) { panel.SetActive(false); Destroy(panel); }
        rows.Clear(); dice.Clear(); stamps.Clear(); flashes.Clear(); reveals.Clear(); dealt = false; closing = false; openedAt = Time.unscaledTime;
        var skin = BoardPresentation.SkinFor(transform);
        var rules = board.Match.Rules;
        var ground = battle.Ground != TerrainEnum.None ? battle.Ground : rules.TerrainOf(battle.Region ?? "");
        var tint = ground != TerrainEnum.None ? TravelBanner.TerrainColor(ground) : skin.colors.gold;
        // A black veil over the whole board that swallows clicks, and the stone plate on it.
        var veil = BoardPresentation.Panel(transform, "Combat screen", new Color(0, 0, 0, .9f));
        panel = veil.gameObject; veil.raycastTarget = true;
        BoardPresentation.Stretch(veil.rectTransform, Vector2.zero, Vector2.one);
        fade = panel.AddComponent<CanvasGroup>(); fade.alpha = 0; fade.blocksRaycasts = true;
        BoardCardPreview.RegisterModal(veil.rectTransform);
        board.preview?.Hide();
        var plate = BoardPresentation.Panel(veil.transform, "Battle plate", new Color(.045f, .035f, .03f, .995f));
        BoardPresentation.Stretch(plate.rectTransform, new Vector2(.11f, .06f), new Vector2(.89f, .94f));
        BoardSurface.Dress(plate, skin.colors.gold, true, false, BoardEmblem.Armies, true);
        var shadow = plate.gameObject.AddComponent<Shadow>(); shadow.effectColor = skin.colors.previewShadow; shadow.effectDistance = skin.preview.shadowOffset * 2;
        // The ground's colour washes the head of the plate, and a glow of it hangs over the rows.
        var wash = BoardPresentation.Panel(plate.transform, "Ground wash", new Color(tint.r, tint.g, tint.b, .06f));
        BoardPresentation.Stretch(wash.rectTransform, new Vector2(0, .825f), new Vector2(1, 1));
        Flash(plate.transform).Configure(tint, 0, true);

        // Where and what.
        string where = battle.Settlement != null ? "COMBAT AT " + battle.Settlement.name.ToUpperInvariant()
            : "COMBAT IN " + PcDescriptionBuilder.FormatDisplayRegionName(battle.Region ?? "the wild").ToUpperInvariant();
        var title = Label(plate.transform, where, 36, skin.colors.gold, new Vector2(.14f, .9f), new Vector2(.86f, .99f));
        if (skin.typography.mastheadFont != null) title.font = skin.typography.mastheadFont;
        title.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .8f);
        var subtitle = Rich(plate.transform, Subtitle(battle, ground), 14, skin.colors.ivory, new Vector2(.14f, .835f), new Vector2(.86f, .9f));
        subtitle.gameObject.AddComponent<CardKeywordHover>().RefreshTargets();
        // Both companies' life, in their colours, in the corners of the head.
        LifePlaque(plate.transform, 0, skin, new Vector2(.015f, .85f), new Vector2(.135f, .975f));
        LifePlaque(plate.transform, 1, skin, new Vector2(.865f, .85f), new Vector2(.985f, .975f));
        BoardPresentation.Rule(plate.transform, skin.colors.gold, new Vector2(.04f, .825f), new Vector2(.96f, .825f));
        footer = Label(plate.transform, "", 11, skin.colors.muted, new Vector2(.03f, .012f), new Vector2(.97f, .05f));

        // One row per attacker. Rows share the space between the head and the footer.
        int count = Mathf.Max(1, battle.Clashes.Count);
        float top = .805f, bottom = .06f, gap = .014f;
        float rowHeight = Mathf.Min(.26f, (top - bottom - gap * (count - 1)) / count);
        for (int i = 0; i < battle.Clashes.Count; i++)
        {
            float rowTop = top - i * (rowHeight + gap);
            var clash = battle.Clashes[i];
            var row = BoardPresentation.Panel(plate.transform, "Clash " + (i + 1), new Color(0, 0, 0, .42f));
            BoardPresentation.Stretch(row.rectTransform, new Vector2(.025f, rowTop - rowHeight), new Vector2(.975f, rowTop));
            var side = clash.Attacker.Owner == 0 ? skin.colors.teal : skin.colors.gold;
            BoardPresentation.Border(row.rectTransform, new Color(side.r, side.g, side.b, .28f), 1);
            var bar = BoardPresentation.Panel(row.transform, "Side bar", side);
            BoardPresentation.Stretch(bar.rectTransform, Vector2.zero, new Vector2(0, 1)); bar.rectTransform.sizeDelta = new Vector2(4, 0);
            var group = row.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
            float at = openedAt + .3f + i * RowStagger;
            rows.Add((group, row.rectTransform, at));
            BuildClash(row.rectTransform, battle, clash, rules, skin, at);
        }
        if (battle.Clashes.Count == 0) Label(plate.transform, "No blows were struck.", 18, skin.colors.muted, new Vector2(.1f, .4f), new Vector2(.9f, .5f));
        panel.transform.SetAsLastSibling();
    }

    string Subtitle(MatchRules.Battle battle, TerrainEnum ground)
    {
        string region = PcDescriptionBuilder.FormatDisplayRegionName(battle.Region ?? "").ToUpperInvariant();
        // The ground as its glyph and a "terrain:" link, so the subtitle explains the ground on hover like a card line.
        string where = ground != TerrainEnum.None
            ? "<link=\"terrain:" + ground + "\">" + region + "  " + CardKeywordGlossary.TerrainGlyph(ground) + " " + ground.ToString().ToUpperInvariant() + " GROUND</link>"
            : region;
        string who = battle.Player == 0 ? "YOUR COMPANY" : "THE ENEMY COMPANY";
        switch (battle.Kind)
        {
            case MatchRules.BattleKind.Road:
                var travel = board.Match.Rules.Travel;
                string road = travel != null && travel.Moving ? "ON THE ROAD TO " + travel.Destination.name.ToUpperInvariant() : battle.Settlement != null ? "AT THE GATES OF " + battle.Settlement.name.ToUpperInvariant() : "HOLDING STILL";
                return where + "   ·   " + who + " " + road + "   ·   STOP " + battle.Stop + " OF " + battle.Stops;
            case MatchRules.BattleKind.Dwellers:
                return where + "   ·   " + who + " FACES THE DWELLERS" + (battle.Neutral ? "   ·   A RETENTION ATTACK: NO BLOOD SPILT" : "   ·   A NORMAL ATTACK") + (battle.Led ? "   ·   THE CHAMPION LEADS, NOTHING ELSE READY" : "");
            case MatchRules.BattleKind.Ambush:
                return where + "   ·   " + (battle.Player == 0 ? "YOU AMBUSH THE ENEMY COMPANY" : "THE ENEMY AMBUSHES YOUR COMPANY") + " FROM THE HAND";
            default:
                return where + "   ·   BOTH COMPANIES HOLD THE TOWN: THE CHAMPIONS MEET";
        }
    }

    void LifePlaque(Transform parent, int player, BoardSkin skin, Vector2 min, Vector2 max)
    {
        var rules = board.Match.Rules;
        var accent = player == 0 ? skin.colors.teal : skin.colors.gold;
        var plaque = BoardPresentation.Panel(parent, "Life " + player, new Color(0, 0, 0, .4f));
        BoardPresentation.Stretch(plaque.rectTransform, min, max);
        BoardPresentation.Border(plaque.rectTransform, new Color(accent.r, accent.g, accent.b, .5f), 1);
        Label(plaque.transform, player == 0 ? "ORREN" : "THE SLEEPLESS EYE", 10, accent, new Vector2(.04f, .55f), new Vector2(.96f, .95f));
        Rich(plaque.transform, "<sprite name=\"health\"> " + Mathf.Max(0, rules.Players[player].Life), 22, skin.colors.ivory, new Vector2(.04f, .05f), new Vector2(.96f, .58f));
    }

    void BuildClash(RectTransform row, MatchRules.Battle battle, MatchRules.Clash clash, MatchRules rules, BoardSkin skin, float dealtAt)
    {
        var attacker = clash.Attacker;
        bool attackerFell = clash.Fights.Any(f => f.Loser == attacker && f.Blow != Blow.None);
        bool attackerWon = !attackerFell && clash.Fights.Any(f => f.Loser != null && f.Loser != attacker);
        // The attacker on the left: portrait, name, and what it fought with.
        Combatant(row, attacker, new Vector2(.012f, .05f), new Vector2(.19f, .95f), skin, clash.FromHand ? "FROM HAND" : null, AttackerStats(clash, rules), attackerWon, attackerFell, dealtAt + .5f + DiceSpin + .3f * Mathf.Max(0, clash.Fights.Count - 1));
        if (clash.Unblocked > 0)
        {
            // Nobody stood in the way: the blow lands on the company itself.
            var name = Label(row, "UNBLOCKED", 30, Blood, new Vector2(.22f, .5f), new Vector2(.7f, .92f));
            if (skin.typography.mastheadFont != null) name.font = skin.typography.mastheadFont;
            Label(row, (battle.Player == 0 ? "Your company" : "The enemy company") + " takes the blow on its life", 14, skin.colors.ivory, new Vector2(.22f, .1f), new Vector2(.7f, .5f));
            var hit = Rich(row, "<sprite name=\"health\"> -" + clash.Unblocked, 40, Blood, new Vector2(.72f, .1f), new Vector2(.98f, .9f));
            hit.name = "Life lost";
            var stampRect = hit.rectTransform;
            stampRect.gameObject.AddComponent<CanvasGroup>().alpha = 0;
            stamps.Add((stampRect, dealtAt + .35f, Blood));
            var flash = Flash(row); flash.Configure(Blood, .85f, false);
            flashes.Add((flash, dealtAt + .35f));
            return;
        }
        // Each blocker it met, left to right, with the duel between them.
        int cells = Mathf.Max(1, clash.Blockers.Count);
        float left = .2f, width = (.99f - left) / cells;
        for (int i = 0; i < clash.Blockers.Count; i++)
        {
            var defender = clash.Blockers[i];
            var fight = clash.Fights.FirstOrDefault(f => f.Defender == defender || f.Attacker == defender);
            float x = left + i * width;
            var cell = BoardPresentation.Panel(row, "Duel", new Color(0, 0, 0, .3f));
            BoardPresentation.Stretch(cell.rectTransform, new Vector2(x + .004f, .04f), new Vector2(x + width - .004f, .96f));
            string tag = clash.StoodFast.Contains(defender) ? "STANDS FAST" : defender.Owner != battle.Player && battle.Kind == MatchRules.BattleKind.Dwellers ? "DWELLERS" : null;
            float settle = dealtAt + .35f + i * .3f;
            if (fight == null)
            {
                Combatant(cell.rectTransform, defender, new Vector2(.7f, .05f), new Vector2(.99f, .95f), skin, tag, null, false, false, settle);
                Label(cell.rectTransform, "SPARED", 22, skin.colors.muted, new Vector2(.02f, .5f), new Vector2(.68f, .9f));
                Label(cell.rectTransform, "the attacker was already down", 11, skin.colors.muted, new Vector2(.02f, .1f), new Vector2(.68f, .5f));
                continue;
            }
            bool defenderFell = fight.Loser == defender && fight.Blow != Blow.None;
            bool defenderWon = fight.Loser == attacker && fight.Blow != Blow.None;
            var defenderStats = fight.Defender == defender ? fight.DefenderStats : fight.AttackerStats;
            Combatant(cell.rectTransform, defender, new Vector2(.7f, .05f), new Vector2(.99f, .95f), skin, tag, defenderStats.attack + "/" + defenderStats.defense, defenderWon, defenderFell, settle + DiceSpin);
            // The dice: attacker's total on top, defender's below, each as attack + die = total.
            var attackerColor = attacker.Owner == 0 ? skin.colors.teal : skin.colors.gold;
            var defenderColor = defender.Owner == 0 ? skin.colors.teal : skin.colors.gold;
            DiceLine(cell.rectTransform, attacker.Card.name, fight.AttackerStats.attack, fight.AttackerRoll, fight.AttackerTotal, attackerColor, new Vector2(.02f, .64f), new Vector2(.68f, .96f), settle, skin);
            var versus = Label(cell.rectTransform, "VS", 15, skin.colors.muted, new Vector2(.02f, .5f), new Vector2(.68f, .64f));
            if (skin.typography.mastheadFont != null) versus.font = skin.typography.mastheadFont;
            DiceLine(cell.rectTransform, defender.Card.name, fight.DefenderStats.attack, fight.DefenderRoll, fight.DefenderTotal, defenderColor, new Vector2(.02f, .3f), new Vector2(.68f, .5f), settle + .15f, skin);
            // The blow, stamped across the foot of the duel once the second die is down.
            string verdict = fight.Loser == null ? "STAND-OFF" : (fight.Loser == defender ? defender.Card.name : attacker.Card.name).ToUpperInvariant() + "  ·  " +
                (fight.Blow == Blow.Killed ? (fight.Loser.IsCharacter ? "STRUCK DOWN" : "KILLED") : fight.Blow == Blow.Wounded ? "WOUNDED" : "TAPPED");
            var color = fight.Loser == null ? skin.colors.muted : fight.Blow == Blow.Killed ? Blood : fight.Blow == Blow.Wounded ? Bruise : Bone;
            var stamp = Label(cell.rectTransform, verdict, 19, color, new Vector2(.02f, .04f), new Vector2(.68f, .3f));
            stamp.fontStyle = FontStyle.Bold;
            stamp.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .9f);
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, fight.Loser == null ? 0 : -4);
            stamp.gameObject.AddComponent<CanvasGroup>().alpha = 0;
            stamps.Add((stamp.rectTransform, settle + .15f + DiceSpin, color));
            if (fight.Loser != null)
            {
                var margin = Label(cell.rectTransform, fight.Margin + " against defense " + (fight.Loser == fight.Attacker ? fight.AttackerStats.defense : fight.DefenderStats.defense), 10, skin.colors.muted, new Vector2(.02f, 0), new Vector2(.68f, .1f));
                var reveal = margin.gameObject.AddComponent<CanvasGroup>(); reveal.alpha = 0; reveals.Add((reveal, settle + .15f + DiceSpin));
                // Light bursts from whoever took the blow: blood-red for a kill, amber for a wound, bone-pale for a tap.
                var flash = fight.Loser == defender ? Flash(cell.transform) : Flash(row);
                flash.Configure(color, fight.Loser == defender ? .85f : .1f, false);
                flashes.Add((flash, settle + .15f + DiceSpin));
            }
        }
    }

    static string AttackerStats(MatchRules.Clash clash, MatchRules rules)
    {
        var fight = clash.Fights.FirstOrDefault();
        if (fight != null) return fight.AttackerStats.attack + "/" + fight.AttackerStats.defense;
        var stats = rules.Stats(clash.Attacker);
        return stats.attack + "/" + stats.defense;
    }

    // A unit's portrait in its company's colour, breathing gold when it won its duel, dimmed and
    // red-lit when it fell, with its name and the numbers it fought with beneath.
    void Combatant(RectTransform parent, MatchRules.Unit unit, Vector2 min, Vector2 max, BoardSkin skin, string tag, string stats, bool won, bool fell, float at)
    {
        var accent = unit.Owner == 0 ? skin.colors.teal : skin.colors.gold;
        var frame = BoardPresentation.Panel(parent, "Combatant", new Color(.02f, .015f, .01f, .95f));
        BoardPresentation.Stretch(frame.rectTransform, min, max);
        var portrait = BoardPresentation.Panel(frame.transform, "Portrait", Color.white);
        BoardPresentation.Stretch(portrait.rectTransform, new Vector2(0, .3f), new Vector2(1, 1));
        portrait.rectTransform.offsetMin = new Vector2(4, 2); portrait.rectTransform.offsetMax = new Vector2(-4, -4);
        portrait.sprite = TravelBanner.Artwork(unit.Card); portrait.preserveAspect = true;
        if (portrait.sprite == null) portrait.color = new Color(.2f, .22f, .25f);
        else { var motion = portrait.gameObject.AddComponent<ZoomImage>(); motion.EnableHoverMotion(); motion.SetHovering(true); motion.SetMotionPhase(at * 3.1f); }
        BoardPresentation.Border(frame.rectTransform, accent, 2);
        var aura = FantasyCardAura.Create(frame.rectTransform);
        aura.SetPresentation(won ? .9f : fell ? .6f : .3f, won ? BoardBattleVfx.Amber : fell ? BoardBattleVfx.Ember : accent, won);
        if (fell) StartCoroutine(Dim(portrait, at));
        var name = Label(frame.transform, unit.Card.name, 13, skin.colors.ivory, new Vector2(.02f, .15f), new Vector2(.98f, .3f));
        name.fontStyle = FontStyle.Bold;
        string line = stats != null ? stats.Replace("/", "<sprite name=\"attack\"> ") + "<sprite name=\"defense\">" : "";
        if (tag != null) line += (line.Length > 0 ? "  ·  " : "") + tag;
        if (line.Length > 0) Rich(frame.transform, line, 12, tag != null ? Bruise : skin.colors.muted, new Vector2(.02f, 0), new Vector2(.98f, .15f));
    }

    // A fallen unit's portrait goes cold and red once the blow has landed.
    System.Collections.IEnumerator Dim(Image portrait, float at)
    {
        while (portrait != null && Time.unscaledTime < at && !dealt) yield return null;
        float t = 0;
        while (portrait != null && t < 1) { t += Time.unscaledDeltaTime * 2.5f; portrait.color = Color.Lerp(Color.white, new Color(.55f, .3f, .3f), t); yield return null; }
    }

    void DiceLine(RectTransform parent, string who, int attack, int roll, int total, Color color, Vector2 min, Vector2 max, float settleAt, BoardSkin skin)
    {
        float w = max.x - min.x;
        var name = Label(parent, who.ToUpperInvariant(), 10, color, new Vector2(min.x, min.y), new Vector2(min.x + w * .3f, max.y));
        name.alignment = TextAnchor.MiddleLeft;
        Rich(parent, attack + "<sprite name=\"attack\">", 17, skin.colors.ivory, new Vector2(min.x + w * .3f, min.y), new Vector2(min.x + w * .47f, max.y));
        Label(parent, "+", 15, skin.colors.muted, new Vector2(min.x + w * .47f, min.y), new Vector2(min.x + w * .53f, max.y));
        // The die: an ivory pip die that tumbles until it settles.
        var die = new Die();
        var face = BoardPresentation.Panel(parent, "Die", new Color(.94f, .89f, .76f));
        var slot = new GameObject("Die slot", typeof(RectTransform)).GetComponent<RectTransform>();
        slot.SetParent(parent, false);
        BoardPresentation.Stretch(slot, new Vector2(min.x + w * .54f, min.y), new Vector2(min.x + w * .7f, max.y));
        face.rectTransform.SetParent(slot, false);
        face.rectTransform.anchorMin = face.rectTransform.anchorMax = face.rectTransform.pivot = Vector2.one * .5f;
        face.rectTransform.sizeDelta = Vector2.one * 34;
        BoardPresentation.Border(face.rectTransform, new Color(.45f, .32f, .16f), 2);
        for (int i = 0; i < 9; i++)
        {
            var pip = BoardPresentation.Panel(face.transform, "Pip", new Color(.12f, .08f, .05f));
            pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(.22f + (i % 3) * .28f, .78f - (i / 3) * .28f);
            pip.rectTransform.sizeDelta = Vector2.one * 6;
            die.pips[i] = pip;
        }
        die.rect = face.rectTransform; die.final = roll; die.settleAt = settleAt + DiceSpin; die.Show(Random.Range(1, 7));
        dice.Add(die);
        Label(parent, "=", 15, skin.colors.muted, new Vector2(min.x + w * .71f, min.y), new Vector2(min.x + w * .77f, max.y));
        var sum = Label(parent, total.ToString(), 26, color, new Vector2(min.x + w * .78f, min.y), new Vector2(min.x + w, max.y));
        sum.fontStyle = FontStyle.Bold;
        sum.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .8f);
        var reveal = sum.gameObject.AddComponent<CanvasGroup>(); reveal.alpha = 0;
        reveals.Add((reveal, settleAt + DiceSpin));
    }

    Text Label(Transform parent, string text, int size, Color color, Vector2 min, Vector2 max)
    {
        var label = BoardPresentation.TextLabel(parent, text, board.interfaceFont, size, color, min, max, TextAnchor.MiddleCenter);
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 7; label.resizeTextMaxSize = size;
        return label;
    }

    /// <summary>A light layer over a panel: its own graphic, since a GameObject carries one graphic only.</summary>
    static ClashFlash Flash(Transform parent)
    {
        var go = new GameObject("Clash flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(ClashFlash));
        go.transform.SetParent(parent, false);
        BoardPresentation.Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one);
        return go.GetComponent<ClashFlash>();
    }

    /// <summary>A TMP label with the card icon sheet, stretched into the given anchors.</summary>
    TextMeshProUGUI Rich(Transform parent, string text, float size, Color color, Vector2 min, Vector2 max)
    {
        var label = TravelBanner.RichLabel(parent, text, board, size, color);
        BoardPresentation.Stretch(label.rectTransform, min, max);
        return label;
    }

    /// <summary>
    /// Light on the battle: a lantern glow that hangs over the plate, or the burst that bursts from a
    /// unit when a blow lands — an expanding ring, rays, and sparks that fade over a second.
    /// </summary>
    sealed class ClashFlash : MaskableGraphic
    {
        Color tint; float x; bool lantern; float start = float.MaxValue, now;
        public void Configure(Color color, float atX, bool ambient) { tint = color; x = atX; lantern = ambient; raycastTarget = false; }
        public void Tick(float at, float time) { start = at; now = time; SetVerticesDirty(); }
        void Update() { if (lantern) { now = Time.unscaledTime; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (lantern)
            {
                // Two lanterns at the head of the plate, their light breathing over the title.
                float pulse = .5f + .12f * Mathf.Sin(now * 1.7f);
                foreach (float side in new[] { r.xMin + 22, r.xMax - 22 })
                {
                    var p = new Vector2(side, r.yMax - 22);
                    FantasyVfxMesh.Disc(vh, p, 120, FantasyVfxMesh.Alpha(tint, .12f * pulse));
                    FantasyVfxMesh.Spark(vh, p, 3 + Mathf.Sin(now * 5 + side) * .5f, Color.Lerp(tint, Color.white, .5f), pulse);
                }
                return;
            }
            float t = now - start;
            if (t < 0 || t > 1.1f) return;
            float alpha = 1 - t / 1.1f;
            var c = new Vector2(r.xMin + r.width * x, r.center.y);
            float radius = 20 + t * 140;
            FantasyVfxMesh.Ring(vh, c, radius, 2, tint, alpha * .9f);
            FantasyVfxMesh.Disc(vh, c, 60 * (1 - t) + 10, FantasyVfxMesh.Alpha(tint, alpha * .5f));
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5 + start;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                FantasyVfxMesh.GlowLine(vh, c + d * (radius * .4f), c + d * (radius * .4f + 30 + 40 * t), 1.5f, tint, alpha);
                FantasyVfxMesh.Spark(vh, c + d * (radius * .55f + 30 * t), 2.5f * alpha + .5f, Color.Lerp(tint, Color.white, .4f), alpha);
            }
        }
    }

    void OnDisable() { if (panel != null) { BoardCardPreview.UnregisterModal((RectTransform)panel.transform); Destroy(panel); } panel = null; }
}
