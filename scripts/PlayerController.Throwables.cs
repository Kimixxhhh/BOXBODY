using Godot;
using FieldKit.Items;
using FieldKit.Weapons;

public partial class PlayerController
{
    public const double GrenadeDrawDuration = .35;
    public const double GrenadeThrowDuration = .55;
    public const double GrenadeStowDuration = .25;
    public const double GrenadeSwapDuration = .18;
    public const double ThrowWheelHoldSeconds = .28;

    private ThrowableItem? _heldThrowable;
    private Vector3 _grenadeTarget;
    private bool _throwPressed;
    private bool _throwHeldReady;
    private double _throwHoldTime;
    private string? _throwFirstId;

    public ThrowableWheelUi? ThrowWheel { get; set; }
    public bool IsThrowSelecting { get; private set; }
    public bool IsThrowWheelOpen => ThrowWheel?.IsOpen == true;
    public ThrowableDefinition? HeldThrowable => _heldThrowable?.Definition;

    // G: a tap draws the first throwable; holding past the threshold opens the
    // radial selector, and releasing G draws the highlighted kind. While a
    // grenade is already in hand, a tap stows it and a hold switches its kind.
    private void BeginThrowSelection()
    {
        if (InputBlocked) return;
        if (IsSwitching)
        {
            ThrowFeedback?.Invoke("正在切换武器");
            return;
        }
        if (IsReloading)
        {
            ThrowFeedback?.Invoke("正在换弹");
            return;
        }
        if (IsGrenadeBusy)
        {
            if (!IsGrenadeReady)
            {
                ThrowFeedback?.Invoke(IsGrenadeThrowing ? "正在投掷手雷"
                    : IsGrenadeStowing ? "正在收起手雷" : "正在准备手雷");
                return;
            }
            _throwPressed = true;
            _throwHeldReady = true;
            _throwHoldTime = 0;
            _throwFirstId = _heldThrowable?.Definition.Id;
            IsThrowSelecting = true;
            ReleaseTrigger();
            return;
        }
        if (!Backpack.TryPeekThrowable(out var first) || first == null)
        {
            ThrowFeedback?.Invoke("背包物品栏中没有手雷");
            return;
        }
        _throwPressed = true;
        _throwHeldReady = false;
        _throwHoldTime = 0;
        _throwFirstId = first.Id;
        IsThrowSelecting = true;
        ReleaseTrigger();
    }

    // Called every frame while G is down. Crossing the threshold opens the
    // wheel once; releasing G is handled by EndThrowSelection.
    private void UpdateThrowHold(double delta)
    {
        if (!_throwPressed) return;
        if (InputBlocked)
        {
            CancelThrowSelection();
            return;
        }
        _throwHoldTime += delta;
        if (!IsThrowWheelOpen && _throwHoldTime >= ThrowWheelHoldSeconds)
            OpenThrowWheel();
    }

    private void OpenThrowWheel()
    {
        var kinds = Backpack.ThrowableKinds();
        if (kinds.Count == 0)
        {
            CancelThrowSelection();
            ThrowFeedback?.Invoke("背包物品栏中没有手雷");
            return;
        }
        ThrowWheel?.Open(kinds, _throwFirstId, _throwHeldReady);
    }

    private void EndThrowSelection()
    {
        if (!_throwPressed) return;
        bool heldReady = _throwHeldReady;
        _throwPressed = false;
        _throwHeldReady = false;
        IsThrowSelecting = false;
        string? firstId = _throwFirstId;
        _throwFirstId = null;

        if (IsThrowWheelOpen)
        {
            string? selected = ThrowWheel?.SelectedId;
            ThrowWheel?.Close();
            string message;
            if (heldReady && selected != null && selected != _heldThrowable?.Definition.Id)
                TrySwapHeldThrowable(selected, out message);
            else if (heldReady)
                message = _heldThrowable != null ? $"继续持握 {_heldThrowable.Definition.Name}" : "已取消选择";
            else
                TryEquipGrenade(selected ?? firstId, out message);
            ThrowFeedback?.Invoke(message);
            return;
        }

        string releaseMessage;
        if (heldReady) TryStowGrenade(out releaseMessage);
        else TryEquipGrenade(firstId, out releaseMessage);
        ThrowFeedback?.Invoke(releaseMessage);
    }

