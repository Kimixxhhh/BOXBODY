using Godot;
using System;
using System.Collections.Generic;

// Persistent one-shot voices for explosion sounds. Keeping this node alive after
// the grenade frees itself lets the tail of the explosion finish naturally.
public partial class ExplosionAudioPlayer : Node
{
    private readonly Dictionary<string, AudioStream?> _streams = new(StringComparer.Ordinal);
    private readonly List<AudioStreamPlayer3D> _voices = new();
    internal event Action<string>? SoundStarted;

    public void Play(string path, Vector3 position)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (Load(path) is not { } stream) return;
        var voice = new AudioStreamPlayer3D {
            Stream = stream, UnitSize = 9, MaxDistance = 80,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseSquareDistance
        };
        AddChild(voice);
        voice.GlobalPosition = position;
        _voices.Add(voice);
        voice.Finished += () => { _voices.Remove(voice); voice.QueueFree(); };
        voice.Play();
        SoundStarted?.Invoke(path);
    }

    public override void _ExitTree()
    {
        foreach (var voice in _voices) voice.QueueFree();
        _voices.Clear();
        foreach (var stream in _streams.Values) stream?.Dispose();
        _streams.Clear();
    }

    private AudioStream? Load(string path)
    {
        if (_streams.TryGetValue(path, out var cached)) return cached;
        AudioStream? stream = null;
        try
        {
            if (ResourceLoader.Exists(path)) stream = GD.Load<AudioStream>(path);
        }
        catch (Exception error)
        {
            GD.PushWarning($"读取爆炸音效失败：{path}（{error.Message}）");
            _streams[path] = null;
            return null;
        }
        if (stream != null)
        {
            // Explosion effects are one-shots even if the source loops.
            stream = (AudioStream)stream.Duplicate();
            switch (stream)
            {
                case AudioStreamWav wav: wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled; break;
                case AudioStreamOggVorbis ogg: ogg.Loop = false; break;
                case AudioStreamMP3 mp3: mp3.Loop = false; break;
            }
        }
        else GD.PushWarning($"爆炸音效无法加载：{path}。请检查资源文件并在 Godot 中导入。");
        _streams[path] = stream;
        return stream;
    }
}
