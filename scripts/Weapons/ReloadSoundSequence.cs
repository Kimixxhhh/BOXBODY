using System;
using System.Collections.Generic;
using System.Linq;

namespace FieldKit.Weapons;

// A snapshot prevents later configuration edits from changing an in-flight reload.
public sealed class ReloadSoundSequence
{
    private ReloadSoundCue[] _cues = Array.Empty<ReloadSoundCue>();
    private int _next;
    private double _elapsed;
    private readonly Action<string> _play;

    public ReloadSoundSequence(Action<string> play) => _play = play;

    public void Start(IEnumerable<ReloadSoundCue> cues)
    {
        _cues = cues.OrderBy(cue => cue.DelaySeconds).ToArray();
        _next = 0;
        _elapsed = 0;
        Advance(0);
    }

    public void Advance(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) return;
        _elapsed += delta;
        while (_next < _cues.Length && _cues[_next].DelaySeconds <= _elapsed)
            _play(_cues[_next++].SoundPath);
    }

    public void Cancel()
    {
        _cues = Array.Empty<ReloadSoundCue>();
        _next = 0;
        _elapsed = 0;
    }
}
