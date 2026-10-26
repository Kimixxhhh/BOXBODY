using System;
using System.Collections.Generic;
using FieldKit.Items;

namespace FieldKit.Weapons;

public enum FireMode { SemiAuto, Automatic }

// Units and interpretation are shared by the standalone editor and the game.
public sealed record WeaponStats
{
    public const double DefaultReloadDuration = 2.65;
    // Existing version 4 AK configurations use medium ammunition by default.
    public AmmoType AmmoType { get; set; } = AmmoType.Medium;
    public required int Price { get; set; }
    public required double Damage { get; set; }
    public required double EnemySlowPercent { get; set; }
    public required double KnockbackSpeed { get; set; }
    public required double FireCooldown { get; set; }
    public required FireMode FireMode { get; set; }
    public required int MagazineCapacity { get; set; }
    public double ReloadDuration { get; set; } = DefaultReloadDuration;
    public double EquipDuration { get; set; } = 0.35;
    public double HolsterDuration { get; set; } = 0.25;
    public required double MovementInertiaPercent { get; set; }
    public required double TurnFollowSpeed { get; set; }
    public required double ProjectileSpeed { get; set; }
    public required double SpreadMax { get; set; }
    public required double SpreadMin { get; set; }
    public required double SpreadPerShot { get; set; }
    public required double SpreadRecovery { get; set; }

    // Helpers expose the exact gameplay contract without adding a combat system.
    public double MovementSpeedMultiplier() => 1 - MovementInertiaPercent / 100;
    public double TurnFollowWeight(double delta) => 1 - Math.Exp(-Math.Max(0, delta) * TurnFollowSpeed);
    public double EnemySpeedMultiplier() => 1 - EnemySlowPercent / 100;
    public double ShotsPerMinute() => 60 / FireCooldown;
    public double SpreadAfterShot(double spread) => Math.Clamp(spread + SpreadPerShot, SpreadMin, SpreadMax);
    public double RecoverSpread(double spread, double previousTimeSinceShot, double delta)
    {
        // Only the portion of this frame after cooldown contributes to recovery.
        double activeTime = Math.Max(0, previousTimeSinceShot + delta - FireCooldown)
            - Math.Max(0, previousTimeSinceShot - FireCooldown);
        return Math.Clamp(spread - SpreadRecovery * Math.Max(0, activeTime), SpreadMin, SpreadMax);
    }
}

public sealed record WeaponDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string ScenePath { get; init; }
    public string GunshotSoundPath { get; init; } = "";
    public double GunshotVolumePercent { get; init; } = 100;
    public List<ReloadSoundCue> ReloadSounds { get; init; } = new();
    public required WeaponStats Stats { get; set; }
}

// Delay is measured from reload start, in seconds, not from the previous cue.
public sealed record ReloadSoundCue
{
    public required string SoundPath { get; init; }
    public required double DelaySeconds { get; init; }
    public double VolumePercent { get; init; } = 100;
}

public sealed record NumericWeaponField(string Key, string Label, string Unit, string Hint,
    double Min, double Max, bool Integer, Func<WeaponStats, double> Read, Action<WeaponStats, double> Write);

public static class WeaponFields
{
    public static readonly IReadOnlyList<NumericWeaponField> All = new NumericWeaponField[] {
        new("price", "购买价格", "$", "在武器箱购买一把武器的费用，必须为非负整数。", 0, 1000000, true, s => s.Price, (s,v) => s.Price = (int)v),
        new("damage", "伤害值", "点", "单次命中的基础伤害。", 0, 1000000, false, s => s.Damage, (s,v) => s.Damage = v),
        new("enemySlowPercent", "减速值", "%", "敌人移速降低比例；0 为不减速，100 为停止。", 0, 100, false, s => s.EnemySlowPercent, (s,v) => s.EnemySlowPercent = v),
        new("knockbackSpeed", "击退值", "米/秒", "命中时，沿子弹飞行方向施加的击退速度。", 0, 10000, false, s => s.KnockbackSpeed, (s,v) => s.KnockbackSpeed = v),
        new("fireCooldown", "开火后冷却时间", "秒", "两次开火的最短间隔；射速 = 60 ÷ 冷却时间。", .001, 3600, false, s => s.FireCooldown, (s,v) => s.FireCooldown = v),
        new("magazineCapacity", "弹夹容量", "发", "每个弹夹可装填的子弹数，必须为整数。", 1, 100000, true, s => s.MagazineCapacity, (s,v) => s.MagazineCapacity = (int)v),
        new("reloadDuration", "换弹时间", "秒", "越小换弹越快，整段动画同步变速；默认 2.65 秒。", .001, 3600, false, s => s.ReloadDuration, (s,v) => s.ReloadDuration = v),
        new("equipDuration", "切枪 / 拿出武器时间", "秒", "从收起位置旋转到持枪姿势的时间；切换武器时使用新枪的该时长。", .001, 3600, false, s => s.EquipDuration, (s,v) => s.EquipDuration = v),
        new("holsterDuration", "收回武器时间", "秒", "从持枪姿势旋转到收起位置的时间；切换武器时先按旧枪的该时长收起。", .001, 3600, false, s => s.HolsterDuration, (s,v) => s.HolsterDuration = v),
        new("movementInertiaPercent", "移动惯性", "%", "持有者移速降低比例；0 为原速，100 为无法移动。", 0, 100, false, s => s.MovementInertiaPercent, (s,v) => s.MovementInertiaPercent = v),
        new("turnFollowSpeed", "转动惯性", "系数", "1 − Exp(−delta × 系数)；值越大跟随越快，0 为不转向。", 0, 1000, false, s => s.TurnFollowSpeed, (s,v) => s.TurnFollowSpeed = v),
        new("projectileSpeed", "子弹飞行速度", "米/秒", "子弹沿发射方向前进的速度。", .001, 100000, false, s => s.ProjectileSpeed, (s,v) => s.ProjectileSpeed = v),
        new("spreadMax", "子弹散布最大值", "度", "偏离瞄准方向的最大半角，不低于最小值。", 0, 180, false, s => s.SpreadMax, (s,v) => s.SpreadMax = v),
        new("spreadMin", "子弹散布最小值", "度", "完全恢复后的基础散布半角。", 0, 180, false, s => s.SpreadMin, (s,v) => s.SpreadMin = v),
        new("spreadPerShot", "子弹散布增加值", "度/发", "每开一枪增加的角度，达到最大值后不再增加。", 0, 180, false, s => s.SpreadPerShot, (s,v) => s.SpreadPerShot = v),
        new("spreadRecovery", "散布减少值", "度/秒", "停止开火且经过冷却时间后，每秒恢复的角度。", 0, 10000, false, s => s.SpreadRecovery, (s,v) => s.SpreadRecovery = v)
    };

    public static List<string> Validate(WeaponStats stats)
    {
        var errors = new List<string>();
        foreach (var field in All)
        {
            double value = field.Read(stats);
            if (!double.IsFinite(value) || value < field.Min || value > field.Max || (field.Integer && value != Math.Truncate(value)))
                errors.Add($"{field.Label}必须在 {field.Min} 至 {field.Max} 之间{(field.Integer ? "，且为整数" : "")}。");
        }
        if (!Enum.IsDefined(stats.FireMode)) errors.Add("开火模式无效。");
        if (!Enum.IsDefined(stats.AmmoType)) errors.Add("弹药类型无效。");
        if (stats.SpreadMin > stats.SpreadMax) errors.Add("子弹散布最小值不能大于最大值。");
        return errors;
    }
}

