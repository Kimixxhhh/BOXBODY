using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FieldKit.Weapons;

namespace WeaponWorkbench;

internal static partial class WorkbenchTests
{
    private static async Task VerifyAudioEditor(MainWindow window, string project, string artifacts, Action<bool, string> check)
    {
        string filename = "weapon-audio-test-" + Guid.NewGuid().ToString("N") + ".wav";
        string fixture = Path.Combine(artifacts, filename);
        string sound = "res://artifacts/" + filename;
        try
        {
            // A temporary, valid silent WAV. It is never assigned to the real game catalog.
            using (var writer = new BinaryWriter(File.Create(fixture)))
            {
                writer.Write("RIFF"u8); writer.Write(8036); writer.Write("WAVEfmt "u8); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(8000);
                writer.Write((short)1); writer.Write((short)8); writer.Write("data"u8); writer.Write(8000);
                writer.Write(Enumerable.Repeat((byte)128, 8000).ToArray());
            }
            window.LoadCatalog("ak47");
            check(window.ReloadSoundRows.Count == 0 && window.GunshotInput.Text == "" && !window.IsDirty,
                "Empty sound configuration opens without creating changes");
            foreach (string bad in new[] { "res://missing.wav", "res://../outside.wav", "C:/outside.wav", "res://data/weapons.json" })
            {
                window.GunshotInput.Text = bad;
                check(!window.CanSave && !window.SaveAll(), "Reject invalid gunshot resource: " + bad);
            }
            window.GunshotInput.Text = sound;
            check(window.CanSave && window.IsDirty, "Gunshot-only edit enables saving");
            window.AddReloadSound();
            check(!window.CanSave, "New reload cue requires an existing audio file");
            window.ReloadSoundRows[0].Path.Text = sound;
            check(window.CanSave, "Zero-delay reload sound is accepted");
            foreach (string bad in new[] { "-0.1", "NaN", "Infinity", "1e9999", "", "abc" })
            {
                window.ReloadSoundRows[0].Delay.Text = bad;
                check(!window.CanSave && !window.SaveAll(), "Reject invalid reload delay: " + bad);
            }
            window.ReloadSoundRows[0].Delay.Text = "1.25";
            window.AddReloadSound(sound, "0");
            check(window.Current!.Saved.ReloadSounds.Count == 0 && window.Current.ReloadSounds.Count == 2,
                "Reload sound drafts do not mutate the last saved configuration");
            window.SelectById("ak47");
            check(window.ReloadSoundRows.Count == 2 && window.ReloadSoundRows[0].Delay.Text == "1.25" && window.GunshotInput.Text == sound,
                "Reselecting a weapon retains all audio drafts");
            check(window.SaveAll() && !window.IsDirty, "Sound-only changes save and clear dirty state");
            window.LoadCatalog("ak47");
            var weapon = window.Current!.Saved;
            check(weapon.GunshotSoundPath == sound && weapon.ReloadSounds.Count == 2 &&
                weapon.ReloadSounds[0] == new ReloadSoundCue { SoundPath = sound, DelaySeconds = 1.25 } && weapon.ReloadSounds[1].DelaySeconds == 0,
                "Gunshot, reload list order, paths and individual delays survive disk reload");
            window.ReloadSoundRows[0].Delay.Text = "0.75";
            window.GunshotInput.Text = "";
            window.ReloadSoundRows[1].Remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.ReloadSoundRows.Count == 1 && window.IsDirty && window.CanSave, "Delete cue, edit delay and clear gunshot are tracked");
            window.ResetCurrent();
            check(window.ReloadSoundRows.Count == 2 && window.ReloadSoundRows[0].Delay.Text == "1.25" && window.GunshotInput.Text == sound && !window.IsDirty,
                "Revert restores the complete saved audio configuration");

            var catalog = new WeaponCatalog { SchemaVersion = 4, Weapons = new() { weapon } };
            void Reject(WeaponDefinition invalid, string message)
            {
                bool rejected = false;
                try { (catalog with { Weapons = new() { invalid } }).Validate(); } catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(weapon with { GunshotSoundPath = "res://outside.txt" }, "Game catalog rejects unsupported gunshot formats");
            Reject(weapon with { ReloadSounds = null! }, "Game catalog rejects a null reload list");
            Reject(weapon with { ReloadSounds = new() { null! } }, "Game catalog rejects a null reload cue");
            foreach (double bad in new[] { -1, double.NaN, double.PositiveInfinity })
                Reject(weapon with { ReloadSounds = new() { new ReloadSoundCue { SoundPath = sound, DelaySeconds = bad } } }, "Game catalog rejects invalid delay: " + bad);
            Reject(weapon with { ReloadSounds = new() { new ReloadSoundCue { SoundPath = "res://../outside.wav", DelaySeconds = 0 } } },
                "Game catalog rejects reload resource path traversal");
            var json = JsonNode.Parse(JsonSerializer.Serialize(catalog, WeaponCatalogStore.JsonOptions))!;
            var legacy = json["weapons"]![0]!.AsObject(); legacy.Remove("gunshotSoundPath"); legacy.Remove("reloadSounds");
            var old = WeaponCatalogStore.Parse(json.ToJsonString()).Get("ak47");
            check(old.GunshotSoundPath == "" && old.ReloadSounds.Count == 0, "Existing version 4 weapons without audio fields remain compatible");
            VerifySoundSequence(check);

            window.AudioCard.BringIntoView();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Capture(window, Path.Combine(artifacts, "weapon-workbench-audio.png"));
            window.Width = 1040; window.Height = 700;
            window.ReloadSoundRows[1].Path.BringIntoView();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Capture(window, Path.Combine(artifacts, "weapon-workbench-audio-small.png"));

            window.GunshotInput.Text = "";
            while (window.ReloadSoundRows.Count > 0) window.ReloadSoundRows[0].Remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.SaveAll(), "Removing all sound assignments saves successfully");
            window.LoadCatalog("ak47");
            check(window.Current!.Saved.GunshotSoundPath == "" && window.Current.Saved.ReloadSounds.Count == 0 && !window.IsDirty,
                "Cleared audio configuration persists across reloads");
        }
        finally { if (File.Exists(fixture)) File.Delete(fixture); }
    }

    private static void VerifySoundSequence(Action<bool, string> check)
    {
        var played = new List<string>();
        var timeline = new ReloadSoundSequence(played.Add);
        var cues = new List<ReloadSoundCue> {
            new() { SoundPath = "late", DelaySeconds = 2 }, new() { SoundPath = "immediate", DelaySeconds = 0 },
            new() { SoundPath = "middle-a", DelaySeconds = .5 }, new() { SoundPath = "middle-b", DelaySeconds = .5 } };
        timeline.Start(cues);
        check(played.SequenceEqual(new[] { "immediate" }), "Zero-delay sound plays exactly at reload start");
        cues.Clear(); timeline.Advance(.49);
        check(played.Count == 1, "Delays are not triggered early and the active timeline owns a snapshot");
        timeline.Advance(.02);
        check(played.SequenceEqual(new[] { "immediate", "middle-a", "middle-b" }), "Same-delay cues both play when a frame crosses their time");
        timeline.Advance(5); timeline.Advance(5);
        check(played.SequenceEqual(new[] { "immediate", "middle-a", "middle-b", "late" }), "Cues play in chronological order exactly once despite large frames");
        timeline.Start(new[] { new ReloadSoundCue { SoundPath = "cancelled", DelaySeconds = 1 } });
        timeline.Advance(.5); timeline.Cancel(); timeline.Advance(100);
        check(played.Count == 4, "Cancelling discards every pending reload sound");
        timeline.Start(new[] { new ReloadSoundCue { SoundPath = "fresh", DelaySeconds = .25 } });
        timeline.Advance(double.NaN); timeline.Advance(-1); timeline.Advance(double.PositiveInfinity); timeline.Advance(.24);
        check(played.Count == 4, "New reload starts its own clock and ignores invalid frame durations");
        timeline.Advance(.02);
        check(played[^1] == "fresh", "Restarted timeline plays cues relative to the new reload");
        timeline.Start(Array.Empty<ReloadSoundCue>()); timeline.Advance(10);
        check(played.Count == 5, "Empty reload lists are silent");
    }
}
