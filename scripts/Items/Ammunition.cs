using System;
using System.Collections.Generic;
using System.Linq;
using FieldKit.Weapons;

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

// Shared view of everything that can occupy one of the sixteen small slots.
public interface IBackpackItem
{
    string Id { get; }
    string Name { get; }
    string IconScenePath { get; }
    string AmountText { get; }
    string HoverText { get; }
}

// Each purchase creates an independent pack; its total capacity never shrinks.
public sealed class AmmoItem : IBackpackItem
{
    public AmmoDefinition Definition { get; }
    public int TotalAmmo => Definition.TotalAmmo;
    public int RemainingAmmo { get; private set; }
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string IconScenePath => Definition.IconScenePath;
    public string AmountText => $"{RemainingAmmo} / {TotalAmmo}";
    public string HoverText => $"{Definition.Name} · 总弹药量 {TotalAmmo} 发 · 剩余弹药量 {RemainingAmmo} 发";

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

// One purchase fills one small slot with the template's slot capacity. Every
// throw removes one item; the slot itself is only released with the last one.
public sealed class ThrowableItem : IBackpackItem
{
    public ThrowableDefinition Definition { get; }
    public int Capacity { get; }
    public int Remaining { get; private set; }
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string IconScenePath => Definition.ItemScenePath;
    public string AmountText => $"{Remaining} / {Capacity}";
    public string HoverText =>
        $"{Definition.Name} · 每格 {Capacity} 枚 · 剩余 {Remaining} 枚 · " +
        $"伤害 {Definition.Damage:0.##} · 范围 {Definition.Range:0.##} 米 · " +
        $"减速 {Definition.EnemySlowPercent:0.##}% / {Definition.SlowDuration:0.##} 秒 · " +
        $"击退 {Definition.KnockbackSpeed:0.##} 米/秒 · 引信 {Definition.FuseTime:0.##} 秒";

    public ThrowableItem(ThrowableDefinition definition)
    {
        if (definition.SlotCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(definition));
        Definition = definition;
        Capacity = definition.SlotCapacity;
        Remaining = Capacity;
    }

    internal bool TakeOne()
    {
        if (Remaining <= 0) return false;
        Remaining--;
        return true;
    }
}

// One entry per throwable kind in the backpack, with the total remaining count
// across every stack of that kind. The hold-G wheel draws one sector per entry.
public sealed record ThrowableKind(string Id, string Name, int Remaining, string IconScenePath);

public sealed class ItemBackpack
{
    public const int Capacity = 16;
    private readonly IBackpackItem?[] _slots = new IBackpackItem?[Capacity];
    public IReadOnlyList<IBackpackItem?> Slots { get; }
    public int Count => _slots.Count(item => item != null);
    public bool IsFull => Count == Capacity;
    public int ThrowableCount => _slots.Count(item => item is ThrowableItem);
    // Total throwables across all stacks, used by the HUD prompt.
    public int ThrowableAmount => _slots.OfType<ThrowableItem>().Sum(item => item.Remaining);

    public ItemBackpack() => Slots = Array.AsReadOnly(_slots);

    internal bool TryAddAmmo(AmmoDefinition definition) => TryAdd(new AmmoItem(definition));

    internal bool TryAddThrowable(ThrowableDefinition definition) => TryAdd(new ThrowableItem(definition));

    private bool TryAdd(IBackpackItem item)
    {
        int slot = Array.IndexOf(_slots, null);
        if (slot < 0) return false;
        _slots[slot] = item;
        return true;
    }

    public int Available(AmmoType type) => _slots.Where(item => item is AmmoItem ammo && ammo.Definition.AmmoType == type)
        .Sum(item => ((AmmoItem)item!).RemainingAmmo);

    internal bool TryMove(int from, int to, IBackpackItem expectedItem)
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
            var item = _slots[i] as AmmoItem;
            if (item == null || item.Definition.AmmoType != type) continue;
            taken += item.Take(requested - taken);
            if (item.RemainingAmmo == 0) _slots[i] = null;
        }
        return taken;
    }

    // The first throwable stack in slot order can be drawn into the hand; a
    // single item is only consumed after the throw animation has released it.
    // Passing an id picks the first stack of that kind instead.
    internal bool TryPeekThrowable(out ThrowableItem? item, string? id = null)
    {
        item = _slots.OfType<ThrowableItem>().FirstOrDefault(entry =>
            entry.Remaining > 0 && (id == null || entry.Id == id));
        return item != null;
    }

    // Distinct throwable kinds in first-slot order, with their remaining totals.
    public IReadOnlyList<ThrowableKind> ThrowableKinds()
    {
        var kinds = new List<ThrowableKind>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in _slots)
        {
            if (slot is not ThrowableItem item || !seen.Add(item.Id)) continue;
            int remaining = _slots.OfType<ThrowableItem>()
                .Where(entry => entry.Id == item.Id).Sum(entry => entry.Remaining);
            kinds.Add(new ThrowableKind(item.Id, item.Name, remaining, item.IconScenePath));
        }
        return kinds;
    }

    // Consume exactly one throwable from the stack that was drawn into the hand,
    // even if the player rearranged the backpack while holding it. The slot is
    // released with the last item, matching ammunition packs.
    internal bool TryConsumeThrowable(ThrowableItem expected)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!ReferenceEquals(_slots[i], expected)) continue;
            if (!expected.TakeOne()) return false;
            if (expected.Remaining == 0) _slots[i] = null;
            return true;
        }
        return false;
    }
}
