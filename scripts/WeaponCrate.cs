using Godot;

public partial class WeaponCrate : StaticBody3D
{
    [Export] public float InteractionDistance { get; set; } = 1.35f;
    public Node3D Visual => GetNode<Node3D>("Visual");
    public Vector3 PromptPosition => GlobalPosition + Vector3.Up * 1.65f;

    public bool IsInReach(Node3D player)
    {
        // Distance to the case footprint, so all four sides have the same usable reach.
        Vector3 local = ToLocal(player.GlobalPosition);
        Vector2 outside = new(Mathf.Max(Mathf.Abs(local.X) - 0.84f, 0), Mathf.Max(Mathf.Abs(local.Z) - 0.48f, 0));
        return outside.Length() <= InteractionDistance && Mathf.Abs(local.Y) < 2.0f;
    }
}
