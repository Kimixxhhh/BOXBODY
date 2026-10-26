using Godot;
using System;
using System.Collections.Generic;
using FieldKit.Items;

// Hold-G radial selector for the throwable kinds currently in the backpack.
// The mouse direction picks a sector; releasing G asks the player controller to
// draw the highlighted kind. A single kind still gets one full-ring sector.
public partial class ThrowableWheelUi : Control
{
    private readonly List<ThrowableKind> _entries = new();
    private int _selected;
    private double _openTime;
    private bool _switching;
    private bool _manualSelection;
    private bool _mouseMoved;
    private Vector2 _openMouse;
    private Font _font = null!;

    public bool IsOpen => Visible;
    public int EntryCount => _entries.Count;
    public IReadOnlyList<ThrowableKind> Entries => _entries;
    public event Action<bool>? OpenChanged;

    public string? SelectedId
    {
        get
        {
            if (!IsOpen || _entries.Count == 0) return null;
            UpdateSelection(GetLocalMousePosition());
            return _entries[_selected].Id;
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif" } };
        Hide();
    }

    public void Open(IReadOnlyList<ThrowableKind> entries, string? initialId, bool switching)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        _switching = switching;
        if (_entries.Count == 0)
        {
            Hide();
            return;
        }
        _selected = 0;
        if (!string.IsNullOrEmpty(initialId))
        {
            int index = _entries.FindIndex(entry => entry.Id == initialId);
            if (index >= 0) _selected = index;
        }
        _manualSelection = false;
        _mouseMoved = false;
        _openTime = 0;
        Modulate = new Color(1, 1, 1, 0);
        Show();
        _openMouse = GetLocalMousePosition();
        OpenChanged?.Invoke(true);
        QueueRedraw();
    }

    public void Close()
    {
        bool wasOpen = IsOpen;
        Hide();
        _entries.Clear();
        _selected = 0;
        _switching = false;
        _manualSelection = false;
        _mouseMoved = false;
        Modulate = Colors.White;
        QueueRedraw();
        if (wasOpen) OpenChanged?.Invoke(false);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _openTime = Math.Min(_openTime + delta, .2);
        Modulate = new Color(1, 1, 1, Mathf.Clamp((float)(_openTime / .12), 0, 1));
        UpdateSelection(GetLocalMousePosition());
        QueueRedraw();
    }

    // Exposed for the smoke test: verifies the angle-to-sector mapping without
    // depending on a platform mouse warp.
    internal int IndexAt(Vector2 point)
    {
        if (_entries.Count <= 1) return 0;
        Vector2 delta = point - Size * .5f;
        if (delta.LengthSquared() < 24 * 24) return _selected;
        float step = Mathf.Tau / _entries.Count;
        float angle = Mathf.PosMod(Mathf.Atan2(delta.Y, delta.X) + Mathf.Pi * .5f, Mathf.Tau);
        return Math.Clamp((int)(angle / step), 0, _entries.Count - 1);
    }

    private void UpdateSelection(Vector2 point)
    {
        if (!Visible || _entries.Count == 0 || _manualSelection) return;
        // The held/first kind stays selected until the mouse actually moves, so
        // opening the wheel never jumps to whatever happens to be under it.
        if (!_mouseMoved)
        {
            if (point.DistanceSquaredTo(_openMouse) < 12 * 12) return;
            _mouseMoved = true;
        }
        // The platform may report a point outside the control (for example a
        // dummy headless display); keep the current sector in that case.
        if (!new Rect2(Vector2.Zero, Size).HasPoint(point)) return;
        int next = IndexAt(point);
        if (next == _selected) return;
        _selected = next;
        QueueRedraw();
    }

    // Test hook: drives the same angle mapping without relying on the platform
    // mouse position, then locks the selection until the wheel is reopened.
    internal void SelectAt(Vector2 point)
    {
        if (!Visible || _entries.Count == 0) return;
        _manualSelection = true;
        int next = IndexAt(point);
        if (next == _selected) return;
        _selected = next;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible || _entries.Count == 0 || Size.X < 10 || Size.Y < 10) return;
        Vector2 center = Size * .5f;
        float minimum = Mathf.Min(Size.X, Size.Y);
        float outer = minimum * .27f;
        float inner = outer * .42f;
        float ease = Mathf.Clamp((float)(_openTime / .12), 0, 1);
        float grow = Mathf.Lerp(.94f, 1, ease);
        outer *= grow;
        inner *= grow;

