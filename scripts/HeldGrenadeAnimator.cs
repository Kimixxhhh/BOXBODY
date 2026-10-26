using Godot;
using System;

// Presentation only: the grenade held in the right hand. CharacterAnimator owns
// the arm pose and the switch between weapon, grenade and weapon again.
public sealed class HeldGrenadeAnimator
{
    private enum Motion { Drawing, Ready, Throwing, Thrown, Stowing, Stowed }
    private const float ReleaseFraction = .55f;
    // Poses are in the character model's local space; -Z is forward. The ready
    // pose keeps the grenade well in front of the torso, and the throw uses a
    // wide raised backswing followed by a long forward/downward follow-through.
    private static readonly Vector3 StowPosition = new(.20f, .55f, .08f);
    private static readonly Vector3 HoldPosition = new(.29f, 1.00f, -.45f);
    private static readonly Vector3 WindupPosition = new(.32f, 1.25f, -.10f);
    private static readonly Vector3 ReleasePosition = new(.11f, .97f, -.72f);
    private static readonly Vector3 FollowPosition = new(.06f, .78f, -.58f);
    private static readonly Quaternion StowRotation = Quaternion.FromEuler(new Vector3(-.55f, .6f, -.8f));
    private static readonly Quaternion HoldRotation = Quaternion.FromEuler(new Vector3(0f, .2f, .05f));
    private static readonly Quaternion WindupRotation = Quaternion.FromEuler(new Vector3(-1.05f, .45f, .55f));
    private static readonly Quaternion ReleaseRotation = Quaternion.FromEuler(new Vector3(1.2f, -.25f, -.35f));
    private static readonly Quaternion FollowRotation = Quaternion.FromEuler(new Vector3(1.55f, -.35f, -.5f));
    private static readonly Vector3 HandGripOffset = new(0f, .025f, 0f);

    public Node3D Root { get; }
    public string ScenePath { get; }
    public bool IsDrawing => _motion == Motion.Drawing;
    public bool IsThrowing => _motion == Motion.Throwing;
    public bool IsStowing => _motion == Motion.Stowing;
    public bool IsReady => _motion == Motion.Ready;
    public bool IsThrowComplete => _motion == Motion.Thrown;
    public bool IsStowComplete => _motion == Motion.Stowed;
    public bool Released { get; private set; }
    public Vector3 ReleaseGlobalPosition { get; private set; }
    public float DrawProgress { get; private set; }
    public float StowProgress { get; private set; }
    public float ArmWeight => IsDrawing ? DrawProgress : IsStowing ? 1 - StowProgress : IsReady || IsThrowing || IsThrowComplete ? 1 : 0;
    public Vector3 RightHandTarget => Root.Position + HandGripOffset;

    private Motion _motion = Motion.Stowed;
    private double _elapsed, _duration;

    public HeldGrenadeAnimator(Node3D model, string scenePath)
    {
        ScenePath = scenePath;
        Root = new Node3D { Name = "HeldGrenade" };
        model.AddChild(Root);
        Root.AddChild(LoadVisual(scenePath));
        Root.Visible = false;
    }

    private static Node3D LoadVisual(string scenePath)
    {
        Node3D visual;
        try
        {
            if (!string.IsNullOrEmpty(scenePath) && ResourceLoader.Exists(scenePath))
            {
                var scene = GD.Load<PackedScene>(scenePath);
                visual = scene?.Instantiate() as Node3D ?? Fallback();
            }
            else visual = Fallback();
        }
        catch (Exception error)
        {
            GD.PushWarning($"无法加载手持投掷物场景 {scenePath}：{error.Message}");
            visual = Fallback();
        }
        // The supplied grenade is only about ten centimetres long; enlarge the
        // presentation copy so it reads clearly in the blocky hand and top-down view.
        visual.Scale = Vector3.One * 2.2f;
        visual.Position = new Vector3(0, -.015f, 0);
        return visual;
    }

