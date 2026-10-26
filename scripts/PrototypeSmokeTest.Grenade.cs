using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FieldKit.Items;
using FieldKit.Weapons;

// A recording target used by --grenade-test to inspect the exact blast payload.
internal sealed partial class GrenadeTestTarget : StaticBody3D, IProjectileTarget
{
    public List<ProjectileHit> Hits { get; } = new();

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 0;
        AddChild(new CollisionShape3D {
            Position = Vector3.Up * 1.1f,
            Shape = new BoxShape3D { Size = new Vector3(1, 1.5f, .5f) }
        });
    }

    public void ApplyHit(ProjectileHit hit) => Hits.Add(hit);
}

public partial class PrototypeSmokeTest
{
    private async Task VerifyGrenades()
    {
        var player = _game.Player;
        var rig = player.Visual.GetNode<Node3D>("Millitia");
        var throwable = _game.Weapons.GetThrowable("grenade");
        Check(throwable is {
            Id: "grenade", Name: "手雷", Damage: > 0, Range: > 0, FuseTime: > 0, SlotCapacity: > 0,
            ItemScenePath: "res://assets/weapons/throwables/grenade.glb",
            ExplosionSoundPath: "res://assets/sfx/weapons/explosion/explosion_grenade_1.wav"
        }, "Catalog exposes the grenade template with its item scene, explosion sound and slot capacity");
        Check(_game.Ui.Items.ItemCount == _game.Weapons.Weapons.Count + ItemCatalog.Ammunition.Count + _game.Weapons.Throwables.Count,
            "Grenade appears in the world-space weapon shop after weapons and ammunition");

        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4);
        PressKey(Key.E);
        await Frames(2);
        SelectShopItem(_game.Weapons.Weapons.Count + ItemCatalog.Ammunition.Count);
        Check(!_game.Ui.BuyButton.Disabled && _game.Ui.BuyButton.Text.Contains("放入背包"),
            "Selecting a grenade offers purchase into the backpack item slots");
        int before = player.Money;
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Check(player.Money == before - throwable.Price && player.WeaponSlots.All(w => w == null) &&
            player.Backpack.ThrowableCount == 1 && player.Backpack.ThrowableAmount == throwable.SlotCapacity &&
            player.Backpack.Slots[0] is ThrowableItem boughtItem &&
            ReferenceEquals(boughtItem.Definition, throwable) && boughtItem.Capacity == throwable.SlotCapacity &&
            boughtItem.Remaining == throwable.SlotCapacity,
            "Grenade purchase costs one price, fills one small slot with its configured stack and leaves both weapon slots empty");
        Check(_game.Ui.GetNode<Label>("Root/Money/Text").Text.Contains($"手雷 ×{throwable.SlotCapacity}"),
            "HUD counts every grenade in the purchased stack");
        await Screenshot("grenade-shop.png");
        PressKey(Key.Tab);
        await Frames(4);
        Check(_game.Inventory.GetNode<Label>("Root/Layout/Storage/Slot01/ItemName").Text == "手雷" &&
            _game.Inventory.GetNode<Label>("Root/Layout/Storage/Slot01/AmmoAmount").Text ==
                $"{throwable.SlotCapacity} / {throwable.SlotCapacity}",
            "Backpack UI displays the whole grenade stack in an item slot, not in a weapon card");
        var preview = _game.Inventory.GetNode<WeaponModelPreview>("ItemPreview_grenade");
        Check(preview.Model.SceneFilePath == throwable.ItemScenePath && preview.OwnWorld3D,
            "Grenade item slot renders the configured item scene in the shared 3D preview");
        Check(ItemSlot(0).TooltipText.Contains("伤害") && ItemSlot(0).TooltipText.Contains("引信") &&
            ItemSlot(0).TooltipText.Contains($"每格 {throwable.SlotCapacity} 枚"),
            "Grenade item slot exposes damage, fuse and stack capacity in its hover description");
        await Screenshot("grenade-inventory.png");
        var grenadeItem = player.Backpack.Slots[0];
        await BeginDrag(0);
        await DropAt(ItemSlot(5).GetGlobalRect().GetCenter());
        Check(ReferenceEquals(player.Backpack.Slots[5], grenadeItem) &&
            _game.Inventory.GetNode<Label>("Root/Layout/Storage/Slot06/ItemName").Text == "手雷",
            "Grenade can be dragged between the backpack's small item slots like ammunition");
        Check(!player.TryEquipGrenade(out var blocked) && blocked.Contains("界面"),
            "G is ignored while the backpack blocks gameplay input");
        PressKey(Key.Tab);
        await Frames(2);

