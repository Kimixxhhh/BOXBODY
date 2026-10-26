using Godot;
using System;
using FieldKit.Weapons;

public readonly record struct ProjectileHit(double Damage, double SpeedMultiplier, Vector3 Knockback, Vector3 Position)
{
    // Bullets use zero duration; explosive throwables carry their template value.
    public double SlowDuration { get; init; }
}

public interface IProjectileTarget
{
    void ApplyHit(ProjectileHit hit);
}

// Sweep the entire distance each physics frame so fast bullets cannot skip thin walls.
public partial class Projectile : Node3D
{
    public const uint HitMask = 1 | 4;
    public const float MaxDistance = 100;
    public const double MaxLifetime = 5;
    public Vector3 Direction { get; private set; }
    public bool HasHit { get; private set; }
    public event Action<ProjectileHit, GodotObject>? Hit;
    private double _speed, _age;
    private float _distance;
    private ProjectileHit _damage;
    private Rid _shooter;

    public void Initialize(Vector3 direction, WeaponStats stats, Rid shooter)
    {
        Direction = direction.Normalized();
        _speed = stats.ProjectileSpeed;
        _shooter = shooter;
        _damage = new ProjectileHit(stats.Damage, stats.EnemySpeedMultiplier(), Direction * (float)stats.KnockbackSpeed, Vector3.Zero);
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D {
            Mesh = new BoxMesh { Size = new Vector3(.035f, .035f, .42f) },
            Position = Vector3.Back * .21f,
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1, .78f, .25f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        LookAt(GlobalPosition + Direction, Vector3.Up);
    }

    // Also check the path through the gun before spawning beyond a nearby obstacle.
    public bool Sweep(Vector3 from, Vector3 to)
    {
        if (HasHit) return true;
        if (from.IsEqualApprox(to)) return false;
        var query = PhysicsRayQueryParameters3D.Create(from, to, HitMask, new Godot.Collections.Array<Rid> { _shooter });
        query.HitFromInside = true;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0) return false;
        HasHit = true;
        GlobalPosition = (Vector3)result["position"];
        var collider = result["collider"].AsGodotObject();
        var hit = _damage with { Position = GlobalPosition };
        if (collider is IProjectileTarget target) target.ApplyHit(hit);
        Hit?.Invoke(hit, collider);
        ShotEffect.Spawn(GetParent(), GlobalPosition, new Color(1, .65f, .2f), .12f, .1);
        Hide();
        QueueFree();
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (HasHit) return;
        double stepTime = Math.Min(delta, MaxLifetime - _age);
        float distance = (float)Math.Min(_speed * stepTime, MaxDistance - _distance);
        Vector3 next = GlobalPosition + Direction * distance;
        if (Sweep(GlobalPosition, next)) return;
        GlobalPosition = next;
        _distance += distance;
        _age += delta;
        if (_distance >= MaxDistance || _age >= MaxLifetime) QueueFree();
    }
}
