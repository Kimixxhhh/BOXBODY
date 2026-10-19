using FieldKit.Weapons;
using Godot;

// The game only reads data. Maintenance UI and file writes live in the separate app.
public static class WeaponDatabase
{
    public static WeaponCatalog Load()
    {
        const string path = "res://data/weapons.json";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) throw new System.IO.IOException($"无法读取武器配置 {path}: {FileAccess.GetOpenError()}");
        return WeaponCatalogStore.Parse(file.GetAsText());
    }
}
