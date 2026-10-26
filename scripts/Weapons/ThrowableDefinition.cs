using System;
using System.Collections.Generic;

namespace FieldKit.Weapons;

// A data-driven throwable template. The game uses these values when a grenade
// is thrown; the standalone editor and the game share this type and validation.
public sealed record ThrowableDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Category { get; init; } = "投掷物";
    public required int Price { get; set; }
    // How many throwables one purchased stack fills into a small item slot,
    // mirroring the fixed "40 rounds per pack" model of ammunition.
    public int SlotCapacity { get; set; } = 1;
    public required double Damage { get; set; }
    public required double Range { get; set; }
    public required double EnemySlowPercent { get; set; }
    public required double SlowDuration { get; set; }
    public required double KnockbackSpeed { get; set; }
    public required double FuseTime { get; set; }
    public required string ItemScenePath { get; init; }
    public string ExplosionSoundPath { get; init; } = "";

    public double EnemySpeedMultiplier() => 1 - EnemySlowPercent / 100;
}

public sealed record NumericThrowableField(string Key, string Label, string Unit, string Hint,
    double Min, double Max, bool Integer,
    Func<ThrowableDefinition, double> Read, Action<ThrowableDefinition, double> Write);

public static class ThrowableFields
{
    public static readonly IReadOnlyList<NumericThrowableField> All = new NumericThrowableField[] {
        new("price", "购买价格", "$", "在武器箱购买一份（一个物品槽位）的费用，必须为非负整数。", 0, 1000000, true,
            t => t.Price, (t, v) => t.Price = (int)v),
        new("slotCapacity", "每格数量", "枚", "一个物品槽位可容纳的数量；一次购买放入这么多枚，与子弹的每份数量相同。", 1, 1000, true,
            t => t.SlotCapacity, (t, v) => t.SlotCapacity = (int)v),
        new("damage", "伤害值", "点", "爆炸范围内每个目标受到的基础伤害。", 0, 1000000, false,
            t => t.Damage, (t, v) => t.Damage = v),
        new("range", "范围", "米", "爆炸作用半径；范围内的目标都会受到伤害、减速和击退。", .1, 1000, false,
            t => t.Range, (t, v) => t.Range = v),
        new("enemySlowPercent", "减速值", "%", "爆炸后敌人移速降低比例；0 为不减速，100 为停止。", 0, 100, false,
            t => t.EnemySlowPercent, (t, v) => t.EnemySlowPercent = v),
        new("slowDuration", "减速时间", "秒", "减速效果持续的时间；0 表示不持续。", 0, 3600, false,
            t => t.SlowDuration, (t, v) => t.SlowDuration = v),
        new("knockbackSpeed", "击退值", "米/秒", "沿爆炸中心指向目标的方向施加的击退速度。", 0, 10000, false,
            t => t.KnockbackSpeed, (t, v) => t.KnockbackSpeed = v),
        new("fuseTime", "引信时间", "秒", "从投出到爆炸的时间；飞行途中也会继续计时。", .05, 3600, false,
            t => t.FuseTime, (t, v) => t.FuseTime = v)
    };

    public static List<string> Validate(ThrowableDefinition throwable)
    {
        var errors = new List<string>();
        foreach (var field in All)
        {
            double value = field.Read(throwable);
            if (!double.IsFinite(value) || value < field.Min || value > field.Max || (field.Integer && value != Math.Truncate(value)))
                errors.Add($"{field.Label}必须在 {field.Min} 至 {field.Max} 之间{(field.Integer ? "，且为整数" : "")}。");
        }
        return errors;
    }
}
