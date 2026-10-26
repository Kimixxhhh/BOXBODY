using Godot;
using System;

public partial class PracticeTarget : StaticBody3D, IProjectileTarget
{
    public double Health { get; private set; } = 100;
    private double _respawn, _flash;
    private MeshInstance3D _board = null!;
    private StandardMaterial3D _material = null!;
    private Label3D _label = null!;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 0;
        AddChild(new CollisionShape3D { Position = Vector3.Up * 1.1f, Shape = new BoxShape3D { Size = new Vector3(1, 1.5f, .25f) } });
        _material = new StandardMaterial3D { AlbedoColor = new Color(.20f, .31f, .28f), Roughness = 1 };
        _board = new MeshInstance3D { Position = Vector3.Up * 1.1f, Mesh = new BoxMesh { Size = new Vector3(1, 1.5f, .25f) }, MaterialOverride = _material };
        AddChild(_board);
        AddChild(new MeshInstance3D { Position = Vector3.Up * .2f, Mesh = new BoxMesh { Size = new Vector3(.12f, .4f, .12f) }, MaterialOverride = _material });
        foreach (var (radius, color, depth) in new[] { (.34f, new Color(.88f, .85f, .70f), .13f), (.22f, new Color(.22f, .32f, .28f), .14f), (.09f, new Color(.95f, .57f, .24f), .15f) })
            _board.AddChild(new MeshInstance3D { Position = new Vector3(0, .12f, depth), RotationDegrees = new Vector3(90, 0, 0),
                Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = .01f },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color } });
        _label = new Label3D { Position = Vector3.Up * 2.15f, FontSize = 32, PixelSize = .007f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, OutlineSize = 6,
            Font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif" } } };
        AddChild(_label);
        RefreshLabel();
    }

    public void ApplyHit(ProjectileHit hit)
    {
        if (Health <= 0) return;
        Health = Math.Max(0, Health - hit.Damage);
        _flash = .12;
        if (Health <= 0)
        {
            _respawn = 3;
            CollisionLayer = 0;
            _board.Hide();
        }
        RefreshLabel();
    }

    private void RefreshLabel() => _label.Text = Health > 0 ? $"练习靶  {Health:0.#} / 100" : "已击倒 · 3 秒后恢复";

    public override void _Process(double delta)
    {
        _flash = Math.Max(0, _flash - delta);
        _material.AlbedoColor = _flash > 0 ? new Color(1, .48f, .2f) : new Color(.20f, .31f, .28f);
        if (_respawn <= 0) return;
        _respawn -= delta;
        if (_respawn <= 0) { Health = 100; CollisionLayer = 4; _board.Show(); RefreshLabel(); }
    }
}
