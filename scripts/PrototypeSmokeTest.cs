using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using FieldKit.Items;

// Runs only with -- --smoke-test. Exercises the real scene, physics, and input path.
public partial class PrototypeSmokeTest : Node
{
    private Prototype _game = null!;
    private int _checks;
    private Node3D? _reloadRig;
    private float _maximumReloadGripError;

    public override async void _Ready()
    {
        try
        {
            _game = GetParent<Prototype>();
            await Frames(12);
            VerifyAmmoBackpack();
            if (OS.GetCmdlineUserArgs().Contains("--reload-timing-test"))
            {
                await VerifyReloadTiming();
                GD.Print($"RELOAD TIMING PASS: {_checks} checks");
                GetTree().Quit();
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--weapon-audio-test"))
            {
                await VerifyWeaponAudio();
                GD.Print($"WEAPON AUDIO PASS: {_checks} checks");
                GetTree().Quit();
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--inventory-drag-test"))
            {
                await VerifyInventoryDragging();
                GD.Print($"INVENTORY DRAG PASS: {_checks} checks");
                GetTree().Quit();
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--ammo-storage-test"))
            {
                await VerifyFullAmmoStorage();
                GD.Print($"AMMO STORAGE PASS: {_checks} checks");
                GetTree().Quit();
                return;
            }
            Check(_game.Player.IsOnFloor(), "Player rests on the white ground");
            Check(_game.Player.Visual.GetChildCount() > 0, "Millitia model is instantiated");
            Node3D rig = _game.Player.Visual.GetNode<Node3D>("Millitia");
            Node3D leftArm = rig.GetNode<Node3D>("LeftArmPivot");
            Node3D rightArm = rig.GetNode<Node3D>("RightArmPivot");
            Node3D leftFoot = rig.GetNode<Node3D>("LeftFootPivot");
            Vector3 footRest = leftFoot.Position;
            await VerifyPurchases(rig);
            Check(rig.GetNode<Node3D>("HeldWeapon/Model").GetChildCount() > 0, "Purchased AK47 asset is equipped");
            Check(HandError(rig) < 0.025f, "Both palms meet the AK grip and handguard");
            Check(Mathf.IsEqualApprox(_game.Camera.Size, 11), "Camera has the wider requested view");
            Check(_game.Ui.Items.ItemCount == _game.Weapons.Weapons.Count + ItemCatalog.Ammunition.Count, "Weapon shop lists weapons and ammunition");
            await VerifyWeaponConfiguration();
            await VerifyInventory();
            PressKey(Key.E);
            await Frames(2);
            Check(!_game.Ui.IsOpen, "E cannot open the crate from outside reach");

            Vector3 start = _game.Player.GlobalPosition;
            Input.ActionPress("move_right");
            float maximumGripError = 0, maximumStep = 0;
            for (int i = 0; i < 20; i++)
            {
                await Frames(1);
                maximumGripError = Mathf.Max(maximumGripError, HandError(rig));
                maximumStep = Mathf.Max(maximumStep, leftFoot.Position.DistanceTo(footRest));
            }
            Check(maximumStep > 0.06f, "Moving still drives visible foot steps");
            Check(maximumGripError < 0.025f, "Hands remain attached to the rifle while walking");
            Input.ActionRelease("move_right");
            await Frames(2);
            Vector3 step = _game.Player.GlobalPosition - start;
            Check(step.X > 0.9f && Mathf.Abs(step.Z) < 0.02f, "D moves right without changing depth");
            await Frames(15);
            Check(HandError(rig) < 0.025f && leftFoot.Position.DistanceTo(footRest) < 0.001f,
                "Stopping smoothly restores the holding idle pose");
            _game.Camera.SnapToTarget();
            Vector3 expected = _game.Player.GlobalPosition + Vector3.Up * _game.Camera.TargetHeight + _game.Camera.Offset;
            Check(_game.Camera.GlobalPosition.DistanceTo(expected) < 0.001f, "Camera stays locked to the player");

            _game.Player.GlobalPosition = new Vector3(-3, 0.01f, 2);
            _game.Player.Velocity = Vector3.Zero;
            await Frames(3);
            start = _game.Player.GlobalPosition;
            Input.ActionPress("move_right");
            Input.ActionPress("move_forward");
            await Frames(20);
            Input.ActionRelease("move_right");
            Input.ActionRelease("move_forward");
            await Frames(2);
            Vector3 diagonal = _game.Player.GlobalPosition - start;
            Check(diagonal.X > 0.5f && diagonal.Z < -0.5f && diagonal.Length() < 1.5f, "Diagonal movement is normalized and camera relative");

            _game.Camera.SnapToTarget();
            Vector2 center = _game.Camera.UnprojectPosition(_game.Player.GlobalPosition + Vector3.Up * _game.Player.AimHeight);
            _game.Player.FaceScreenPoint(center + new Vector2(250, 0));
            Check((-_game.Player.Visual.GlobalBasis.Z).Dot(Vector3.Right) > 0.98f, "Mouse on the right turns the model right");
            _game.Player.FaceScreenPoint(center + new Vector2(0, -150));
            Check((-_game.Player.Visual.GlobalBasis.Z).Dot(Vector3.Forward) > 0.98f, "Mouse above turns the model forward");

            _game.Player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, 0.02f, 0);
            _game.Player.Velocity = Vector3.Zero;
            await Frames(5);
            Check(_game.Crate.IsInReach(_game.Player), "Crate is reachable from its side");
            PressKey(Key.E);
            await Frames(2);
            Check(_game.Ui.IsOpen && _game.Player.InputBlocked, "E opens the weapon shop and blocks movement");
            PressKey(Key.R);
            await Frames(2);
            Check(!_game.Player.IsReloading, "R is ignored while the crate list is open");
            start = _game.Player.GlobalPosition;
            Input.ActionPress("move_right");
            await Frames(10);
            Input.ActionRelease("move_right");
            Check(_game.Player.GlobalPosition.DistanceTo(start) < 0.02f, "Player remains still while the list is open");
            await Screenshot("interaction-list.png");
            PressKey(Key.Escape);
            await Frames(2);
            Check(!_game.Ui.IsOpen && !_game.Player.InputBlocked, "Escape closes the list and restores movement");
            PressKey(Key.E);
            await Frames(2);
            PressKey(Key.E);
            await Frames(2);
            Check(!_game.Ui.IsOpen, "E also closes the list");
            PressKey(Key.E);
            await Frames(2);
            _game.Ui.GetNode<Button>("Root/Modal/Center/Panel/Margin/Column/Header/Close").EmitSignal(Button.SignalName.Pressed);
            Check(!_game.Ui.IsOpen, "Close button closes the list");
            PressKey(Key.E);
            await Frames(2);
            _game.Player.GlobalPosition = new Vector3(-5, 0.02f, 0);
            await Frames(3);
            Check(!_game.Ui.IsOpen, "Leaving reach closes an open list");

            _game.Player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, 0.02f, 0);
            _game.Player.Velocity = Vector3.Zero;
            await Frames(4);
            Input.ActionPress("move_right");
            await Frames(45);
            Input.ActionRelease("move_right");
            await Frames(2);
            Check(_game.Player.GlobalPosition.X < _game.Crate.GlobalPosition.X - 1.1f, "Crate collision prevents walking through it");
            await Frames(15);
            Check(leftFoot.Position.DistanceTo(footRest) < 0.001f, "Blocked movement does not keep playing the walk cycle");
            _game.Player.GlobalPosition = new Vector3(-0.3f, 0.01f, 1.1f);
            _game.Player.Velocity = Vector3.Zero;
            _game.Camera.SnapToTarget();
            await Frames(6);
            Input.WarpMouse(_game.Camera.UnprojectPosition(_game.Crate.GlobalPosition + Vector3.Up * .8f));
            await Frames(6);
            await Screenshot("prototype.png");
            await VerifyReloadAnimation(rig);
            if (DisplayServer.GetName() != "headless")
            {
                float cameraSize = _game.Camera.Size;
                _game.Camera.Size = 4;
                _game.Player.Visual.Rotation = new Vector3(0, 0.6f, 0);
                _game.Player.InputBlocked = true;
                await Frames(15);
                await Screenshot("character-idle.png");
                _game.Player.InputBlocked = false;
                Input.ActionPress("move_back");
                await Frames(11);
                await Screenshot("character-walk.png");
                Node3D maskArm = _game.GetNode<Node3D>("CrateOutline/SilhouetteMask/PlayerMask/Millitia/LeftArmPivot");
                Check(maskArm.Transform.IsEqualApprox(leftArm.Transform), "Outline occlusion follows animated limbs");
                Input.ActionRelease("move_back");
                _game.Player.GlobalPosition = new Vector3(-0.3f, 0.01f, 1.1f);
                _game.Player.Velocity = Vector3.Zero;
                _game.Camera.Size = cameraSize;
                _game.Camera.SnapToTarget();
                await Frames(15);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var outlined = GetViewport().GetTexture().GetImage();
                var outlineLayer = _game.GetNode<CanvasLayer>("CrateOutline/WhiteOutline");
                outlineLayer.Hide();
                await Frames(3);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var plain = GetViewport().GetTexture().GetImage();
                int brightPixels = 0;
                for (int y = 0; y < plain.GetHeight(); y++)
                    for (int x = 0; x < plain.GetWidth(); x++)
                    {
                        Color a = outlined.GetPixel(x, y), b = plain.GetPixel(x, y);
                        if (a.R > 0.95f && a.G > 0.95f && a.B > 0.95f && a.R - b.R > 0.035f) brightPixels++;
                    }
                Check(brightPixels > 100 && brightPixels < 12000, $"White silhouette is visible ({brightPixels} bright edge pixels)");
                outlineLayer.Show();
                GetWindow().Size = new Vector2I(960, 640);
                await Frames(8);
                var mask = _game.GetNode<SubViewport>("CrateOutline/SilhouetteMask");
                Vector2 visible = GetViewport().GetVisibleRect().Size;
                Check(mask.Size == new Vector2I(Mathf.RoundToInt(visible.X), Mathf.RoundToInt(visible.Y)), "Outline viewport follows window resizing");
                await Screenshot("resized.png");
                GetWindow().Size = new Vector2I(800, 600);
                PressKey(Key.Tab);
                await Frames(8);
                Control inventoryLayout = _game.Inventory.GetNode<Control>("Root/Layout");
                Vector2 inventoryEnd = inventoryLayout.Position + inventoryLayout.Size * inventoryLayout.Scale;
                Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
                Check(inventoryLayout.Position.X >= 0 && inventoryLayout.Position.Y >= 0 &&
                    inventoryEnd.X <= viewportSize.X && inventoryEnd.Y <= viewportSize.Y,
                    "Inventory fits the minimum 800 by 600 window");
                await Screenshot("inventory-small.png");
                PressKey(Key.Escape);
                await Frames(2);
                _game.Player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
                _game.Player.Velocity = Vector3.Zero;
                await Frames(3);
                PressKey(Key.E);
                await Frames(3);
                Control shopPanel = _game.Ui.GetNode<Control>("Root/Modal/Center/Panel");
                Rect2 shopBounds = shopPanel.GetGlobalRect();
                Check(shopBounds.Position.X >= 0 && shopBounds.Position.Y >= 0 &&
                    shopBounds.End.X <= viewportSize.X && shopBounds.End.Y <= viewportSize.Y,
                    "Weapon shop fits the minimum 800 by 600 window");
                await Screenshot("weapon-shop-small.png");
                PressKey(Key.Escape);
            }
            GD.Print($"SMOKE PASS: {_checks} checks");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"SMOKE FAIL: {error}");
            GetTree().Quit(1);
        }
    }

