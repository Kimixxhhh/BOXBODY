using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The supplied Millitia asset is a collection of rigid meshes, with no skeleton.
// Pose the instance around shoulder pivots and animate its separate feet.
public sealed class CharacterAnimator
{
    private const float ArmLengthScale = 1.15f;
    private readonly Node3D _model;
    private readonly Node3D _visual;
    private readonly ArmRig _leftArm, _rightArm;
    private readonly Node3D _leftFoot, _rightFoot;
    public HeldWeaponAnimator? Weapon { get; private set; }
    private readonly Vector3 _modelRestPosition, _leftFootRestPosition, _rightFootRestPosition;
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
        _leftFoot = CreateFoot(parts, "Left");
        _rightFoot = CreateFoot(parts, "Right");
        _leftFootRestPosition = _leftFoot.Position;
        _rightFootRestPosition = _rightFoot.Position;
        _leftArm.Rest(0);
        _rightArm.Rest(0);
    }

    public void EquipWeapon(string scenePath)
    {
        // Create the replacement before retiring the current prop so a failed load
        // leaves the previous equipment intact and the purchase can be rejected.
        var next = new HeldWeaponAnimator(_model, scenePath);
        if (Weapon != null)
        {
            _model.RemoveChild(Weapon.Root);
            Weapon.Root.QueueFree();
        }
        Weapon = next;
        Weapon.Root.Name = "HeldWeapon";
        _leftArm.Reach(Weapon.LeftHandTarget);
        _rightArm.Reach(Weapon.RightHandTarget);
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
        return new ArmRig(pivot, elbow, upperLength, handReach, side == "Left" ? -1 : 1);
    }

    private sealed class ArmRig(Node3D shoulder, Node3D elbow, float upperLength, float forearmLength, float side)
    {
        public void Rest(float swing)
        {
            shoulder.Rotation = new Vector3(swing, 0, 0);
            elbow.Rotation = new Vector3(-0.12f, 0, 0);
        }

        public void Reach(Vector3 target)
        {
            Vector3 delta = target - shoulder.Position;
            float distance = Mathf.Clamp(delta.Length(), 0.02f, upperLength + forearmLength - 0.001f);
            Vector3 direction = delta.Normalized();
            Vector3 preferredBend = new Vector3(side, -0.8f, 0.15f);
            Vector3 bend = (preferredBend - direction * preferredBend.Dot(direction)).Normalized();
            float along = (upperLength * upperLength - forearmLength * forearmLength + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 joint = shoulder.Position + direction * along + bend * height;
            Vector3 reachableTarget = shoulder.Position + direction * distance;
            shoulder.Basis = new Basis(new Quaternion(Vector3.Down, (joint - shoulder.Position).Normalized()));
            Basis lowerBasis = new(new Quaternion(Vector3.Down, (reachableTarget - joint).Normalized()));
            elbow.Basis = shoulder.Basis.Inverse() * lowerBasis;
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
        if (Weapon != null)
        {
            Weapon.Update(delta, _blend, _phase);
            _leftArm.Reach(Weapon.LeftHandTarget);
            _rightArm.Reach(Weapon.RightHandTarget);
        }
        else
        {
            _leftArm.Rest(step * 0.25f);
            _rightArm.Rest(-step * 0.25f);
        }
        _leftFoot.Position = _leftFootRestPosition + _travel * (step * 0.19f) + Vector3.Up * (liftLeft * 0.10f);
        _rightFoot.Position = _rightFootRestPosition - _travel * (step * 0.19f) + Vector3.Up * (liftRight * 0.10f);
        _model.Position = _modelRestPosition + Vector3.Up * (Mathf.Abs(Mathf.Sin(_phase * 2)) * 0.018f * _blend);
    }
}
