using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using FieldKit.Weapons;

namespace WeaponWorkbench;

// Interactive, native 3D viewport. Godot supplies mesh data from the selected
// scene; WPF draws the geometry directly, with no thumbnail/icon dependency.
internal sealed class WeaponScenePreview : Grid, IDisposable
{
    private readonly string _project;
    private readonly Viewport3D _viewport = new();
    private readonly TextBlock _status = new() { Text = "正在加载 3D 模型…", TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11, Margin = new Thickness(8) };
    private readonly ModelVisual3D _visual = new();
    private readonly AxisAngleRotation3D _rotation = new(new Vector3D(0,1,0), 0);
    private readonly OrthographicCamera _camera = new();
    private CancellationTokenSource? _cancel;
    private string? _requested;
    private bool _disposed;
    private Point? _drag;
    private double _baseWidth = 1;
    internal string? LoadedScenePath { get; private set; }
    internal int SurfaceCount { get; private set; }
    internal Task Pending { get; private set; } = Task.CompletedTask;
    internal string Status => _status.Text;
    internal string EmptyMessage { get; set; } = "请选择有效的武器场景";

    public WeaponScenePreview(string project)
    {
        _project = project; Height = 128; ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromRgb(240,244,240));
        Cursor = Cursors.Hand;
        ToolTip = "拖动旋转模型，滚轮缩放，双击恢复视角";
        _viewport.Camera = _camera; _viewport.Children.Add(_visual);
        Children.Add(_viewport); Children.Add(_status);
        MouseLeftButtonDown += (_, e) => {
            if (e.ClickCount == 2) { _rotation.Angle = 0; _camera.Width = _baseWidth; }
            else { _drag = e.GetPosition(this); CaptureMouse(); }
        };
        MouseMove += (_, e) => {
            if (_drag is Point last) { Point next = e.GetPosition(this); _rotation.Angle += (next.X-last.X)*.7; _drag = next; }
        };
        MouseLeftButtonUp += (_, _) => { _drag = null; ReleaseMouseCapture(); };
        LostMouseCapture += (_, _) => _drag = null;
        MouseWheel += (_, e) => { _camera.Width = Math.Clamp(_camera.Width * (e.Delta > 0 ? .9 : 1.1),_baseWidth*.3,_baseWidth*3); e.Handled = true; };
    }

    internal void SetScene(string? scene)
    {
        if (_disposed || scene == _requested) return;
        _requested = scene; _cancel?.Cancel();
        LoadedScenePath = null; SurfaceCount = 0; _visual.Content = null;
        _status.Visibility = Visibility.Visible;
        if (scene == null) { _status.Text = EmptyMessage; Pending = Task.CompletedTask; return; }
        _status.Text = "正在加载 3D 模型…";
        _cancel = new CancellationTokenSource();
        Pending = LoadAsync(scene,_cancel.Token);
    }

    private async Task LoadAsync(string scene, CancellationToken token)
    {
        string output = Path.Combine(Path.GetTempPath(),"FieldKitMesh-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            await Task.Delay(250,token);
            string engine = Environment.GetEnvironmentVariable("GODOT_DOTNET") ?? "D:/godotCs/Godot_v4.7.1-stable_mono_win64_console.exe";
            if (!File.Exists(engine)) throw new IOException("3D 预览需要 Godot；请设置 GODOT_DOTNET。参数编辑仍可使用。");
            var start = new ProcessStartInfo(engine) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string arg in new[] { "--headless", "--path", _project, "--script", "res://scripts/tools/ExportWeaponMesh.gd", "--", scene, output }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new IOException("无法启动模型读取器。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using var terminate = timeout.Token.Register(() => {
                try { if (!process.HasExited) process.Kill(entireProcessTree:true); }
                catch (InvalidOperationException) { }
            });
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree:true);
                await process.WaitForExitAsync(); await Task.WhenAll(stdout,stderr);
                if (!token.IsCancellationRequested) throw new IOException("模型读取超时，请检查场景是否已导入。");
                throw;
            }
            await Task.WhenAll(stdout,stderr);
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !File.Exists(output)) throw new IOException("无法读取场景模型。请检查场景为 Node3D，并已在 Godot 中导入。");
            if (new FileInfo(output).Length > 64 * 1024 * 1024) throw new IOException("场景过大，无法预览。");
            var data = JsonSerializer.Deserialize<MeshDocument>(await File.ReadAllTextAsync(output,token),new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            token.ThrowIfCancellationRequested();
            await Dispatcher.InvokeAsync(() => {
                if (_disposed || token.IsCancellationRequested || _requested != scene) return;
                Display(data ?? throw new IOException("场景没有模型数据。"));
                LoadedScenePath = scene; _status.Visibility = Visibility.Collapsed;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!Dispatcher.HasShutdownStarted) await Dispatcher.InvokeAsync(() => {
                if (!_disposed && !token.IsCancellationRequested && _requested == scene) { _status.Text = error.Message; _status.Visibility = Visibility.Visible; }
            });
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }

    private void Display(MeshDocument data)
    {
        var geometry = new Model3DGroup();
        foreach (var surface in data.Surfaces)
        {
            var mesh = new MeshGeometry3D();
            for (int i = 0; i+2 < surface.Positions.Length; i+=3) mesh.Positions.Add(new Point3D(surface.Positions[i],surface.Positions[i+1],surface.Positions[i+2]));
            for (int i = 0; i+2 < surface.Normals.Length; i+=3) mesh.Normals.Add(new Vector3D(surface.Normals[i],surface.Normals[i+1],surface.Normals[i+2]));
            foreach (int i in surface.Indices) mesh.TriangleIndices.Add(i);
            mesh.Freeze();
            var c = surface.Color;
            static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value,0,1)*255);
            var brush = new SolidColorBrush(Color.FromArgb(Channel(c[3]),Channel(c[0]),Channel(c[1]),Channel(c[2]))); brush.Freeze();
            var material = new DiffuseMaterial(brush); material.Freeze();
            geometry.Children.Add(new GeometryModel3D(mesh,material) { BackMaterial = material });
        }
        if (geometry.Children.Count == 0 || geometry.Bounds.IsEmpty) throw new IOException("场景没有可显示的网格。");
        var bounds = geometry.Bounds;
        double span = Math.Max(.1, Math.Max(bounds.SizeX,Math.Max(bounds.SizeY,bounds.SizeZ)));
        var transform = new Transform3DGroup();
        transform.Children.Add(new TranslateTransform3D(-bounds.X-bounds.SizeX/2,-bounds.Y-bounds.SizeY/2,-bounds.Z-bounds.SizeZ/2));
        _rotation.Angle = 0; transform.Children.Add(new RotateTransform3D(_rotation)); geometry.Transform = transform;
        var world = new Model3DGroup();
        world.Children.Add(new AmbientLight(Color.FromRgb(170,170,170)));
        world.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-1,-.8,.3)));
        world.Children.Add(geometry); _visual.Content = world;
        _camera.Position = new Point3D(span*3,span*.25,0);
        _camera.LookDirection = new Vector3D(-span*3,-span*.25,0); _camera.UpDirection = new Vector3D(0,1,0);
        _camera.NearPlaneDistance = .01; _camera.FarPlaneDistance = span*20;
        _baseWidth = Math.Max(span*1.25,bounds.SizeY*1.5*1.8); _camera.Width = _baseWidth;
        SurfaceCount = data.Surfaces.Length;
    }

    public void Dispose() { _disposed = true; _cancel?.Cancel(); }
    private sealed class MeshDocument { public MeshSurface[] Surfaces { get; set; } = []; }
    private sealed class MeshSurface
    {
        public double[] Positions { get; set; } = [];
        public double[] Normals { get; set; } = [];
        public int[] Indices { get; set; } = [];
        public double[] Color { get; set; } = [.55,.58,.56,1];
    }
}
