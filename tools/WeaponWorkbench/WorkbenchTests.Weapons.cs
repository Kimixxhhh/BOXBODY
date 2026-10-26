using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FieldKit.Weapons;

namespace WeaponWorkbench;

internal static partial class WorkbenchTests
{
    private static async Task VerifyNewWeapons(MainWindow window, string path, string artifacts, Action<bool, string> check)
    {
        window.LoadCatalog("ak47");
        var saved = window.Current!.Saved;
        string original = JsonSerializer.Serialize(saved, WeaponCatalogStore.JsonOptions);
        check(window.IdentityInputs["id"].IsReadOnly, "Saved weapon identifiers are protected from accidental edits");
        window.IdentityInputs["name"].Text = "AK 草稿";
        window.SearchInput.Text = "no-match";
        check(!window.VisibleWeaponIds.Any(), "Search filters the weapon list");
        window.AddWeaponButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var created = window.Current!;
        string id = created.Id;
        check(created.IsNew && window.Drafts.Count == 2 && window.IsDirty && !window.CanSave && window.SearchInput.Text == "" && window.VisibleWeaponIds.Contains(id),
            "Add button selects a visible unsaved weapon and requires a model before saving");
        check(!window.IdentityInputs["id"].IsReadOnly && created.ReloadSounds.Count == 0 && created.GunshotVolume == "100",
            "A new weapon has an editable generated identifier and independent audio defaults");
        check(window.Drafts[0].Name == "AK 草稿" && window.Drafts[0].Saved.Name == saved.Name,
            "Adding a weapon preserves pending edits on the existing weapon");
        window.ResourceInputs["scenePath"].Text = saved.ScenePath;
        check(window.CanSave, "A valid existing model enables saving a new weapon");
        foreach (string key in new[] { "name", "category", "id" })
        {
            string before = window.IdentityInputs[key].Text;
            window.IdentityInputs[key].Text = "   ";
            check(!window.CanSave && !window.SaveAll(), "Blank new-weapon identity is rejected: " + key);
            window.IdentityInputs[key].Text = before;
        }
        foreach (string invalid in new[] { saved.Id, "bad id", "../weapon", "编号" })
        {
            window.IdentityInputs["id"].Text = invalid;
            check(!window.CanSave && !window.SaveAll(), "Invalid or duplicate weapon identifier blocks saving: " + invalid);
        }
        window.IdentityInputs["id"].Text = "test_carbine";
        id = created.Id;
        window.IdentityInputs["name"].Text = "测试卡宾枪";
        window.IdentityInputs["category"].Text = "卡宾枪";
        window.Inputs["damage"].Text = "42.5";
        window.GunshotVolumeInput.Text = "65";
        window.SearchInput.Text = "测试卡宾";
        check(window.VisibleWeaponIds.SequenceEqual(new[] { id }), "Search reflects the new weapon's edited metadata immediately");
        window.SearchInput.Text = "";
        window.AddNewWeapon();
        string secondId = window.Current!.Id;
        check(secondId != id && window.Drafts.Select(d => d.Id).Distinct().Count() == 3, "Multiple unsaved weapons receive distinct identifiers");
        window.IdentityInputs["id"].Text = id;
        window.ResourceInputs["scenePath"].Text = saved.ScenePath;
        check(!window.CanSave && !window.SaveAll(), "Duplicate identifiers between two new drafts cannot be saved");
        window.ResetCurrent();
        check(ReferenceEquals(window.Current, created) && window.Drafts.Count == 2 && window.CanSave, "Cancel new removes only the selected unsaved weapon");
        window.SelectById(saved.Id);
        check(window.IdentityInputs["name"].Text == "AK 草稿", "Switching weapons retains an existing metadata draft");
        window.ResetCurrent();
        window.SelectById(id);
        check(window.IdentityInputs["name"].Text == "测试卡宾枪" && window.Inputs["damage"].Text == "42.5" && window.GunshotVolumeInput.Text == "65",
            "Switching away and back retains all new-weapon fields");
        check(window.SaveAll() && !window.IsDirty && !created.IsNew && window.IdentityInputs["id"].IsReadOnly,
            "Saving commits the new weapon and fixes its identifier");
        var stored = new WeaponCatalogStore(path).Load();
        var weapon = stored.Get(id);
        check(stored.Weapons.Count == 2 && weapon.Name == "测试卡宾枪" && weapon.Category == "卡宾枪" && weapon.ScenePath == saved.ScenePath &&
            weapon.Stats.Damage == 42.5 && weapon.GunshotVolumePercent == 65, "Game catalog loader reads every field of the newly saved weapon");
        check(JsonSerializer.Serialize(stored.Get(saved.Id), WeaponCatalogStore.JsonOptions) == original,
            "Adding a weapon does not alter the existing weapon's saved configuration");
        check(new WeaponCatalogStore(path + ".bak").Load().Weapons.Count == 1, "New-weapon save preserves the previous catalog in its backup");
        window.LoadCatalog(id);
        check(window.Current!.Id == id && window.Drafts.Count == 2 && !window.IsDirty, "Reload restores the new weapon and its selection");
        await window.ModelPreview.Pending;
        check(window.ModelPreview.SurfaceCount > 0, "New weapon model previews from the saved resource");
        window.IdentityInputs["name"].Text = "卡宾枪 改型";
        window.IdentityInputs["category"].Text = "步枪";
        check(window.SaveAll(), "Saved weapons can be renamed and recategorized");
        window.LoadCatalog(id);
        check(window.Current!.Name == "卡宾枪 改型" && window.Current.Category == "步枪", "Metadata edits survive reopening the catalog");

        window.Width = 1390; window.Height = 950;
        await Task.Delay(100); // Let the native window apply its resize before capturing layout.
        window.AddNewWeapon();
        window.IdentityInputs["name"].Text = "轻型步枪";
        window.IdentityInputs["category"].Text = "突击步枪";
        window.ResourceInputs["scenePath"].Text = saved.ScenePath;
        await window.ModelPreview.Pending;
        ((FrameworkElement)window.IdentityCard.Parent).BringIntoView(new Rect(0, 0, 1, 1));
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "weapon-workbench-new.png"));
        window.Width = 1040; window.Height = 700;
        await Task.Delay(100);
        ((FrameworkElement)window.IdentityCard.Parent).BringIntoView(new Rect(0, 0, 1, 1));
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "weapon-workbench-new-small.png"));
        check(Math.Abs(window.ActualWidth - 1040) < 1 && Math.Abs(window.ActualHeight - 700) < 1 &&
            window.IdentityInputs.Values.All(input => input.ActualWidth > 100) && window.AddWeaponButton.ActualWidth > 100,
            "New-weapon controls remain usable at minimum window size");

        var otherStore = new WeaponCatalogStore(path);
        var external = otherStore.Load(); external.Get(id).Stats.Price = 1234; otherStore.Save(external);
        check(!window.SaveAll() && window.Current!.IsNew && window.IsDirty, "A conflicting save preserves the unsaved new weapon for review");
        check(new WeaponCatalogStore(path).Load().Weapons.Count == 2, "A conflict never partially appends a weapon");
        window.LoadCatalog(id);
        check(window.Drafts.Count == 2 && !window.IsDirty && window.Current!.Saved.Stats.Price == 1234,
            "Reload discards uncommitted additions and reads the current catalog");
        window.AddNewWeapon(); window.ResetCurrent();
        check(window.Drafts.Count == 2 && !window.IsDirty, "Cancelling the only new draft returns the editor to a clean state");
    }
}
