using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace WeaponWorkbench;

public sealed class ReloadSoundDraft
{
    public string SoundPath { get; set; } = "";
    public string Delay { get; set; } = "0";
}

internal sealed record ReloadSoundRow(TextBox Path, TextBox Delay, Button Remove, TextBlock Message);

public sealed partial class MainWindow
{
    internal readonly TextBox GunshotInput = new() { FontSize = 12, Height = 34 };
    internal readonly List<ReloadSoundRow> ReloadSoundRows = new();
    internal Border AudioCard { get; private set; } = null!;
    private readonly StackPanel _reloadSoundRows = new();
    private readonly TextBlock _gunshotMessage = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,18) };

    private void BuildAudioCard()
    {
        var content = new StackPanel();
        AudioCard = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Margin = new Thickness(0,0,0,16), Child = content };
        _editor.Children.Add(AudioCard);
        var heading = Label("05   武器音效", 16, Ink, FontWeights.SemiBold);
        heading.Margin = new Thickness(0,0,0,17); content.Children.Add(heading);
        content.Children.Add(Label("枪声", 12, Ink, FontWeights.SemiBold));
        var gunshotRow = new DockPanel { Margin = new Thickness(0,6,0,0) };
        var clear = SmallButton("清空"); clear.Click += (_, _) => GunshotInput.Text = "";
        DockPanel.SetDock(clear, Dock.Right); gunshotRow.Children.Add(clear);
        AddAudioBrowseButton(gunshotRow, GunshotInput);
        gunshotRow.Children.Add(GunshotInput); content.Children.Add(gunshotRow); content.Children.Add(_gunshotMessage);
        GunshotInput.TextChanged += (_, _) => {
            if (_binding || Current == null) return;
            Current.GunshotSoundPath = GunshotInput.Text.Trim(); RefreshState();
        };

        var reloadHeader = new DockPanel();
        var add = SmallButton("＋ 添加音效"); add.Click += (_, _) => AddReloadSound();
        DockPanel.SetDock(add, Dock.Right); reloadHeader.Children.Add(add);
        var reloadTitle = Label("换弹音效列表", 12, Ink, FontWeights.SemiBold);
        reloadTitle.VerticalAlignment = VerticalAlignment.Center; reloadHeader.Children.Add(reloadTitle); content.Children.Add(reloadHeader);
        var help = Label("延迟从换弹开始计算，单位为秒；0 表示立即播放。相同延迟可同时播放，切换武器会取消换弹音效。", 11, Muted);
        help.TextWrapping = TextWrapping.Wrap; help.Margin = new Thickness(0,8,0,10); help.LineHeight = 20;
        content.Children.Add(help); content.Children.Add(_reloadSoundRows);
    }

    private static Button SmallButton(string text) => new() {
        Content = text, Padding = new Thickness(12,7,12,7), Margin = new Thickness(8,0,0,0), MinHeight = 34 };

    private void AddAudioBrowseButton(DockPanel row, TextBox input)
    {
        var browse = SmallButton("选择文件");
        DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
        browse.Click += (_, _) => {
            var dialog = new OpenFileDialog { InitialDirectory = _project, CheckFileExists = true, Filter = "音效文件|*.wav;*.ogg;*.mp3" };
            if (dialog.ShowDialog(this) != true) return;
            string relative = Path.GetRelativePath(_project, dialog.FileName).Replace('\\','/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            { SetStatus("请选择游戏工程内的音效文件。外部音效请先放入工程目录。", true); return; }
            input.Text = "res://" + relative;
        };
    }

    internal void AddReloadSound(string path = "", string delay = "0")
    {
        if (Current == null) return;
        Current.ReloadSounds.Add(new ReloadSoundDraft { SoundPath = path, Delay = delay });
        RebuildReloadSoundRows(); RefreshState();
        ReloadSoundRows[^1].Path.BringIntoView(); ReloadSoundRows[^1].Path.Focus();
    }

    private void RebuildReloadSoundRows()
    {
        _reloadSoundRows.Children.Clear(); ReloadSoundRows.Clear();
        if (Current == null) return;
        if (Current.ReloadSounds.Count == 0)
        {
            _reloadSoundRows.Children.Add(Label("尚未添加换弹音效。点击“添加音效”设置文件和播放时间。", 11, Muted));
            return;
        }
        foreach (var cue in Current.ReloadSounds)
        {
            var body = new StackPanel();
            _reloadSoundRows.Children.Add(new Border { Background = Surface, CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12), Margin = new Thickness(0,0,0,8), Child = body });
            var header = new DockPanel { Margin = new Thickness(0,0,0,6) };
            var remove = SmallButton("删除"); remove.Padding = new Thickness(10,3,10,3); remove.MinHeight = 26;
            DockPanel.SetDock(remove, Dock.Right); header.Children.Add(remove);
            var label = Label($"音效 {ReloadSoundRows.Count + 1}", 12, Ink, FontWeights.SemiBold);
            label.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(label); body.Children.Add(header);
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); body.Children.Add(grid);
            var pathPanel = new StackPanel(); pathPanel.Children.Add(Label("音效文件", 10, Muted)); grid.Children.Add(pathPanel);
            var row = new DockPanel { Margin = new Thickness(0,5,0,0) };
            var path = new TextBox { Text = cue.SoundPath, FontSize = 12, Height = 34 };
            AddAudioBrowseButton(row, path); row.Children.Add(path); pathPanel.Children.Add(row);
            var delayPanel = new StackPanel { Margin = new Thickness(14,0,0,0) };
            Grid.SetColumn(delayPanel, 1); grid.Children.Add(delayPanel);
            delayPanel.Children.Add(Label("播放延迟 · 秒", 10, Muted));
            var delay = new TextBox { Text = cue.Delay, Height = 34, Margin = new Thickness(0,5,0,0), ToolTip = "从换弹开始计算，0 为立即播放" };
            delayPanel.Children.Add(delay);
            var message = Label("", 10, Muted); message.TextWrapping = TextWrapping.Wrap; message.Margin = new Thickness(0,5,0,0);
            body.Children.Add(message); ReloadSoundRows.Add(new ReloadSoundRow(path, delay, remove, message));
            path.TextChanged += (_, _) => { if (!_binding) { cue.SoundPath = path.Text.Trim(); RefreshState(); } };
            delay.TextChanged += (_, _) => { if (!_binding) { cue.Delay = delay.Text.Trim(); RefreshState(); } };
            remove.Click += (_, _) => { Current?.ReloadSounds.Remove(cue); RebuildReloadSoundRows(); RefreshState(); };
        }
    }

    private void RefreshAudioState(Dictionary<string, string> errors)
    {
        bool bad = errors.TryGetValue("gunshotSoundPath", out string? error);
        GunshotInput.BorderBrush = bad ? Error : Line;
        _gunshotMessage.Foreground = bad ? Error : Muted;
        _gunshotMessage.Text = bad ? error! : "WAV / OGG / MP3；留空表示不播放枪声。";
        for (int i = 0; i < ReloadSoundRows.Count; i++)
        {
            var row = ReloadSoundRows[i];
            bool badPath = errors.TryGetValue($"reloadSound:{i}", out string? pathError);
            bool badDelay = errors.TryGetValue($"reloadDelay:{i}", out string? delayError);
            row.Path.BorderBrush = badPath ? Error : Line; row.Delay.BorderBrush = badDelay ? Error : Line;
            row.Message.Foreground = badPath || badDelay ? Error : Muted;
            row.Message.Text = badPath || badDelay ? string.Join(" ", new[] { pathError, delayError }.Where(s => s != null)) : "在换弹开始后的指定时刻播放一次。";
        }
    }
}
