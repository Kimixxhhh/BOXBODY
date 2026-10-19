using Godot;
using System;
using System.Collections.Generic;
using FieldKit.Weapons;

// Separate voices allow overlapping cues without cutting off the previous sound.
public partial class WeaponAudioPlayer : Node
{
    private readonly Dictionary<string, AudioStream?> _streams = new(StringComparer.Ordinal);
    private readonly List<AudioStreamPlayer> _reloadVoices = new();
    private readonly List<AudioStreamPlayer> _gunshotVoices = new();
    private readonly ReloadSoundSequence _reload;
    private WeaponDefinition? _weapon;
    internal event Action<string>? SoundStarted;

    public WeaponAudioPlayer() => _reload = new ReloadSoundSequence(path => Play(path, _reloadVoices));

    public void Equip(WeaponDefinition weapon)
    {
        CancelReload();
        StopVoices(_gunshotVoices);
        _weapon = weapon;
        // Warm the cache when equipping, so disk reads do not delay individual cues.
        Load(weapon.GunshotSoundPath);
        foreach (var cue in weapon.ReloadSounds) Load(cue.SoundPath);
    }

    public void StartReload()
    {
        CancelReload();
        if (_weapon != null) _reload.Start(_weapon.ReloadSounds);
    }

    public void Advance(double delta) => _reload.Advance(delta);

    // Called by the firing system when a shot succeeds; no firing system exists yet.
    public void PlayGunshot()
    {
        if (_weapon != null) Play(_weapon.GunshotSoundPath, _gunshotVoices);
    }

    public void CancelReload()
    {
        _reload.Cancel();
        StopVoices(_reloadVoices);
    }

    public override void _ExitTree()
    {
        CancelReload();
        StopVoices(_gunshotVoices);
        foreach (var stream in _streams.Values) stream?.Dispose();
        _streams.Clear();
    }

    private static void StopVoices(List<AudioStreamPlayer> voices)
    {
        foreach (var voice in voices) { voice.Stop(); voice.QueueFree(); }
        voices.Clear();
    }

    private void Play(string path, List<AudioStreamPlayer> voices)
    {
        if (Load(path) is not { } stream) return;
        var voice = new AudioStreamPlayer { Stream = stream };
        AddChild(voice);
        voices.Add(voice);
        voice.Finished += () => { voices.Remove(voice); voice.QueueFree(); };
        voice.Play();
        SoundStarted?.Invoke(path);
    }

    private AudioStream? Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_streams.TryGetValue(path, out var cached)) return cached;
        AudioStream? stream = null;
        try
        {
            if (ResourceLoader.Exists(path)) stream = GD.Load<AudioStream>(path);
        }
        catch (Exception error)
        {
            GD.PushWarning($"读取武器音效失败：{path}（{error.Message}）");
            _streams[path] = null;
            return null;
        }
        if (stream != null)
        {
            // Weapon effects are one-shots even if the source was imported with looping enabled.
            stream = (AudioStream)stream.Duplicate();
            switch (stream)
            {
                case AudioStreamWav wav: wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled; break;
                case AudioStreamOggVorbis ogg: ogg.Loop = false; break;
                case AudioStreamMP3 mp3: mp3.Loop = false; break;
            }
        }
        else GD.PushWarning($"武器音效无法加载：{path}。请检查资源文件并在 Godot 中导入。");
        _streams[path] = stream;
        return stream;
    }
}
