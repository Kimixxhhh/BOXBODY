using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FieldKit.Weapons;
using FieldKit.Items;

namespace WeaponWorkbench;

internal static partial class WorkbenchTests
{
    public static int Run(Application app, string project)
    {
        string artifacts = Path.Combine(project, "artifacts");
        Directory.CreateDirectory(artifacts);
        string temp = Path.Combine(Path.GetTempPath(), "FieldKitWorkbenchTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string path = Path.Combine(temp, "weapons.json");
        // Use a controlled temporary fixture so user tuning and newly added weapons
        // do not invalidate the editor's baseline checks. The real catalog is read-only.
        var source = new WeaponCatalogStore(Path.Combine(project, "data", "weapons.json")).Load();
        var sourceWeapon = source.Get("ak47");
        var fixture = source with {
            Weapons = new() { sourceWeapon with {
                Stats = sourceWeapon.Stats with { ReloadDuration = WeaponStats.DefaultReloadDuration } } },
            // Keep the throwable baseline independent of the designer's current
            // gameplay tuning in the real catalog, just like the weapon fixture.
            Throwables = new() {
                new ThrowableDefinition {
                    Id = "grenade", Name = "手雷", Category = "投掷物",
                    Price = 600, SlotCapacity = 3, Damage = 120, Range = 4.5, EnemySlowPercent = 50,
                    SlowDuration = 3, KnockbackSpeed = 8, FuseTime = 2.5,
                    ItemScenePath = "res://assets/weapons/throwables/grenade.glb",
                    ExplosionSoundPath = "res://assets/sfx/weapons/explosion/explosion_grenade_1.wav"
                }
            }
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(fixture, WeaponCatalogStore.JsonOptions));
        var log = new List<string>();
        int exit = 0;
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new MainWindow(path, project);
        var original = new WeaponCatalogStore(path).Load().Get("ak47");
        void Check(bool success, string description)
        {
            if (!success) throw new InvalidOperationException(description);
            log.Add("PASS: " + description);
        }
        window.Loaded += async (_, _) => {
            try
            {
                Check(window.Drafts.Count == 1 && window.Drafts[0].Saved.Id == "ak47" && window.Inputs.Count == 16 && window.ResourceInputs.Count == 1,
                    "Only AK47 remains, with scene configuration and no icon field");
                foreach (var draft in window.Drafts)
                    Check(File.Exists(WeaponResources.Resolve(project, draft.Saved.ScenePath)), $"Scene resolves: {draft.Saved.Id}");
                await window.ModelPreview.Pending;
                Check(window.ModelPreview.SurfaceCount > 0 && window.ModelPreview.LoadedScenePath == original.ScenePath,
                    "Native 3D viewport loads geometry from the configured scene: " + window.ModelPreview.Status);
                Check(!window.IsDirty && !window.CanSave, "Opening editor does not modify configuration");
                Check(window.Inputs["reloadDuration"].Text == "2.65", "Reload duration defaults to the original 2.65 seconds");
                Check(window.Inputs["equipDuration"].Text == "0.35" && window.Inputs["holsterDuration"].Text == "0.25",
                    "Draw and holster timings default to 0.35 and 0.25 seconds");
                foreach (string field in new[] { "reloadDuration", "equipDuration", "holsterDuration" })
                {
                    foreach (string bad in new[] { "0", "-1", "0.0001", "3601", "NaN", "Infinity", "" })
                    {
                        window.Inputs[field].Text = bad;
                        Check(!window.CanSave && !window.SaveAll(), $"Invalid {field} is rejected: {bad}");
                    }
                    window.Inputs[field].Text = field == "reloadDuration" ? "2.65" : field == "equipDuration" ? "0.35" : "0.25";
                }
                window.Inputs["reloadDuration"].Text = "1.325";
                Check(window.IsDirty && window.CanSave, "A reload-duration-only edit enables saving");
                window.ResetCurrent();
                Check(!window.IsDirty && window.Inputs["reloadDuration"].Text == "2.65", "Revert restores the saved reload duration");
                window.Inputs["reloadDuration"].Text = "1.325";
                window.Inputs["equipDuration"].Text = "0.45";
                window.Inputs["holsterDuration"].Text = "0.3";
                Check(window.AmmoTypeInput.SelectedValue is AmmoType.Medium, "AK47 uses medium ammunition in the editor");
                window.AmmoTypeInput.SelectedIndex = -1;
                Check(!window.CanSave && !window.SaveAll(), "Missing ammunition selection blocks saving");
                window.AmmoTypeInput.SelectedValue = AmmoType.Medium;
                window.Inputs["damage"].Text = "45.25";
                foreach (string bad in new[] { "-1", "2.5", "1000001", "NaN", "" })
                {
                    window.Inputs["price"].Text = bad;
                    Check(!window.CanSave && !window.SaveAll(), "Invalid price is rejected: " + bad);
                }
                window.Inputs["price"].Text = "0";
                Check(window.CanSave, "Zero price is supported for free weapons");
                window.Inputs["price"].Text = "1750";
                window.Inputs["enemySlowPercent"].Text = "25";
                window.Inputs["knockbackSpeed"].Text = "3.5";
                window.Inputs["fireCooldown"].Text = "0.2";
                window.Inputs["magazineCapacity"].Text = "42";
                window.Inputs["movementInertiaPercent"].Text = "20";
                window.Inputs["turnFollowSpeed"].Text = "12.5";
                window.Inputs["projectileSpeed"].Text = "150";
                window.Inputs["spreadMin"].Text = "2";
                window.Inputs["spreadMax"].Text = "10";
                window.Inputs["spreadPerShot"].Text = "1.5";
                window.Inputs["spreadRecovery"].Text = "3";
                window.SetMode(FireMode.SemiAuto);
                Check(window.IsDirty && window.CanSave, "Valid form edits enable save");
                window.SelectById("ak47");
                Check(window.Inputs["damage"].Text == "45.25" && window.Inputs["reloadDuration"].Text == "1.325", "Selecting AK47 preserves its draft including reload duration");
                window.Inputs["spreadMin"].Text = "11";
                Check(!window.CanSave && !window.SaveAll(), "Min spread exceeding max blocks save");
                window.SelectById("ak47");
                window.Inputs["spreadMin"].Text = "2";
                foreach (var field in new[] { "fireCooldown", "projectileSpeed", "magazineCapacity" })
                {
                    string original = window.Inputs[field].Text;
                    window.Inputs[field].Text = "0"; Check(!window.CanSave, $"Reject zero {field}");
                    window.Inputs[field].Text = original;
                }
                window.Inputs["magazineCapacity"].Text = "2.5";
                Check(!window.CanSave, "Magazine capacity must be an integer");
                window.Inputs["magazineCapacity"].Text = "42";
                foreach (string bad in new[] { "", "NaN", "Infinity", "-1", "abc", "1e9999" })
                {
                    window.Inputs["damage"].Text = bad;
                    Check(!window.CanSave, $"Reject invalid numeric input: {bad}");
                }
                window.Inputs["damage"].Text = "45.25";
                window.Inputs["enemySlowPercent"].Text = "101";
                Check(!window.CanSave, "Enemy slow is limited to 100 percent");
                window.Inputs["enemySlowPercent"].Text = "25";
                foreach (string bad in new[] { "-1", "NaN", "1001" })
                {
                    window.Inputs["turnFollowSpeed"].Text = bad;
                    Check(!window.CanSave, "Invalid turning coefficient is rejected: " + bad);
                }
                window.Inputs["turnFollowSpeed"].Text = "0";
                Check(window.CanSave, "Zero turning coefficient is valid");
                window.Inputs["turnFollowSpeed"].Text = "12.5";
                foreach (string bad in new[] { "", "res://scenes/missing.tscn", "res://../AK47.tscn", "C:/outside.glb", "res://data/weapons.json" })
                {
                    window.ResourceInputs["scenePath"].Text = bad;
                    Check(!window.CanSave && !window.SaveAll(), "Invalid scene is rejected: " + bad);
                }
                window.ResourceInputs["scenePath"].Text = "res://assets/weapons/rifle/AK47.glb";
                await window.ModelPreview.Pending;
                Check(window.ModelPreview.LoadedScenePath == "res://assets/weapons/rifle/AK47.glb", "Changing scene refreshes the 3D model");
                Check(window.CanSave, "Existing replacement scene is accepted");
                Check(window.SaveAll(), "Saving all edited weapons succeeds");
                Check(!window.IsDirty && File.Exists(path + ".bak"), "Save clears dirty state and creates backup");
                var store = new WeaponCatalogStore(path);
                var saved = store.Load();
                var stats = saved.Get("ak47").Stats;
                Check(stats.AmmoType == AmmoType.Medium && stats.Price == 1750 && stats.Damage == 45.25 && stats.EnemySlowPercent == 25 && stats.KnockbackSpeed == 3.5 &&
                    stats.FireCooldown == .2 && stats.FireMode == FireMode.SemiAuto && stats.MagazineCapacity == 42 && stats.ReloadDuration == 1.325 &&
                    stats.EquipDuration == .45 && stats.HolsterDuration == .3 &&
                    stats.MovementInertiaPercent == 20 && stats.ProjectileSpeed == 150 && stats.SpreadMin == 2 &&
                    stats.SpreadMax == 10 && stats.SpreadPerShot == 1.5 && stats.SpreadRecovery == 3 && stats.TurnFollowSpeed == 12.5,
                    "All 16 tuning properties including reload, draw and holster timings survive disk round trip");
                Check(saved.Get("ak47").ScenePath == "res://assets/weapons/rifle/AK47.glb" && !File.ReadAllText(path).Contains("iconPath"),
                    "Scene survives disk round trip without any icon field");
                Check(System.Text.Json.JsonSerializer.Serialize(new WeaponCatalogStore(path + ".bak").Load().Get("ak47"), WeaponCatalogStore.JsonOptions) ==
                    System.Text.Json.JsonSerializer.Serialize(original, WeaponCatalogStore.JsonOptions), "Backup contains previous complete AK configuration");
                var turn = stats with { TurnFollowSpeed = 10 };
                Check(Math.Abs(turn.TurnFollowWeight(.1) - (1-Math.Exp(-1))) < .000001, "Turn coefficient multiplies delta in the requested formula");
                Check((turn with { TurnFollowSpeed = 0 }).TurnFollowWeight(.1) == 0 && (turn with { TurnFollowSpeed = 20 }).TurnFollowWeight(.1) > turn.TurnFollowWeight(.1),
                    "Zero disables turning and higher coefficients follow faster");
                Check(Math.Abs(Math.Pow(1-turn.TurnFollowWeight(1.0/30),30) - Math.Pow(1-turn.TurnFollowWeight(1.0/60),60)) < .000001,
                    "Exponential turn response is frame-rate independent");
                Check(Math.Abs(stats.ShotsPerMinute() - 300) < .000001 && stats.MovementSpeedMultiplier() == .8 && stats.EnemySpeedMultiplier() == .75,
                    "Fire rate and movement formulas follow defined units");
                Check(stats.SpreadAfterShot(9) == 10 && stats.RecoverSpread(10,0,.1) == 10,
                    "Spread is capped and does not recover during cooldown");
                Check(Math.Abs(stats.RecoverSpread(10,.1,.2) - 9.7) < .000001 && stats.RecoverSpread(10,1,20) == 2,
                    "Spread recovery respects cooldown boundary and minimum");
                var zeroRecovery = stats with { SpreadRecovery = 0 };
                Check(zeroRecovery.RecoverSpread(10,2,20) == 10, "Zero recovery keeps spread unchanged");
                window.Inputs["damage"].Text = "999";
                window.ResetCurrent();
                Check(window.Inputs["damage"].Text == "45.25" && !window.IsDirty, "Revert restores last saved values");
                var otherStore = new WeaponCatalogStore(path);
                var external = otherStore.Load(); external.Get("ak47").Stats.Damage = 75; otherStore.Save(external);
                window.Inputs["damage"].Text = "80";
                Check(!window.SaveAll() && window.IsDirty && window.Status.Contains("其他窗口"), "External modifications prevent overwrite and retain draft");
                Check(new WeaponCatalogStore(path).Load().Get("ak47").Stats.Damage == 75, "Conflict leaves external data intact");
                window.LoadCatalog("ak47");
                Check(window.Inputs["damage"].Text == "75" && !window.IsDirty, "Reload reads updated disk values");
                window.Inputs["damage"].Text = "76";
                Check(window.SaveAll(), "Repeated saves replace previous backup successfully");
                string good = File.ReadAllText(path);
                Check(WeaponCatalogStore.Parse(good.Replace("\"reloadDuration\": 1.325,", "")).Get("ak47").Stats.ReloadDuration == 2.65,
                    "Older catalogs without reload duration retain the original timing");
                Check(WeaponCatalogStore.Parse(good.Replace("\"equipDuration\": 0.45,", "").Replace("\"holsterDuration\": 0.3,", ""))
                        .Get("ak47").Stats is { EquipDuration: 0.35, HolsterDuration: 0.25 },
                    "Older catalogs without draw/holster timings keep the 0.35/0.25 defaults");
                foreach (string bad in new[] { "0", "-1", "3601", "null" })
                {
                    bool invalidDuration = false;
                    try { WeaponCatalogStore.Parse(good.Replace("\"reloadDuration\": 1.325", "\"reloadDuration\": " + bad)); }
                    catch { invalidDuration = true; }
                    Check(invalidDuration, "Game catalog rejects invalid reload duration: " + bad);
                }
                File.WriteAllText(path, "{ broken");
                bool rejected = false;
                try { new WeaponCatalogStore(path).Load(); } catch { rejected = true; }
                Check(rejected && File.ReadAllText(path) == "{ broken", "Corrupt files are rejected without being overwritten");
                File.WriteAllText(path, good);
                string missing = good.Replace("\"damage\": 76,", "");
                rejected = false;
                try { WeaponCatalogStore.Parse(missing); } catch { rejected = true; }
                Check(rejected, "Missing required fields are rejected");
                rejected = false;
                try { WeaponCatalogStore.Parse(good.Replace("\"price\": 1750,", "")); } catch { rejected = true; }
                Check(rejected, "Missing required price cannot silently become free");
                rejected = false;
                try { WeaponCatalogStore.Parse(good.Replace("\"ammoType\": \"Medium\"", "\"ammoType\": \"Unknown\"")); } catch { rejected = true; }
                Check(rejected, "Unknown ammunition type is rejected");
                var duplicate = store.Load(); duplicate.Weapons.Add(duplicate.Weapons[0]);
                rejected = false;
                try { duplicate.Validate(); } catch { rejected = true; }
                Check(rejected, "Duplicate weapon IDs are rejected");

                await VerifyAudioEditor(window, project, artifacts, Check);
                await VerifyNewWeapons(window, path, artifacts, Check);
                await VerifyThrowables(window, path, artifacts, Check);

                // Render a fresh, unchanged editor view against the actual catalog.
                var preview = new MainWindow(Path.Combine(project, "data", "weapons.json"));
                preview.Show();
                await preview.ModelPreview.Pending;
                await preview.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(preview, Path.Combine(artifacts, "weapon-workbench.png"));
                preview.Inputs["reloadDuration"].BringIntoView();
                await preview.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(preview, Path.Combine(artifacts, "weapon-workbench-reload.png"));
                preview.Inputs["holsterDuration"].BringIntoView();
                await preview.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(preview, Path.Combine(artifacts, "weapon-workbench-switch.png"));
                preview.Width = 1040; preview.Height = 700;
                await preview.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(preview, Path.Combine(artifacts, "weapon-workbench-small.png"));
                preview.Inputs["reloadDuration"].BringIntoView();
                await preview.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(preview, Path.Combine(artifacts, "weapon-workbench-reload-small.png"));
                preview.Inputs["holsterDuration"].BringIntoView();
                await preview.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(preview, Path.Combine(artifacts, "weapon-workbench-switch-small.png"));
                Check(preview.ActualWidth >= 1040 && preview.ActualHeight >= 700, "Editor renders at minimum supported size");
                preview.CloseForTest();
                log.Add($"WORKBENCH PASS: {log.Count} checks");
            }
            catch (Exception error) { exit = 1; log.Add("FAIL: " + error); }
            finally
            {
                File.WriteAllLines(Path.Combine(artifacts, "weapon-workbench-tests.log"), log);
                // Delete only known files created in this unique test directory.
                foreach (string name in new[] { "weapons.json", "weapons.json.bak", "weapons.json.lock" })
                    if (File.Exists(Path.Combine(temp, name))) File.Delete(Path.Combine(temp, name));
                if (!Directory.EnumerateFileSystemEntries(temp).Any()) Directory.Delete(temp);
                window.CloseForTest(); app.Shutdown(exit);
            }
        };
        app.Run(window);
        return exit;
    }

    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