    private static MeshInstance3D Fallback() => new() {
        Mesh = new SphereMesh { Radius = .09f, Height = .18f, RadialSegments = 16, Rings = 8 },
        MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.23f, .28f, .21f), Roughness = .7f }
    };

    public void BeginDraw(double duration)
    {
        Released = false;
        DrawProgress = 0;
        _motion = Motion.Drawing;
        _elapsed = 0;
        _duration = double.IsFinite(duration) ? Math.Max(0, duration) : 0;
        Root.Visible = true;
        SetPose(StowPosition, StowRotation);
        if (_duration <= 0)
        {
            DrawProgress = 1;
            SetPose(HoldPosition, HoldRotation);
            _motion = Motion.Ready;
        }
    }

    public bool BeginThrow(double duration)
    {
        if (_motion != Motion.Ready) return false;
        _motion = Motion.Throwing;
        _elapsed = 0;
        _duration = double.IsFinite(duration) ? Math.Max(.01, duration) : .01;
        Released = false;
        return true;
    }

    public bool BeginStow(double duration)
    {
        if (_motion != Motion.Ready) return false;
        _motion = Motion.Stowing;
        _elapsed = 0;
        _duration = double.IsFinite(duration) ? Math.Max(0, duration) : 0;
        StowProgress = 0;
        if (_duration <= 0)
        {
            StowProgress = 1;
            SetPose(StowPosition, StowRotation);
            Root.Visible = false;
            _motion = Motion.Stowed;
        }
        return true;
    }

    public void Update(float delta, float walkBlend, float walkPhase)
    {
        switch (_motion)
        {
            case Motion.Drawing:
                Advance(delta);
                float draw = Mathf.Clamp((float)(_elapsed / _duration), 0, 1);
                DrawProgress = Ease(draw);
                SetPose(StowPosition.Lerp(HoldPosition, DrawProgress),
                    StowRotation.Slerp(HoldRotation, DrawProgress));
                if (draw >= 1) _motion = Motion.Ready;
                break;
            case Motion.Ready:
                SetPose(HoldPosition + Vector3.Up * (Mathf.Sin(walkPhase * 2) * .006f * walkBlend), HoldRotation);
                break;
            case Motion.Throwing:
                Advance(delta);
                float throwT = Mathf.Clamp((float)(_elapsed / _duration), 0, 1);
                if (!Released && throwT < ReleaseFraction)
                {
                    float windup = Ease(throwT / ReleaseFraction);
                    SetPose(HoldPosition.Lerp(WindupPosition, windup), HoldRotation.Slerp(WindupRotation, windup));
                }
                else if (!Released)
                {
                    // Capture the hand at the forward release pose before hiding
                    // the visual; the gameplay projectile starts exactly here.
                    SetPose(ReleasePosition, ReleaseRotation);
                    ReleaseGlobalPosition = Root.GlobalTransform * HandGripOffset;
                    Released = true;
                    Root.Visible = false;
                }
                else
                {
                    float follow = Ease((throwT - ReleaseFraction) / (1 - ReleaseFraction));
                    SetPose(ReleasePosition.Lerp(FollowPosition, follow),
                        ReleaseRotation.Slerp(FollowRotation, follow));
                    Root.Visible = false;
                }
                if (throwT >= 1) _motion = Motion.Thrown;
                break;
            case Motion.Stowing:
                Advance(delta);
                StowProgress = Ease(Mathf.Clamp((float)(_elapsed / _duration), 0, 1));
                SetPose(HoldPosition.Lerp(StowPosition, StowProgress),
                    HoldRotation.Slerp(StowRotation, StowProgress));
                if (StowProgress >= 1)
                {
                    Root.Visible = false;
                    _motion = Motion.Stowed;
                }
                break;
        }
    }

    private void Advance(float delta) => _elapsed += Math.Max(0, delta);

    private void SetPose(Vector3 position, Quaternion rotation)
    {
        Root.Position = position;
        Root.Rotation = rotation.GetEuler();
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }
}
