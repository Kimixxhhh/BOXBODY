using System;
using System.IO;
using System.Linq;

namespace FieldKit.Weapons;

public static class WeaponResources
{
    public static string? PathError(string? path) => ResourcePathError(path, "武器场景", new[] { ".tscn", ".scn", ".glb", ".gltf" });

    public static string? SoundPathError(string? path, bool optional = false) => optional && path == "" ? null
        : ResourcePathError(path, "音效", new[] { ".wav", ".ogg", ".mp3" });

    private static string? ResourcePathError(string? path, string label, string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("res://", StringComparison.Ordinal)) return label + "需要工程内的 res:// 路径。";
        string relative = path[6..];
        if (relative.Length == 0 || relative.Contains('\\') || relative.Contains(':') || relative.Split('/').Any(p => p is "" or "." or "..") || relative.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return label + "路径无效。";
        return extensions.Contains(Path.GetExtension(path).ToLowerInvariant()) ? null : label + "文件格式不支持。";
    }

    public static string Resolve(string project, string resource) => Path.GetFullPath(Path.Combine(project, resource[6..].Replace('/', Path.DirectorySeparatorChar)));

    public static string? FileError(string project, string path)
    {
        string? error = PathError(path);
        if (error != null) return error;
        try { return File.Exists(Resolve(project, path)) ? null : "武器场景文件不存在。"; }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return "无法访问资源文件。"; }
    }

    public static string? SoundFileError(string project, string path, bool optional = false)
    {
        string? error = SoundPathError(path, optional);
        if (error != null || (optional && path == "")) return error;
        try { return File.Exists(Resolve(project, path)) ? null : "音效文件不存在。"; }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return "无法访问音效文件。"; }
    }
}
