using System;
using UnityEngine;

/// <summary>One player's floating materials. Payment is checked in full before any deduction.</summary>
public sealed class PlayerMaterials
{
    public static readonly string[] Names = { "Leather", "Mounts", "Timber", "Iron", "Steel", "Mithril", "Gold" };
    private readonly int[] amounts = new int[7];
    public int this[int index] => amounts[index];
    public void Clear() => Array.Clear(amounts, 0, amounts.Length);
    public void Grant(CardData land)
    {
        if (land == null || land.GetCardType() != CardTypeEnum.Land) return;
        int[] grants = { land.leatherGranted, land.mountsGranted, land.timberGranted, land.ironGranted,
            land.steelGranted, land.mithrilGranted, land.goldGranted };
        for (int i = 0; i < amounts.Length; i++) amounts[i] += Mathf.Max(0, grants[i]);
    }
    public bool CanAfford(CardData card) => CheckPayment(card, false);
    public bool TrySpend(CardData card) => CheckPayment(card, true);
    private bool CheckPayment(CardData card, bool spend)
    {
        if (card == null) return false;
        int[] costs = { card.leatherRequired, card.mountsRequired, card.timberRequired, card.ironRequired,
            card.steelRequired, card.mithrilRequired, card.GetTotalGoldCost() };
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
