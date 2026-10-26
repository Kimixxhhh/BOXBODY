using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The supplied Millitia asset is a collection of rigid meshes, with no skeleton.
// Pose the instance around shoulder pivots and animate its separate feet.
public sealed class CharacterAnimator
{
    private const float ArmLengthScale = 1.15f;
    private const float BodyHeightScale = 1.25f;
    private readonly Node3D _model;
    private readonly Node3D _visual;
    private readonly ArmRig _leftArm, _rightArm;
    private readonly Node3D _leftFoot, _rightFoot;
    public HeldWeaponAnimator? Weapon { get; private set; }
    public HeldGrenadeAnimator? Grenade { get; private set; }
    public bool IsSwitching => _switchState != SwitchState.Idle;
    public bool IsGrenadeBusy => _grenadeState != GrenadeState.None;
    public bool IsGrenadeHeld => Grenade != null;
    public bool IsGrenadeReady => _grenadeState == GrenadeState.Ready && Grenade?.IsReady == true;
    public bool IsGrenadeDrawing => _grenadeState is GrenadeState.WaitingForHolster or GrenadeState.Equipping;
    public bool IsGrenadeThrowing => _grenadeState == GrenadeState.Throwing;
    public bool IsGrenadeStowing => _grenadeState == GrenadeState.Stowing;
    public event Action? WeaponVisualChanged;
    public event Action? GrenadeStateChanged;
    public event Action<Vector3>? GrenadeReleased;
    public event Action? GrenadeThrowFinished;
    public event Action? GrenadeStowFinished;
    private enum SwitchState { Idle, Holstering, Drawing }
    private enum GrenadeState { None, WaitingForHolster, Equipping, Ready, Throwing, Stowing }
    private SwitchState _switchState;
    private GrenadeState _grenadeState;
    private HeldWeaponAnimator? _pendingWeapon;
    private double _pendingEquipDuration;
    private HeldGrenadeAnimator? _pendingGrenade;
    private double _pendingGrenadeDrawDuration;
    private string? _restoreWeaponScenePath;
    private double _restoreWeaponEquipDuration;
    private bool _grenadeReleaseSent;
    private bool _drawFromRest;
    private readonly Vector3 _modelRestPosition, _leftFootRestPosition, _rightFootRestPosition;
    private readonly Vector3 _weaponHeightOffset;
    private float _phase;
    private float _blend;
    private Vector3 _travel = Vector3.Forward;

    public CharacterAnimator(Node3D visual)
    {
        _visual = visual;
        _model = visual.GetNode<Node3D>("Millitia");
        _modelRestPosition = _model.Position;
        MeshInstance3D[] parts = _model.GetChildren().OfType<MeshInstance3D>().ToArray();
        _leftArm = CreateArm(parts, "Left");
        _rightArm = CreateArm(parts, "Right");
        _weaponHeightOffset = Vector3.Up * _leftArm.HeightOffset;
        // Stretch the body from ground level after the arms have been reparented.
        // Keeping their length unchanged gives the resting hands ground clearance.
        Basis taller = Basis.FromScale(new Vector3(1, BodyHeightScale, 1));
        foreach (MeshInstance3D part in parts.Where(p => p.GetParent() == _model))
            part.Transform = new Transform3D(taller * part.Basis, taller * part.Position);
        _leftFoot = CreateFoot(parts, "Left");
        _rightFoot = CreateFoot(parts, "Right");
        _leftFootRestPosition = _leftFoot.Position;
        _rightFootRestPosition = _rightFoot.Position;
        _leftArm.Rest(0);
        _rightArm.Rest(0);
    }

    public bool BeginSwitch(string scenePath, double holsterDuration, double equipDuration)
    {
        if (IsSwitching || IsGrenadeBusy) return false;
        // Load the replacement before touching the current prop so a failed load
        // leaves the previous equipment intact and the purchase can be rejected.
        HeldWeaponAnimator next;
        try { next = new HeldWeaponAnimator(_model, scenePath, _weaponHeightOffset); }
        catch (Exception error)
        {
            GD.PushWarning($"无法加载武器模型：{error.Message}");
            return false;
        }
        next.Root.Name = "PendingWeapon";
        next.Root.Visible = false;
        if (Weapon == null)
        {
            InstallWeapon(next, equipDuration, fromRest: true);
            return true;
        }

        _pendingWeapon = next;
        _pendingEquipDuration = Math.Max(0, equipDuration);
        Weapon.BeginHolster(holsterDuration);
        if (Weapon.IsHolstering)
        {
            _switchState = SwitchState.Holstering;
        }
        else
        {
            // A zero holster time skips straight to the draw.
            _pendingWeapon = null;
            InstallWeapon(next, equipDuration, fromRest: false);
        }
        return true;
    }

