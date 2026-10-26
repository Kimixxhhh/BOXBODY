using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldKit.Items;

namespace FieldKit.Weapons;

public sealed record WeaponCatalog
{
    public required int SchemaVersion { get; init; }
    public required List<WeaponDefinition> Weapons { get; init; }
    // Optional so version 4 catalogs written before throwables existed still load.
    public List<ThrowableDefinition> Throwables { get; init; } = new();

    public WeaponDefinition Get(string id) => Weapons.FirstOrDefault(w => w.Id == id)
        ?? throw new InvalidDataException($"找不到武器：{id}");

    public ThrowableDefinition GetThrowable(string id) => Throwables.FirstOrDefault(t => t.Id == id)
        ?? throw new InvalidDataException($"找不到投掷物：{id}");

    public void Validate()
    {
        if (SchemaVersion != 4) throw new InvalidDataException("不支持的武器配置版本，需要版本 4（含购买价格与投掷物模板）。");
        if (Weapons == null || Weapons.Count == 0) throw new InvalidDataException("武器配置不能为空。");
        if (Throwables == null) throw new InvalidDataException("投掷物模板列表不能为 null。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var weapon in Weapons)
        {
            if (weapon == null || string.IsNullOrWhiteSpace(weapon.Id) || !ids.Add(weapon.Id))
                throw new InvalidDataException("武器 ID 为空或重复。");
            if (string.IsNullOrWhiteSpace(weapon.Name) || string.IsNullOrWhiteSpace(weapon.Category))
                throw new InvalidDataException($"{weapon.Id} 的名称或分类无效。");
            if (WeaponResources.PathError(weapon.ScenePath) is string sceneError)
                throw new InvalidDataException($"{weapon.Name}：{sceneError}");
            if (WeaponResources.SoundPathError(weapon.GunshotSoundPath, optional: true) is string gunshotError)
                throw new InvalidDataException($"{weapon.Name} 枪声：{gunshotError}");
            if (weapon.ReloadSounds == null) throw new InvalidDataException($"{weapon.Name} 换弹音效列表不能为 null。");
            if (!double.IsFinite(weapon.GunshotVolumePercent) || weapon.GunshotVolumePercent < 0 || weapon.GunshotVolumePercent > 200)
                throw new InvalidDataException($"{weapon.Name} 枪声音量必须在 0–200% 之间。");
            for (int i = 0; i < weapon.ReloadSounds.Count; i++)
            {
                var cue = weapon.ReloadSounds[i];
                if (cue == null) throw new InvalidDataException($"{weapon.Name} 换弹音效 {i + 1} 不能为空。");
                if (!double.IsFinite(cue.VolumePercent) || cue.VolumePercent < 0 || cue.VolumePercent > 200)
                    throw new InvalidDataException($"{weapon.Name} 换弹音效 {i + 1} 的音量必须在 0–200% 之间。");
                if (WeaponResources.SoundPathError(cue.SoundPath) is string soundError)
                    throw new InvalidDataException($"{weapon.Name} 换弹音效 {i + 1}：{soundError}");
                if (!double.IsFinite(cue.DelaySeconds) || cue.DelaySeconds < 0)
                    throw new InvalidDataException($"{weapon.Name} 换弹音效 {i + 1} 的播放延迟必须是大于或等于 0 的秒数。");
            }
            if (weapon.Stats == null) throw new InvalidDataException($"{weapon.Name} 缺少属性。");
            var errors = WeaponFields.Validate(weapon.Stats);
            if (errors.Count > 0) throw new InvalidDataException($"{weapon.Name}：{string.Join(" ", errors)}");
        }
        var throwableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var throwable in Throwables)
        {
            if (throwable == null || string.IsNullOrWhiteSpace(throwable.Id) || !throwableIds.Add(throwable.Id))
                throw new InvalidDataException("投掷物模板 ID 为空或重复。");
            if (string.IsNullOrWhiteSpace(throwable.Name) || string.IsNullOrWhiteSpace(throwable.Category))
                throw new InvalidDataException($"{throwable.Id} 的名称或分类无效。");
            if (WeaponResources.ItemScenePathError(throwable.ItemScenePath) is string itemSceneError)
                throw new InvalidDataException($"{throwable.Name}：{itemSceneError}");
            if (WeaponResources.SoundPathError(throwable.ExplosionSoundPath, optional: true) is string explosionError)
                throw new InvalidDataException($"{throwable.Name} 爆炸音效：{explosionError}");
            var errors = ThrowableFields.Validate(throwable);
            if (errors.Count > 0) throw new InvalidDataException($"{throwable.Name}：{string.Join(" ", errors)}");
        }
    }
}

public sealed class WeaponCatalogStore
{
    public static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter<FireMode>(allowIntegerValues: false),
            new JsonStringEnumConverter<AmmoType>(allowIntegerValues: false) }
    };
    public string FilePath { get; }
    private string? _loadedHash;
    public WeaponCatalogStore(string filePath) => FilePath = Path.GetFullPath(filePath);

    public static WeaponCatalog Parse(string json)
    {
        var result = JsonSerializer.Deserialize<WeaponCatalog>(json.TrimStart('\uFEFF'), JsonOptions)
            ?? throw new InvalidDataException("武器配置内容为空。");
        result.Validate();
        return result;
    }

    public WeaponCatalog Load()
    {
        byte[] bytes = File.ReadAllBytes(FilePath);
        WeaponCatalog catalog = Parse(System.Text.Encoding.UTF8.GetString(bytes));
        _loadedHash = Convert.ToHexString(SHA256.HashData(bytes));
        return catalog;
    }

    public void Save(WeaponCatalog catalog)
    {
        catalog.Validate();
        if (_loadedHash == null) throw new InvalidOperationException("请先读取配置再保存。");
        // Serialize before touching disk. Lock out other tool instances for the full
        // compare-and-replace transaction, and keep the previous complete file.
        string lockPath = FilePath + ".lock";
        using var saveLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (!File.Exists(FilePath) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FilePath))) != _loadedHash)
            throw new IOException("配置已被其他窗口或程序修改。请重新载入后再编辑，避免覆盖他人的修改。");
        string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, JsonOptions);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Replace(temp, FilePath, FilePath + ".bak", true);
            _loadedHash = Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
