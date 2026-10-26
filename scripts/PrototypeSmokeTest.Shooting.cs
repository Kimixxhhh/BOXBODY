using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FieldKit.Items;
using FieldKit.Weapons;

public partial class PrototypeSmokeTest
{
    private async Task VerifyShooting()
    {
        var player = _game.Player;
        var original = _game.Weapons.Get("ak47");
        var stats = original.Stats with { Price = 1000, MagazineCapacity = 10, ReloadDuration = .12,
            EquipDuration = 0, HolsterDuration = 0,
            FireMode = FireMode.SemiAuto, FireCooldown = .1, SpreadMin = 2, SpreadMax = 6, SpreadPerShot = 2, SpreadRecovery = 4,
            TurnFollowSpeed = 0, ProjectileSpeed = 10, Damage = 30, EnemySlowPercent = 20, KnockbackSpeed = 3 };
        const string soundPath = "res://shooting-test.wav";
        using var sound = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format8Bits, MixRate = 8000, Data = new byte[8000] };
        sound.TakeOverPath(soundPath);
        var weapon = original with { Stats = stats, GunshotSoundPath = soundPath, GunshotVolumePercent = 35, ReloadSounds = new() };
        _game.Weapons.Weapons[0] = weapon;
        int shots = 0, sounds = 0;
        var directions = new List<Vector3>();
        var shotTimes = new List<ulong>();
        Projectile? last = null;
        player.ShotFired += bullet => { shots++; last = bullet; directions.Add(bullet.Direction); shotTimes.Add(Engine.GetPhysicsFrames()); };
        player.WeaponAudio.SoundStarted += _ => sounds++;
        Check(!player.TryFire(out _) && shots == 0 && sounds == 0, "Unarmed firing creates no bullet or sound");
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4); PressKey(Key.E); await Frames(2);
        Check(player.TryPurchaseWeapon(weapon.Id, out string firstPurchase), "Shooting setup buys the first gun: " + firstPurchase);
        await WaitForWeaponSwitch();
        Check(player.TryPurchaseWeapon(weapon.Id, out string secondPurchase), "Shooting setup buys the second gun: " + secondPurchase);
        await WaitForWeaponSwitch();
        Check(player.TryPurchaseAmmo(ItemCatalog.MediumAmmo.Id, out string ammoPurchase), "Shooting setup buys ammunition: " + ammoPurchase);
        PressKey(Key.Escape); await Frames(2);
        await WaitForWeaponSwitch();
        Check(!player.TryFire(out _) && shots == 0 && sounds == 0, "An empty magazine cannot shoot or play gunfire");
        Check(player.TryStartReload(out _) && !player.TryFire(out _), "Firing is blocked throughout reload");
        await Frames(15);
        Check(player.MagazineAmmo(1) == 10 && player.Backpack.Available(AmmoType.Medium) == 30, "Reload transfers rounds before shooting");
        player.GlobalPosition = new Vector3(-7, .01f, 1);
        player.Visual.Rotation = Vector3.Zero;
        await Frames(3);
        FireMouse(true); await Frames(3);
        Check(shots == 1 && sounds == 1 && player.MagazineAmmo(1) == 9 && player.MagazineAmmo(0) == 0,
            "Left click consumes one round from only the active magazine and plays one gunshot");
        Check(player.WeaponAudio.GetChildren().OfType<AudioStreamPlayer>().Any(v => Mathf.IsEqualApprox(v.VolumeLinear, .35f)),
            "Successful firing uses the maintained gunshot volume");
        Check(_game.Ui.GetNode<Label>("Root/Money/Text").Text.Contains("9 / 10"), "Shooting immediately updates the ammunition HUD");
        Check(directions[0].Dot(Vector3.Forward) >= Mathf.Cos(Mathf.DegToRad(2.01f)), "First shot respects minimum spread and the current facing direction");
        Vector3 before = last!.GlobalPosition;
        await Frames(6);
        Check(last.GlobalPosition.DistanceTo(before) > .75f && last.GlobalPosition.DistanceTo(before) < 1.3f,
            "Visible projectile moves at configured speed instead of hitting instantly");
        await Frames(8);
        Check(shots == 1, "Holding the mouse in semi-auto mode never repeats a shot");
        FireMouse(false); FireMouse(true); FireMouse(false); await Frames(2);
        Check(shots == 2 && !player.TryFire(out _), "A new semi-auto press fires once and cooldown blocks immediate repeat");
        bool switchedAway = await SelectWeaponAndWait(0);
        bool switchedBack = await SelectWeaponAndWait(1);
        Check(switchedAway && switchedBack && !player.TryFire(out _),
            "Switching away and back cannot reset a slot's cooldown");
        await Frames(2);
        Check(HandError(player.Visual.GetNode<Node3D>("Millitia")) < .025f, "Recoil preserves both hand grips");
        await Frames(120);
        Check(Math.Abs(player.CurrentSpread - stats.SpreadMin) < .001, "Spread recovers to its minimum after cooldown");

        stats.FireMode = FireMode.Automatic;
        int firstAutoShot = shots;
        FireMouse(true); await Frames(26); FireMouse(false);
        int automaticShots = shots - firstAutoShot;
        Check(automaticShots >= 4 && automaticShots <= 5, "Automatic fire follows the configured 0.1-second interval");
        Check(shotTimes.Skip(firstAutoShot).Zip(shotTimes.Skip(firstAutoShot + 1)).All(pair => pair.Second - pair.First >= 5),
            "Automatic fire never emits extra shots between cooldown intervals");
        Check(player.CurrentSpread > stats.SpreadMin && player.CurrentSpread <= stats.SpreadMax, "Continuous fire increases spread without exceeding the maximum");
        Check(directions.All(direction => Math.Abs(direction.Y) < .0001 && direction.Dot(Vector3.Forward) >= Mathf.Cos(Mathf.DegToRad(6.01f))),
            "All projectiles stay inside the configured horizontal spread angle");
        await Frames(12);
        int stopped = shots;
        await Frames(10);
        Check(shots == stopped, "Releasing the mouse stops automatic fire");

        PressKey(Key.Tab); await Frames(2);
        FireMouse(true); await Frames(5);
        Check(!player.TryFire(out _) && shots == stopped, "Inventory clicks cannot shoot or spend ammunition");
        PressKey(Key.Tab); await Frames(10);
        Check(shots == stopped, "Closing inventory while the mouse is held does not leak a shot");
        FireMouse(false); FireMouse(true); await Frames(2);
        PressKey(Key.Tab); await Frames(10);
        stopped = shots;
        PressKey(Key.Tab); await Frames(10);
        Check(shots == stopped, "Opening a menu cancels an already-held automatic trigger");
        FireMouse(false);
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        await Frames(3); PressKey(Key.E); await Frames(2);
        Check(!player.TryFire(out _), "The weapon shop also blocks firing");
        PressKey(Key.Escape); await Frames(2);
        player.GlobalPosition = new Vector3(-7, .01f, 1);
        FireMouse(true); await Frames(90); FireMouse(false);
        Check(player.MagazineAmmo(1) == 0 && sounds == shots && !player.TryFire(out _), "Automatic fire stops at zero ammunition without extra bullets or sounds");
        Check(player.TryStartReload(out _), "An emptied magazine can be reloaded");
        await Frames(15);

        // At 1,000 rounds/second, several shots fall within each physics tick.
        stats.FireCooldown = .001;
        int rapidStart = shots;
        FireMouse(true); await Frames(5); FireMouse(false);
        Check(shots - rapidStart == 10 && player.MagazineAmmo(1) == 0, "Sub-frame cooldowns retain their rate and never overdraw a magazine");
        stats.FireCooldown = .1;
        Check(player.TryStartReload(out _), "Reload after rapid fire remains available");
        await Frames(15);
        player.GlobalPosition = new Vector3(-7, .01f, 1);
        player.Visual.Rotation = Vector3.Zero;
        var closeWall = new StaticBody3D { Position = new Vector3(-7, 1.5f, .3f), CollisionLayer = 1 };
        closeWall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2, 3, .05f) } });
        _game.AddChild(closeWall);
        await Frames(3);
        Check(player.TryFire(out _) && last!.HasHit, "A gun extending through a close wall hits the obstruction instead of shooting through it");
        closeWall.QueueFree();

        await VerifyProjectileHits(stats, player);
        await VerifyShootingPreview(stats, player);
    }

    private async Task VerifyProjectileHits(WeaponStats stats, PlayerController player)
    {
        var target = new PracticeTarget { Position = new Vector3(30, 0, -3) };
        var behind = new PracticeTarget { Position = new Vector3(30, 0, -4) };
        _game.AddChild(target); _game.AddChild(behind);
        await Frames(2);
        Projectile MakeBullet(Vector3 start, WeaponStats config)
        {
            var bullet = new Projectile { Position = start };
            bullet.Initialize(Vector3.Forward, config, player.GetRid());
            _game.AddChild(bullet);
            return bullet;
        }
        var config = stats with { ProjectileSpeed = 100000 };
        var bullet = MakeBullet(new Vector3(30, 1.1f, 0), config);
        ProjectileHit? damage = null;
        int hits = 0;
        bullet.Hit += (hit, _) => { hits++; damage = hit; };
        config.Damage = 999; // In-flight bullets keep the values captured when fired.
        await Frames(3);
        Check(target.Health == 70 && behind.Health == 100 && hits == 1, "A high-speed projectile damages only the first target, exactly once");
        Check(damage is { Damage: 30, SpeedMultiplier: .8 } && damage.Value.Knockback.IsEqualApprox(Vector3.Forward * 3),
            "Hit data snapshots damage, slow multiplier and knockback direction at firing time");
        var wall = new StaticBody3D { Position = new Vector3(30, 1.1f, -1), CollisionLayer = 1 };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2, 2, .01f) } });
        _game.AddChild(wall); await Frames(2);
        MakeBullet(new Vector3(30, 1.1f, 0), stats with { ProjectileSpeed = 100000 });
        await Frames(3);
        Check(target.Health == 70, "Swept collision stops even a 1 cm wall from being tunneled through");
        wall.QueueFree(); await Frames(2);
        MakeBullet(new Vector3(30, 1.1f, 0), stats with { ProjectileSpeed = 100000, Damage = 100 });
        await Frames(3);
        Check(target.Health == 0 && target.CollisionLayer == 0, "Lethal hits disable the practice target");
        await Frames(190);
        Check(target.Health == 100 && target.CollisionLayer == 4, "Destroyed practice targets restore after three seconds");
        var expiry = MakeBullet(new Vector3(60, 1.1f, 0), stats with { ProjectileSpeed = .001 });
        expiry._PhysicsProcess(6);
        Check(expiry.IsQueuedForDeletion(), "Slow missed bullets expire after their lifetime");
        var range = MakeBullet(new Vector3(60, 1.1f, 0), stats with { ProjectileSpeed = 100000 });
        range._PhysicsProcess(1);
        Check(range.IsQueuedForDeletion() && range.GlobalPosition.DistanceTo(new Vector3(60, 1.1f, 0)) <= Projectile.MaxDistance + .01f,
            "Fast missed bullets stop at maximum range and are cleaned up");
        target.QueueFree(); behind.QueueFree();
    }

    private async Task VerifyShootingPreview(WeaponStats stats, PlayerController player)
    {
        stats.SpreadMin = stats.SpreadMax = 0;
        stats.ProjectileSpeed = 30;
        stats.TurnFollowSpeed = 10;
        player.GlobalPosition = new Vector3(-3.0848f, .01f, 0);
        player.Visual.Rotation = Vector3.Zero;
        _game.Camera.SnapToTarget();
        Vector2 point = _game.Camera.UnprojectPosition(new Vector3(-3.0848f, player.AimHeight, -5));
        Input.WarpMouse(point);
        await Frames(4);
        var target = _game.GetNode<PracticeTarget>("PracticeTarget1");
        double health = target.Health;
        Check(player.TryFire(out _), "Reloaded weapon can shoot at the practice range");
        await Frames(9);
        Check(target.Health < health, "Player-fired bullet hits the visible practice target");
        await Screenshot("shooting-hit.png");
        await Frames(8);
        FireMouse(true); await Frames(2);
        await Screenshot("shooting-fire.png");
        FireMouse(false);
    }

    private void FireMouse(bool pressed) => Input.ParseInputEvent(new InputEventMouseButton {
        ButtonIndex = MouseButton.Left, Pressed = pressed, Position = GetViewport().GetMousePosition(), GlobalPosition = GetViewport().GetMousePosition() });
}