    // G first holsters the current weapon, then draws the configured grenade into
    // the right hand. The weapon scene is remembered so a throw or cancel can
    // restore exactly what the player was holding.
    public bool BeginGrenadeEquip(string grenadeScenePath, double holsterDuration, double drawDuration,
        string? restoreWeaponScenePath, double restoreEquipDuration)
    {
        if (IsSwitching || IsGrenadeBusy) return false;
        HeldGrenadeAnimator next;
        try { next = new HeldGrenadeAnimator(_model, grenadeScenePath); }
        catch (Exception error)
        {
            GD.PushWarning($"无法加载手持投掷物：{error.Message}");
            return false;
        }
        _restoreWeaponScenePath = restoreWeaponScenePath;
        _restoreWeaponEquipDuration = Math.Max(0, restoreEquipDuration);
        if (Weapon != null)
        {
            _pendingGrenade = next;
            _pendingGrenadeDrawDuration = Math.Max(0, drawDuration);
            Weapon.BeginHolster(holsterDuration);
            if (Weapon.IsHolstering)
            {
                _grenadeState = GrenadeState.WaitingForHolster;
            }
            else
            {
                _pendingGrenade = null;
                InstallGrenade(next, drawDuration);
            }
        }
        else InstallGrenade(next, drawDuration);
        GrenadeStateChanged?.Invoke();
        return true;
    }

    public bool TryBeginGrenadeThrow(double duration)
    {
        if (_grenadeState != GrenadeState.Ready || Grenade == null || !Grenade.BeginThrow(duration)) return false;
        _grenadeState = GrenadeState.Throwing;
        _grenadeReleaseSent = false;
        GrenadeStateChanged?.Invoke();
        return true;
    }

    public bool BeginGrenadeStow(double duration)
    {
        if (_grenadeState != GrenadeState.Ready || Grenade == null || !Grenade.BeginStow(duration)) return false;
        _grenadeState = GrenadeState.Stowing;
        GrenadeStateChanged?.Invoke();
        return true;
    }

    // Swap the ready grenade for another kind without restoring the weapon.
    // The new visual is drawn from the stow pose so the change reads clearly.
    public bool ReplaceGrenade(string grenadeScenePath, double drawDuration)
    {
        if (_grenadeState != GrenadeState.Ready || Grenade == null) return false;
        HeldGrenadeAnimator next;
        try { next = new HeldGrenadeAnimator(_model, grenadeScenePath); }
        catch (Exception error)
        {
            GD.PushWarning($"无法切换手持投掷物：{error.Message}");
            return false;
        }
        RemoveGrenade();
        Grenade = next;
        Grenade.Root.Name = "HeldGrenade";
        _grenadeState = GrenadeState.Equipping;
        Grenade.BeginDraw(Math.Max(0, drawDuration));
        Grenade.Update(0, _blend, _phase);
        ReachGrenade(0);
        if (!Grenade.IsDrawing)
        {
            _grenadeState = GrenadeState.Ready;
            GrenadeStateChanged?.Invoke();
        }
        WeaponVisualChanged?.Invoke();
        GrenadeStateChanged?.Invoke();
        return true;
    }

    private void InstallGrenade(HeldGrenadeAnimator next, double drawDuration)
    {
        if (Weapon != null)
        {
            _model.RemoveChild(Weapon.Root);
            Weapon.Root.QueueFree();
            Weapon = null;
        }
        Grenade = next;
        Grenade.Root.Name = "HeldGrenade";
        _grenadeState = GrenadeState.Equipping;
        Grenade.BeginDraw(drawDuration);
        Grenade.Update(0, _blend, _phase);
        ReachGrenade(0);
        if (!Grenade.IsDrawing)
        {
            _grenadeState = GrenadeState.Ready;
            GrenadeStateChanged?.Invoke();
        }
        WeaponVisualChanged?.Invoke();
    }