    public void CancelThrowSelection()
    {
        if (!_throwPressed && !IsThrowSelecting && !IsThrowWheelOpen) return;
        _throwPressed = false;
        _throwHeldReady = false;
        _throwFirstId = null;
        IsThrowSelecting = false;
        ThrowWheel?.Close();
    }

    // G: draw the first throwable kind in backpack order.
    public bool TryEquipGrenade(out string message) => TryEquipGrenade(null, out message);

    // Draw the first stack of a specific kind; null keeps the old "first stack"
    // behavior used by the tap path.
    public bool TryEquipGrenade(string? throwableId, out string message)
    {
        if (InputBlocked) { message = "关闭界面后才能拿出手雷"; return false; }
        if (IsSwitching) { message = "正在切换武器"; return false; }
        if (IsReloading) { message = "正在换弹"; return false; }
        if (IsGrenadeBusy)
        {
            message = IsGrenadeReady ? "手雷已拿在手中，左键投掷或按 G 收起" : "正在准备手雷";
            return false;
        }
        if (!Backpack.TryPeekThrowable(out var item, throwableId) || item == null)
        {
            message = throwableId == null ? "背包物品栏中没有手雷" : "背包中没有该投掷物";
            return false;
        }
        var weapon = EquippedWeapon;
        if (!_animator.BeginGrenadeEquip(item.Definition.ItemScenePath,
                weapon?.Stats.HolsterDuration ?? 0,
                GrenadeDrawDuration,
                weapon?.ScenePath,
                weapon?.Stats.EquipDuration ?? 0))
        {
            message = "无法拿出手雷";
            return false;
        }
        _heldThrowable = item;
        ReleaseTrigger();
        WeaponAudio.CancelReload();
        message = weapon != null
            ? $"正在收回 {weapon.Name}，拿出 {item.Definition.Name}"
            : $"已拿出 {item.Definition.Name}，左键投掷";
        return true;
    }

    // Replace the grenade already in hand without dropping the holstered weapon.
    private bool TrySwapHeldThrowable(string throwableId, out string message)
    {
        if (!IsGrenadeReady || _heldThrowable == null)
        {
            message = "手雷尚未准备好";
            return false;
        }
        if (!Backpack.TryPeekThrowable(out var item, throwableId) || item == null)
        {
            message = "背包中没有该投掷物";
            return false;
        }
        if (item.Definition.Id == _heldThrowable.Definition.Id)
        {
            message = $"{item.Definition.Name} 已在手中";
            return false;
        }
        if (!_animator.ReplaceGrenade(item.Definition.ItemScenePath, GrenadeSwapDuration))
        {
            message = "无法切换投掷物";
            return false;
        }
        _heldThrowable = item;
        message = $"已切换为 {item.Definition.Name}";
        return true;
    }

    // Left click: throw the grenade currently in hand toward the cursor.
    public bool TryThrowHeldGrenade(out string message) => TryThrowHeldGrenadeAt(GrenadeTargetPoint(), out message);

    internal bool TryThrowHeldGrenadeAt(Vector3 target, out string message)
    {
        if (InputBlocked) { message = "关闭界面后才能投掷手雷"; return false; }
        if (!IsGrenadeReady)
        {
            message = IsGrenadeBusy ? "手雷尚未准备好" : "尚未拿出手雷";
            return false;
        }
        if (_heldThrowable == null)
        {
            message = "背包物品栏中没有手雷";
            return false;
        }
        if (!_animator.TryBeginGrenadeThrow(GrenadeThrowDuration))
        {
            message = "无法投掷手雷";
            return false;
        }
        _grenadeTarget = ClampGrenadeTarget(target);
        ReleaseTrigger();
        message = "正在投掷手雷…";
        return true;
    }

    // G while ready: put the grenade away without consuming it and restore the
    // weapon that was holstered when the grenade was drawn.
    public bool TryStowGrenade(out string message)
    {
        if (!IsGrenadeReady)
        {
            message = IsGrenadeBusy ? "手雷尚未准备好" : "尚未拿出手雷";
            return false;
        }
        if (!_animator.BeginGrenadeStow(GrenadeStowDuration))
        {
            message = "无法收起手雷";
            return false;
        }
        _heldThrowable = null;
        ReleaseTrigger();
        message = "正在收起手雷…";
        return true;
    }