        // Keep the real catalog untouched: mutate a copy-backed draft of the
        // in-memory template and restore every value before the test exits.
        var original = throwable with { };
        throwable.Damage = 40;
        throwable.Range = 4;
        throwable.EnemySlowPercent = 60;
        throwable.SlowDuration = 2.5;
        throwable.KnockbackSpeed = 7;
        throwable.FuseTime = .9;
        var near = new GrenadeTestTarget { Name = "GrenadeNearTarget", Position = new Vector3(20, 0, 24) };
        var far = new GrenadeTestTarget { Name = "GrenadeFarTarget", Position = new Vector3(30, 0, 24) };
        _game.AddChild(near);
        _game.AddChild(far);
        player.GlobalPosition = new Vector3(20, .02f, 20);
        player.Velocity = Vector3.Zero;
        player.Visual.Rotation = Vector3.Zero;
        _game.Camera.SnapToTarget();
        await Frames(8);

        // Pressing G must only take the grenade out. It must not consume the item
        // or create a projectile; those happen when the left mouse button fires.
        PressKey(Key.G);
        await Frames(2);
        Check(player.IsGrenadeBusy && !player.IsGrenadeReady && player.IsGrenadeHeld &&
            player.Backpack.ThrowableCount == 1 && player.Backpack.Slots[5] is ThrowableItem &&
            player.HeldGrenade != null && !rig.HasNode("HeldWeapon"),
            "G holsters the current weapon and starts drawing a grenade without throwing it");
        await WaitForState(() => player.IsGrenadeReady, 90, "Grenade draw did not finish");
        Check(player.IsGrenadeReady && rig.HasNode("HeldGrenade") && !rig.HasNode("HeldWeapon") &&
            player.Backpack.ThrowableCount == 1 && _game.GetNodeOrNull<GrenadeProjectile>("Grenade") == null,
            "The grenade is visible in the right hand and remains in the item slot until thrown");
        Check(player.HeldGrenade!.Position.Z < -.35f && player.HeldGrenade.Position.Y > .95f,
            "The held grenade is carried forward and away from the torso");
        float handError = rig.GetNode<Node3D>("RightArmPivot/Elbow/HandGrip").GlobalPosition
            .DistanceTo(player.HeldGrenade!.GlobalPosition);
        Check(handError < .06f, $"Right hand holds the grenade while ready (error {handError * 100:F2} cm)");
        Check(_game.Ui.GetNode<Label>("Root/Money/Text").Text.Contains("手雷已就绪"),
            "HUD changes from weapon ammunition to the held-grenade prompt");
        await Screenshot("grenade-held.png");
        if (DisplayServer.GetName() != "headless")
        {
            float cameraSize = _game.Camera.Size;
            _game.Camera.Size = 4;
            _game.Camera.SnapToTarget();
            await Frames(4);
            await Screenshot("grenade-held-close.png");
            _game.Camera.Size = cameraSize;
            _game.Camera.SnapToTarget();
            await Frames(2);
        }

