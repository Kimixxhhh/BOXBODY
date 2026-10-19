using Godot;
using System.Collections.Generic;

public partial class CrateOutline : Node
{
    private Camera3D _sourceCamera = null!;
    private Camera3D _maskCamera = null!;
    private SubViewport _mask = null!;
    private Node3D _crateSource = null!, _playerSource = null!, _crateMask = null!, _playerMask = null!;
    private ShaderMaterial _material = null!;
    private readonly List<(Node3D Source, Node3D Mask)> _playerPose = new();

    public void Initialize(Camera3D camera, Node3D crate, Node3D player)
    {
        ProcessPriority = 10;
        _sourceCamera = camera;
        _crateSource = crate;
        _playerSource = player;
        _mask = new SubViewport
        {
            Name = "SilhouetteMask", OwnWorld3D = true, TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Disabled
        };
        AddChild(_mask);
        _mask.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black }
        });
        _maskCamera = new Camera3D { Current = true };
        _mask.AddChild(_maskCamera);
        // A red mask for the entire prop avoids outlining internal triangle edges.
        // Green represents the character and prevents the outline from drawing over them.
        _crateMask = MakeMask(crate, Colors.Red);
        _playerMask = MakeMask(player, Colors.Green);
        _crateMask.Name = "CrateMask";
        _playerMask.Name = "PlayerMask";
        _mask.AddChild(_crateMask);
        _mask.AddChild(_playerMask);
        CachePosePairs(_playerSource, _playerMask);
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/crate_outline.gdshader") };
        _material.SetShaderParameter("mask_texture", _mask.GetTexture());
        var layer = new CanvasLayer { Layer = 1, Name = "WhiteOutline" };
        AddChild(layer);
        var overlay = new ColorRect { Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        GetViewport().SizeChanged += ResizeMask;
        ResizeMask();
        SyncTransforms();
    }

    private static Node3D MakeMask(Node3D source, Color color)
    {
        var clone = (Node3D)source.Duplicate();
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        ApplyMaterial(clone, mat);
        return clone;
    }

    public void RefreshPlayerEquipment()
    {
        _playerPose.Clear();
        _mask.RemoveChild(_playerMask);
        _playerMask.QueueFree();
        _playerMask = MakeMask(_playerSource, Colors.Green);
        _playerMask.Name = "PlayerMask";
        _mask.AddChild(_playerMask);
        CachePosePairs(_playerSource, _playerMask);
        SyncTransforms();
    }

    private static void ApplyMaterial(Node node, Material material)
    {
        if (node is MeshInstance3D mesh)
        {
            mesh.MaterialOverride = material;
            mesh.MaterialOverlay = null;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        foreach (Node child in node.GetChildren()) ApplyMaterial(child, material);
    }

    private void CachePosePairs(Node source, Node mask)
    {
        for (int i = 0; i < source.GetChildCount(); i++)
        {
            Node child = source.GetChild(i);
            Node copy = mask.GetChild(i);
            if (child is Node3D source3D && copy is Node3D mask3D)
                _playerPose.Add((source3D, mask3D));
            CachePosePairs(child, copy);
        }
    }

    private void ResizeMask()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        _mask.Size = new Vector2I(Mathf.Max(1, Mathf.RoundToInt(size.X)), Mathf.Max(1, Mathf.RoundToInt(size.Y)));
        _material.SetShaderParameter("pixel_size", new Vector2(1f / _mask.Size.X, 1f / _mask.Size.Y));
    }

    public override void _Process(double delta)
    {
        if (_sourceCamera != null) SyncTransforms();
    }

    private void SyncTransforms()
    {
        _maskCamera.GlobalTransform = _sourceCamera.GlobalTransform;
        _maskCamera.Projection = _sourceCamera.Projection;
        _maskCamera.Size = _sourceCamera.Size;
        _maskCamera.Fov = _sourceCamera.Fov;
        _maskCamera.Near = _sourceCamera.Near;
        _maskCamera.Far = _sourceCamera.Far;
        _crateMask.GlobalTransform = _crateSource.GlobalTransform;
        _playerMask.GlobalTransform = _playerSource.GlobalTransform;
        // Match each animated limb as well as the character root for correct occlusion.
        foreach (var (source, mask) in _playerPose) mask.Transform = source.Transform;
    }
}
