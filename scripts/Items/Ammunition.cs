using System;
using System.Collections.Generic;
using System.Linq;

namespace FieldKit.Items;

public enum AmmoType { Medium }

public sealed record AmmoDefinition(string Id, string Name, AmmoType AmmoType, int TotalAmmo, int Price)
{
    public string IconScenePath { get; init; } = "";
}

public static class ItemCatalog
{
    public static readonly AmmoDefinition MediumAmmo = new("medium_ammo", "中型子弹", AmmoType.Medium, 40, 250) {
        IconScenePath = "res://assets/weapons/ammo/bullet_ak_triple.glb"
    };
    public static IReadOnlyList<AmmoDefinition> Ammunition { get; } = Array.AsReadOnly(new[] { MediumAmmo });
    public static AmmoDefinition? Find(string id) => Ammunition.FirstOrDefault(item => item.Id == id);
    public static string AmmoName(AmmoType type) => Ammunition.FirstOrDefault(item => item.AmmoType == type)?.Name ?? "未知弹药";
}

// Each purchase creates an independent pack; its total capacity never shrinks.
public sealed class AmmoItem
{
    public AmmoDefinition Definition { get; }
    public int TotalAmmo => Definition.TotalAmmo;
    public int RemainingAmmo { get; private set; }

    public AmmoItem(AmmoDefinition definition)
    {
        if (definition.TotalAmmo <= 0 || definition.Price < 0) throw new ArgumentOutOfRangeException(nameof(definition));
        Definition = definition;
        RemainingAmmo = TotalAmmo;
    }

    internal int Take(int requested)
    {
        int amount = Math.Min(Math.Max(0, requested), RemainingAmmo);
        RemainingAmmo -= amount;
        return amount;
    }
}

public sealed class AmmoBackpack
{
    public const int Capacity = 16;
    private readonly AmmoItem?[] _slots = new AmmoItem?[Capacity];
    public IReadOnlyList<AmmoItem?> Slots { get; }
    public int Count => _slots.Count(item => item != null);
    public bool IsFull => Count == Capacity;

    public AmmoBackpack() => Slots = Array.AsReadOnly(_slots);

    internal bool TryAdd(AmmoDefinition definition)
    {
        int slot = Array.IndexOf(_slots, null);
        if (slot < 0) return false;
        _slots[slot] = new AmmoItem(definition);
        return true;
    }

    public int Available(AmmoType type) => _slots.Where(item => item?.Definition.AmmoType == type).Sum(item => item!.RemainingAmmo);

    internal bool TryMove(int from, int to, AmmoItem expectedItem)
    {
        if (from < 0 || from >= Capacity || to < 0 || to >= Capacity ||
            expectedItem == null || !ReferenceEquals(_slots[from], expectedItem)) return false;
        // Move the actual instances, including their remaining ammunition. A full
        // target swaps places; a stale drag can never move a replacement item.
        (_slots[from], _slots[to]) = (_slots[to], _slots[from]);
        return true;
    }

    // Consume matching packs in slot order, preserving partially used packs.
    internal int Take(AmmoType type, int requested)
    {
        int taken = 0;
        for (int i = 0; i < _slots.Length && taken < requested; i++)
        {
            var item = _slots[i];
            if (item == null || item.Definition.AmmoType != type) continue;
            taken += item.Take(requested - taken);
            if (item.RemainingAmmo == 0) _slots[i] = null;
        }
        return taken;
    }
}