    private void ReachGrenade(float step)
    {
        if (Grenade == null) return;
        _rightArm.Reach(Grenade.RightHandTarget, Grenade.ArmWeight, -step * .25f);
        _leftArm.Rest(step * .25f);
    }

    private void FinishGrenadeThrow()
    {
        RemoveGrenade();
        _grenadeState = GrenadeState.None;
        RestoreWeapon();
        GrenadeThrowFinished?.Invoke();
        GrenadeStateChanged?.Invoke();
    }

    private void FinishGrenadeStow()
    {
        RemoveGrenade();
        _grenadeState = GrenadeState.None;
        RestoreWeapon();
        GrenadeStowFinished?.Invoke();
        GrenadeStateChanged?.Invoke();
    }

    private void RemoveGrenade()
    {
        if (Grenade != null)
        {
            _model.RemoveChild(Grenade.Root);
            Grenade.Root.QueueFree();
            Grenade = null;
        }
        WeaponVisualChanged?.Invoke();
    }

    private void RestoreWeapon()
    {
        string? scenePath = _restoreWeaponScenePath;
        _restoreWeaponScenePath = null;
        if (scenePath == null || Weapon != null) return;
        try
        {
            var next = new HeldWeaponAnimator(_model, scenePath, _weaponHeightOffset);
            InstallWeapon(next, _restoreWeaponEquipDuration, fromRest: false);
        }
        catch (Exception error)
        {
            GD.PushWarning($"无法恢复原武器：{error.Message}");
        }
    }

    private void InstallWeapon(HeldWeaponAnimator next, double equipDuration, bool fromRest)
    {
        if (Weapon != null)
        {
            _model.RemoveChild(Weapon.Root);
            Weapon.Root.QueueFree();
        }
        Weapon = next;
        Weapon.Root.Name = "HeldWeapon";
        Weapon.Root.Visible = true;
        Weapon.BeginDraw(equipDuration);
        _drawFromRest = fromRest;
        _switchState = Weapon.IsDrawing ? SwitchState.Drawing : SwitchState.Idle;
        // Apply the stowed pose before the first rendered frame of the new prop.
        Weapon.Update(0, _blend, _phase);
        ReachWeapon(fromRest && Weapon.IsDrawing ? 0f : 1f, 0f);
        // The outline cache must be rebuilt now that the old prop no longer exists.
        WeaponVisualChanged?.Invoke();
    }

    private void ReachWeapon(float weight, float step)
    {
        if (Weapon == null) return;
        _leftArm.Reach(Weapon.LeftHandTarget, weight, step * .25f);
        _rightArm.Reach(Weapon.RightHandTarget, weight, -step * .25f);
    }

    private ArmRig CreateArm(IEnumerable<MeshInstance3D> parts, string side)
    {
        var upper = parts.Where(p => p.Name.ToString().StartsWith("arm" + side, StringComparison.Ordinal)).ToArray();
        var lower = parts.Where(p => p.Name.ToString().StartsWith("hand" + side, StringComparison.Ordinal)).ToArray();
        if (upper.Length == 0 || lower.Length == 0)
            throw new InvalidOperationException($"Millitia is missing the {side} arm meshes.");

        MeshInstance3D reference = upper[0];
        Aabb bounds = reference.GetAabb();
        Aabb posedBounds = reference.Transform * bounds;
        var shoulder = new Vector3(posedBounds.GetCenter().X, posedBounds.End.Y, 0.01255921f);
        float heightOffset = shoulder.Y * (BodyHeightScale - 1);
        shoulder.Y += heightOffset;
        var pivot = new Node3D { Name = side + "ArmPivot", Position = shoulder };
        _model.AddChild(pivot);
        float upperLength = bounds.Size.Y * reference.Basis.Y.Length() * ArmLengthScale;
        float lowerLength = lower[0].GetAabb().Size.Y * lower[0].Basis.Y.Length() * ArmLengthScale;
        foreach (MeshInstance3D part in upper) AttachStraightSegment(part, pivot, Vector3.Zero);
        var elbow = new Node3D { Name = "Elbow", Position = Vector3.Down * upperLength };
        pivot.AddChild(elbow);
        foreach (MeshInstance3D part in lower) AttachStraightSegment(part, elbow, Vector3.Zero);
        // Aim the middle of the skin-colored palm at the prop, not the fingertip.
        float handReach = lowerLength - 0.045f * ArmLengthScale;
        elbow.AddChild(new Marker3D { Name = "HandGrip", Position = Vector3.Down * handReach });
        return new ArmRig(pivot, elbow, upperLength, handReach, side == "Left" ? -1 : 1, heightOffset);
    }

