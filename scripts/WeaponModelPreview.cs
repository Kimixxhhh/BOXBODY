using Godot;

// An isolated 3D scene renders directly into the UI; no icon texture is needed.
public partial class WeaponModelPreview : SubViewport
{
    public Node3D Model { get; private set; } = null!;

    public void Initialize(string scenePath, Vector2I? viewportSize = null, Vector3? viewDirection = null)
    {
        Size = viewportSize ?? new Vector2I(1000, 220);
        TransparentBg = true;
        OwnWorld3D = true;
        Msaa3D = Viewport.Msaa.Msaa4X;
        RenderTargetUpdateMode = UpdateMode.Disabled;
        var world = new Node3D { Name = "PreviewWorld" };
        AddChild(world);
        Model = GD.Load<PackedScene>(scenePath).Instantiate<Node3D>();
        DisableGameplay(Model);
        world.AddChild(Model);
        Aabb? bounds = null;
        CollectBounds(Model, Transform3D.Identity, ref bounds);
        var box = bounds ?? new Aabb(new Vector3(-.1f,-.2f,-.5f), new Vector3(.2f,.4f,1));
        Vector3 center = box.GetCenter();
        var camera = new Camera3D {
            Projection = Camera3D.ProjectionType.Orthogonal, KeepAspect = Camera3D.KeepAspectEnum.Height,
            Size = Mathf.Max(.05f, Mathf.Max(box.Size.Y * 1.4f + box.Size.X * .15f, box.Size.Z / (Size.X / (float)Size.Y) * 1.2f)),
            Near = .01f, Far = Mathf.Max(20,box.Size.Length()*10), Current = true
        };
        world.AddChild(camera);
        camera.Position = center + (viewDirection.HasValue
            ? viewDirection.Value.Normalized() * Mathf.Max(3, box.Size.Length() * 3)
            : new Vector3(Mathf.Max(3,box.Size.Length()*3),.25f,0));
        camera.LookAt(center, Vector3.Up);
        if (viewDirection.HasValue)
        {
            Aabb projected = camera.Transform.AffineInverse() * box;
            camera.Size = Mathf.Max(.001f, Mathf.Max(projected.Size.Y, projected.Size.X / (Size.X / (float)Size.Y)) * 1.12f);
        }
        world.AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White, AmbientLightEnergy = .8f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled
        }});
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35,-45,-20), LightEnergy = 1.6f });
    }

    private static void DisableGameplay(Node node)
    {
        // Preview instances are never gameplay actors or active scene scripts.
        node.SetScript(default);
        node.ProcessMode = ProcessModeEnum.Disabled;
        foreach (Node child in node.GetChildren()) DisableGameplay(child);
    }

    private static void CollectBounds(Node node, Transform3D transform, ref Aabb? bounds)
    {
        if (node is Node3D spatial) transform *= spatial.Transform;
        if (node is MeshInstance3D mesh && mesh.Visible)
        {
            Aabb box = transform * mesh.GetAabb();
            bounds = bounds.HasValue ? bounds.Value.Merge(box) : box;
        }
        foreach (Node child in node.GetChildren()) CollectBounds(child, transform, ref bounds);
    }
}
