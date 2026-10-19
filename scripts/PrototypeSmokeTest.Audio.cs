using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FieldKit.Items;
using FieldKit.Weapons;

public partial class PrototypeSmokeTest
{
    private async Task VerifyWeaponAudio()
    {
        var player = _game.Player;
        var original = _game.Weapons.Get("ak47");
        var paths = new[] { "shot", "start", "magazine", "click", "bolt", "tail" }.Select(name => $"res://audio-test-{name}.wav").ToArray();
        // Register in-memory WAV resources with Godot's real loader, leaving project assets untouched.
        var fixtures = paths.Select(path => {
            var stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format8Bits, MixRate = 8000,
                Data = new byte[16000], LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopEnd = 16000 };
            stream.TakeOverPath(path); return stream;
        }).ToArray();
        var weapon = original with { Stats = original.Stats with { Price = 1000 }, GunshotSoundPath = paths[0],
            ReloadSounds = new() {
                new() { SoundPath = paths[4], DelaySeconds = 1.8 }, new() { SoundPath = paths[1], DelaySeconds = 0 },
                new() { SoundPath = paths[2], DelaySeconds = .5 }, new() { SoundPath = paths[3], DelaySeconds = .5 },
                new() { SoundPath = paths[5], DelaySeconds = 4 } } };
        _game.Weapons.Weapons[0] = weapon;
        var played = new List<string>();
        player.WeaponAudio.SoundStarted += played.Add;
        int Voices() => player.WeaponAudio.GetChildren().OfType<AudioStreamPlayer>().Count(v => !v.IsQueuedForDeletion() && v.Playing);
        Check(!player.TryStartReload(out _) && played.Count == 0, "Unarmed reload starts no audio");
        player.GlobalPosition = _game.Crate.GlobalPosition + new Vector3(-1.7f, .02f, 0);
        player.Velocity = Vector3.Zero;
        await Frames(4); PressKey(Key.E); await Frames(2);
        Check(player.TryPurchaseWeapon(weapon.Id, out _) && player.TryPurchaseWeapon(weapon.Id, out _), "Purchase and equip weapons with audio assignments");
        PressKey(Key.Escape); await Frames(2);
        Check(!player.TryStartReload(out _) && played.Count == 0, "Reload without matching ammunition starts no audio");
        PressKey(Key.E); await Frames(2);
        Check(player.TryPurchaseAmmo(ItemCatalog.MediumAmmo.Id, out _) && player.TryPurchaseAmmo(ItemCatalog.MediumAmmo.Id, out _),
            "Audio test buys ammunition through the real shop");
        Check(!player.TryStartReload(out _) && played.Count == 0, "Blocked reload while the shop is open remains silent");
        PressKey(Key.Escape); await Frames(2);
        Check(player.SelectWeaponSlot(0) && player.TryStartReload(out _) && played.SequenceEqual(new[] { paths[1] }),
            "Successful reload immediately plays the zero-delay cue");
        Check(!player.TryStartReload(out _) && played.Count == 1, "Repeated reload input cannot replay or restart audio");
        await ReloadUntil(.7f);
        Check(played.SequenceEqual(new[] { paths[1], paths[2], paths[3] }) && Voices() == 3,
            "Reload clock plays delayed and simultaneous sounds on independent active voices");
        Check(player.WeaponAudio.GetChildren().OfType<AudioStreamPlayer>().All(v => ((AudioStreamWav)v.Stream).LoopMode == AudioStreamWav.LoopModeEnum.Disabled) &&
            fixtures.All(f => f.LoopMode == AudioStreamWav.LoopModeEnum.Forward), "Effects are one-shots without mutating imported source resources");
        Check(player.SelectWeaponSlot(1) && !player.IsReloading && Voices() == 0, "Switching weapons stops ongoing reload voices immediately");
        player.WeaponAudio.Advance(10); await Frames(2);
        Check(played.Count == 3 && player.Backpack.Available(AmmoType.Medium) == 80, "Cancelled reload has no late sounds or ammunition consumption");
        played.Clear();
        Check(player.TryStartReload(out _) && played.SequenceEqual(new[] { paths[1] }), "Next weapon starts a fresh audio timeline");
        await ReloadUntil(_game.Player.ReloadDuration);
        Check(played.SequenceEqual(new[] { paths[1], paths[2], paths[3], paths[4] }) && player.MagazineAmmo(1) == 30 &&
            player.Backpack.Available(AmmoType.Medium) == 50, "Complete reload plays all due cues and loads the normal ammunition amount");
        player.WeaponAudio.Advance(10); player.WeaponAudio.Advance(10);
        Check(played.Count == 5 && played[^1] == paths[5], "Configured late cue is preserved after the animation and still plays only once");
        Check(!player.TryStartReload(out _) && played.Count == 5, "Full magazine does not retrigger reload audio");
        player.WeaponAudio.PlayGunshot(); player.WeaponAudio.PlayGunshot();
        Check(played.TakeLast(2).All(path => path == paths[0]) && Voices() >= 2, "Gunshot playback uses the assigned sound and allows successive shots to overlap");
        player.WeaponAudio.Equip(original);
        played.Clear(); player.WeaponAudio.PlayGunshot(); player.WeaponAudio.StartReload(); player.WeaponAudio.Advance(10);
        Check(played.Count == 0 && Voices() == 0, "Unassigned sounds are silent and replacing the weapon clears old audio");
        await Frames(4); // Let the audio server release stopped playback handles before shutting down.
        foreach (var fixture in fixtures) fixture.Dispose();
    }
}