    private sealed class ArmRig(Node3D shoulder, Node3D elbow, float upperLength, float forearmLength, float side, float heightOffset)
    {
        public float HeightOffset { get; } = heightOffset;
        public void Rest(float swing)
        {
            shoulder.Rotation = new Vector3(swing, 0, 0);
            elbow.Rotation = new Vector3(-0.12f, 0, 0);
        }

        public void Reach(Vector3 target, float weight = 1f, float restSwing = 0f)
        {
            if (weight <= 0.001f)
            {
                Rest(restSwing);
                return;
            }
            Vector3 delta = target - shoulder.Position;
            float distance = Mathf.Clamp(delta.Length(), 0.02f, upperLength + forearmLength - 0.001f);
            Vector3 direction = delta.Normalized();
            Vector3 preferredBend = new Vector3(side, -0.8f, 0.15f);
            Vector3 bend = (preferredBend - direction * preferredBend.Dot(direction)).Normalized();
            float along = (upperLength * upperLength - forearmLength * forearmLength + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 joint = shoulder.Position + direction * along + bend * height;
            Vector3 reachableTarget = shoulder.Position + direction * distance;
            Basis reachShoulder = new(new Quaternion(Vector3.Down, (joint - shoulder.Position).Normalized()));
            Basis lowerBasis = new(new Quaternion(Vector3.Down, (reachableTarget - joint).Normalized()));
            Basis reachElbow = reachShoulder.Inverse() * lowerBasis;
            if (weight >= 0.999f)
            {
                shoulder.Basis = reachShoulder;
                elbow.Basis = reachElbow;
                return;
            }
            // First draw: unarmed arms rotate toward the stowed grips while the rifle rises.
            Quaternion restShoulder = Quaternion.FromEuler(new Vector3(restSwing, 0, 0));
            Quaternion restElbow = Quaternion.FromEuler(new Vector3(-0.12f, 0, 0));
            shoulder.Basis = new Basis(restShoulder.Slerp(reachShoulder.GetRotationQuaternion(), weight));
            elbow.Basis = new Basis(restElbow.Slerp(reachElbow.GetRotationQuaternion(), weight));
        }
    }

    private static void AttachStraightSegment(MeshInstance3D part, Node3D pivot, Vector3 joint)
    {
        // Positive local Y points from shoulder to wrist in this negatively scaled GLB.
        // Rotate the full imported basis; preserve its scale, handedness and materials.
        Basis down = new Basis(new Quaternion(part.Basis.Y.Normalized(), Vector3.Down)) * part.Basis;
        // Extend only the limb axis, keeping arm width and shoulder position unchanged.
        down.Y *= ArmLengthScale;
        Aabb bounds = part.GetAabb();
        Vector3 top = bounds.GetCenter();
        top.Y = bounds.Position.Y;
        part.Reparent(pivot, false);
        part.Transform = new Transform3D(down, joint - down * top);
    }

    private Node3D CreateFoot(IEnumerable<MeshInstance3D> parts, string side)
    {
        // ShoeLeft.002/ShoeRight.002 are actually torso details, so classify by height too.
        var shoes = parts.Where(p => p.Name.ToString().StartsWith("Shoe" + side, StringComparison.Ordinal)
            && (p.Transform * p.GetAabb()).GetCenter().Y < 0.3f).ToArray();
        if (shoes.Length == 0) throw new InvalidOperationException($"Millitia is missing the {side} foot meshes.");
        Aabb bounds = shoes[0].Transform * shoes[0].GetAabb();
        Vector3 origin = bounds.GetCenter();
        origin.Y = bounds.Position.Y;
        var pivot = new Node3D { Name = side + "FootPivot", Position = origin };
        _model.AddChild(pivot);
        foreach (MeshInstance3D part in shoes)
        {
            Transform3D rest = part.Transform;
            rest.Origin -= origin;
            part.Reparent(pivot, false);
            part.Transform = rest;
        }
        return pivot;
    }