        Check(player.TryThrowHeldGrenadeAt(new Vector3(20, .06f, 24), out var throwMessage),
            "Right-hand grenade accepts a throw target: " + throwMessage);
        await Frames(2);
        Check(player.IsGrenadeBusy && !player.IsGrenadeReady && player.Backpack.ThrowableCount == 1 &&
            player.Backpack.ThrowableAmount == throwable.SlotCapacity &&
            _game.GetNodeOrNull<GrenadeProjectile>("Grenade") == null,
            "The throw animation starts before the grenade item is consumed or released");
        if (DisplayServer.GetName() != "headless")
        {
            float cameraSize = _game.Camera.Size;
            _game.Camera.Size = 4;
            _game.Camera.SnapToTarget();
            await Frames(4);
            await Screenshot("grenade-windup.png");
            await Frames(14);
            await Screenshot("grenade-throw.png");
            _game.Camera.Size = cameraSize;
            _game.Camera.SnapToTarget();
        }
        await WaitForState(() => _game.GetNodeOrNull<GrenadeProjectile>("Grenade") != null, 60,
            "Grenade projectile was not released from the hand");
        var liveGrenade = _game.GetNode<GrenadeProjectile>("Grenade");
        Check(player.Backpack.ThrowableCount == 1 && player.Backpack.ThrowableAmount == throwable.SlotCapacity - 1 &&
            player.Backpack.Count == 1 && player.Backpack.Slots[5] is ThrowableItem {
                Remaining: var remaining, Capacity: var capacity } &&
            remaining == throwable.SlotCapacity - 1 && capacity == throwable.SlotCapacity &&
            liveGrenade.Definition == throwable && liveGrenade.FuseRemaining > 0,
            "The release consumes exactly one grenade from the stack and spawns the configured projectile");
        bool exploded = false;
        Vector3 blast = Vector3.Zero;
        string? sound = null;
        _game.ExplosionAudio.SoundStarted += path => sound = path;
        liveGrenade.Exploded += g => { exploded = true; blast = g.GlobalPosition; };
        await Frames(30);
        Check(!liveGrenade.HasExploded && !exploded, "Grenade waits for its configured fuse instead of exploding on landing");
        await Screenshot("grenade-ground.png");
        for (int i = 0; i < 90 && !exploded; i++) await Frames(1);
        await Screenshot("grenade-explosion.png");
        Check(exploded && near.Hits.Count == 1 && far.Hits.Count == 0,
            $"Grenade explodes after the fuse and affects only targets inside its range (near {near.Hits.Count}, far {far.Hits.Count}, blast {blast})");
        var hit = near.Hits[0];
        Check(Math.Abs(hit.Damage - 40) < .0001 && Math.Abs(hit.SpeedMultiplier - .4) < .0001 &&
            Math.Abs(hit.SlowDuration - 2.5) < .0001 && Math.Abs(hit.Knockback.Length() - 7) < .001 &&
            Math.Abs(hit.Knockback.Y) < .0001,
            "Explosion forwards template damage, slow value/duration and horizontal knockback");
        Check(sound == throwable.ExplosionSoundPath, "Explosion plays the configured sound once");
        await WaitForState(() => !player.IsGrenadeBusy && !player.IsSwitching, 90,
            "Grenade throw did not finish");
        Check(!player.IsGrenadeHeld && !rig.HasNode("HeldGrenade") && !rig.HasNode("HeldWeapon"),
            "After throwing with no weapon equipped the character returns to empty hands");

        // Throwing the rest of the stack must remove one grenade per throw and
        // release the item slot only with the last one. Aim away from the targets
        // so the extra explosions cannot add hits to the recorded blast.
        while (player.Backpack.ThrowableAmount > 0)
        {
            int beforeDraw = player.Backpack.ThrowableAmount;
            Check(player.TryEquipGrenade(out var drawMessage),
                "The remaining grenade stack can be drawn again: " + drawMessage);
            await WaitForState(() => player.IsGrenadeReady, 90, "Grenade redraw did not finish");
            Check(player.Backpack.ThrowableAmount == beforeDraw &&
                player.Backpack.ThrowableCount == 1,
                "Drawing from the stack does not consume a grenade");
            Check(player.TryThrowHeldGrenadeAt(new Vector3(20, .06f, 10), out var drainThrow),
                "A grenade from the remaining stack accepts a throw: " + drainThrow);
            await WaitForState(() => player.Backpack.ThrowableAmount == beforeDraw - 1, 60,
                "Throwing from the stack did not consume exactly one grenade");
            Check(beforeDraw == 1
                    ? player.Backpack.Count == 0
                    : player.Backpack.ThrowableCount == 1 && player.Backpack.ThrowableAmount == beforeDraw - 1,
                beforeDraw == 1
                    ? "The item slot is released after the last grenade of the stack is thrown"
                    : "A partially used grenade stack keeps its slot");
            await WaitForState(() => !player.IsGrenadeBusy && !player.IsSwitching, 120,
                "Grenade throw did not finish");
            _game.GetNodeOrNull<GrenadeProjectile>("Grenade")?.QueueFree();
            await Frames(2);
        }
        Check(player.Backpack.Count == 0, "Throwing the whole stack frees its item slot");
        Check(!player.TryEquipGrenade(out var empty) && empty.Contains("没有手雷"),
            "An empty backpack rejects drawing another grenade without creating one");

