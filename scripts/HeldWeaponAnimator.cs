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
    public Node3D Root { get; }
    public Marker3D Muzzle { get; }
    public string ScenePath { get; }
    private float _recoil;
    public bool IsReloading { get; private set; }
    public double ReloadElapsed { get; private set; }
    public double ReloadDuration { get; private set; } = WeaponStats.DefaultReloadDuration;
    public float DrawProgress { get; private set; } = 1f;
    public bool IsDrawing => _drawing;
    public bool IsHolstering => _holstering;
    public bool IsBusy => _drawing || _holstering;
    public Vector3 RightHandTarget => Root.Transform * _rightGrip;
    public Vector3 LeftHandTarget => Root.Transform * _leftHandTarget.Position;

    private readonly Node3D _magazine;
    private readonly Vector3 _magazineRest;
    private readonly Vector3 _magazineHandOffset;
    private readonly Marker3D _leftHandTarget;
    private readonly Vector3 _rightGrip, _supportGrip;
    private readonly bool _straightMagazine;
    private readonly Vector3 _magazineDrop, _magazineTilt;
    // Broaden the slim source mesh to match the character's blocky hands.
    // Hold the rifle farther in front of the torso; hand targets follow the prop.
    private readonly Vector3 _holdPosition = new(0.0848f, 0.885f, -0.41106f);
    // Lower the rifle and tip the muzzle down while the hands rotate it back up.
    private static readonly Vector3 StowOffset = new(0.0f, -0.24f, 0.08f);
    private static readonly Vector3 StowEuler = new(-0.70f, 0.15f, 0.45f);
    private static readonly Quaternion StowRotation = Quaternion.FromEuler(StowEuler);
    private double _drawElapsed;
    private double _drawDuration;
    private bool _drawing, _holstering;

    public HeldWeaponAnimator(Node3D model, string scenePath, Vector3 holdOffset = default)
    {
        ScenePath = scenePath;
        var profile = PresentationProfile.For(scenePath);
        _holdPosition += holdOffset + profile.HoldOffset;
        _rightGrip = profile.RightGrip;
        _supportGrip = profile.SupportGrip;
        _magazineRest = profile.MagazinePivot;
        _magazineHandOffset = profile.MagazineHandOffset;
        _straightMagazine = profile.StraightMagazine;
        _magazineDrop = profile.MagazineDrop;
        _magazineTilt = profile.MagazineTilt;
        var scene = GD.Load<PackedScene>(scenePath) ?? throw new InvalidOperationException($"无法加载武器场景：{scenePath}");
        var asset = scene.Instantiate<Node3D>();
        Root = new Node3D { Name = "HeldWeapon", Scale = new Vector3(1.8f, 1.2f, 1.17f) };
        model.AddChild(Root);
        asset.Name = "Model";
        Root.AddChild(asset);
        Muzzle = FindMuzzle(asset);
        var magazineMesh = Descendants(asset).OfType<MeshInstance3D>()
            .FirstOrDefault(p => p.Name.ToString().Contains("magazine", StringComparison.OrdinalIgnoreCase));
        _magazine = new Node3D { Name = "MagazineMotion", Position = _magazineRest };
        Root.AddChild(_magazine);
        if (magazineMesh != null)
        {
            Transform3D magazineInWeapon = Root.GlobalTransform.AffineInverse() * magazineMesh.GlobalTransform;
            magazineMesh.Reparent(_magazine, false);
            magazineInWeapon.Origin -= _magazineRest;
            magazineMesh.Transform = magazineInWeapon;
        }
        Root.AddChild(new Marker3D { Name = "RightGrip", Position = _rightGrip });
        Root.AddChild(new Marker3D { Name = "SupportGrip", Position = _supportGrip });
        _leftHandTarget = new Marker3D { Name = "LeftHandTarget", Position = _supportGrip };
        Root.AddChild(_leftHandTarget);
        Update(0, 0, 0);
    }

    private static Marker3D FindMuzzle(Node3D asset)
    {
        if (asset.GetNodeOrNull<Marker3D>("Muzzle") is { } direct) return direct;
        Node3D? socket = asset.FindChild("Muzzle", true, false) as Node3D
            ?? asset.FindChild("MuzzleSocket", true, false) as Node3D;
        if (socket is Marker3D marker) return marker;
        if (socket != null)
        {
            var muzzle = new Marker3D { Name = "Muzzle" };
            asset.AddChild(muzzle);
            muzzle.GlobalTransform = socket.GlobalTransform;
            return muzzle;
        }
        var fallback = new Marker3D { Name = "Muzzle", Position = new Vector3(0, .159f, -.647f) };
        asset.AddChild(fallback);
        return fallback;
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
        if (IsReloading || IsBusy || !double.IsFinite(duration) || duration <= 0) return false;
        ReloadDuration = duration;
        IsReloading = true;
        ReloadElapsed = 0;
        _recoil = 0;
        return true;
    }

    public void BeginDraw(double duration)
    {
        _drawing = true;
        _holstering = false;
        _drawElapsed = 0;
        _drawDuration = double.IsFinite(duration) ? Math.Max(0, duration) : 0;
        DrawProgress = 0;
        _recoil = 0;
        IsReloading = false;
        ReloadElapsed = 0;
        if (_drawDuration <= 0) { DrawProgress = 1; _drawing = false; }
    }

    public void BeginHolster(double duration)
    {
        _holstering = true;
        _drawing = false;
        _drawElapsed = 0;
        _drawDuration = double.IsFinite(duration) ? Math.Max(0, duration) : 0;
        DrawProgress = 1;
        _recoil = 0;
        IsReloading = false;
        ReloadElapsed = 0;
        if (_drawDuration <= 0) { DrawProgress = 0; _holstering = false; }
    }

    private static float Ease(float time, float from, float to)
    {
        float t = Mathf.Clamp((time - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public void Update(float delta, float walkBlend, float walkPhase)
    {
        _recoil = Mathf.MoveToward(_recoil, 0, delta * 8);
        if (_drawing || _holstering)
        {
            _drawElapsed += delta;
            float t = _drawDuration <= 0 ? 1 : Mathf.Clamp((float)(_drawElapsed / _drawDuration), 0, 1);
            float eased = t * t * (3 - 2 * t);
            if (_drawing)
            {
                DrawProgress = eased;
                if (t >= 1) { DrawProgress = 1; _drawing = false; }
            }
            else
            {
                DrawProgress = 1 - eased;
                if (t >= 1) { DrawProgress = 0; _holstering = false; }
            }
        }
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
        // Reloading lowers the rifle slightly without tilting it: the body keeps a
        // stable, upright silhouette while the hands work the magazine.
        Vector3 holdPosition = _holdPosition + new Vector3(0.015f, -0.035f, -0.015f) * pose;
        Root.Position = (_holdPosition + StowOffset).Lerp(holdPosition, DrawProgress)
            + Vector3.Up * (Mathf.Sin(walkPhase * 2) * 0.006f * walkBlend) + Vector3.Back * (_recoil * .035f);
        Root.Rotation = StowRotation.Slerp(Quaternion.Identity, DrawProgress).GetEuler();

        if (_straightMagazine) UpdateStraightReload(time);
        else UpdateRockReload(time);
    }

    private void UpdateRockReload(float time)
    {
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

        Vector3 magazineHand = _magazine.Transform * _magazineHandOffset;
        Vector3 hand;
        if (time < 0.48f) hand = _supportGrip.Lerp(magazineHand, Ease(time, 0.12f, 0.48f));
        else if (time < 1.87f) hand = magazineHand;
        // No bolt pull: after the magazine seats, the hand returns straight to the handguard.
        else hand = magazineHand.Lerp(_supportGrip, Ease(time, 1.87f, 2.5f));
        _leftHandTarget.Position = hand;
    }

    private void UpdateStraightReload(float time)
    {
        float drop;
        if (time < 0.45f) drop = 0;
        else if (time < 0.95f) drop = Ease(time, 0.45f, 0.95f);
        else if (time < 1.30f) drop = 1;
        else if (time < 1.90f) drop = 1 - Ease(time, 1.30f, 1.90f);
        else drop = 0;
        _magazine.Position = _magazineRest + _magazineDrop * drop;
        _magazine.Rotation = _magazineTilt * drop;

        Vector3 magazineHand = _magazine.Transform * _magazineHandOffset;
        Vector3 hand;
        if (time < 0.10f) hand = _supportGrip;
        else if (time < 0.45f) hand = _supportGrip.Lerp(magazineHand, Ease(time, 0.10f, 0.45f));
        else if (time < 1.90f) hand = magazineHand;
        else if (time < 2.45f) hand = magazineHand.Lerp(_supportGrip, Ease(time, 1.90f, 2.45f));
        else hand = _supportGrip;
        _leftHandTarget.Position = hand;
    }

    public void Kick() => _recoil = 1;

    private sealed record PresentationProfile(
        Vector3 HoldOffset,
        Vector3 RightGrip,
        Vector3 SupportGrip,
        Vector3 MagazinePivot,
        Vector3 MagazineHandOffset,
        bool StraightMagazine,
        Vector3 MagazineDrop,
        Vector3 MagazineTilt)
    {
        public static PresentationProfile For(string scenePath) =>
            scenePath.Contains("FNFAL", StringComparison.OrdinalIgnoreCase) ? Fal : Ak;

        private static readonly PresentationProfile Ak = new(
            Vector3.Zero,
            new Vector3(0.028f, 0.05f, 0.078f),
            new Vector3(-0.045f, 0.12f, -0.29f),
            new Vector3(0f, 0.125f, -0.10f),
            new Vector3(-0.044f, -0.11f, -0.055f),
            false,
            Vector3.Zero,
            Vector3.Zero);

        private static readonly PresentationProfile Fal = new(
            new Vector3(0.061f, 0f, 0.05f),
            new Vector3(-0.006f, 0.05f, 0.021f),
            new Vector3(-0.045f, 0.12f, -0.29f),
            new Vector3(-0.0335f, 0.12f, -0.1575f),
            new Vector3(-0.026f, -0.090f, 0.010f),
            true,
            new Vector3(0f, -0.26f, 0.02f),
            Vector3.Zero);
    }
}