    private void Check(bool success, string description)
    {
        if (!success) throw new InvalidOperationException(description);
        _checks++;
        GD.Print($"PASS: {description}");
    }

    private async Task VerifyPurchases(Node3D rig)
    {
        var player = _game.Player;
        var weapon = _game.Weapons.Get("ak47");
        int originalPrice = weapon.Stats.Price;
        Vector3 start = player.GlobalPosition;
        Check(player.Money == 5000 && player.WeaponSlots.All(w => w == null) && player.EquippedWeapon == null,
            "New game starts with 5000 dollars and two empty weapon slots");
        Check(!rig.HasNode("HeldWeapon") && Mathf.IsEqualApprox(player.EquippedMoveSpeed, player.MoveSpeed),
            "Unarmed character has no weapon model or weapon movement penalty");
        PressKey(Key.R);
        PressKey(Key.Key1);
        PressKey(Key.Key2);
        await Frames(2);
        Check(!player.IsReloading && player.EquippedWeapon == null, "Reload and switching empty slots are safe");
        player.Visual.Rotation = Vector3.Zero;
        _game.Camera.SnapToTarget();
        player.FaceScreenPoint(_game.Camera.UnprojectPosition(player.GlobalPosition + Vector3.Right * 3 + Vector3.Up * player.AimHeight));
        Check(Mathf.Abs(player.Visual.Rotation.Y) > .01f, "Unarmed aiming works without a weapon definition");
        Input.ActionPress("move_left");
        await Frames(8);
        Input.ActionRelease("move_left");
        Check(player.GlobalPosition.DistanceTo(start) > .2f, "Unarmed movement works at the base speed");
        PressKey(Key.Tab);
        await Frames(3);
        Check(!_game.Inventory.HasNode("Root/Layout/PrimaryWeapon/WeaponPreview") &&
            !_game.Inventory.HasNode("Root/Layout/SecondaryWeapon/WeaponPreview"), "Both starting inventory cards have no weapon preview");
        await Screenshot("inventory-empty.png");
        PressKey(Key.Tab);
        await Frames(2);
        Check(!player.TryPurchaseWeapon(weapon.Id, out _) && player.Money == 5000, "Closed shop cannot purchase or charge money");
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4);
        weapon.Stats.Price = 5001;
        PressKey(Key.E);
        await Frames(2);
        Check(_game.Ui.BuyButton.Disabled && !player.TryPurchaseWeapon(weapon.Id, out var insufficient) &&
            insufficient.Contains("资金不足") && player.Money == 5000 && player.WeaponSlots.All(w => w == null),
            "Unaffordable purchase is disabled and never charges or equips");
        Check(!player.TryPurchaseWeapon("missing", out _) && player.Money == 5000, "Unknown weapon cannot be purchased");
        Vector3 atCrate = player.GlobalPosition;
        player.GlobalPosition = new Vector3(-10, .02f, 0);
        Check(!player.TryPurchaseWeapon(weapon.Id, out var tooFar) && tooFar.Contains("靠近") && player.Money == 5000,
            "Purchase rechecks reach even before an open shop automatically closes");
        player.GlobalPosition = atCrate;
        PressKey(Key.E);
        await Frames(2);
        weapon.Stats.Price = 2500;
        PressKey(Key.E);
        await Frames(3);
        Check(!_game.Ui.BuyButton.Disabled, "Affordable selected weapon enables purchase");
        await Screenshot("weapon-shop.png");
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Check(player.Money == 2500 && player.WeaponSlots[0] == weapon && player.WeaponSlots[1] == null && player.ActiveWeaponSlot == 0,
            "First UI purchase charges once and automatically equips slot one");
        Check(rig.HasNode("HeldWeapon/Model") &&
            _game.Inventory.HasNode("Root/Layout/PrimaryWeapon/WeaponPreview") &&
            !_game.Inventory.HasNode("Root/Layout/SecondaryWeapon/WeaponPreview"), "First purchase updates character and only the first inventory preview");
        Check(_game.Ui.GetNode<Label>("Root/Money/Text").Text.Contains("2500$") &&
            _game.Inventory.GetNode<Label>("Root/Layout/Money").Text.Contains("2500$"), "HUD and inventory display the remaining balance");
        PressKey(Key.Tab);
        await Frames(3);
        await Screenshot("inventory-one-weapon.png");
        PressKey(Key.Tab);
        await Frames(2);
        PressKey(Key.E);
        await Frames(2);
        Check(_game.Ui.IsOpen && !_game.Ui.BuyButton.Disabled, "Reopening shop allows purchase into the remaining slot");
        PressKey(Key.Escape);
        await Frames(2);
        PressKey(Key.R);
        await Frames(3);
        Check(!player.IsReloading && player.MagazineAmmo(0) == 0 && player.Backpack.Count == 0,
            "Newly purchased gun has an empty magazine and cannot reload without matching ammunition");
        PressKey(Key.E);
        await Frames(2);
        SelectShopItem(_game.Weapons.Weapons.Count);
        await Screenshot("ammunition-shop.png");
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Check(player.Money == 2250 && player.Backpack.Count == 1 && player.Backpack.Slots[0] is { TotalAmmo: 40, RemainingAmmo: 40 },
            "Buying medium ammunition charges 250 and creates a separate 40/40 item in the first small slot");
        Check(player.WeaponSlots[1] == null && player.ActiveWeaponSlot == 0,
            "Buying ammunition never occupies or changes the equipped weapon slots");
        Check(_game.Inventory.GetNode<Label>("Root/Layout/Storage/Slot01/AmmoAmount").Text == "40 / 40",
            "Backpack item shows its remaining and total ammunition");
        // Leave exact funds for the second gun without changing the saved catalog.
        weapon.Stats.Price = 2250;
        SelectShopItem(0);
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Check(player.Money == 0 && player.WeaponSlots.All(w => w != null) && player.ActiveWeaponSlot == 1,
            "Exact remaining funds buy a second weapon into slot two");
        Check(_game.Inventory.HasNode("Root/Layout/SecondaryWeapon/WeaponPreview"), "Second purchase creates the second slot preview");
        SelectShopItem(_game.Weapons.Weapons.Count);
        Check(_game.Ui.BuyButton.Disabled && !player.TryPurchaseAmmo(ItemCatalog.MediumAmmo.Id, out var noMoney) &&
            noMoney.Contains("资金不足") && player.Money == 0 && player.Backpack.Count == 1,
            "Unaffordable ammunition is disabled and does not create an item or deduct money");
        SelectShopItem(0);
        weapon.Stats.Price = 0;
        Check(_game.Ui.BuyButton.Disabled && !player.TryPurchaseWeapon(weapon.Id, out var full) && full.Contains("已满") && player.Money == 0,
            "Full slots reject even a free weapon without replacing equipment");
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        Check(player.WeaponSlots.All(w => w == weapon) && player.Money == 0, "Repeated purchase events cannot overfill slots or charge twice");
        weapon.Stats.Price = originalPrice;
        PressKey(Key.Key1);
        await Frames(2);
        Check(player.ActiveWeaponSlot == 1, "Shop blocks weapon switching");
        PressKey(Key.Escape);
        await Frames(2);
        PressKey(Key.Key1);
        await Frames(3);
        Check(player.ActiveWeaponSlot == 0 && HandError(rig) < .025f, "Key one selects the first purchased weapon with valid hand targets");
        PressKey(Key.Key2);
        await Frames(3);
        Check(player.ActiveWeaponSlot == 1 && rig.GetChildren().Count(n => n is Node3D && n.Name == "HeldWeapon") == 1,
            "Key two selects the second weapon without duplicating held props");
        Check(_game.GetNode<Node3D>("CrateOutline/SilhouetteMask/PlayerMask/Millitia").HasNode("HeldWeapon/Model"),
            "Outline occlusion updates when held equipment changes");
        weapon.Stats.Price = originalPrice;
        player.GlobalPosition = start;
        player.Velocity = Vector3.Zero;
        _game.Camera.SnapToTarget();
        await Frames(12);
    }

    private async Task VerifyWeaponConfiguration()
    {
        Check(_game.Weapons.Weapons.Count == 1 && _game.Player.EquippedWeapon?.Id == "ak47",
            "Game loads the shared weapon catalog and assigns AK properties");
        foreach (var weapon in _game.Weapons.Weapons)
            Check(ResourceLoader.Exists(weapon.ScenePath), $"Weapon scene loads: {weapon.Id}");
        var preview = _game.Inventory.GetNode<WeaponModelPreview>("Root/Layout/PrimaryWeapon/WeaponPreview");
        Check(preview.Model.SceneFilePath == _game.Player.EquippedWeapon!.ScenePath && preview.OwnWorld3D,
            "Inventory previews the configured 3D scene in an isolated world");
        Check(preview.RenderTargetUpdateMode == SubViewport.UpdateMode.Disabled, "Closed inventory does not render the weapon preview");
        Check(_game.Player.Visual.GetNode<Node3D>("Millitia/HeldWeapon/Model").SceneFilePath == _game.Player.EquippedWeapon.ScenePath,
            "Held weapon instantiates the configured scene");
        var equipped = _game.Player.EquippedWeapon;
        VerifyTurnFollow(equipped);
        var originalStats = equipped.Stats;
        Vector3 start = _game.Player.GlobalPosition;
        equipped.Stats = originalStats with { MovementInertiaPercent = 50 };
        Check(Mathf.IsEqualApprox(_game.Player.EquippedMoveSpeed, _game.Player.MoveSpeed * .5f),
            "Movement inertia scales the holder's movement speed");
        Input.ActionPress("move_left");
        await Frames(15);
        Input.ActionRelease("move_left");
        await Frames(2);
        float distance = _game.Player.GlobalPosition.DistanceTo(start);
        Check(distance > .35f && distance < .65f, "50 percent inertia produces half-speed physical movement");
        equipped.Stats = originalStats with { MovementInertiaPercent = 100 };
        Vector3 stopped = _game.Player.GlobalPosition;
        Input.ActionPress("move_left");
        await Frames(10);
        Input.ActionRelease("move_left");
        Check(_game.Player.GlobalPosition.DistanceTo(stopped) < .02f, "100 percent inertia prevents horizontal movement");
        // Subsequent movement/animation tests use fixed tuning, independent of the
        // designer's saved values. No test changes the actual configuration file.
        equipped.Stats = originalStats with { MovementInertiaPercent = 0, TurnFollowSpeed = 1000, MagazineCapacity = 30 };
        _game.Player.GlobalPosition = start;
        _game.Player.Velocity = Vector3.Zero;
        _game.Camera.SnapToTarget();
        await Frames(15);
    }

    private void VerifyTurnFollow(FieldKit.Weapons.WeaponDefinition equipped)
    {
        var originalStats = equipped.Stats;
        _game.Camera.SnapToTarget();
        Vector3 aimOrigin = _game.Player.GlobalPosition + Vector3.Up * _game.Player.AimHeight;
        Vector2 Target(float angle) => _game.Camera.UnprojectPosition(aimOrigin + new Vector3(-Mathf.Sin(angle),0,-Mathf.Cos(angle)) * 3);
        float Turn(float angle, float delta, double coefficient)
        {
            equipped.Stats = originalStats with { TurnFollowSpeed = coefficient };
            _game.Player.Visual.GlobalRotation = Vector3.Zero;
            _game.Player.FaceScreenPoint(Target(angle), delta);
            return _game.Player.Visual.GlobalRotation.Y;
        }
        float step45 = Turn(Mathf.Pi/4, 1f/60, 10);
        float step90 = Turn(Mathf.Pi/2, 1f/60, 10);
        Check(step90 > 0 && step90 < Mathf.Pi/2 && Mathf.Abs(step90 - step45*2) < .001f,
            "Larger aim error produces proportionally faster turning without snapping");
        Check(Turn(Mathf.Pi/2,1f/60,20) > step90 && Mathf.Abs(Turn(Mathf.Pi/2,1f/60,0)) < .0001f,
            "Turn coefficient controls follow speed and zero stops turning");
        float Simulate(int fps)
        {
            equipped.Stats = originalStats with { TurnFollowSpeed = 10 };
            _game.Player.Visual.GlobalRotation = Vector3.Zero;
            for (int i = 0; i < fps/2; i++) _game.Player.FaceScreenPoint(Target(Mathf.Pi/2),1f/fps);
            return _game.Player.Visual.GlobalRotation.Y;
        }
        Check(Mathf.Abs(Simulate(30)-Simulate(120)) < .0001f, "Turning matches across 30 and 120 FPS");
        _game.Player.Visual.GlobalRotation = new Vector3(0,Mathf.DegToRad(179),0);
        _game.Player.FaceScreenPoint(Target(Mathf.DegToRad(-179)),1f/60);
        Check(Mathf.Abs(_game.Player.Visual.GlobalRotation.Y) > Mathf.DegToRad(178), "Turning follows the short arc across angle wrap");
        equipped.Stats = originalStats;
    }

    private async Task VerifyInventory()
    {
        Check(!_game.Inventory.IsOpen, "Inventory is closed on startup");
        PressKey(Key.Tab);
        await Frames(4);
        Check(_game.Inventory.IsOpen && _game.Player.InputBlocked, "Tab opens inventory away from the crate");
        Check(_game.Inventory.SlotCount == 16 &&
            _game.Inventory.GetNode<Control>("Root/Layout/Storage").GetChildCount() == 16,
            "Inventory provides two rows of eight item slots");
        Vector3 start = _game.Player.GlobalPosition;
        Basis facing = _game.Player.Visual.Basis;
        Input.ActionPress("move_right");
        PressKey(Key.R);
        PressKey(Key.E);
        await Frames(8);
        Input.ActionRelease("move_right");
        Check(_game.Player.GlobalPosition.DistanceTo(start) < 0.02f &&
            _game.Player.Visual.Basis.IsEqualApprox(facing), "Inventory blocks movement and aiming");
        Check(!_game.Player.IsReloading && !_game.Ui.IsOpen, "Inventory blocks reload and crate interaction");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Tab, PhysicalKeycode = Key.Tab, Pressed = true, Echo = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Tab, PhysicalKeycode = Key.Tab, Pressed = false });
        await Frames(2);
        Check(_game.Inventory.IsOpen, "Holding Tab does not repeatedly toggle inventory");
        await Screenshot("inventory.png");
        PressKey(Key.Tab);
        await Frames(2);
        Check(!_game.Inventory.IsOpen && !_game.Player.InputBlocked, "Tab closes inventory and restores input");
        PressKey(Key.Tab);
        await Frames(2);
        PressKey(Key.Escape);
        await Frames(2);
        Check(!_game.Inventory.IsOpen && !_game.Player.InputBlocked, "Escape closes inventory");
        PressKey(Key.Tab);
        await Frames(2);
        _game.Inventory.GetNode<Button>("Root/Layout/Close").EmitSignal(Button.SignalName.Pressed);
        Check(!_game.Inventory.IsOpen && !_game.Player.InputBlocked, "Inventory close button restores input");
        _game.Player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, 0.02f, 0);
        _game.Player.Velocity = Vector3.Zero;
        await Frames(4);
        Check(_game.Crate.IsInReach(_game.Player), "Crate is in reach for the inventory switch");
        PressKey(Key.E);
        await Frames(2);
        Check(_game.Ui.IsOpen, "Crate opens before switching to inventory");
        PressKey(Key.Tab);
        await Frames(2);
        Check(_game.Inventory.IsOpen && !_game.Ui.IsOpen && _game.Player.InputBlocked,
            "Tab replaces crate modal with inventory without unblocking input");
        PressKey(Key.Tab);
        _game.Player.GlobalPosition = start;
        _game.Player.Velocity = Vector3.Zero;
        _game.Camera.SnapToTarget();
        await Frames(4);
    }

    private static float HandError(Node3D rig, bool rightOnly = false, bool followReloadTarget = false)
    {
        float right = rig.GetNode<Node3D>("RightArmPivot/Elbow/HandGrip").GlobalPosition.DistanceTo(
            rig.GetNode<Node3D>("HeldWeapon/RightGrip").GlobalPosition);
        if (rightOnly) return right;
        float left = rig.GetNode<Node3D>("LeftArmPivot/Elbow/HandGrip").GlobalPosition.DistanceTo(
            rig.GetNode<Node3D>(followReloadTarget ? "HeldWeapon/LeftHandTarget" : "HeldWeapon/SupportGrip").GlobalPosition);
        return Mathf.Max(left, right);
    }

    private async Task VerifyReloadAnimation(Node3D rig)
    {
        _reloadRig = rig;
        _maximumReloadGripError = 0;
        PressKey(Key.R);
        await Frames(8);
        Check(_game.Player.IsReloading && _game.Player.Backpack.Available(AmmoType.Medium) == 40,
            "Reload start reserves no ammunition and does not load rounds before the animation completes");
        PressKey(Key.Key1);
        await Frames(3);
        Check(!_game.Player.IsReloading && _game.Player.MagazineAmmo(0) == 0 && _game.Player.MagazineAmmo(1) == 0 &&
            _game.Player.Backpack.Available(AmmoType.Medium) == 40, "Switching guns cancels reload without consuming or granting rounds");
        await Frames(165);
        Check(_game.Player.Backpack.Available(AmmoType.Medium) == 40 && _game.Player.MagazineAmmo(0) == 0,
            "Cancelled reload cannot complete later on the newly selected gun");
        PressKey(Key.Key2);
        await Frames(3);
        Node3D magazine = rig.GetNode<Node3D>("HeldWeapon/MagazineMotion");
        Transform3D magazineRest = magazine.Transform;
        Vector3 playerPosition = _game.Player.GlobalPosition;
        float cameraSize = _game.Camera.Size;
        if (DisplayServer.GetName() != "headless") _game.Camera.Size = 4;
        PressKey(Key.R);
        await Frames(8);
        Check(_game.Player.IsReloading, "R triggers the reload animation");
        double elapsed = _game.Player.ReloadElapsed;
        PressKey(Key.R);
        await Frames(2);
        Check(_game.Player.ReloadElapsed >= elapsed, "Pressing R again does not restart an active reload");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, PhysicalKeycode = Key.R, Pressed = true, Echo = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, PhysicalKeycode = Key.R, Pressed = false });
        await Frames(2);
        Check(_game.Player.ReloadElapsed >= elapsed, "Key-repeat events do not restart the animation");
        Check(_game.Player.MagazineAmmo(1) == 0 && _game.Player.Backpack.Available(AmmoType.Medium) == 40,
            "Repeated reload presses do not duplicate or consume ammunition early");
        _game.Player.InputBlocked = true;
        _game.Player.Visual.Rotation = new Vector3(0, 2.7f, 0);
        await Screenshot("reload-01-start.png");
        await ReloadUntil(0.98f);
        Check(magazine.Position.DistanceTo(magazineRest.Origin) > 0.20f, "Magazine visibly leaves the rifle during reload");
        Check(HandError(rig, true) < 0.025f, "Right hand stays on the rifle during reload");
        await Screenshot("reload-02-magazine-out.png");
        await ReloadUntil(1.6f);
        await Screenshot("reload-03-inserting.png");
        await ReloadUntil(2.13f);
        Check(magazine.Position.DistanceTo(magazineRest.Origin) < 0.002f, "Magazine seats back into the rifle");
        await Screenshot("reload-04-finish.png");
        await ReloadUntil(_game.Player.ReloadDuration);
        Check(!_game.Player.IsReloading && magazine.Transform.IsEqualApprox(magazineRest), "Reload finishes and fully resets the magazine pose");
        Check(HandError(rig) < 0.025f, "Both hands return to their holding positions");
        Check(_game.Player.MagazineAmmo(1) == 30 && _game.Player.MagazineAmmo(0) == 0 &&
            _game.Player.Backpack.Slots[0] is { TotalAmmo: 40, RemainingAmmo: 10 },
            "Reload fills only the active 30-round magazine and leaves a 10/40 ammunition item");
        Check(_game.Inventory.GetNode<Label>("Root/Layout/Storage/Slot01/AmmoAmount").Text == "10 / 40" &&
            _game.Inventory.GetNode<Label>("Root/Layout/SecondaryWeapon/MagazineAmmo").Text == "30 / 30",
            "Reload completion updates the inventory magazine and partial ammunition pack");
        _game.Player.InputBlocked = false;
        PressKey(Key.R);
        await Frames(2);
        Check(!_game.Player.IsReloading && _game.Player.Backpack.Available(AmmoType.Medium) == 10,
            "Full magazine blocks reload without wasting the remaining ammunition");
        PressKey(Key.Tab);
        await Frames(3);
        await Screenshot("inventory-ammunition.png");
        PressKey(Key.Tab);
        await Frames(2);
        PressKey(Key.Key1);
        await Frames(3);
        PressKey(Key.R);
        await Frames(2);
        Check(_game.Player.IsReloading, "The other empty magazine reloads using the remaining partial pack");
        Vector3 beforeMove = _game.Player.GlobalPosition;
        Input.ActionPress("move_left");
        await Frames(12);
        Input.ActionRelease("move_left");
        Check(_game.Player.IsReloading && _game.Player.GlobalPosition.DistanceTo(beforeMove) > 0.4f,
            "Reload animation coexists with character movement");
        await ReloadUntil(_game.Player.ReloadDuration);
        Check(_game.Player.MagazineAmmo(0) == 10 && _game.Player.MagazineAmmo(1) == 30 && _game.Player.Backpack.Count == 0,
            "Insufficient reserve loads the available 10 rounds, removes the exhausted pack and keeps gun magazines independent");
        PressKey(Key.R);
        await Frames(3);
        Check(!_game.Player.IsReloading && _game.Player.MagazineAmmo(0) == 10,
            "Empty backpack prevents further reloads instead of creating ammunition");
        _game.Player.GlobalPosition = playerPosition;
        _game.Player.Velocity = Vector3.Zero;
        _game.Camera.Size = cameraSize;
        _game.Camera.SnapToTarget();
        await Frames(15);
        Check(_maximumReloadGripError < 0.025f,
            $"Both hands track the enlarged rifle throughout reload (max error {_maximumReloadGripError * 100:F2} cm)");
        _reloadRig = null;
    }

    private void SelectShopItem(int index)
    {
        _game.Ui.Items.Select(index);
        _game.Ui.Items.EmitSignal(ItemList.SignalName.ItemSelected, (long)index);
    }

    private void VerifyAmmoBackpack()
    {
        var bag = new AmmoBackpack();
        var other = ItemCatalog.MediumAmmo with { Id = "test_other", AmmoType = (AmmoType)999 };
        Check(bag.TryAdd(other) && bag.TryAdd(ItemCatalog.MediumAmmo) && bag.TryAdd(ItemCatalog.MediumAmmo),
            "Each ammunition purchase creates its own small-slot item");
        Check(!ReferenceEquals(bag.Slots[1], bag.Slots[2]), "Identical ammunition items keep independent remaining counts");
        Check(bag.Take(AmmoType.Medium, 30) == 30 && bag.Slots[0]!.RemainingAmmo == 40 && bag.Slots[1]!.RemainingAmmo == 10,
            "Reload skips incompatible ammunition and preserves the partly used pack");
        Check(bag.Take(AmmoType.Medium, 25) == 25 && bag.Slots[1] == null && bag.Slots[2]!.RemainingAmmo == 25 && bag.Count == 2,
            "One reload spans matching packs, takes only missing rounds and frees exhausted slots");
        Check(bag.TryAdd(ItemCatalog.MediumAmmo) && bag.Slots[1] is { RemainingAmmo: 40 }, "New ammunition reuses the first freed slot");
        Check(bag.Take(AmmoType.Medium, 100) == 65 && bag.Available(AmmoType.Medium) == 0 && bag.Count == 1,
            "Ammunition shortage transfers only the actual reserve without consuming a different type");
        Check(bag.Take(AmmoType.Medium, 30) == 0 && bag.Slots[0]!.RemainingAmmo == 40,
            "A backpack containing only incompatible ammo cannot supply the weapon");
        Check(bag.Take((AmmoType)999, 0) == 0 && bag.Slots[0]!.RemainingAmmo == 40,
            "A full magazine requesting zero rounds leaves all items untouched");
    }

    private async Task VerifyFullAmmoStorage()
    {
        var player = _game.Player;
        string id = ItemCatalog.MediumAmmo.Id;
        Check(!player.TryPurchaseAmmo(id, out _) && player.Money == 5000 && player.Backpack.Count == 0,
            "Ammunition cannot be purchased with the shop closed");
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4);
        PressKey(Key.E);
        await Frames(2);
        SelectShopItem(_game.Weapons.Weapons.Count);
        Check(!player.TryPurchaseAmmo("missing", out _) && player.Money == 5000, "Unknown ammunition cannot be bought");
        Vector3 near = player.GlobalPosition;
        player.GlobalPosition = new Vector3(-10, .02f, 0);
        Check(!player.TryPurchaseAmmo(id, out _) && player.Money == 5000, "Ammunition purchase rechecks crate reach");
        player.GlobalPosition = near;
        for (int i = 0; i < AmmoBackpack.Capacity; i++) _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Check(player.Money == 1000 && player.Backpack.Count == 16 && player.WeaponSlots.All(w => w == null) &&
            player.Backpack.Slots.All(item => item is { TotalAmmo: 40, RemainingAmmo: 40 }),
            "Sixteen UI purchases cost 4000 and occupy exactly the sixteen small slots");
        Check(_game.Ui.BuyButton.Disabled && !player.TryPurchaseAmmo(id, out var full) && full.Contains("已满") && player.Money == 1000,
            "Full item storage blocks purchase even when the player can afford it");
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        Check(player.Money == 1000 && player.Backpack.Count == 16, "Repeated purchase on a full backpack cannot charge or overflow");
        PressKey(Key.Tab);
        await Frames(3);
        Check(_game.Inventory.GetNode<Label>("Root/Layout/StorageCount").Text == "16 / 16", "Inventory displays all occupied small slots");
        await Screenshot("inventory-ammunition-full.png");
        if (DisplayServer.GetName() != "headless")
        {
            GetWindow().Size = new Vector2I(800, 600);
            await Frames(6);
            await Screenshot("inventory-ammunition-full-small.png");
        }
    }

    private Button ItemSlot(int index) => _game.Inventory.GetNode<Button>($"Root/Layout/Storage/Slot{index + 1:00}");

    private void MoveMouse(Vector2 position, Vector2 relative, bool held = false) => GetViewport().PushInput(
        new InputEventMouseMotion { Position = position, GlobalPosition = position, Relative = relative,
            ButtonMask = held ? MouseButtonMask.Left : 0 }, true);

    private void MouseButtonAt(Vector2 position, bool pressed) => GetViewport().PushInput(
        new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left,
            Pressed = pressed, ButtonMask = pressed ? MouseButtonMask.Left : 0 }, true);

    private async Task BeginDrag(int slot)
    {
        Vector2 start = ItemSlot(slot).GetGlobalRect().GetCenter();
        MoveMouse(start, Vector2.Zero);
        MouseButtonAt(start, true);
        await Frames(2);
        MoveMouse(start + new Vector2(25, 0), new Vector2(25, 0), true);
        await Frames(2);
    }

    private async Task DropAt(Vector2 position)
    {
        MoveMouse(position, new Vector2(25, 0), true);
        await Frames(2);
        MouseButtonAt(position, false);
        await Frames(3);
    }

    private async Task VerifyInventoryDragging()
    {
        var player = _game.Player;
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4);
        PressKey(Key.E);
        await Frames(2);
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        SelectShopItem(_game.Weapons.Weapons.Count);
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        PressKey(Key.Escape);
        await Frames(3);
        PressKey(Key.R);
        await Frames(3);
        await ReloadUntil(_game.Player.ReloadDuration);
        var partial = player.Backpack.Slots[0]!;
        var full = player.Backpack.Slots[1]!;
        int money = player.Money;
        Check(partial.RemainingAmmo == 10 && full.RemainingAmmo == 40, "Drag scenario has independent partial and full ammunition packs");
        Check(!player.TryMoveBackpackItem(0, 15, partial), "Inventory movement is unavailable while the backpack is closed");
        PressKey(Key.Tab);
        await Frames(6);
        var iconPreview = _game.Inventory.GetNode<WeaponModelPreview>("ItemPreview_medium_ammo");
        Check(iconPreview.Model.SceneFilePath == "res://assets/weapons/ammo/bullet_ak_triple.glb" && iconPreview.OwnWorld3D,
            "Ammunition icon renders the requested bullet_ak_triple model in an isolated preview");
        Check(ReferenceEquals(ItemSlot(0).GetNode<TextureRect>("ItemIcon").Texture, ItemSlot(1).GetNode<TextureRect>("ItemIcon").Texture),
            "Identical ammunition slots share one model preview texture");
        await BeginDrag(0);
        Check(GetViewport().GuiIsDragging(), "Holding and moving the mouse starts the native inventory drag");
        await Screenshot("inventory-dragging.png");
        await DropAt(ItemSlot(15).GetGlobalRect().GetCenter());
        Check(ReferenceEquals(player.Backpack.Slots[15], partial) && player.Backpack.Slots[0] == null && partial.RemainingAmmo == 10,
            "Dropping into an empty slot moves the actual partial pack without resetting its ammunition");
        await BeginDrag(15);
        await DropAt(ItemSlot(1).GetGlobalRect().GetCenter());
        Check(ReferenceEquals(player.Backpack.Slots[1], partial) && ReferenceEquals(player.Backpack.Slots[15], full) && full.RemainingAmmo == 40,
            "Dropping onto an occupied slot swaps the two independent item instances");
        Check(player.Money == money && player.Backpack.Count == 2 && player.Backpack.Available(AmmoType.Medium) == 50,
            "Dragging preserves currency, item count and all remaining ammunition");
        Check(ItemSlot(1).GetNode<Label>("AmmoAmount").Text == "10 / 40" && ItemSlot(15).GetNode<Label>("AmmoAmount").Text == "40 / 40",
            "Destination slots immediately display the moved counts");
        await BeginDrag(1);
        await DropAt(new Vector2(20, GetViewport().GetVisibleRect().Size.Y / 2));
        Check(ReferenceEquals(player.Backpack.Slots[1], partial) && !GetViewport().GuiIsDragging(), "Dropping outside the item slots cancels without losing the item");
        await BeginDrag(1);
        await DropAt(_game.Inventory.GetNode<Control>("Root/Layout/PrimaryWeapon").GetGlobalRect().GetCenter());
        Check(ReferenceEquals(player.Backpack.Slots[1], partial), "An ammunition item cannot be dropped into a weapon slot");
        await BeginDrag(1);
        await DropAt(ItemSlot(1).GetGlobalRect().GetCenter());
        Check(ReferenceEquals(player.Backpack.Slots[1], partial) && player.Backpack.Count == 2, "Dropping onto the source slot does not duplicate the item");
        await BeginDrag(0);
        Check(!GetViewport().GuiIsDragging(), "Empty slots cannot initiate a drag");
        MouseButtonAt(ItemSlot(0).GetGlobalRect().GetCenter(), false);
        await BeginDrag(1);
        PressKey(Key.Escape);
        await Frames(3);
        Check(!_game.Inventory.IsOpen && !GetViewport().GuiIsDragging() && ReferenceEquals(player.Backpack.Slots[1], partial),
            "Closing the inventory cancels an active drag without changing the source");
        PressKey(Key.Tab);
        await Frames(3);
        await DropAt(ItemSlot(0).GetGlobalRect().GetCenter());
        Check(ReferenceEquals(player.Backpack.Slots[1], partial) && player.Backpack.Slots[0] == null,
            "A cancelled drag cannot move an item after reopening the inventory");
        await Screenshot("inventory-items-reordered.png");
        await BeginDrag(1);
        await DropAt(ItemSlot(0).GetGlobalRect().GetCenter());
        // Complete a real reload while its source pack is being dragged. The
        // consumed instance must invalidate the drag, never resurrect the pack.
        PressKey(Key.Tab);
        await Frames(2);
        PressKey(Key.E);
        await Frames(2);
        _game.Weapons.Get("ak47").Stats.Price = money;
        SelectShopItem(0);
        _game.Ui.BuyButton.EmitSignal(Button.SignalName.Pressed);
        PressKey(Key.Escape);
        await Frames(2);
        PressKey(Key.R);
        await Frames(3);
        PressKey(Key.Tab);
        await Frames(3);
        await BeginDrag(0);
        Check(GetViewport().GuiIsDragging() && player.IsReloading, "Inventory can be rearranged while an earlier reload is completing");
        await ReloadUntil(_game.Player.ReloadDuration);
        await Frames(2);
        Check(!GetViewport().GuiIsDragging() && player.Backpack.Slots[0] == null && full.RemainingAmmo == 20 && player.MagazineAmmo(1) == 30,
            "Reload finds moved packs in their new slots and cancels dragging an exhausted source");
        await DropAt(ItemSlot(3).GetGlobalRect().GetCenter());
        Check(player.Backpack.Count == 1 && player.Backpack.Slots[3] == null, "Releasing a stale drag cannot recreate consumed ammunition");
        if (DisplayServer.GetName() != "headless")
        {
            GetWindow().Size = new Vector2I(800, 600);
            await Frames(6);
        }
        await BeginDrag(15);
        await DropAt(ItemSlot(2).GetGlobalRect().GetCenter());
        Check(ReferenceEquals(player.Backpack.Slots[2], full) && player.Backpack.Slots[15] == null && full.RemainingAmmo == 20,
            "Drag coordinates remain correct after resizing the inventory");
        await Screenshot("inventory-items-small.png");
        PressKey(Key.Tab);
        await Frames(2);
        Check(iconPreview.RenderTargetUpdateMode == SubViewport.UpdateMode.Disabled, "Closed inventory stops the shared ammunition icon preview");
    }

    private async Task ReloadUntil(double seconds)
    {
        for (int i = 0; i < 360 && _game.Player.IsReloading && _game.Player.ReloadElapsed < seconds; i++)
            await Frames(1);
        if (_game.Player.IsReloading && _game.Player.ReloadElapsed < seconds)
            throw new InvalidOperationException("Reload timeline did not advance");
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (_reloadRig != null && _game.Player.IsReloading)
                _maximumReloadGripError = Mathf.Max(_maximumReloadGripError, HandError(_reloadRig, followReloadTarget: true));
        }
    }

    private void PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }

    private async Task Screenshot(string filename)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng("res://artifacts/" + filename);
    }
}
