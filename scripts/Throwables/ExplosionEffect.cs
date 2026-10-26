using Godot;

// A short, self-freeing blast sphere and light. It is visual feedback only; the
// actual damage is applied by GrenadeProjectile in the same physics step.
public partial class ExplosionEffect : Node3D
{
    private const double Lifetime = .42;
    private MeshInstance3D _sphere = null!;
    private MeshInstance3D _ring = null!;
    private OmniLight3D _light = null!;
    private StandardMaterial3D _material = null!;
    private StandardMaterial3D _ringMaterial = null!;
    private float _radius;
    private double _age;

    public static void Spawn(Node parent, Vector3 position, float radius)
    {
        var effect = new ExplosionEffect { Name = "ExplosionEffect", _radius = Mathf.Max(.25f, radius) };
        parent.AddChild(effect);
        effect.GlobalPosition = position;
    }

    public override void _Ready()
    {
        _material = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1f, .38f, .08f, .82f),
            EmissionEnabled = true,
            Emission = new Color(1f, .28f, .04f),
            EmissionEnergyMultiplier = 3f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _sphere = new MeshInstance3D {
            Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 20, Rings = 10 },
            MaterialOverride = _material,
            Scale = Vector3.One * .2f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_sphere);
        _ringMaterial = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1f, .6f, .2f, .48f),
            EmissionEnabled = true,
            Emission = new Color(1f, .38f, .06f),
            EmissionEnergyMultiplier = 2.5f
        };
        _ring = new MeshInstance3D {
            Mesh = new TorusMesh { InnerRadius = .82f, OuterRadius = 1f, Rings = 40, RingSegments = 8 },
            MaterialOverride = _ringMaterial,
            Position = Vector3.Up * .03f,
            Scale = Vector3.One * .2f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_ring);
        _light = new OmniLight3D {
            LightColor = new Color(1f, .55f, .22f),
            LightEnergy = 7,
            OmniRange = _radius * 3.2f
        };
        AddChild(_light);
    }

    public override void _Process(double delta)
    {
        _age += delta;
        float t = Mathf.Clamp((float)(_age / Lifetime), 0, 1);
        float grow = Mathf.Lerp(.2f, _radius, Mathf.Sqrt(t));
        _sphere.Scale = Vector3.One * grow;
        float ringGrow = Mathf.Lerp(.2f, _radius, Mathf.Sqrt(t));
        _ring.Scale = new Vector3(ringGrow, 1, ringGrow);
        float fade = (1 - t) * (1 - t);
        _material.AlbedoColor = new Color(1f, .38f, .08f, .82f * fade);
        _ringMaterial.AlbedoColor = new Color(1f, .6f, .2f, .48f * fade);
        _light.LightEnergy = 7 * fade;
        if (t >= .7) _ring.Hide();
        if (t >= 1)
        {
            QueueFree();
            return;
        }
        // White-hot core in the first half, cooling as the sphere expands.
        float heat = Mathf.Max(0, 1 - t * 2);
        _material.Emission = new Color(1f, .28f + .3f * heat, .04f + .2f * heat);
    }
}