    private void OnGrenadeReleased(Vector3 releasePosition)
    {
        var item = _heldThrowable;
        _heldThrowable = null;
        if (item == null) return;
        // Consume only one item from the exact stack that was drawn, even if the
        // backpack was rearranged while it was in the hand. The slot stays until
        // the stack is empty.
        if (!Backpack.TryConsumeThrowable(item))
        {
            ThrowFeedback?.Invoke("手雷已不在背包中，投掷取消");
            GrenadeStateChanged?.Invoke();
            return;
        }
        int remaining = item.Remaining;
        Vector3 velocity = GrenadeVelocity(releasePosition, _grenadeTarget);
        var grenade = new GrenadeProjectile { Name = "Grenade" };
        grenade.Initialize(item.Definition, velocity);
        GetParent().AddChild(grenade);
        grenade.GlobalPosition = releasePosition;
        InventoryChanged?.Invoke();
        ThrowFeedback?.Invoke(remaining > 0
            ? $"已投掷 {item.Definition.Name} · 剩余 {remaining} 枚 · 引信 {item.Definition.FuseTime:0.##} 秒"
            : $"已投掷 {item.Definition.Name} · 引信 {item.Definition.FuseTime:0.##} 秒");
        GrenadeStateChanged?.Invoke();
    }

    private void OnGrenadeThrowFinished()
    {
        _heldThrowable = null;
        if (EquippedWeapon is { } weapon) WeaponAudio.Equip(weapon);
        GrenadeStateChanged?.Invoke();
    }

    private void OnGrenadeStowFinished()
    {
        _heldThrowable = null;
        if (EquippedWeapon is { } weapon) WeaponAudio.Equip(weapon);
        ThrowFeedback?.Invoke("已收起手雷，重新取回武器");
        GrenadeStateChanged?.Invoke();
    }

    private void OnGrenadeStateChanged()
    {
        GrenadeStateChanged?.Invoke();
        if (IsGrenadeReady)
            ThrowFeedback?.Invoke(EquippedWeapon is { } weapon
                ? $"{weapon.Name} 已收回 · 左键投掷手雷"
                : "手雷已就绪 · 左键投掷");
    }

    private Vector3 GrenadeTargetPoint()
    {
        Vector3 fallback = GlobalPosition + -Visual.GlobalBasis.Z * 8;
        if (Camera == null) return fallback;
        Vector2 screen = GetViewport().GetMousePosition();
        Vector3 origin = Camera.ProjectRayOrigin(screen);
        Vector3 ray = Camera.ProjectRayNormal(screen);
        if (Mathf.Abs(ray.Y) < .0001f) return fallback;
        float distance = (0.06f - origin.Y) / ray.Y;
        if (distance <= 0) return fallback;
        Vector3 point = origin + ray * distance;
        Vector3 offset = point - GlobalPosition;
        offset.Y = 0;
        if (offset.LengthSquared() < .0001f) return fallback;
        return ClampGrenadeTarget(point);
    }

    private Vector3 ClampGrenadeTarget(Vector3 target)
    {
        Vector3 flat = target - GlobalPosition;
        flat.Y = 0;
        float reach = flat.Length();
        if (reach < .01f) flat = -Visual.GlobalBasis.Z;
        else flat = flat / reach;
        reach = Mathf.Clamp(reach, 1.2f, 18f);
        target = GlobalPosition + flat * reach;
        target.Y = .06f;
        return target;
    }

    private static Vector3 GrenadeVelocity(Vector3 start, Vector3 target)
    {
        float horizontal = new Vector2(target.X - start.X, target.Z - start.Z).Length();
        float flight = Mathf.Clamp(horizontal / 13f, .4f, 1.1f);
        float vertical = (target.Y - start.Y + .5f * GrenadeProjectile.Gravity * flight * flight) / flight;
        return new Vector3((target.X - start.X) / flight, vertical, (target.Z - start.Z) / flight);
    }
}
