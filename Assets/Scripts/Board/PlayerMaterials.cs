using System;
using System.Linq;
using UnityEngine;

/// <summary>One player's floating materials. Payment is checked in full before any deduction.</summary>
public sealed class PlayerMaterials
{
    public static readonly string[] Names = { "Leather", "Mounts", "Timber", "Iron", "Steel", "Mithril", "Gold" };
    private readonly int[] amounts = new int[7];
    public int this[int index] => amounts[index];
    public void Clear() => Array.Clear(amounts, 0, amounts.Length);
    public PlayerMaterials Copy()
    {
        var copy = new PlayerMaterials();
        Array.Copy(amounts, copy.amounts, amounts.Length);
        return copy;
    }
    /// <summary>What left the pool since <paramref name="before"/>: the exact split a joker cost was paid in.</summary>
    public PlayerMaterials SpentSince(PlayerMaterials before)
    {
        var spent = new PlayerMaterials();
        for (int i = 0; i < amounts.Length; i++) spent.amounts[i] = Mathf.Max(0, before.amounts[i] - amounts[i]);
        return spent;
    }
    /// <summary>Puts a measured spend back. Additive, so lands tapped in the meantime keep their mana.</summary>
    public void Refund(PlayerMaterials spent)
    {
        for (int i = 0; i < amounts.Length; i++) amounts[i] += spent.amounts[i];
    }
    public void Grant(CardData land)
    {
        if (land == null || land.GetCardType() != CardTypeEnum.Land) return;
        var grants = Grants(land);
        for (int i = 0; i < amounts.Length; i++) amounts[i] += Mathf.Max(0, grants[i]);
    }
    public bool CanAfford(CardData card) => CheckPayment(card, false);
    public bool TrySpend(CardData card) => CheckPayment(card, true);
    static int[] Costs(CardData card) => new[] { card.leatherRequired, card.mountsRequired, card.timberRequired, card.ironRequired,
        card.steelRequired, card.mithrilRequired, card.GetTotalGoldCost() };
    static int[] Grants(CardData land) => new[] { land.leatherGranted, land.mountsGranted, land.timberGranted, land.ironGranted,
        land.steelGranted, land.mithrilGranted, land.goldGranted };
    // What the pool still owes on a card: per material, then the generic (joker) part not covered by leftovers.
    private void Owed(CardData card, int[] owed, out int generic)
    {
        var costs = Costs(card);
        int leftover = 0;
        for (int i = 0; i < amounts.Length; i++)
        {
            owed[i] = Mathf.Max(0, Mathf.Max(0, costs[i]) - amounts[i]);
            leftover += Mathf.Max(0, amounts[i] - Mathf.Max(0, costs[i]));
        }
        generic = Mathf.Max(0, Mathf.Max(0, card.jokerRequired) - leftover);
    }
    /// <summary>"2 leather, 1 gold and 1 of any material": what the pool is short of for a card, or null when it can pay.</summary>
    public string Shortfall(CardData card)
    {
        if (card == null || CanAfford(card)) return null;
        var owed = new int[amounts.Length];
        Owed(card, owed, out int generic);
        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < owed.Length; i++) if (owed[i] > 0) parts.Add(owed[i] + " " + Names[i].ToLowerInvariant());
        if (generic > 0) parts.Add(generic + " of any material");
        if (parts.Count == 0) return "materials";
        if (parts.Count == 1) return parts[0];
        return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
    }
    /// <summary>How much of what a card still owes this land's grant would cover. Zero means tapping it brings the card no closer.</summary>
    public int Help(CardData land, CardData card)
    {
        if (land == null || card == null || land.GetCardType() != CardTypeEnum.Land) return 0;
        var owed = new int[amounts.Length];
        Owed(card, owed, out int generic);
        var grants = Grants(land);
        int help = 0, spare = 0;
        for (int i = 0; i < owed.Length; i++)
        {
            int grant = Mathf.Max(0, grants[i]);
            int used = Mathf.Min(owed[i], grant);
            help += used; spare += grant - used;
        }
        return help + Mathf.Min(generic, spare);
    }
    private bool CheckPayment(CardData card, bool spend)
    {
        if (card == null) return false;
        var costs = Costs(card);
        var remaining = new int[amounts.Length];
        int total = 0;
        for (int i = 0; i < amounts.Length; i++)
        {
            remaining[i] = amounts[i] - Mathf.Max(0, costs[i]);
            if (remaining[i] < 0) return false;
            total += remaining[i];
        }
        // Joker requirements are generic costs, paid from leftovers in the displayed order.
        int generic = Mathf.Max(0, card.jokerRequired);
        if (total < generic) return false;
        if (!spend) return true;
        for (int i = 0; i < amounts.Length; i++)
        {
            int paid = Mathf.Min(remaining[i], generic);
            amounts[i] = remaining[i] - paid;
            generic -= paid;
        }
        return true;
    }
}
