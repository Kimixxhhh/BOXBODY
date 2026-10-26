using Godot;
using System;
using System.Linq;
using FieldKit.Weapons;

public partial class PlayerController
{
    private readonly WeaponFireState[] _fireStates = Enumerable.Range(0, WeaponSlotCount).Select(_ => new WeaponFireState()).ToArray();
    private readonly RandomNumberGenerator _shotRandom = new();
    private double _combatTime;
    private bool _triggerHeld, _triggerPressed;
    public event Action<Projectile>? ShotFired;
    public event Action? TargetHit;
    public double CurrentSpread => ActiveWeaponSlot < 0 ? 0 : _fireStates[ActiveWeaponSlot].Spread;

    private void ReleaseTrigger() { _triggerHeld = false; _triggerPressed = false; }

    public override void _Input(InputEvent @event)
    {
        // Preserve a queued click even if press and release arrive before the next physics tick.
        if (@event.IsActionReleased("fire")) _triggerHeld = false;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut)
        {
            ReleaseTrigger();
            CancelThrowSelection();
        }
    }

    private void UpdateFiring(double delta)
    {
        double frameStart = _combatTime;
        _combatTime += delta;
        bool pressed = _triggerPressed;
        _triggerPressed = false;
        if (InputBlocked) ReleaseTrigger();
        if (IsThrowSelecting)
        {
            ReleaseTrigger();
            for (int slot = 0; slot < WeaponSlotCount; slot++)
                if (_weaponSlots[slot] is { } equipped) _fireStates[slot].Advance(_combatTime, equipped.Stats);
            return;
        }
        if (IsGrenadeBusy)
        {
            ReleaseTrigger();
            for (int slot = 0; slot < WeaponSlotCount; slot++)
                if (_weaponSlots[slot] is { } equipped) _fireStates[slot].Advance(_combatTime, equipped.Stats);
            return;
        }
        if ((_triggerHeld || pressed) && EquippedWeapon is { } weapon)
        {
            var state = _fireStates[ActiveWeaponSlot];
            if (pressed)
            {
                TryFireAt(_combatTime, out string message);
                if (message.Length > 0) ReloadFeedback?.Invoke(message);
            }
            else if (weapon.Stats.FireMode == FireMode.Automatic && !IsReloading && !IsSwitching && !InputBlocked)
            {
                // Retain sub-frame shot times so rapid weapons keep their configured rate.
                double shotTime = Math.Max(frameStart, state.NextShotTime);
                while (shotTime <= _combatTime + 1e-9)
                {
                    if (!TryFireAt(shotTime, out _)) break;
                    shotTime = state.NextShotTime;
                }
            }
        }
        for (int slot = 0; slot < WeaponSlotCount; slot++)
            if (_weaponSlots[slot] is { } equipped) _fireStates[slot].Advance(_combatTime, equipped.Stats);
    }

    internal bool TryFire(out string message) => TryFireAt(_combatTime, out message);

    private bool TryFireAt(double time, out string message)
    {
        message = "";
        if (InputBlocked) { message = "关闭界面后才能射击"; return false; }
        if (IsThrowSelecting) { message = "正在选择投掷物"; return false; }
        if (IsGrenadeBusy) { message = "正在持握手雷"; return false; }
        if (IsSwitching) { message = "正在切换武器"; return false; }
        if (EquippedWeapon is not { } weapon || _animator.Weapon is not { } held) { message = "尚未装备武器"; return false; }
        if (IsReloading) { message = "正在换弹"; return false; }
        if (_magazineAmmo[ActiveWeaponSlot] <= 0) { message = "弹匣已空，按 R 换弹"; return false; }
        var state = _fireStates[ActiveWeaponSlot];
        if (time + 1e-9 < state.NextShotTime) return false;
        state.Advance(time, weapon.Stats);
        Vector3 forward = -Visual.GlobalBasis.Z;
        forward.Y = 0;
        Vector3 direction = forward.Normalized().Rotated(Vector3.Up, Mathf.DegToRad(_shotRandom.RandfRange(-(float)state.Spread, (float)state.Spread)));
        Vector3 muzzle = held.Muzzle.GlobalPosition;
        var projectile = new Projectile { Name = "Bullet" };
        projectile.Initialize(direction, weapon.Stats, GetRid());
        // Position before entering the tree so its initial look direction uses the right origin.
        projectile.Position = GetParent<Node3D>().ToLocal(muzzle);
        GetParent().AddChild(projectile);
        projectile.Hit += (_, collider) => { if (collider is IProjectileTarget) TargetHit?.Invoke(); };
        _magazineAmmo[ActiveWeaponSlot]--;
        state.RecordShot(time, weapon.Stats);
        WeaponAudio.PlayGunshot();
        held.Kick();
        ShotEffect.Spawn(GetParent(), muzzle, new Color(1, .9f, .5f), .16f, .045);
        InventoryChanged?.Invoke();
        ShotFired?.Invoke(projectile);
        Vector3 chamber = GlobalPosition;
        chamber.Y = muzzle.Y;
        projectile.Sweep(chamber, muzzle);
        return true;
    }
}
