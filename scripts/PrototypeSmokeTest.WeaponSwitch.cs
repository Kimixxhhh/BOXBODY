using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class PrototypeSmokeTest
{
    private async Task VerifyWeaponSwitch()
    {
        var player = _game.Player;
        var original = _game.Weapons.Get("ak47");
        // Cheap initial draw: both weapons start with very short timings.
        var first = original with { Stats = original.Stats with { Price = 1000, EquipDuration = .05, HolsterDuration = .05 } };
        var second = original with { Stats = original.Stats with { Price = 1000, EquipDuration = .05, HolsterDuration = .05 } };
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4); PressKey(Key.E); await Frames(2);
        _game.Weapons.Weapons[0] = first;
        Check(player.TryPurchaseWeapon(first.Id, out _), "Switch test buys the first weapon");
        await WaitForWeaponSwitch();
        _game.Weapons.Weapons[0] = second;
        Check(player.TryPurchaseWeapon(second.Id, out _), "Switch test buys the second weapon");
        await WaitForWeaponSwitch();
        PressKey(Key.Escape); await Frames(2);
        await WaitForWeaponSwitch();

        // Slot one is active. Its holster becomes .15 and the other weapon's draw .4:
        // switching to slot zero must take 0.55 s; switching back takes .1 + .3 = .4 s.
        first.Stats.EquipDuration = .4;
        first.Stats.HolsterDuration = .1;
        second.Stats.EquipDuration = .3;
        second.Stats.HolsterDuration = .15;

        // The mouse wheel uses the same occupied-slot cycle and animation as the
        // number keys: up selects the previous slot, down the next, wrapping.
        Wheel(MouseButton.WheelUp);
        await Frames(2);
        Check(player.IsSwitching, "Wheel up starts the same holster/draw switch as the number keys");
        await WaitForWeaponSwitch();
        Check(player.ActiveWeaponSlot == 0, "Mouse wheel up switches to the previous equipped weapon slot");
        Wheel(MouseButton.WheelDown);
        await Frames(2);
        await WaitForWeaponSwitch();
        Check(player.ActiveWeaponSlot == 1, "Mouse wheel down switches to the next equipped slot and wraps");

        player.SetProcess(false);
        var rig = player.Visual.GetNode<Node3D>("Millitia");
        Node3D Held() => rig.GetNode<Node3D>("HeldWeapon");
        Transform3D holdPose = Held().Transform;

        Check(player.SelectWeaponSlot(0), "Switch to slot zero starts the holster and draw");
        Check(player.IsSwitching && !player.TryStartReload(out string switchMessage) && switchMessage.Contains("切换"),
            "Reloading is blocked while the weapon is being holstered and drawn");
        double elapsed = 0;
        bool sawRotation = false;
        float maximumHandError = 0;
        while (player.IsSwitching && elapsed < 2)
        {
            player._Process(1.0 / 120.0);
            elapsed += 1.0 / 120.0;
            if (!player.IsSwitching) break;
            sawRotation |= Held().Rotation.Length() > .05f;
            maximumHandError = Mathf.Max(maximumHandError, HandError(rig));
        }
        Check(Math.Abs(elapsed - .55) <= 1.0 / 120.0 + 1e-6,
            $"Switch uses the old weapon's holster and the new weapon's draw time ({elapsed:F3} s)");
        Check(sawRotation, "The rifle rotates down and back up during the switch");
        Check(maximumHandError < .025f,
            $"Both hands follow the rotating rifle through the whole switch (max error {maximumHandError * 100:F2} cm)");
        Check(!player.IsSwitching && Held().Rotation.Length() < .0001f && Held().Transform.IsEqualApprox(holdPose),
            "The new weapon finishes exactly in the holding pose");

        // Second switch, stepped to fixed checkpoints for visual inspection.
        if (DisplayServer.GetName() != "headless")
        {
            _game.Camera.Size = 3.0f;
            _game.Camera.SnapToTarget();
            player.Visual.Rotation = new Vector3(0, 2.7f, 0);
        }
        Check(player.SelectWeaponSlot(1), "Switch back starts the second holster and draw");
        player.InputBlocked = true;
        void Advance(double seconds)
        {
            int steps = Math.Max(1, (int)Math.Round(seconds * 120));
            for (int i = 0; i < steps; i++) player._Process(1.0 / 120.0);
        }
        Advance(.05);
        await Screenshot("switch-01-holster.png");
        Advance(.07);
        await Screenshot("switch-02-stowed.png");
        Advance(.13);
        await Screenshot("switch-03-drawing.png");
        Advance(.16);
        await Screenshot("switch-04-held.png");
        Check(!player.IsSwitching && Held().Rotation.Length() < .0001f,
            "The second switch also ends in the holding pose");
        player.InputBlocked = false;
        _game.Camera.Size = 11;
        _game.Camera.SnapToTarget();
        player.SetProcess(true);
        await Frames(3);
    }

    private void Wheel(MouseButton button)
    {
        Vector2 position = GetViewport().GetMousePosition();
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = button, Pressed = true, Position = position, GlobalPosition = position
        });
    }
}
