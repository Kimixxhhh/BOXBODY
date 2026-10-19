using Godot;
using System;
using System.Linq;
using FieldKit.Weapons;

// Presentation only: this owns the held prop and a reload timeline, not ammunition or firing.
public sealed class HeldWeaponAnimator
{
    // Keyframes below were authored on a 2.65-second timeline. Gameplay duration
    // is captured per reload, then mapped onto that timeline for every moving part.
    private const float AnimationDuration = (float)WeaponStats.DefaultReloadDuration;
    public static readonly Vector3 RightGrip = new(0.028f, 0.05f, 0.078f);
    public static readonly Vector3 SupportGrip = new(-0.045f, 0.12f, -0.29f);
    public Node3D Root { get; }
    public bool IsReloading { get; private set; }
    public double ReloadElapsed { get; private set; }
    public double ReloadDuration { get; private set; } = WeaponStats.DefaultReloadDuration;
    public Vector3 RightHandTarget => Root.Transform * RightGrip;
    public Vector3 LeftHandTarget => Root.Transform * _leftHandTarget.Position;

    private readonly Node3D _magazine;
    private readonly MeshInstance3D? _bolt;
    private readonly Vector3 _magazineRest;
    private readonly Transform3D _boltRest;
    private readonly Marker3D _leftHandTarget;
    // Broaden the slim source mesh to match the character's blocky hands.
    // Hold the rifle farther in front of the torso; hand targets follow the prop.
    private readonly Vector3 _holdPosition = new(0.0848f, 0.885f, -0.41106f);

    public HeldWeaponAnimator(Node3D model, string scenePath)
    {
        var scene = GD.Load<PackedScene>(scenePath) ?? throw new InvalidOperationException($"无法加载武器场景：{scenePath}");
        var asset = scene.Instantiate<Node3D>();
        Root = new Node3D { Name = "HeldWeapon", Scale = new Vector3(1.8f, 1.2f, 1.17f) };
        model.AddChild(Root);
        asset.Name = "Model";
        Root.AddChild(asset);
        var magazineMesh = Descendants(asset).OfType<MeshInstance3D>()
            .FirstOrDefault(p => p.Name.ToString().StartsWith("Magazine", StringComparison.Ordinal));
        _bolt = Descendants(asset).OfType<MeshInstance3D>()
            .FirstOrDefault(p => p.Name.ToString().StartsWith("Trigger_10", StringComparison.Ordinal)
                && p.Position.X > 0.01f);
        _boltRest = _bolt?.Transform ?? Transform3D.Identity;
        _magazineRest = new Vector3(0, 0.125f, -0.10f);
        _magazine = new Node3D { Name = "MagazineMotion", Position = _magazineRest };
        Root.AddChild(_magazine);
        if (magazineMesh != null)
        {
            Transform3D magazineInWeapon = Root.GlobalTransform.AffineInverse() * magazineMesh.GlobalTransform;
            magazineMesh.Reparent(_magazine, false);
            magazineInWeapon.Origin -= _magazineRest;
            magazineMesh.Transform = magazineInWeapon;
        }
        Root.AddChild(new Marker3D { Name = "RightGrip", Position = RightGrip });
        Root.AddChild(new Marker3D { Name = "SupportGrip", Position = SupportGrip });
        _leftHandTarget = new Marker3D { Name = "LeftHandTarget", Position = SupportGrip };
        Root.AddChild(_leftHandTarget);
        Update(0, 0, 0);
    }

    private static System.Collections.Generic.IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }

    public bool TryReload(double duration)
    {
        if (IsReloading || !double.IsFinite(duration) || duration <= 0) return false;
        ReloadDuration = duration;
        IsReloading = true;
        ReloadElapsed = 0;
        return true;
    }

    private static float Ease(float time, float from, float to)
    {
        float t = Mathf.Clamp((time - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public void Update(float delta, float walkBlend, float walkPhase)
    {
        if (IsReloading)
        {
            ReloadElapsed += delta;
            if (ReloadElapsed >= ReloadDuration)
            {
                IsReloading = false;
                ReloadElapsed = 0;
            }
        }
        float time = IsReloading ? (float)(ReloadElapsed / ReloadDuration * AnimationDuration) : 0;
        float pose = Ease(time, 0, 0.28f) * (1 - Ease(time, 2.25f, AnimationDuration));
        Root.Position = _holdPosition + new Vector3(0.015f, -0.035f, -0.015f) * pose
            + Vector3.Up * (Mathf.Sin(walkPhase * 2) * 0.006f * walkBlend);
        Root.Rotation = new Vector3(0.10f, -0.10f, 0.28f) * pose;

        Vector3 offset;
        float rock;
        if (time < 0.95f)
        {
            float t = Ease(time, 0.50f, 0.95f);
            offset = new Vector3(-0.10f, -0.24f, 0.045f) * t;
            rock = t;
        }
        else if (time < 1.25f)
        {
            float t = Ease(time, 0.95f, 1.12f);
            offset = new Vector3(-0.10f, -0.24f, 0.045f).Lerp(new Vector3(-0.23f, -0.33f, 0.18f), t);
            rock = 1;
        }
        else if (time < 1.68f)
        {
            float t = Ease(time, 1.25f, 1.68f);
            offset = new Vector3(-0.23f, -0.33f, 0.18f).Lerp(new Vector3(-0.025f, -0.10f, 0.025f), t);
            rock = Mathf.Lerp(1, 0.35f, t);
        }
        else
        {
            float t = 1 - Ease(time, 1.68f, 1.87f);
            offset = new Vector3(-0.025f, -0.10f, 0.025f) * t;
            rock = t * 0.35f;
        }
        _magazine.Position = _magazineRest + offset;
        _magazine.Rotation = new Vector3(-0.28f, 0, -0.15f) * rock;
        float boltPull = Ease(time, 2.04f, 2.13f) * (1 - Ease(time, 2.13f, 2.24f));
        Transform3D boltPose = _boltRest;
        boltPose.Origin += Vector3.Back * (0.045f * boltPull);
        if (_bolt != null) _bolt.Transform = boltPose;

        Vector3 magazineHand = _magazine.Transform * new Vector3(-0.044f, -0.11f, -0.055f);
        Vector3 boltHand = new(0.06f, 0.17f, -0.10f + 0.045f * boltPull);
        Vector3 hand;
        if (time < 0.48f) hand = SupportGrip.Lerp(magazineHand, Ease(time, 0.12f, 0.48f));
        else if (time < 1.89f) hand = magazineHand;
        else if (time < 2.04f) hand = magazineHand.Lerp(boltHand, Ease(time, 1.89f, 2.04f));
        else hand = boltHand.Lerp(SupportGrip, Ease(time, 2.24f, AnimationDuration));
        _leftHandTarget.Position = hand;
    }
}
