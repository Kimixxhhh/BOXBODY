using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FieldKit.Weapons;
using FieldKit.Items;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float MoveSpeed { get; set; } = 4.0f;
    [Export] public float Gravity { get; set; } = 22.0f;
    [Export] public float AimHeight { get; set; } = 0.94f;
    private bool _inputBlocked;
    public bool InputBlocked
    {
        get => _inputBlocked;
        set { _inputBlocked = value; if (value) { ReleaseTrigger(); CancelThrowSelection(); } }
    }
    public const int InitialMoney = 5000;
    public const int WeaponSlotCount = 2;
    public int Money { get; private set; } = InitialMoney;
    public int ActiveWeaponSlot { get; private set; } = -1;
    private readonly WeaponDefinition?[] _weaponSlots = new WeaponDefinition?[WeaponSlotCount];
    public IReadOnlyList<WeaponDefinition?> WeaponSlots { get; }
    public WeaponDefinition? EquippedWeapon => ActiveWeaponSlot < 0 ? null : _weaponSlots[ActiveWeaponSlot];
    public event Action? LoadoutChanged;
    public event Action? InventoryChanged;
    public event Action? WeaponVisualChanged;
    public event Action? GrenadeStateChanged;
    public event Action<string>? ReloadFeedback;
    public event Action<string>? ThrowFeedback;
    public ItemBackpack Backpack { get; } = new();
    private readonly int[] _magazineAmmo = new int[WeaponSlotCount];
    private int _reloadSlot = -1;
    public int MagazineAmmo(int slot) => slot >= 0 && slot < WeaponSlotCount ? _magazineAmmo[slot] : 0;
    public float EquippedMoveSpeed => MoveSpeed * (float)(_animator?.IsGrenadeBusy == true ? 1 : EquippedWeapon?.Stats.MovementSpeedMultiplier() ?? 1);
    public Node3D Visual { get; private set; } = null!;
    public FollowCamera Camera { get; set; } = null!;
    private CharacterAnimator _animator = null!;
    internal WeaponAudioPlayer WeaponAudio { get; private set; } = null!;
    public bool IsReloading => _animator.Weapon?.IsReloading ?? false;
    public bool IsSwitching => _animator.IsSwitching;
    public bool IsGrenadeBusy => _animator.IsGrenadeBusy;
    public bool IsGrenadeHeld => _animator.IsGrenadeHeld;
    public bool IsGrenadeReady => _animator.IsGrenadeReady;
    public bool IsGrenadeDrawing => _animator.IsGrenadeDrawing;
    public bool IsGrenadeThrowing => _animator.IsGrenadeThrowing;
    public bool IsGrenadeStowing => _animator.IsGrenadeStowing;
    public Node3D? HeldGrenade => _animator.Grenade?.Root;
    public Marker3D? HeldMuzzle => _animator.Weapon?.Muzzle;
    public double ReloadElapsed => _animator.Weapon?.ReloadElapsed ?? 0;
    public double ReloadDuration => _animator.Weapon?.ReloadDuration ?? 0;

    public PlayerController() => WeaponSlots = Array.AsReadOnly(_weaponSlots);

    public override void _Ready()
    {
        Visual = GetNode<Node3D>("Visual");
        _animator = new CharacterAnimator(Visual);
        _animator.WeaponVisualChanged += () => WeaponVisualChanged?.Invoke();
        _animator.GrenadeStateChanged += OnGrenadeStateChanged;
        _animator.GrenadeReleased += OnGrenadeReleased;
        _animator.GrenadeThrowFinished += OnGrenadeThrowFinished;
        _animator.GrenadeStowFinished += OnGrenadeStowFinished;
        WeaponAudio = new WeaponAudioPlayer { Name = "WeaponAudio" };
        AddChild(WeaponAudio);
        _shotRandom.Randomize();
        // The supplied model faces -Z. Rotate only the visual so movement and camera stay independent.
        ProcessPhysicsPriority = -10;
    }

    public string? PurchaseBlockReason(WeaponDefinition weapon)
    {
        if (IsGrenadeBusy) return "正在持握手雷";
        if (Array.IndexOf(_weaponSlots, null) < 0) return "两个武器槽位已满";
        if (weapon.Stats.Price < 0) return "武器价格无效";
        if (Money < weapon.Stats.Price) return $"资金不足，还差 {weapon.Stats.Price - Money}$";
        return null;
    }

    public bool TryPurchaseWeapon(string weaponId, out string message)
    {
        var game = GetParent<Prototype>();
        if (!game.Ui.IsOpen || game.Inventory.IsOpen || !game.Crate.IsInReach(this))
        {
            message = "请靠近武器箱并打开购买列表";
            return false;
        }
        if (IsSwitching) { message = "正在切换武器"; return false; }
        if (IsGrenadeBusy) { message = "正在持握手雷"; return false; }
        var weapon = game.Weapons.Weapons.FirstOrDefault(w => w.Id == weaponId);
        if (weapon == null) { message = "武器不存在"; return false; }
        if (PurchaseBlockReason(weapon) is string reason) { message = reason; return false; }
        int slot = Array.IndexOf(_weaponSlots, null);
        if (!TryEquipModel(weapon)) { message = "武器资源加载失败，未扣款"; return false; }
        _weaponSlots[slot] = weapon;
        ActiveWeaponSlot = slot;
        Money -= weapon.Stats.Price;
        LoadoutChanged?.Invoke();
        InventoryChanged?.Invoke();
        message = $"已购买 {weapon.Name}，装备至武器槽位 {slot + 1}";
        return true;
    }

    public string? PurchaseBlockReason(AmmoDefinition item)
    {
        if (Backpack.IsFull) return "背包物品栏已满";
        if (Money < item.Price) return $"资金不足，还差 {item.Price - Money}$";
        return null;
    }

    public bool TryPurchaseAmmo(string itemId, out string message)
    {
        var game = GetParent<Prototype>();
        if (!game.Ui.IsOpen || game.Inventory.IsOpen || !game.Crate.IsInReach(this))
        {
            message = "请靠近武器箱并打开购买列表";
            return false;
        }
        var item = ItemCatalog.Find(itemId);
        if (item == null) { message = "物品不存在"; return false; }
        if (PurchaseBlockReason(item) is string reason) { message = reason; return false; }
        if (!Backpack.TryAddAmmo(item)) { message = "背包物品栏已满"; return false; }
        Money -= item.Price;
        InventoryChanged?.Invoke();
        message = $"已购买 {item.Name}（{item.TotalAmmo} 发），放入背包物品栏";
        return true;
    }

    public string? PurchaseBlockReason(ThrowableDefinition throwable)
    {
        if (Backpack.IsFull) return "背包物品栏已满";
        if (Money < throwable.Price) return $"资金不足，还差 {throwable.Price - Money}$";
        return null;
    }

    public bool TryPurchaseThrowable(string throwableId, out string message)
    {
        var game = GetParent<Prototype>();
        if (!game.Ui.IsOpen || game.Inventory.IsOpen || !game.Crate.IsInReach(this))
        {
            message = "请靠近武器箱并打开购买列表";
            return false;
        }
        var throwable = game.Weapons.Throwables.FirstOrDefault(t => t.Id == throwableId);
        if (throwable == null) { message = "投掷物不存在"; return false; }
        if (PurchaseBlockReason(throwable) is string reason) { message = reason; return false; }
        if (!Backpack.TryAddThrowable(throwable)) { message = "背包物品栏已满"; return false; }
        Money -= throwable.Price;
        InventoryChanged?.Invoke();
        message = $"已购买 {throwable.Name}（{throwable.SlotCapacity} 枚），放入背包物品栏";
        return true;
    }

    public bool TryStartReload(out string message)
    {
        if (InputBlocked) { message = "关闭界面后才能换弹"; return false; }
        if (IsSwitching) { message = "正在切换武器"; return false; }
        if (IsGrenadeBusy) { message = "正在持握手雷"; return false; }
        if (EquippedWeapon is not { } weapon) { message = "尚未装备武器"; return false; }
        if (IsReloading) { message = "正在换弹"; return false; }
        if (_magazineAmmo[ActiveWeaponSlot] >= weapon.Stats.MagazineCapacity) { message = "弹匣已满"; return false; }
        if (Backpack.Available(weapon.Stats.AmmoType) == 0)
        {
            message = $"背包中没有{ItemCatalog.AmmoName(weapon.Stats.AmmoType)}";
            return false;
        }
        if (_animator.Weapon?.TryReload(weapon.Stats.ReloadDuration) != true) { message = "无法换弹"; return false; }
        _reloadSlot = ActiveWeaponSlot;
        WeaponAudio.StartReload();
        message = "正在换弹…";
        return true;
    }

    public bool TryMoveBackpackItem(int from, int to, IBackpackItem expectedItem)
    {
        if (!GetParent<Prototype>().Inventory.IsOpen || !Backpack.TryMove(from, to, expectedItem)) return false;
        InventoryChanged?.Invoke();
        return true;
    }

    private void CompleteReload()
    {
        int slot = _reloadSlot;
        _reloadSlot = -1;
        if (slot < 0 || slot != ActiveWeaponSlot || _weaponSlots[slot] is not { } weapon) return;
        int missing = Math.Max(0, weapon.Stats.MagazineCapacity - _magazineAmmo[slot]);
        int loaded = Backpack.Take(weapon.Stats.AmmoType, missing);
        _magazineAmmo[slot] += loaded;
        InventoryChanged?.Invoke();
        ReloadFeedback?.Invoke($"换弹完成 · 补充 {loaded} 发");
    }

    public bool SelectWeaponSlot(int slot)
    {
        if (InputBlocked || IsSwitching || IsGrenadeBusy || slot < 0 || slot >= WeaponSlotCount || _weaponSlots[slot] is not { } weapon) return false;
        if (slot == ActiveWeaponSlot) return true;
        if (!TryEquipModel(weapon)) return false;
        ActiveWeaponSlot = slot;
        LoadoutChanged?.Invoke();
        return true;
    }

    // Mouse wheel cycling: direction -1 selects the previous occupied slot and
    // +1 the next one, wrapping around and deliberately skipping empty slots.
    public bool CycleWeapon(int direction)
    {
        if (InputBlocked || IsSwitching || IsGrenadeBusy) return false;
        int[] occupied = Enumerable.Range(0, WeaponSlotCount).Where(i => _weaponSlots[i] != null).ToArray();
        if (occupied.Length == 0) return false;
        int current = Array.IndexOf(occupied, ActiveWeaponSlot);
        if (occupied.Length == 1) return ActiveWeaponSlot == occupied[0] || SelectWeaponSlot(occupied[0]);
        int step = direction < 0 ? -1 : 1;
        int next = current < 0
            ? (step > 0 ? 0 : occupied.Length - 1)
            : (current + step + occupied.Length) % occupied.Length;
        return SelectWeaponSlot(occupied[next]);
    }

    private bool TryEquipModel(WeaponDefinition weapon)
    {
        try
        {
            double holsterDuration = EquippedWeapon?.Stats.HolsterDuration ?? 0;
            if (!_animator.BeginSwitch(weapon.ScenePath, holsterDuration, weapon.Stats.EquipDuration)) return false;
            WeaponAudio.Equip(weapon);
            _reloadSlot = -1; // Switching/purchasing cancels the old reload without consuming ammunition.
            return true;
        }
        catch (Exception error) { GD.PushWarning($"无法装备 {weapon.Name}：{error.Message}"); return false; }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 input = InputBlocked ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 forward = -Camera.GlobalBasis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        Vector3 right = Camera.GlobalBasis.X;
        right.Y = 0;
        right = right.Normalized();
        Vector3 move = right * input.X - forward * input.Y;
        Velocity = new Vector3(move.X * EquippedMoveSpeed, IsOnFloor() ? 0 : Velocity.Y - Gravity * (float)delta, move.Z * EquippedMoveSpeed);
        MoveAndSlide();
        UpdateFiring(delta);
    }

    public override void _Process(double delta)
    {
        UpdateThrowHold(delta);
        if (!InputBlocked && !IsThrowSelecting)
            FaceScreenPoint(GetViewport().GetMousePosition(), (float)delta);
        bool wasReloading = IsReloading;
        _animator.Update(GetRealVelocity(), (float)delta, MoveSpeed, IsOnFloor() && !InputBlocked);
        WeaponAudio.Advance(delta);
        if (wasReloading && !IsReloading) CompleteReload();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (InputBlocked || @event.IsEcho()) return;
        // G resolves on release so a tap can draw immediately while a hold can
        // open the throwable wheel without triggering the tap action first.
        if (@event.IsActionReleased("throw"))
        {
            EndThrowSelection();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (IsThrowSelecting)
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                CancelThrowSelection();
                GetViewport().SetInputAsHandled();
            }
            else if (@event is InputEventKey or InputEventMouseButton)
                GetViewport().SetInputAsHandled();
            return;
        }
        if (@event.IsActionPressed("throw"))
        {
            BeginThrowSelection();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (IsGrenadeBusy)
        {
            if (@event.IsActionPressed("fire"))
            {
                string throwMessage;
                if (IsGrenadeReady) TryThrowHeldGrenade(out throwMessage);
                else throwMessage = "手雷尚未准备好";
                ThrowFeedback?.Invoke(throwMessage);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event.IsActionPressed("reload") ||
                @event.IsActionPressed("weapon_prev") || @event.IsActionPressed("weapon_next") ||
                (@event is InputEventKey { Pressed: true } hotkey && (hotkey.PhysicalKeycode == Key.Key1 || hotkey.PhysicalKeycode == Key.Key2)))
            {
                ThrowFeedback?.Invoke("正在持握手雷，按 G 收起或左键投掷");
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        if (@event.IsActionPressed("fire"))
        {
            if (!_triggerHeld) _triggerPressed = true;
            _triggerHeld = true;
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey { Pressed: true } key && (key.PhysicalKeycode == Key.Key1 || key.PhysicalKeycode == Key.Key2))
        {
            SelectWeaponSlot(key.PhysicalKeycode == Key.Key1 ? 0 : 1);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("weapon_prev"))
        {
            CycleWeapon(-1);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("weapon_next"))
        {
            CycleWeapon(1);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("reload"))
        {
            TryStartReload(out string message);
            ReloadFeedback?.Invoke(message);
            GetViewport().SetInputAsHandled();
        }
    }

    public void FaceScreenPoint(Vector2 screenPoint, float delta = 1f / 60)
    {
        Vector3 origin = Camera.ProjectRayOrigin(screenPoint);
        Vector3 ray = Camera.ProjectRayNormal(screenPoint);
        if (Mathf.Abs(ray.Y) < 0.0001f)
            return;
        // Intersect the cursor ray with a horizontal plane through the character's torso.
        float distance = (GlobalPosition.Y + AimHeight - origin.Y) / ray.Y;
        if (distance <= 0)
            return;
        Vector3 target = origin + ray * distance;
        target.Y = Visual.GlobalPosition.Y;
        if (Visual.GlobalPosition.DistanceSquaredTo(target) > 0.01f)
        {
            Vector3 direction = target - Visual.GlobalPosition;
            float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
            double follow = IsGrenadeBusy ? 10 : EquippedWeapon?.Stats.TurnFollowSpeed ?? 10;
            float weight = (float)(1 - Math.Exp(-delta * follow));
            Vector3 rotation = Visual.GlobalRotation;
            rotation.Y = Mathf.LerpAngle(rotation.Y, targetYaw, weight);
            Visual.GlobalRotation = rotation;
        }
    }
}
