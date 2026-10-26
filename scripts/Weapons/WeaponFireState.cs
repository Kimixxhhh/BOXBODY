using System;

namespace FieldKit.Weapons;

// One state per inventory slot: holstering cannot reset cooldown or accumulated spread.
public sealed class WeaponFireState
{
    public double NextShotTime { get; private set; }
    public double Spread { get; private set; }
    private double _updatedAt, _lastShotAt;
    private bool _hasShot;

    public void Advance(double time, WeaponStats stats)
    {
        if (!_hasShot) Spread = stats.SpreadMin;
        else Spread = stats.RecoverSpread(Spread, Math.Max(0, _updatedAt - _lastShotAt), Math.Max(0, time - _updatedAt));
        _updatedAt = time;
    }

    public void RecordShot(double time, WeaponStats stats)
    {
        Advance(time, stats);
        Spread = stats.SpreadAfterShot(Spread);
        _hasShot = true;
        _lastShotAt = time;
        NextShotTime = time + stats.FireCooldown;
    }
}
