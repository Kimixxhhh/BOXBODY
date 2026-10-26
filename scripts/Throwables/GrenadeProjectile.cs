using Godot;
using System;
using System.Collections.Generic;
using FieldKit.Weapons;

// A thrown grenade follows a ballistic arc, bounces off world geometry and
// explodes when its template fuse expires. Damage is resolved with one sphere
// query so every target inside the configured range receives the same template.
public partial class GrenadeProjectile : Node3D
{
    public const uint HitMask = 1 | 4;
    public const float Gravity = 22f;
    public const float Restitution = .32f;
    public ThrowableDefinition Definition { get; private set; } = null!;
    public Vector3 Velocity { get; private set; }
    public double FuseRemaining { get; private set; }
    public bool HasExploded { get; private set; }
    public event Action<GrenadeProjectile>? Exploded;
    private Node3D _visual = null!;
    private OmniLight3D? _glow;
    private float _radius = .09f;
    private float _age;

    public void Initialize(ThrowableDefinition definition, Vector3 velocity)
    {
        Definition = definition;
        Velocity = velocity;
        FuseRemaining = definition.FuseTime;
    }

    public override void _Ready()
    {
        _visual = LoadVisual();
        AddChild(_visual);
        // A tiny real-world grenade is hard to follow from the top-down camera;
        // the glow makes its position and shortening fuse readable.
        _glow = new OmniLight3D {
            LightColor = new Color(1f, .45f, .12f), LightEnergy = .7f,
            OmniRange = 1.9f, ShadowEnabled = false
        };
        AddChild(_glow);
    }

    private Node3D LoadVisual()
    {
        try
        {
            if (!string.IsNullOrEmpty(Definition.ItemScenePath) && ResourceLoader.Exists(Definition.ItemScenePath))
            {
                var scene = GD.Load<PackedScene>(Definition.ItemScenePath);
                if (scene?.Instantiate() is Node3D model)
                {
                    model.Scale = Vector3.One * 2f;
                    model.Position = Vector3.Up * .1f;
                    return model;
                }
            }
        }
        catch (Exception error)
        {
            GD.PushWarning($"无法加载投掷物场景 {Definition.ItemScenePath}：{error.Message}");
        }
        // A configurable scene is preferred, but a missing asset must not make a
        // purchased grenade unusable or silently swallow it.
        var fallback = new MeshInstance3D {
            Mesh = new SphereMesh { Radius = _radius, Height = _radius * 2, RadialSegments = 16, Rings = 8 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.23f, .28f, .21f), Roughness = .7f },
            Scale = Vector3.One * 2f,
            Position = Vector3.Up * .1f
        };
        return fallback;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (HasExploded) return;
        FuseRemaining -= delta;
        if (FuseRemaining <= 0)
        {
            Explode();
            return;
        }

        float step = (float)delta;
        _age += step;
        if (_glow != null)
        {
            float urgency = Mathf.Lerp(5f, 18f, 1 - (float)(FuseRemaining / Math.Max(.01, Definition.FuseTime)));
            _glow.LightEnergy = .35f + .65f * (.5f + .5f * Mathf.Sin(_age * urgency));
        }
        Vector3 previous = GlobalPosition;
        Velocity += Vector3.Down * Gravity * step;
        Vector3 next = previous + Velocity * step;
        var query = PhysicsRayQueryParameters3D.Create(previous, next, HitMask);
        query.HitFromInside = true;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            Vector3 normal = (Vector3)result["normal"];
            GlobalPosition = (Vector3)result["position"] + normal * .055f;
            Vector3 bounced = Velocity.Bounce(normal) * Restitution;
            bounced.X *= .55f;
            bounced.Z *= .55f;
            // Settle instead of jittering forever on the ground plane.
            if (normal.Y > .7f && Mathf.Abs(bounced.Y) < .7f) bounced.Y = 0;
            Velocity = bounced;
        }
        else GlobalPosition = next;

        if (_visual != null)
        {
            _visual.RotateX(7f * step);
            _visual.RotateZ(2.7f * step);
        }
        if (GlobalPosition.Y < -20) QueueFree();
    }

    public void Explode()
    {
        if (HasExploded) return;
        HasExploded = true;
        Vector3 center = GlobalPosition;
        Node parent = GetParent();
        ExplosionEffect.Spawn(parent, center, (float)Definition.Range);
        parent.GetNodeOrNull<ExplosionAudioPlayer>("ExplosionAudio")?.Play(Definition.ExplosionSoundPath, center);
        ApplyExplosion(center);
        Exploded?.Invoke(this);
        Hide();
        SetPhysicsProcess(false);
        QueueFree();
    }

    private void ApplyExplosion(Vector3 center)
    {
        var query = new PhysicsShapeQueryParameters3D {
            Shape = new SphereShape3D { Radius = (float)Definition.Range },
            Transform = new Transform3D(Basis.Identity, center),
            CollisionMask = 4,
            CollideWithBodies = true,
            CollideWithAreas = false
        };
        var seen = new HashSet<ulong>();
        foreach (var result in GetWorld3D().DirectSpaceState.IntersectShape(query, 64))
        {
            var collider = result["collider"].AsGodotObject();
            if (collider is not IProjectileTarget target) continue;
            if (collider is Node node && !seen.Add(node.GetInstanceId())) continue;
            Vector3 targetCenter = collider is Node3D spatial ? spatial.GlobalPosition + Vector3.Up : center;
            Vector3 direction = targetCenter - center;
            direction.Y = 0;
            if (direction.LengthSquared() > .0001f) direction = direction.Normalized();
            var hit = new ProjectileHit(Definition.Damage, Definition.EnemySpeedMultiplier(),
                direction * (float)Definition.KnockbackSpeed, targetCenter) {
                SlowDuration = Definition.SlowDuration
            };
            target.ApplyHit(hit);
        }
    }
}