    public void Update(Vector3 realVelocity, float delta, float moveSpeed, bool canWalk)
    {
        realVelocity.Y = 0;
        float speed = realVelocity.Length();
        float target = canWalk && speed > 0.08f ? Mathf.Clamp(speed / moveSpeed, 0, 1) : 0;
        _blend = Mathf.MoveToward(_blend, target, delta * 7);
        if (target > 0)
        {
            Vector3 direction = (_visual.GlobalBasis.Inverse() * realVelocity).Normalized();
            _travel = _travel.Lerp(direction, 1 - Mathf.Exp(-12 * delta));
            _phase = Mathf.PosMod(_phase + speed * delta * Mathf.Tau / 2.2f, Mathf.Tau);
        }
        if (_blend == 0) _phase = 0;

        float step = Mathf.Sin(_phase) * _blend;
        float liftLeft = Mathf.Max(0, Mathf.Cos(_phase)) * _blend;
        float liftRight = Mathf.Max(0, -Mathf.Cos(_phase)) * _blend;
        UpdateEquipment(delta, step);
        _leftFoot.Position = _leftFootRestPosition + _travel * (step * 0.19f) + Vector3.Up * (liftLeft * 0.10f);
        _rightFoot.Position = _rightFootRestPosition - _travel * (step * 0.19f) + Vector3.Up * (liftRight * 0.10f);
        _model.Position = _modelRestPosition + Vector3.Up * (Mathf.Abs(Mathf.Sin(_phase * 2)) * 0.018f * _blend);
    }

    private void UpdateEquipment(float delta, float step)
    {
        if (Grenade != null)
        {
            Grenade.Update(delta, _blend, _phase);
            if (_grenadeState == GrenadeState.Throwing)
            {
                if (Grenade.Released && !_grenadeReleaseSent)
                {
                    _grenadeReleaseSent = true;
                    GrenadeReleased?.Invoke(Grenade.ReleaseGlobalPosition);
                    GrenadeStateChanged?.Invoke();
                }
                if (Grenade.IsThrowComplete)
                {
                    FinishGrenadeThrow();
                    return;
                }
            }
            else if (_grenadeState == GrenadeState.Stowing && Grenade.IsStowComplete)
            {
                FinishGrenadeStow();
                return;
            }
            else if (_grenadeState == GrenadeState.Equipping && !Grenade.IsDrawing)
            {
                _grenadeState = GrenadeState.Ready;
                GrenadeStateChanged?.Invoke();
            }
            ReachGrenade(step);
            return;
        }

        if (Weapon != null)
        {
            Weapon.Update(delta, _blend, _phase);
            if (_grenadeState == GrenadeState.WaitingForHolster)
            {
                if (!Weapon.IsHolstering && _pendingGrenade != null)
                {
                    var next = _pendingGrenade;
                    _pendingGrenade = null;
                    InstallGrenade(next, _pendingGrenadeDrawDuration);
                    if (Grenade != null)
                    {
                        Grenade.Update(0, _blend, _phase);
                        ReachGrenade(step);
                    }
                    return;
                }
                // Keep both hands on the grips while the rifle rotates down. This
                // is the same follow update used by a normal weapon-to-weapon
                // switch; without it the rifle would stow while the hands stayed
                // frozen at the holding pose.
                ReachWeapon(1f, step);
                return;
            }
            if (_switchState == SwitchState.Holstering && !Weapon.IsHolstering)
            {
                var next = _pendingWeapon;
                _pendingWeapon = null;
                if (next != null) InstallWeapon(next, _pendingEquipDuration, fromRest: false);
                else _switchState = SwitchState.Idle;
            }
            else if (_switchState == SwitchState.Drawing && !Weapon.IsDrawing)
            {
                _switchState = SwitchState.Idle;
            }
            float weight = _switchState == SwitchState.Drawing && _drawFromRest ? Weapon.DrawProgress : 1f;
            ReachWeapon(weight, step);
            return;
        }

        _leftArm.Rest(step * 0.25f);
        _rightArm.Rest(-step * 0.25f);
    }
}
