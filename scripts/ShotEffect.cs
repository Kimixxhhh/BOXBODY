using Godot;

// Short-lived world-space flashes never become part of the character's outline rig.
public partial class ShotEffect : MeshInstance3D
{
    private double _remaining;

    public static void Spawn(Node parent, Vector3 position, Color color, float size, double duration)
    {
        var effect = new ShotEffect {
            Mesh = new SphereMesh { Radius = size / 2, Height = size, RadialSegments = 8, Rings = 4 },
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color },
            CastShadow = ShadowCastingSetting.Off,
            _remaining = duration };
        parent.AddChild(effect);
        effect.GlobalPosition = position;
    }

    public override void _Process(double delta)
    {
        _remaining -= delta;
        if (_remaining <= 0) QueueFree();
    }
}