        throwable.Damage = original.Damage;
        throwable.Range = original.Range;
        throwable.EnemySlowPercent = original.EnemySlowPercent;
        throwable.SlowDuration = original.SlowDuration;
        throwable.KnockbackSpeed = original.KnockbackSpeed;
        throwable.FuseTime = original.FuseTime;
        near.QueueFree();
        far.QueueFree();

        // Now repeat with a real weapon: G must holster it, LMB must throw, and
        // the weapon must be drawn again afterwards. A second G first verifies
        // the cancel path without consuming the grenade.
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4);
        PressKey(Key.E);
        await Frames(2);
        SelectShopItem(0);
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        await WaitForWeaponSwitch();
        Check(player.EquippedWeapon?.Id == "ak47" && rig.HasNode("HeldWeapon"),
            "The holster/restore scenario starts with the AK drawn");
        SelectShopItem(_game.Weapons.Weapons.Count + ItemCatalog.Ammunition.Count);
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(2);
        Check(player.Backpack.ThrowableCount == 1 && player.Backpack.ThrowableAmount == throwable.SlotCapacity,
            "The holster/restore scenario has one full grenade stack in the item slots");
        PressKey(Key.Escape);
        await Frames(2);

        PressKey(Key.G);
        await Frames(2);
        Check(player.IsGrenadeBusy && player.Backpack.ThrowableCount == 1 && player.EquippedWeapon?.Id == "ak47",
            "G starts holstering the AK while the grenade remains in the backpack");
        if (DisplayServer.GetName() != "headless")
        {
            float cameraSize = _game.Camera.Size;
            _game.Camera.Size = 4;
            _game.Camera.SnapToTarget();
            await Frames(2);
            await Screenshot("grenade-holster.png");
            _game.Camera.Size = cameraSize;
            _game.Camera.SnapToTarget();
            await Frames(1);
        }
        float maxHolsterHandError = rig.HasNode("HeldWeapon") ? HandError(rig) : 0;
        for (int i = 0; i < 120 && rig.HasNode("HeldWeapon"); i++)
        {
            await Frames(1);
            if (rig.HasNode("HeldWeapon"))
                maxHolsterHandError = Mathf.Max(maxHolsterHandError, HandError(rig));
        }
        Check(maxHolsterHandError < .025f,
            $"Both hands follow the AK down while it is holstered for the grenade (max error {maxHolsterHandError * 100:F2} cm)");
        await WaitForState(() => player.IsGrenadeReady, 120, "Grenade draw after holstering the AK did not finish");
        Check(player.IsGrenadeReady && !rig.HasNode("HeldWeapon") && rig.HasNode("HeldGrenade"),
            "The AK is fully holstered while the grenade is held");
        Check(!player.CycleWeapon(1), "Wheel weapon cycling is blocked while holding a grenade");
        Check(_game.Ui.GetNode<Label>("Root/Money/Text").Text.Contains("手雷已就绪"),
            "HUD shows the held grenade instead of AK ammunition");
        await Screenshot("grenade-held-ak.png");
        PressKey(Key.Tab);
        await Frames(4);
        Check(_game.Inventory.GetNode<Label>("Root/Layout/PrimaryWeapon/WeaponState").Text == "已收回",
            "Backpack weapon card marks the holstered AK as stowed while the grenade is in hand");
        await Screenshot("grenade-held-inventory.png");
        PressKey(Key.Tab);
        await Frames(2);

        PressKey(Key.G);
        await Frames(2);
        Check(player.IsGrenadeBusy && !player.IsGrenadeReady,
            "G while ready starts stowing the grenade instead of throwing it");
        await WaitForState(() => !player.IsGrenadeBusy && !player.IsSwitching, 120,
            "Grenade stow did not finish");
        Check(player.Backpack.ThrowableCount == 1 && player.Backpack.ThrowableAmount == throwable.SlotCapacity &&
            rig.HasNode("HeldWeapon") && !rig.HasNode("HeldGrenade"),
            "Cancelling with G keeps the whole grenade stack in the backpack and restores the AK");

        PressKey(Key.G);
        await WaitForState(() => player.IsGrenadeReady, 120, "Grenade could not be drawn again");
        FireMouse(true);
        await Frames(2);
        FireMouse(false);
        Check(player.IsGrenadeBusy && !player.IsGrenadeReady && player.Backpack.ThrowableCount == 1 &&
            player.Backpack.ThrowableAmount == throwable.SlotCapacity,
            "Left mouse starts the throw animation while the grenade stack is still in the slot");
        await WaitForState(() => player.Backpack.ThrowableAmount == throwable.SlotCapacity - 1, 60,
            "Left mouse did not release the held grenade");
        var thrownByMouse = _game.GetNodeOrNull<GrenadeProjectile>("Grenade");
        Check(thrownByMouse != null, "Left mouse throws the grenade currently in hand");
        thrownByMouse?.QueueFree();
        await WaitForState(() => !player.IsGrenadeBusy && !player.IsSwitching, 120,
            "The AK was not drawn after throwing the grenade");
        Check(player.IsGrenadeBusy == false && rig.HasNode("HeldWeapon") && !rig.HasNode("HeldGrenade") &&
            player.EquippedWeapon?.Id == "ak47" &&
            player.Backpack.ThrowableCount == 1 && player.Backpack.ThrowableAmount == throwable.SlotCapacity - 1,
            "After the throw the previously holstered AK is automatically drawn again and the stack keeps its remainder");

        // Hold-G wheel: one sector per throwable kind, mouse direction selects,
        // release draws the highlighted kind; a held grenade can be swapped too.
        KeyDown(Key.G);
        await Frames(30);
        Check(player.IsThrowWheelOpen && _game.ThrowWheel.EntryCount == 1 &&
            _game.ThrowWheel.SelectedId == "grenade",
            "Holding G with one backpack kind opens a one-sector wheel");
        await Screenshot("throwable-wheel-single.png");
        KeyUp(Key.G);
        await Frames(2);
        Check(player.IsGrenadeBusy && player.HeldThrowable?.Id == "grenade",
            "Releasing a one-sector wheel draws the only throwable kind");
        await WaitForState(() => player.IsGrenadeReady, 120, "Wheel-selected grenade did not finish drawing");
        PressKey(Key.G);
        await WaitForState(() => !player.IsGrenadeBusy && !player.IsSwitching, 120,
            "Wheel-selected grenade did not stow");
        Check(player.EquippedWeapon?.Id == "ak47" && player.Backpack.ThrowableKinds().Count == 1,
            "Tapping G stows the drawn grenade and keeps one kind in the backpack");

        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4);
        PressKey(Key.E);
        await Frames(2);
        SelectShopItem(_game.Weapons.Weapons.Count + ItemCatalog.Ammunition.Count + 1);
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        PressKey(Key.Escape);
        await Frames(2);
        var kinds = player.Backpack.ThrowableKinds();
        Check(kinds.Count == 2 && kinds[0].Id == "grenade" && kinds[1].Id == "concussiongrenade" &&
            kinds[0].Remaining == throwable.SlotCapacity - 1 && kinds[1].Remaining == 2,
            "Backpack exposes one wheel entry per throwable kind with total remaining counts");

        // A tap still uses the first slot stack even when two kinds are present.
        PressKey(Key.G);
        await Frames(2);
        Check(player.HeldThrowable?.Id == "grenade",
            "A short G tap draws the first backpack kind, not the last selected kind");
        await WaitForState(() => player.IsGrenadeReady, 120, "Tap-selected grenade did not finish drawing");
        PressKey(Key.G);
        await WaitForState(() => !player.IsGrenadeBusy && !player.IsSwitching, 120,
            "Tap-selected grenade did not stow");

        Vector2 center = GetViewport().GetVisibleRect().Size * .5f;
        KeyDown(Key.G);
        await Frames(30);
        Check(player.IsThrowWheelOpen && _game.ThrowWheel.EntryCount == 2 &&
            _game.ThrowWheel.Entries[0].Id == "grenade" &&
            _game.ThrowWheel.Entries[1].Id == "concussiongrenade",
            "Holding G opens two wheel sectors for the two backpack kinds");
        Check(!_game.Ui.GetNode<Control>("Root/Money").Visible &&
            !_game.Ui.GetNode<Control>("Root/Instructions").Visible,
            "Opening the wheel hides the normal HUD");
        _game.ThrowWheel.SelectAt(center + new Vector2(-220, 0));
        await Frames(4);
        await Screenshot("throwable-wheel.png");
        Check(_game.ThrowWheel.SelectedId == "concussiongrenade" &&
            _game.ThrowWheel.IndexAt(center + new Vector2(-220, 0)) == 1 &&
            _game.ThrowWheel.IndexAt(center + new Vector2(220, 0)) == 0,
            "Wheel sectors follow the mouse direction and pick the flashbang on the left");
        KeyUp(Key.G);
        await Frames(2);
        Check(_game.Ui.GetNode<Control>("Root/Money").Visible,
            "Closing the wheel restores the normal HUD");
        Check(player.IsGrenadeBusy && player.HeldThrowable?.Id == "concussiongrenade" &&
            player.Backpack.ThrowableKinds().Count == 2,
            "Releasing the wheel draws the highlighted flashbang without consuming the stack");
        await WaitForState(() => player.IsGrenadeReady, 120, "Wheel-selected flashbang did not finish drawing");
        Check(!rig.HasNode("HeldWeapon") && rig.HasNode("HeldGrenade"),
            "The AK stays holstered while the wheel-selected flashbang is ready");

        // A second hold while holding switches the kind in place.
        KeyDown(Key.G);
        await Frames(30);
        Check(player.IsThrowWheelOpen && _game.ThrowWheel.SelectedId == "concussiongrenade",
            "Holding G while ready opens the wheel with the held kind preselected");
        _game.ThrowWheel.SelectAt(center + new Vector2(220, 0));
        await Frames(4);
        Check(_game.ThrowWheel.SelectedId == "grenade",
            "Moving to the other sector selects the grenade for an in-hand swap");
        KeyUp(Key.G);
        await Frames(2);
        Check(player.HeldThrowable?.Id == "grenade",
            "Releasing the wheel swaps the ready grenade without restoring the weapon");
        await WaitForState(() => player.IsGrenadeReady, 90, "In-hand throwable swap did not finish");
        Check(player.IsGrenadeReady && rig.HasNode("HeldGrenade") && !rig.HasNode("HeldWeapon") &&
            player.Backpack.ThrowableKinds().Count == 2,
            "The swapped grenade is ready in hand and both kinds remain in the backpack");
        PressKey(Key.G);
        await WaitForState(() => !player.IsGrenadeBusy && !player.IsSwitching, 120,
            "The swapped grenade did not stow");
        Check(player.EquippedWeapon?.Id == "ak47" && rig.HasNode("HeldWeapon"),
            "Tapping G after the swap restores the holstered AK");

        player.GlobalPosition = new Vector3(-2, .02f, 1);
        player.Velocity = Vector3.Zero;
        _game.Camera.SnapToTarget();
        await Frames(4);
    }

    private async Task WaitForState(Func<bool> condition, int maxFrames, string error)
    {
        for (int i = 0; i < maxFrames && !condition(); i++) await Frames(1);
        if (!condition()) throw new InvalidOperationException(error);
    }
}
