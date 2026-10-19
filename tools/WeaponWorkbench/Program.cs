using System.IO;
using System.Windows;

namespace WeaponWorkbench;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            string project = FindProject(args);
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            if (args.Contains("--self-test")) return WorkbenchTests.Run(app, project);
            return app.Run(new MainWindow(Path.Combine(project, "data", "weapons.json")));
        }
        catch (Exception error)
        {
            if (args.Contains("--self-test"))
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-failure.txt"), error.ToString());
            else MessageBox.Show(error.Message, "武器维护工具无法启动", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    private static string FindProject(string[] args)
    {
        int index = Array.IndexOf(args, "--project");
        if (index >= 0 && index + 1 < args.Length)
        {
            string path = Path.GetFullPath(args[index + 1]);
            if (!File.Exists(Path.Combine(path, "project.godot"))) throw new IOException("指定目录不是游戏工程。");
            return path;
        }
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            for (DirectoryInfo? directory = new(start); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "project.godot"))) return directory.FullName;
        throw new IOException("未找到游戏工程。请通过 RunWeaponEditor.cmd 启动。");
    }
}