        DrawRect(new Rect2(Vector2.Zero, Size), new Color(.02f, .03f, .03f, .42f * ease));
        DrawArc(center, outer + 6, 0, Mathf.Tau, 96, new Color(0, 0, 0, .22f * ease), 10, true);

        int count = _entries.Count;
        float step = Mathf.Tau / count;
        for (int i = 0; i < count; i++)
        {
            bool active = i == _selected;
            Color fill = active ? new Color(.72f, .79f, .64f, .97f) : new Color(.075f, .095f, .09f, .96f);
            Color edge = active ? new Color(.87f, .91f, .81f) : new Color(.30f, .36f, .34f);
            float a0 = -Mathf.Pi * .5f + i * step;
            float a1 = a0 + step;
            if (count == 1)
            {
                DrawArc(center, (inner + outer) * .5f, 0, Mathf.Tau, 96, fill, outer - inner, true);
                DrawArc(center, outer, 0, Mathf.Tau, 96, edge, 2, true);
                DrawArc(center, inner, 0, Mathf.Tau, 96, edge, 2, true);
            }
            else
            {
                DrawColoredPolygon(Sector(center, inner, outer, a0, a1), fill);
                DrawArc(center, outer, a0, a1, 40, edge, active ? 2.5f : 1.5f, true);
                DrawArc(center, inner, a0, a1, 40, edge, active ? 2.5f : 1.5f, true);
                DrawLine(center + Polar(a0, inner), center + Polar(a0, outer), edge, 1.5f, true);
            }
            float labelAngle = count == 1 ? -Mathf.Pi * .5f : (a0 + a1) * .5f;
            float radius = (inner + outer) * .5f;
            float maxWidth = count == 1 ? radius * 1.65f : 2 * radius * Mathf.Sin(step * .5f) * .8f;
            DrawEntry(center + Polar(labelAngle, radius), maxWidth, _entries[i], active);
        }

        DrawCircle(center, inner - 9, new Color(.045f, .06f, .055f, .98f));
        DrawArc(center, inner - 9, 0, Mathf.Tau, 64, new Color(.45f, .53f, .50f), 1.5f, true);
        DrawCentered("投掷物", center + new Vector2(0, -6), inner * 1.7f, 20, new Color(.92f, .94f, .93f));
        DrawCentered("移动鼠标选择", center + new Vector2(0, 16), inner * 1.7f, 12,
            new Color(.62f, .69f, .66f));
        DrawCentered(_switching ? "松开 G 切换" : "松开 G 拿出", center + new Vector2(0, 33),
            inner * 1.7f, 12, new Color(.72f, .79f, .64f));
    }

    private void DrawEntry(Vector2 point, float maxWidth, ThrowableKind entry, bool active)
    {
        Color nameColor = active ? new Color(.06f, .09f, .075f) : new Color(.91f, .94f, .93f);
        Color countColor = active ? new Color(.12f, .16f, .13f) : new Color(.72f, .79f, .64f);
        DrawCentered(FitText(entry.Name, maxWidth, 17), point + new Vector2(0, -1), maxWidth, 17, nameColor);
        DrawCentered($"×{entry.Remaining}", point + new Vector2(0, 19), maxWidth, 13, countColor);
    }

    private void DrawCentered(string text, Vector2 baseline, float width, int size, Color color) =>
        DrawString(_font, new Vector2(baseline.X - width * .5f, baseline.Y), text,
            HorizontalAlignment.Center, width, size, color);

    private string FitText(string text, float width, int size)
    {
        if (_font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X <= width) return text;
        string trimmed = text;
        while (trimmed.Length > 1 &&
               _font.GetStringSize(trimmed + "…", HorizontalAlignment.Left, -1, size).X > width)
            trimmed = trimmed[..^1];
        return trimmed + "…";
    }

    private static Vector2 Polar(float angle, float radius) =>
        new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

    private static Vector2[] Sector(Vector2 center, float inner, float outer, float a0, float a1)
    {
        int segments = Mathf.Max(4, Mathf.CeilToInt((a1 - a0) / Mathf.Tau * 72));
        var points = new Vector2[segments * 2 + 2];
        int index = 0;
        for (int step = 0; step <= segments; step++)
            points[index++] = center + Polar(Mathf.Lerp(a0, a1, step / (float)segments), outer);
        for (int step = segments; step >= 0; step--)
            points[index++] = center + Polar(Mathf.Lerp(a0, a1, step / (float)segments), inner);
        return points;
    }
}
