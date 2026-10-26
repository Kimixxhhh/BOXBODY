using Godot;
using System;

public partial class CombatReticle : Control
{
    public PlayerController Player { get; set; } = null!;
    private double _hitTime;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Player.TargetHit += MarkHit;
    }
    private void MarkHit() => _hitTime = .15;
    public override void _ExitTree() => Player.TargetHit -= MarkHit;
    public override void _Process(double delta) { _hitTime = Math.Max(0, _hitTime - delta); QueueRedraw(); }
    public override void _Draw()
    {
        if (Player.InputBlocked || Player.IsThrowSelecting) return;
        Vector2 point = GetLocalMousePosition();
        if (Player.IsGrenadeBusy)
        {
            Color grenadeColor = Player.IsGrenadeReady ? new Color(.72f, .79f, .64f) : new Color(.6f, .67f, .65f);
            DrawArc(point, 11, 0, Mathf.Tau, 32, grenadeColor, 2, true);
            DrawCircle(point, 2, grenadeColor);
            return;
        }
        if (Player.EquippedWeapon == null) return;
        float gap = 4 + Mathf.Min(18, (float)Player.CurrentSpread * 2);
        Color color = Player.IsReloading ? new Color(.6f, .6f, .6f) : new Color(.16f, .25f, .21f);
        foreach (Vector2 axis in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
            DrawLine(point + axis * gap, point + axis * (gap + 6), color, 2, true);
        if (_hitTime > 0)
            foreach (Vector2 axis in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                DrawLine(point + axis * 4, point + axis * 9, new Color(.9f, .3f, .1f), 2, true);
    }
}
