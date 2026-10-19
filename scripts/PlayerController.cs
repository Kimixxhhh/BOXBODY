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
    [Export] public float AimHeight { get; set; } = 0.75f;
    public bool InputBlocked { get; set; }
    public const int InitialMoney = 5000;
    public const int WeaponSlotCount = 2;
    public int Money { get; private set; } = InitialMoney;
    public int ActiveWeaponSlot { get; private set; } = -1;
    private readonly WeaponDefinition?[] _weaponSlots = new WeaponDefinition?[WeaponSlotCount];
    public IReadOnlyList<WeaponDefinition?> WeaponSlots { get; }
    public WeaponDefinition? EquippedWeapon => ActiveWeaponSlot < 0 ? null : _weaponSlots[ActiveWeaponSlot];
    public event Action? LoadoutChanged;
    public event Action? InventoryChanged;
    public event Action<string>? ReloadFeedback;
    public AmmoBackpack Backpack { get; } = new();
    private readonly int[] _magazineAmmo = new int[WeaponSlotCount];
    private int _reloadSlot = -1;
    public int MagazineAmmo(int slot) => slot >= 0 && slot < WeaponSlotCount ? _magazineAmmo[slot] : 0;
    public float EquippedMoveSpeed => MoveSpeed * (float)(EquippedWeapon?.Stats.MovementSpeedMultiplier() ?? 1);
    public Node3D Visual { get; private set; } = null!;
    public FollowCamera Camera { get; set; } = null!;
    private CharacterAnimator _animator = null!;
    internal WeaponAudioPlayer WeaponAudio { get; private set; } = null!;
    public bool IsReloading => _animator.Weapon?.IsReloading ?? false;
    public double ReloadElapsed => _animator.Weapon?.ReloadElapsed ?? 0;
    public double ReloadDuration => _animator.Weapon?.ReloadDuration ?? 0;

    public PlayerController() => WeaponSlots = Array.AsReadOnly(_weaponSlots);

    public override void _Ready()
    {
        Visual = GetNode<Node3D>("Visual");
        _animator = new CharacterAnimator(Visual);
        WeaponAudio = new WeaponAudioPlayer { Name = "WeaponAudio" };
        AddChild(WeaponAudio);
        // The supplied model faces -Z. Rotate only the visual so movement and camera stay independent.
        ProcessPhysicsPriority = -10;
    }

    public string? PurchaseBlockReason(WeaponDefinition weapon)
    {
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
        if (!Backpack.TryAdd(item)) { message = "背包物品栏已满"; return false; }
        Money -= item.Price;
        InventoryChanged?.Invoke();
        message = $"已购买 {item.Name}（{item.TotalAmmo} 发），放入背包物品栏";
        return true;
    }

    public bool TryStartReload(out string message)
    {
        if (InputBlocked) { message = "关闭界面后才能换弹"; return false; }
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

    public bool TryMoveBackpackItem(int from, int to, AmmoItem expectedItem)
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
        if (InputBlocked || slot < 0 || slot >= WeaponSlotCount || _weaponSlots[slot] is not { } weapon) return false;
        if (slot == ActiveWeaponSlot) return true;
        if (!TryEquipModel(weapon)) return false;
        ActiveWeaponSlot = slot;
        LoadoutChanged?.Invoke();
        return true;
    }

    private bool TryEquipModel(WeaponDefinition weapon)
    {
        try
        {
            _animator.EquipWeapon(weapon.ScenePath);
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
    }

    public override void _Process(double delta)
    {
        if (!InputBlocked)
            FaceScreenPoint(GetViewport().GetMousePosition(), (float)delta);
        bool wasReloading = IsReloading;
        _animator.Update(GetRealVelocity(), (float)delta, MoveSpeed, IsOnFloor() && !InputBlocked);
        WeaponAudio.Advance(delta);
        if (wasReloading && !IsReloading) CompleteReload();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (InputBlocked || @event.IsEcho()) return;
        if (@event is InputEventKey { Pressed: true } key && (key.PhysicalKeycode == Key.Key1 || key.PhysicalKeycode == Key.Key2))
        {
            SelectWeaponSlot(key.PhysicalKeycode == Key.Key1 ? 0 : 1);
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
            float weight = (float)(EquippedWeapon?.Stats.TurnFollowWeight(delta) ?? (1 - Math.Exp(-10 * delta)));
            Vector3 rotation = Visual.GlobalRotation;
            rotation.Y = Mathf.LerpAngle(rotation.Y, targetYaw, weight);
            Visual.GlobalRotation = rotation;
        }
    }
}
