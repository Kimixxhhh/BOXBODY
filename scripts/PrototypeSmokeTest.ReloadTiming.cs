using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FieldKit.Items;

public partial class PrototypeSmokeTest
{
    private async Task VerifyReloadTiming()
    {
        var player = _game.Player;
        var original = _game.Weapons.Get("ak47");
        var fast = original with { Stats = original.Stats with { Price = 1000, ReloadDuration = 1.325, EquipDuration = .05, HolsterDuration = .05 } };
        var slow = original with { Stats = original.Stats with { Price = 1000, ReloadDuration = 5.3, EquipDuration = .05, HolsterDuration = .05 } };
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4); PressKey(Key.E); await Frames(2);
        _game.Weapons.Weapons[0] = fast;
        Check(player.TryPurchaseWeapon(fast.Id, out _), "Purchase a weapon with a fast reload configuration");
        await WaitForWeaponSwitch();
        _game.Weapons.Weapons[0] = slow;
        Check(player.TryPurchaseWeapon(slow.Id, out _) && player.TryPurchaseAmmo(ItemCatalog.MediumAmmo.Id, out _) &&
            player.TryPurchaseAmmo(ItemCatalog.MediumAmmo.Id, out _), "Purchase a slow weapon and two ammunition packs");
        PressKey(Key.Escape); await Frames(2);
        await WaitForWeaponSwitch();

        // Step the real player update deterministically to inspect either side of the deadline.
        player.SetProcess(false);
        bool SelectAndFinish(int slot)
        {
            if (!player.SelectWeaponSlot(slot)) return false;
            for (int i = 0; i < 900 && player.IsSwitching; i++) player._Process(1.0 / 60.0);
            return !player.IsSwitching;
        }
        var rig = player.Visual.GetNode<Node3D>("Millitia");
        Node3D Held() => rig.GetNode<Node3D>("HeldWeapon");
        Transform3D[] Pose() => new[] { Held() }.Concat(Held().FindChildren("*", "Node3D", true, false).Cast<Node3D>()).Select(n => n.Transform).ToArray();
        bool SamePose(Transform3D[] a, Transform3D[] b) => a.Length == b.Length && a.Zip(b).All(p => p.First.IsEqualApprox(p.Second));
        var checkpoints = new[] { .1, .2, .375, .5, .7, .805, .9, .99 };
        var fastPoses = new List<Transform3D[]>();
        Check(SelectAndFinish(0) && player.TryStartReload(out _) && player.ReloadDuration == 1.325,
            "Reload captures the equipped weapon's configured duration");
        foreach (double progress in checkpoints)
        {
            player._Process(progress * 1.325 - player.ReloadElapsed);
            fastPoses.Add(Pose());
            Check(player.IsReloading && player.MagazineAmmo(0) == 0 && player.Backpack.Available(AmmoType.Medium) == 80 &&
                HandError(rig, followReloadTarget: true) < .025f, $"Fast reload at {progress:P0}: hands follow the animation without consuming ammo early");
        }
        double elapsed = player.ReloadElapsed;
        fast.Stats.ReloadDuration = 99;
        Check(!player.TryStartReload(out _) && player.ReloadElapsed == elapsed && player.ReloadDuration == 1.325,
            "Repeated reload and later stat changes do not reset or stretch an active reload");
        fast.Stats.ReloadDuration = 1.325;
        Check(SelectAndFinish(1) && !player.IsReloading && player.Backpack.Available(AmmoType.Medium) == 80,
            "Switching to a different-duration weapon cancels without spending ammunition");
        var rest = Pose();
        Check(player.TryStartReload(out _) && player.ReloadDuration == 5.3, "Next reload uses the slower weapon's own duration");
        for (int i = 0; i < checkpoints.Length; i++)
        {
            player._Process(checkpoints[i] * 5.3 - player.ReloadElapsed);
            Check(SamePose(Pose(), fastPoses[i]) && HandError(rig, followReloadTarget: true) < .025f && player.MagazineAmmo(1) == 0,
                $"Fast and slow reloads have identical gun, magazine and hand poses at {checkpoints[i]:P0}");
        }
        player._Process(5.299 - player.ReloadElapsed);
        Check(player.IsReloading && player.Backpack.Available(AmmoType.Medium) == 80, "Slow reload does not fill at the former 2.65-second deadline or just before its own deadline");
        player._Process(.002);
        Check(!player.IsReloading && player.MagazineAmmo(1) == 30 && player.MagazineAmmo(0) == 0 &&
            player.Backpack.Available(AmmoType.Medium) == 50 && SamePose(rest, Pose()) && HandError(rig) < .025f,
            "Slow reload finishes at its configured time, fills only its slot and restores the entire holding pose");
        Check(!player.TryStartReload(out _), "Full magazine still prevents reload after changing the duration");
        Check(SelectAndFinish(0) && player.TryStartReload(out _), "Fast weapon can restart after cancellation");
        player._Process(1.324);
        Check(player.IsReloading && player.MagazineAmmo(0) == 0 && player.Backpack.Available(AmmoType.Medium) == 50,
            "Fast reload waits for its full configured duration");
        player._Process(.002);
        Check(!player.IsReloading && player.MagazineAmmo(0) == 30 && player.Backpack.Available(AmmoType.Medium) == 20,
            "Fast reload completes before the old fixed duration and consumes exactly thirty rounds");

        foreach (int fps in new[] { 30, 60, 120 })
        {
            fast.Stats.MagazineCapacity++;
            Check(player.TryStartReload(out _), $"Start frame-rate check at {fps} FPS");
            double time = 0;
            while (player.IsReloading && time < 2)
            {
                player._Process(1.0 / fps); time += 1.0 / fps;
            }
            Check(!player.IsReloading && time + .000001 >= 1.325 && time < 1.325 + 1.0 / fps + .000001,
                $"Reload completes within one frame of the configured deadline at {fps} FPS");
        }
        fast.Stats.MagazineCapacity++;
        fast.Stats.ReloadDuration = .001;
        int reserve = player.Backpack.Available(AmmoType.Medium);
        Check(player.TryStartReload(out _), "Minimum duration can start a reload");
        player._Process(.2); player._Process(.2);
        Check(!player.IsReloading && player.Backpack.Available(AmmoType.Medium) == reserve - 1,
            "A frame longer than the full reload still completes once and only consumes missing rounds");
        player.SetProcess(true);
        await Frames(2);
    }
}
