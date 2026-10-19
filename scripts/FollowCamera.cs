using Godot;

public partial class FollowCamera : Camera3D
{
    [Export] public Vector3 Offset { get; set; } = new(0, 16.5f, 11);
    [Export] public float TargetHeight { get; set; } = 0.7f;
    public Node3D Target { get; set; } = null!;

    public override void _Ready() => ProcessPriority = -20;

    public override void _Process(double delta) => SnapToTarget();

    public void SnapToTarget()
    {
        if (Target == null)
            return;
        Vector3 focus = Target.GlobalPosition + Vector3.Up * TargetHeight;
        GlobalPosition = focus + Offset;
        LookAt(focus, Vector3.Up);
    }
}
