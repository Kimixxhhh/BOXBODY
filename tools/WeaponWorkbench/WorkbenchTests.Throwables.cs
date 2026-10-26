using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FieldKit.Weapons;

namespace WeaponWorkbench;

internal static partial class WorkbenchTests
{
    private static async Task VerifyThrowables(MainWindow window, string path, string artifacts, Action<bool, string> check)
    {
        window.SwitchMode(true);
        check(window.ThrowablesMode && window.CurrentThrowable?.Id == "grenade" && window.ThrowableInputs.Count == ThrowableFields.All.Count,
            "Throwable tab opens the grenade template with every typed property");
        var initial = window.CurrentThrowable!;
        check(initial.Saved.Name == "手雷" && initial.Saved.Category == "投掷物" &&
            initial.Saved.ItemScenePath == "res://assets/weapons/throwables/grenade.glb" &&
            initial.Saved.ExplosionSoundPath == "res://assets/sfx/weapons/explosion/explosion_grenade_1.wav",
            "Grenade template retains name, item scene and explosion sound");
        check(window.ThrowableInputs["damage"].Text == "120" && window.ThrowableInputs["range"].Text == "4.5" &&
            window.ThrowableInputs["enemySlowPercent"].Text == "50" && window.ThrowableInputs["slowDuration"].Text == "3" &&
            window.ThrowableInputs["knockbackSpeed"].Text == "8" && window.ThrowableInputs["fuseTime"].Text == "2.5" &&
            window.ThrowableInputs["price"].Text == "600" && window.ThrowableInputs["slotCapacity"].Text == "3",
            "Grenade template starts with the requested damage, range, slow, knockback, fuse, price and slot capacity values");
        var legacy = System.Text.Json.JsonSerializer.Deserialize<ThrowableDefinition>(
            "{\"id\":\"legacy\",\"name\":\"旧手雷\",\"price\":1,\"damage\":1,\"range\":1," +
            "\"enemySlowPercent\":0,\"slowDuration\":0,\"knockbackSpeed\":0,\"fuseTime\":1," +
            "\"itemScenePath\":\"res://assets/weapons/throwables/grenade.glb\"}", WeaponCatalogStore.JsonOptions)!;
        check(legacy.SlotCapacity == 1, "Throwable templates without slotCapacity default to one item per slot");
        check(!window.IsDirty && !window.CanSave, "Opening the throwable template does not create a change");
        await window.ThrowablePreview.Pending;
        check(window.ThrowablePreview.LoadedScenePath == initial.Saved.ItemScenePath && window.ThrowablePreview.SurfaceCount > 0,
            "Throwable item scene renders in its own 3D preview");

        foreach (string key in new[] { "damage", "range", "enemySlowPercent", "slowDuration", "knockbackSpeed", "fuseTime", "price", "slotCapacity" })
        {
            string original = window.ThrowableInputs[key].Text;
            foreach (string bad in new[] { "", "NaN", "Infinity", "-1", "1e9999", "abc" })
            {
                window.ThrowableInputs[key].Text = bad;
                check(!window.CanSave && !window.SaveAll(), $"Invalid throwable {key} is rejected: {bad}");
            }
            window.ThrowableInputs[key].Text = original;
        }
        foreach (string bad in new[] { "-1", "1000001", "2.5" })
        {
            window.ThrowableInputs["price"].Text = bad;
            check(!window.CanSave && !window.SaveAll(), "Throwable price must be a non-negative integer: " + bad);
        }
        window.ThrowableInputs["price"].Text = "0";
        check(window.CanSave, "Free throwable templates are supported");
        window.ThrowableInputs["price"].Text = "600";
        foreach (string bad in new[] { "0", "-1", "1001", "2.5" })
        {
            window.ThrowableInputs["slotCapacity"].Text = bad;
            check(!window.CanSave && !window.SaveAll(), "Throwable slot capacity must be an integer from 1 to 1000: " + bad);
        }
        window.ThrowableInputs["slotCapacity"].Text = "1";
        check(window.CanSave, "A slot capacity of one item is accepted");
        window.ThrowableInputs["slotCapacity"].Text = "1000";
        check(window.CanSave, "The maximum slot capacity is accepted");
        window.ThrowableInputs["slotCapacity"].Text = "3";
        window.ThrowableInputs["range"].Text = "0.1";
        check(window.CanSave, "The minimum positive explosion range is accepted");
        window.ThrowableInputs["range"].Text = "4.5";
        window.ThrowableInputs["slowDuration"].Text = "0";
        window.ThrowableInputs["knockbackSpeed"].Text = "0";
        window.ThrowableInputs["enemySlowPercent"].Text = "100";
        check(window.CanSave, "Zero duration/knockback and full slow are accepted");
        window.ThrowableInputs["slowDuration"].Text = "3";
        window.ThrowableInputs["knockbackSpeed"].Text = "8";
        window.ThrowableInputs["enemySlowPercent"].Text = "50";

        foreach (string bad in new[] { "", "res://scenes/missing.tscn", "res://../grenade.glb", "C:/outside.glb", "res://data/weapons.json" })
        {
            window.ThrowableResourceInputs["itemScenePath"].Text = bad;
            check(!window.CanSave && !window.SaveAll(), "Invalid throwable item scene is rejected: " + bad);
        }
        window.ThrowableResourceInputs["itemScenePath"].Text = "res://assets/weapons/throwables/grenade.glb";
        window.ThrowableInputs["damage"].Text = "121";
        check(window.CanSave, "Existing throwable item scene is accepted");
        foreach (string bad in new[] { "res://missing.wav", "res://../outside.wav", "C:/outside.wav", "res://data/weapons.json" })
        {
            window.ExplosionSoundInput.Text = bad;
            check(!window.CanSave && !window.SaveAll(), "Invalid explosion sound is rejected: " + bad);
        }
        window.ExplosionSoundInput.Text = "";
        check(window.CanSave, "Explosion sound can be cleared to silence");
        window.ExplosionSoundInput.Text = "res://assets/sfx/weapons/explosion/explosion_grenade_1.wav";

        window.ThrowableInputs["damage"].Text = "77.5";
        window.ThrowableInputs["range"].Text = "6.5";
        window.ThrowableInputs["enemySlowPercent"].Text = "42";
        window.ThrowableInputs["slowDuration"].Text = "4.25";
        window.ThrowableInputs["knockbackSpeed"].Text = "9.5";
        window.ThrowableInputs["fuseTime"].Text = "1.75";
        window.ThrowableInputs["price"].Text = "450";
        window.ThrowableInputs["slotCapacity"].Text = "5";
        check(window.IsDirty && window.CanSave, "Valid throwable-only edits enable saving");
        window.SwitchMode(false);
        window.SwitchMode(true);
        check(window.ThrowableInputs["damage"].Text == "77.5" && window.ThrowableInputs["fuseTime"].Text == "1.75" &&
            window.ThrowableInputs["slotCapacity"].Text == "5",
            "Switching between weapon and throwable tabs retains throwable drafts");
        window.SelectThrowableById("grenade");
        check(window.ThrowableInputs["damage"].Text == "77.5" && window.ThrowableInputs["slowDuration"].Text == "4.25" &&
            window.ThrowableInputs["slotCapacity"].Text == "5",
            "Reselecting the throwable preserves its full draft");
        window.ResetCurrentThrowable();
        check(window.ThrowableInputs["damage"].Text == "120" && window.ThrowableInputs["slotCapacity"].Text == "3" &&
            !window.IsDirty, "Revert restores all saved throwable values");

        window.Width = 1390; window.Height = 950;
        window.ScrollThrowableEditorToTop();
        await window.ThrowablePreview.Pending;
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "weapon-workbench-throwables.png"));
        window.ThrowableInputs["fuseTime"].BringIntoView();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "weapon-workbench-throwables-effects.png"));
        window.ExplosionSoundInput.BringIntoView();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "weapon-workbench-throwables-audio.png"));
        window.Width = 1040; window.Height = 700;
        window.ScrollThrowableEditorToTop();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "weapon-workbench-throwables-small.png"));
        check(window.ActualWidth >= 1040 && window.ActualHeight >= 700 && window.ThrowableInputs.Values.All(input => input.ActualWidth > 100),
            "Throwable editor remains usable at the minimum supported window size");
        window.Width = 1390; window.Height = 950;

        window.AddNewThrowable();
        var created = window.CurrentThrowable!;
        check(created.IsNew && window.ThrowableDrafts.Count == 2 && window.IsDirty && !window.CanSave &&
            window.ThrowableInputs["slotCapacity"].Text == "3" &&
            window.ThrowableResourceInputs["itemScenePath"].Text == "",
            "New throwable requires an item scene before saving and starts with three items per slot");
        foreach (string key in new[] { "name", "category" })
        {
            string before = window.ThrowableIdentityInputs[key].Text;
            window.ThrowableIdentityInputs[key].Text = "   ";
            check(!window.CanSave && !window.SaveAll(), "Blank new throwable identity is rejected: " + key);
            window.ThrowableIdentityInputs[key].Text = before;
        }
        foreach (string invalid in new[] { "grenade", "bad id", "../throwable", "编号" })
        {
            window.ThrowableIdentityInputs["id"].Text = invalid;
            check(!window.CanSave && !window.SaveAll(), "Invalid or duplicate throwable identifier blocks saving: " + invalid);
        }
        window.ThrowableIdentityInputs["id"].Text = "test_grenade";
        string id = created.Id;
        window.ThrowableIdentityInputs["name"].Text = "测试手雷";
        window.ThrowableIdentityInputs["category"].Text = "投掷物";
        window.ThrowableResourceInputs["itemScenePath"].Text = "res://assets/weapons/throwables/grenade.glb";
        window.ExplosionSoundInput.Text = "res://assets/sfx/weapons/explosion/explosion_grenade_1.wav";
        window.ThrowableInputs["damage"].Text = "35";
        window.ThrowableInputs["range"].Text = "3.5";
        window.ThrowableInputs["enemySlowPercent"].Text = "25";
        window.ThrowableInputs["slowDuration"].Text = "1.5";
        window.ThrowableInputs["knockbackSpeed"].Text = "2.5";
        window.ThrowableInputs["fuseTime"].Text = "1.25";
        window.ThrowableInputs["price"].Text = "150";
        window.ThrowableInputs["slotCapacity"].Text = "6";
        check(window.CanSave, "Complete new throwable template enables saving");
        check(window.SaveAll() && !window.IsDirty && !created.IsNew, "Saving commits the new throwable template");
        var stored = new WeaponCatalogStore(path).Load();
        var saved = stored.GetThrowable(id);
        check(stored.Throwables.Count == 2 && saved.Name == "测试手雷" && saved.Price == 150 &&
            saved.SlotCapacity == 6 &&
            saved.Damage == 35 && saved.Range == 3.5 && saved.EnemySlowPercent == 25 && saved.SlowDuration == 1.5 &&
            saved.KnockbackSpeed == 2.5 && saved.FuseTime == 1.25 &&
            saved.ItemScenePath == "res://assets/weapons/throwables/grenade.glb" &&
            saved.ExplosionSoundPath == "res://assets/sfx/weapons/explosion/explosion_grenade_1.wav",
            "Every throwable property survives the disk round trip");
        check(new WeaponCatalogStore(path + ".bak").Load().Throwables.Count == 1,
            "Throwable save preserves the previous catalog in its backup");
        check(new WeaponCatalog { SchemaVersion = 4, Weapons = new() { stored.Weapons[0] } }.Throwables.Count == 0,
            "Catalogs without a throwables list still default to an empty template list");
        var catalog = new WeaponCatalog { SchemaVersion = 4, Weapons = new() { stored.Weapons[0] }, Throwables = new() { saved } };
        void Reject(ThrowableDefinition invalid, string message)
        {
            bool rejected = false;
            try { (catalog with { Throwables = new() { invalid } }).Validate(); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, message);
        }
        Reject(saved with { ItemScenePath = "res://outside.txt" }, "Game catalog rejects unsupported item scene formats");
        Reject(saved with { ItemScenePath = "res://../grenade.glb" }, "Game catalog rejects item scene path traversal");
        Reject(saved with { ExplosionSoundPath = "res://outside.txt" }, "Game catalog rejects unsupported explosion sound formats");
        Reject(saved with { Id = "" }, "Game catalog rejects blank throwable identifiers");
        foreach (int bad in new[] { 0, -1, 1001 })
            Reject(saved with { SlotCapacity = bad }, "Game catalog rejects invalid slot capacity: " + bad);
        foreach (double bad in new[] { -1, 0, double.NaN, double.PositiveInfinity })
            Reject(saved with { FuseTime = bad }, "Game catalog rejects invalid fuse time: " + bad);
        foreach (double bad in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Reject(saved with { Range = bad }, "Game catalog rejects invalid explosion range: " + bad);
        bool duplicateRejected = false;
        try { (catalog with { Throwables = new() { saved, saved } }).Validate(); }
        catch (InvalidDataException) { duplicateRejected = true; }
        check(duplicateRejected, "Game catalog rejects duplicate throwable identifiers");
        window.LoadCatalog();
        check(window.ThrowablesMode && window.CurrentThrowable?.Id == id && window.ThrowableDrafts.Count == 2 && !window.IsDirty,
            "Reload restores both throwable templates and the active selection");
        await window.ThrowablePreview.Pending;
        check(window.ThrowablePreview.LoadedScenePath == saved.ItemScenePath && window.ThrowablePreview.SurfaceCount > 0,
            "Reloaded throwable preview renders the configured item scene");

        window.ThrowableDrafts[0].Name = "手雷 改型";
        window.SelectThrowableById(window.ThrowableDrafts[0].Id);
        window.ThrowableIdentityInputs["name"].Text = "手雷 改型";
        check(window.SaveAll(), "Existing throwable templates can be renamed and retuned");
        window.LoadCatalog(window.ThrowableDrafts[0].Id);
        check(window.CurrentThrowable?.Name == "手雷 改型", "Throwable metadata edits persist after reopening");
        await window.ThrowablePreview.Pending;
        window.SwitchMode(false);
        check(!window.ThrowablesMode && window.Current != null, "Switching back restores the weapon editor");
    }
}
