using Godot;
using System;
using System.Threading.Tasks;
using FieldKit.Items;

public partial class PrototypeSmokeTest
{
    private async Task VerifyFnfal()
    {
        var player = _game.Player;
        var fal = _game.Weapons.Get("fnfal");
        var ak = _game.Weapons.Get("ak47");
        var rig = player.Visual.GetNode<Node3D>("Millitia");
        Node3D Held() => rig.GetNode<Node3D>("HeldWeapon");
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4); PressKey(Key.E); await Frames(2);
        Check(player.TryPurchaseWeapon(fal.Id, out _), "FNFAL test buys the FNFAL");
        await WaitForWeaponSwitch();
        _switchRig = rig;
        _maximumSwitchGripError = 0;
        Check(player.TryPurchaseWeapon(ak.Id, out _), "FNFAL test buys the AK for comparison");
        await WaitForWeaponSwitch();
        Check(_maximumSwitchGripError < .025f,
            $"FNFAL hands follow the holster and draw (max error {_maximumSwitchGripError * 100:F2} cm)");
        _switchRig = null;
        Check(player.TryPurchaseAmmo(ItemCatalog.MediumAmmo.Id, out _), "FNFAL test buys ammunition");
        PressKey(Key.Escape); await Frames(2);
        await WaitForWeaponSwitch();

        bool headless = DisplayServer.GetName() == "headless";
        float cameraSize = _game.Camera.Size;
        if (!headless)
        {
            _game.Camera.Size = 3f;
            _game.Camera.SnapToTarget();
            player.InputBlocked = true;
            player.Visual.Rotation = new Vector3(0, 2.7f, 0);
        }
        float akX = Held().Position.X;
        await Screenshot("ak47-held.png");
        if (!headless) player.InputBlocked = false;
        _switchRig = rig;
        _maximumSwitchGripError = 0;
        Check(await SelectWeaponAndWait(0), "FNFAL test switches to the FNFAL");
        Check(_maximumSwitchGripError < .025f,
            $"FNFAL hands follow the switch into the hold pose (max error {_maximumSwitchGripError * 100:F2} cm)");
        _switchRig = null;
        if (!headless)
        {
            player.InputBlocked = true;
            player.Visual.Rotation = new Vector3(0, 2.7f, 0);
            _game.Camera.SnapToTarget();
        }
        float falX = Held().Position.X;
        Check(falX - akX > .05f && falX - akX < .07f,
            $"FNFAL hold position is shifted right toward the AK position ({falX - akX:F3} m)");
        Check(HandError(rig) < .025f, "FNFAL right and support hands meet the rifle grips");
        Marker3D? muzzle = player.HeldMuzzle;
        Check(muzzle != null, "FNFAL exposes a muzzle marker");
        if (muzzle != null)
        {
            Vector3 local = Held().GlobalTransform.AffineInverse() * muzzle.GlobalPosition;
            Check(local.Z < -.8f, $"FNFAL muzzle uses the modeled muzzle socket ({local.Z:F2} m forward)");
        }
        Transform3D hold = Held().Transform;
        await Screenshot("fnfal-held.png");

        _reloadRig = rig;
        _maximumReloadGripError = 0;
        player.InputBlocked = false;
        PressKey(Key.R);
        await Frames(3);
        Check(player.IsReloading, "FNFAL reload starts");
        if (!headless)
        {
            player.InputBlocked = true;
            player.Visual.Rotation = new Vector3(0, 2.7f, 0);
        }
        var magazine = Held().GetNode<Node3D>("MagazineMotion");
        Check(magazine.GetChildCount() > 0, "FNFAL magazine mesh is attached to the reload pivot");
        Transform3D rest = magazine.Transform;
        await Screenshot("fnfal-reload-01-start.png");
        await ReloadUntil(.8f);
        Check(magazine.Position.Y < rest.Origin.Y - .25f, "FNFAL magazine pulls straight down out of the receiver");
        Check(Mathf.Abs(magazine.Position.X - rest.Origin.X) < .001f &&
            Mathf.Abs(magazine.Position.Z - rest.Origin.Z) < .03f,
            "FNFAL magazine stays on its straight insertion axis");
        Check(magazine.Rotation.Length() < .001f, "FNFAL magazine does not rock like the AK magazine");
        float magHandError = HandError(rig, followReloadTarget: true);
        Check(magHandError < .025f, $"FNFAL support hand follows the magazine down ({magHandError * 100:F2} cm)");
        Check(Held().Rotation.Length() < .0001f, "FNFAL reload keeps the rifle upright");
        await Screenshot("fnfal-reload-02-mag-out.png");
        await ReloadUntil(1.25f);
        await Screenshot("fnfal-reload-03-inserting.png");
        await ReloadUntil(1.55f);
        Check(magazine.Position.DistanceTo(rest.Origin) < .002f, "FNFAL magazine seats back into the receiver");
        await Screenshot("fnfal-reload-04-finish.png");
        await ReloadUntil(player.ReloadDuration);
        Check(!player.IsReloading && magazine.Transform.IsEqualApprox(rest) && Held().Transform.IsEqualApprox(hold),
            "FNFAL reload finishes and restores the holding pose");
        Check(player.MagazineAmmo(0) == 20 && player.Backpack.Available(AmmoType.Medium) == 20,
            "FNFAL reload fills exactly its twenty-round magazine");
        Check(HandError(rig) < .025f, "FNFAL hands return to their holding positions");
        Check(_maximumReloadGripError < .025f,
            $"FNFAL hands track the rifle throughout reload (max error {_maximumReloadGripError * 100:F2} cm)");
        _reloadRig = null;

        player.InputBlocked = false;
        if (!headless)
        {
            _game.Camera.Size = cameraSize;
            _game.Camera.SnapToTarget();
        }
        await Frames(3);
    }
}
