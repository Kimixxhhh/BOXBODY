using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using FieldKit.Weapons;
using FieldKit.Items;

namespace WeaponWorkbench;

public sealed class WeaponDraft
{
    public WeaponDefinition Saved { get; private set; }
    public Dictionary<string, string> Values { get; } = new();
    public FireMode Mode { get; set; }
    public AmmoType AmmoType { get; set; }
    public string ScenePath { get; set; } = "";
    public string GunshotSoundPath { get; set; } = "";
    public List<ReloadSoundDraft> ReloadSounds { get; } = new();
    private readonly string _project;
    public WeaponDraft(WeaponDefinition saved, string project) { Saved = saved; _project = project; Reset(); }
    public void Reset()
    {
        foreach (var field in WeaponFields.All) Values[field.Key] = field.Read(Saved.Stats).ToString("G", CultureInfo.InvariantCulture);
        Mode = Saved.Stats.FireMode;
        AmmoType = Saved.Stats.AmmoType;
        ScenePath = Saved.ScenePath;
        GunshotSoundPath = Saved.GunshotSoundPath;
        ReloadSounds.Clear();
        ReloadSounds.AddRange(Saved.ReloadSounds.Select(cue => new ReloadSoundDraft {
            SoundPath = cue.SoundPath, Delay = cue.DelaySeconds.ToString("G", CultureInfo.InvariantCulture) }));
    }
    public bool TryBuild(out WeaponStats stats, out Dictionary<string, string> errors)
    {
        stats = Saved.Stats with { FireMode = Mode, AmmoType = AmmoType };
        errors = new();
        foreach (var field in WeaponFields.All)
        {
            if (!double.TryParse(Values[field.Key], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                !double.IsFinite(value) || value < field.Min || value > field.Max || (field.Integer && value != Math.Truncate(value)))
                errors[field.Key] = $"请输入 {field.Min}–{field.Max}{(field.Integer ? " 的整数" : " 的数值")}";
            else field.Write(stats, value);
        }
        if (stats.SpreadMin > stats.SpreadMax)
        {
            errors["spreadMin"] = "最小值不能超过最大值";
            errors["spreadMax"] = "最大值不能低于最小值";
        }
        if (!Enum.IsDefined(Mode)) errors["fireMode"] = "请选择有效的开火模式";
        if (!Enum.IsDefined(AmmoType)) errors["ammoType"] = "请选择有效的弹药类型";
        if (WeaponResources.FileError(_project, ScenePath) is string sceneError) errors["scenePath"] = sceneError;
        if (WeaponResources.SoundFileError(_project, GunshotSoundPath, optional: true) is string gunshotError)
            errors["gunshotSoundPath"] = gunshotError;
        for (int i = 0; i < ReloadSounds.Count; i++)
        {
            var cue = ReloadSounds[i];
            if (WeaponResources.SoundFileError(_project, cue.SoundPath) is string soundError) errors[$"reloadSound:{i}"] = soundError;
            if (!double.TryParse(cue.Delay, NumberStyles.Float, CultureInfo.InvariantCulture, out double delay) || !double.IsFinite(delay) || delay < 0)
                errors[$"reloadDelay:{i}"] = "播放延迟须为大于或等于 0 的秒数。";
        }
        return errors.Count == 0;
    }
    public List<ReloadSoundCue> BuildReloadSounds() => ReloadSounds.Select(cue => new ReloadSoundCue {
        SoundPath = cue.SoundPath, DelaySeconds = double.Parse(cue.Delay, CultureInfo.InvariantCulture) }).ToList();
    public bool IsDirty => !TryBuild(out var stats, out _) || stats != Saved.Stats || ScenePath != Saved.ScenePath ||
        GunshotSoundPath != Saved.GunshotSoundPath || !BuildReloadSounds().SequenceEqual(Saved.ReloadSounds);
    public void Accept(WeaponDefinition saved) { Saved = saved; Reset(); }
}

public sealed partial class MainWindow : Window
{
    private static readonly Brush Ink = Brush("#203330"), Muted = Brush("#71807C"), Teal = Brush("#236B58"),
        Line = Brush("#DCE4DF"), Error = Brush("#B33B32"), Surface = Brush("#F3F6F3");
    private readonly WeaponCatalogStore _store;
    private readonly string _project;
    private WeaponCatalog? _catalog;
    internal readonly List<WeaponDraft> Drafts = new();
    internal readonly Dictionary<string, TextBox> Inputs = new();
    internal readonly Dictionary<string, TextBox> ResourceInputs = new();
    private readonly Dictionary<string, TextBlock> _resourceMessages = new();
    internal readonly WeaponScenePreview ModelPreview;
    private readonly TextBlock _resourceCount = new();
    private readonly Dictionary<string, TextBlock> _messages = new();
    private readonly Dictionary<string, TextBlock> _listLabels = new();
    private readonly ListBox _list = new();
    private readonly TextBox _search = new();
    private readonly StackPanel _editor = new();
    private readonly TextBlock _status = new(), _summary = new(), _title = new(), _subtitle = new(), _dirtyText = new();
    private readonly Button _save = new(), _reset = new();
    private ComboBox _mode = null!;
    internal ComboBox AmmoTypeInput { get; private set; } = null!;
    private bool _binding, _skipClose;
    internal WeaponDraft? Current { get; private set; }
    internal bool CanSave => _save.IsEnabled;
    internal string Status => _status.Text;
    internal bool IsDirty => Drafts.Any(d => d.IsDirty);

    public MainWindow(string filePath, string? project = null)
    {
        _store = new WeaponCatalogStore(filePath);
        _project = project ?? Directory.GetParent(Path.GetDirectoryName(filePath)!)!.FullName;
        ModelPreview = new WeaponScenePreview(_project);
        Title = "Field Kit · 武器维护工具";
        Width = 1390; Height = 950; MinWidth = 1040; MinHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Surface; Foreground = Ink; FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13;
        BuildResources();
        BuildLayout();
        LoadCatalog();
        Closing += (_, e) => { if (!_skipClose && !ConfirmDiscardOrSave()) e.Cancel = true; };
        Closed += (_, _) => ModelPreview.Dispose();
        PreviewKeyDown += (_, e) => {
            if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { SaveAll(); e.Handled = true; }
        };
    }

    private void BuildResources()
    {
        var text = new Style(typeof(TextBox));
        text.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        text.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        text.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
        text.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        text.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
        text.Setters.Add(new Setter(Control.FontSizeProperty, 15.0));
        text.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        Resources[typeof(TextBox)] = text;
        var button = new Style(typeof(Button));
        button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(17, 10, 17, 10)));
        button.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        button.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        button.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
        button.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        button.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content); template.VisualTree = border;
        button.Setters.Add(new Setter(Control.TemplateProperty, template));
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(OpacityProperty, .78)); button.Triggers.Add(hover);
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(OpacityProperty, .4)); button.Triggers.Add(disabled);
        Resources[typeof(Button)] = button;
    }

    private void BuildLayout()
    {
        var root = new Grid { Background = Surface }; Content = root;
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(82) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel { Margin = new Thickness(26, 16, 26, 16) };
        root.Children.Add(new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(0,0,0,1), Child = header });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        var reload = new Button { Content = "重新载入", Margin = new Thickness(0,0,10,0) };
        reload.Click += (_, _) => { if (ConfirmDiscardOrSave()) LoadCatalog(Current?.Saved.Id); };
        actions.Children.Add(reload);
        _save.Content = "保存全部   Ctrl+S"; _save.Background = Teal; _save.Foreground = Brushes.White; _save.BorderBrush = Teal;
        _save.Click += (_, _) => SaveAll(); actions.Children.Add(_save);
        var brand = new StackPanel(); header.Children.Add(brand);
        brand.Children.Add(Label("武器维护", 24, Ink, FontWeights.SemiBold));
        brand.Children.Add(Label("FIELD KIT  /  独立配置工具", 11, Muted));

        var body = new Grid(); Grid.SetRow(body, 1); root.Children.Add(body);
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new Grid { Margin = new Thickness(18, 24, 18, 16) };
        var sideBorder = new Border { Background = Brush("#E8EEEA"), BorderBrush = Line, BorderThickness = new Thickness(0,0,1,0), Child = sidebar };
        body.Children.Add(sideBorder);
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.RowDefinitions.Add(new RowDefinition());
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.Children.Add(Label("武器资源", 15, Ink, FontWeights.SemiBold));
        var searchBox = new Grid { Margin = new Thickness(0,14,0,16) };
        _search.ToolTip = "按名称、分类或 ID 搜索"; _search.FontSize = 12;
        var placeholder = Label("搜索名称、分类或 ID", 11, Muted);
        placeholder.IsHitTestVisible = false; placeholder.Margin = new Thickness(10,0,0,0); placeholder.VerticalAlignment = VerticalAlignment.Center;
        searchBox.Children.Add(_search); searchBox.Children.Add(placeholder);
        _search.TextChanged += (_, _) => { placeholder.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Visible : Visibility.Collapsed; FilterList(); };
        Grid.SetRow(searchBox, 1); sidebar.Children.Add(searchBox);
        _list.Background = Brushes.Transparent; _list.BorderThickness = new Thickness(0);
        _list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        Grid.SetRow(_list, 2); sidebar.Children.Add(_list);
        _list.SelectionChanged += (_, _) => {
            if (!_binding && _list.SelectedItem is ListBoxItem item && item.Tag is WeaponDraft draft) Select(draft);
        };
        _resourceCount.FontSize = 11; _resourceCount.Foreground = Muted;
        _resourceCount.Margin = new Thickness(4,16,4,0); _resourceCount.LineHeight = 20;
        Grid.SetRow(_resourceCount, 3); sidebar.Children.Add(_resourceCount);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(scroll, 1); body.Children.Add(scroll);
        _editor.Margin = new Thickness(28, 22, 28, 24); scroll.Content = _editor;
        var identity = new DockPanel { Margin = new Thickness(0,0,0,18) };
        _reset.Content = "撤销本武器修改"; _reset.VerticalAlignment = VerticalAlignment.Center;
        _reset.Click += (_, _) => {
            if (Current != null && MessageBox.Show(this, "撤销当前武器所有未保存的修改？", "撤销修改", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                ResetCurrent();
        };
        DockPanel.SetDock(_reset, Dock.Right); identity.Children.Add(_reset);
        var identityText = new StackPanel(); identity.Children.Add(identityText);
        _title.FontSize = 25; _title.FontWeight = FontWeights.SemiBold;
        _subtitle.FontSize = 11; _subtitle.Foreground = Muted; _subtitle.Margin = new Thickness(0,5,0,0);
        identityText.Children.Add(_title); identityText.Children.Add(_subtitle); _editor.Children.Add(identity);
        BuildResourceCard();

        var cards = new Grid();
        cards.ColumnDefinitions.Add(new ColumnDefinition()); cards.ColumnDefinitions.Add(new ColumnDefinition());
        cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _editor.Children.Add(cards);
        AddCard(cards, 0, 0, "01", "价格与命中效果", new[] { "price", "damage", "enemySlowPercent", "knockbackSpeed" });
        AddCard(cards, 1, 0, "02", "开火与装填", new[] { "fireCooldown", "magazineCapacity", "fireMode", "ammoType", "reloadDuration" });
        AddCard(cards, 0, 1, "03", "移动、转向与弹道", new[] { "movementInertiaPercent", "turnFollowSpeed", "projectileSpeed" });
        AddCard(cards, 1, 1, "04", "散布控制", new[] { "spreadMin", "spreadMax", "spreadPerShot", "spreadRecovery" });
        BuildAudioCard();
        var summaryPanel = new Border { Background = Brush("#E5EEE7"), CornerRadius = new CornerRadius(6), Padding = new Thickness(18,13,18,13), Margin = new Thickness(0,8,0,0) };
        _summary.Foreground = Teal; _summary.TextWrapping = TextWrapping.Wrap; _summary.LineHeight = 23;
        summaryPanel.Child = _summary; _editor.Children.Add(summaryPanel);
        var scope = Label("保存后，重启游戏读取配置。换弹音效已接入换弹流程；枪声与射击、命中属性为后续开火玩法预留。", 11, Muted);
        scope.TextWrapping = TextWrapping.Wrap; scope.Margin = new Thickness(2,12,2,0); _editor.Children.Add(scope);

        var footer = new Grid { Margin = new Thickness(25,12,25,12) };
        footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var footerBorder = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(0,1,0,0), Child = footer };
        Grid.SetRow(footerBorder, 2); root.Children.Add(footerBorder);
        var state = new StackPanel(); footer.Children.Add(state);
        _status.TextWrapping = TextWrapping.Wrap; state.Children.Add(_status);
        var path = Label("配置文件  ·  data/weapons.json     /     保存时保留上一版 .bak", 11, Muted);
        path.ToolTip = _store.FilePath; path.Margin = new Thickness(0,4,12,0); state.Children.Add(path);
        _dirtyText.VerticalAlignment = VerticalAlignment.Center; _dirtyText.Foreground = Teal;
        _dirtyText.Margin = new Thickness(20,0,0,0); Grid.SetColumn(_dirtyText, 1); footer.Children.Add(_dirtyText);
    }

    private void BuildResourceCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var card = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18), Margin = new Thickness(0,0,0,16), Child = grid };
        _editor.Children.Add(card);
        var preview = new StackPanel { Margin = new Thickness(0,0,22,0), VerticalAlignment = VerticalAlignment.Center };
        preview.Children.Add(ModelPreview); preview.Children.Add(Label("3D 模型 · 拖动旋转 / 滚轮缩放", 10, Muted)); grid.Children.Add(preview);
        var rows = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(rows,1); grid.Children.Add(rows);
        {
            const string key = "scenePath";
            rows.Children.Add(Label("武器场景 · Node3D", 12, Ink, FontWeights.SemiBold));
            var row = new DockPanel { Margin = new Thickness(0,5,0,0) };
            var browse = new Button { Content = "选择文件", Padding = new Thickness(12,7,12,7), Margin = new Thickness(8,0,0,0) };
            DockPanel.SetDock(browse,Dock.Right); row.Children.Add(browse);
            var input = new TextBox { FontSize = 12, Height = 34 };
            ResourceInputs[key] = input; row.Children.Add(input); rows.Children.Add(row);
            var message = Label("",10,Muted); message.TextWrapping = TextWrapping.Wrap; message.Margin = new Thickness(0,4,0,10);
            _resourceMessages[key] = message; rows.Children.Add(message);
            input.TextChanged += (_, _) => {
                if (_binding || Current == null) return;
                Current.ScenePath = input.Text.Trim();
                RefreshState();
            };
            browse.Click += (_, _) => {
                var dialog = new OpenFileDialog { InitialDirectory = _project, CheckFileExists = true,
                    Filter = "Godot 武器场景|*.tscn;*.scn;*.glb;*.gltf" };
                if (dialog.ShowDialog(this) != true) return;
                string relative = Path.GetRelativePath(_project,dialog.FileName).Replace('\\','/');
                if (relative.StartsWith("../",StringComparison.Ordinal) || Path.IsPathRooted(relative))
                { SetStatus("请选择游戏工程内的资源文件。外部文件请先放入工程目录。",true); return; }
                input.Text = "res://" + relative;
            };
        }
    }

    private void AddCard(Grid parent, int col, int row, string index, string title, string[] keys)
    {
        var card = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Margin = new Thickness(col == 0 ? 0 : 8,0,col == 0 ? 8 : 0,16) };
        Grid.SetColumn(card, col); Grid.SetRow(card, row); parent.Children.Add(card);
        var content = new StackPanel(); card.Child = content;
        var heading = Label($"{index}   {title}", 16, Ink, FontWeights.SemiBold); heading.Margin = new Thickness(0,0,0,17); content.Children.Add(heading);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < (keys.Length + 1) / 2; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(grid);
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            var fieldPanel = new StackPanel { Margin = new Thickness(i % 2 == 0 ? 0 : 8,0,i % 2 == 0 ? 8 : 0,12) };
            Grid.SetRow(fieldPanel, i / 2); Grid.SetColumn(fieldPanel, i % 2); grid.Children.Add(fieldPanel);
            if (key == "ammoType")
            {
                fieldPanel.Children.Add(Label("对应弹药", 12, Ink));
                AmmoTypeInput = new ComboBox { ItemsSource = ItemCatalog.Ammunition, DisplayMemberPath = "Name", SelectedValuePath = "AmmoType",
                    Height = 36, Margin = new Thickness(0,6,0,0), Padding = new Thickness(8,5,8,5) };
                fieldPanel.Children.Add(AmmoTypeInput);
                var help = Label("换弹时仅消耗背包中的对应弹药。", 10, Muted);
                help.TextWrapping = TextWrapping.Wrap; help.Margin = new Thickness(0,5,0,0); fieldPanel.Children.Add(help);
                AmmoTypeInput.SelectionChanged += (_, _) => {
                    if (_binding || Current == null) return;
                    Current.AmmoType = AmmoTypeInput.SelectedValue is AmmoType type ? type : (AmmoType)(-1);
                    RefreshState();
                };
                continue;
            }
            if (key == "fireMode")
            {
                fieldPanel.Children.Add(Label("开火模式", 12, Ink));
                _mode = new ComboBox { ItemsSource = new[] { "单发", "连发" }, Height = 36, Margin = new Thickness(0,6,0,0), Padding = new Thickness(8,5,8,5) };
                fieldPanel.Children.Add(_mode);
                var help = Label("单发：按一次一发；连发：按住持续开火。", 10, Muted);
                help.TextWrapping = TextWrapping.Wrap; help.Margin = new Thickness(0,5,0,0); fieldPanel.Children.Add(help);
                _mode.SelectionChanged += (_, _) => { if (!_binding && Current != null) { Current.Mode = _mode.SelectedIndex == 1 ? FireMode.Automatic : FireMode.SemiAuto; RefreshState(); } };
                continue;
            }
            var field = WeaponFields.All.First(f => f.Key == key);
            fieldPanel.Children.Add(Label(field.Label, 12, Ink));
            var valueRow = new Grid { Margin = new Thickness(0,6,0,0) };
            valueRow.ColumnDefinitions.Add(new ColumnDefinition()); valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var input = new TextBox { Height = 36, ToolTip = field.Hint, VerticalContentAlignment = VerticalAlignment.Center };
            Inputs[key] = input; valueRow.Children.Add(input);
            var unit = Label(field.Unit, 10, Muted); unit.Margin = new Thickness(7,0,0,0); unit.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(unit,1); valueRow.Children.Add(unit); fieldPanel.Children.Add(valueRow);
            var message = Label(field.Hint, 10, Muted); message.TextWrapping = TextWrapping.Wrap; message.MinHeight = 31;
            message.Margin = new Thickness(0,5,0,0); fieldPanel.Children.Add(message); _messages[key] = message;
            input.TextChanged += (_, _) => { if (!_binding && Current != null) { Current.Values[key] = input.Text.Trim(); RefreshState(); } };
        }
        if (index == "03")
        {
            var contract = Label("移速 = 基础移速 × (1 − 移动惯性 / 100)\n击退方向始终与命中子弹的方向一致。", 11, Muted);
            contract.LineHeight = 23; contract.TextWrapping = TextWrapping.Wrap; contract.Margin = new Thickness(0,2,0,0); content.Children.Add(contract);
        }
    }

    internal void LoadCatalog(string? selected = null)
    {
        try
        {
            var loaded = _store.Load();
            _catalog = loaded;
            ModelPreview.SetScene(null);
            Drafts.Clear(); Drafts.AddRange(loaded.Weapons.Select(w => new WeaponDraft(w, _project)));
            _resourceCount.Text = $"{Drafts.Count} 个已登记武器\n● 表示尚未保存的修改";
            _binding = true; _list.Items.Clear(); _listLabels.Clear();
            foreach (var draft in Drafts)
            {
                var text = Label(draft.Saved.Name, 14, Ink, FontWeights.SemiBold);
                _listLabels[draft.Saved.Id] = text;
                var stack = new StackPanel { Margin = new Thickness(8,8,8,8) };
                stack.Children.Add(text); stack.Children.Add(Label(draft.Saved.Category, 10, Muted));
                _list.Items.Add(new ListBoxItem { Content = stack, Tag = draft, Margin = new Thickness(0,0,0,4) });
            }
            _binding = false;
            _search.Text = "";
            SelectById(selected ?? Drafts[0].Saved.Id);
            _editor.IsEnabled = true;
            SetStatus($"已载入 {Drafts.Count} 个武器。数值为初始调参配置，可按玩法调整。", false);
        }
        catch (Exception error)
        {
            _binding = false;
            if (_catalog == null) { _editor.IsEnabled = false; _save.IsEnabled = false; }
            SetStatus("载入失败：" + error.Message, true);
        }
    }

    internal void SelectById(string id)
    {
        var item = _list.Items.Cast<ListBoxItem>().FirstOrDefault(i => ((WeaponDraft)i.Tag).Saved.Id == id) ?? (ListBoxItem)_list.Items[0];
        _list.SelectedItem = item;
        Select((WeaponDraft)item.Tag);
    }

    private void Select(WeaponDraft draft)
    {
        Current = draft; _binding = true;
        _title.Text = draft.Saved.Name;
        _subtitle.Text = $"{draft.Saved.Category}   /   {draft.Saved.Id}";
        ResourceInputs["scenePath"].Text = draft.ScenePath;
        foreach (var field in WeaponFields.All) Inputs[field.Key].Text = draft.Values[field.Key];
        _mode.SelectedIndex = draft.Mode == FireMode.Automatic ? 1 : 0;
        AmmoTypeInput.SelectedValue = draft.AmmoType;
        GunshotInput.Text = draft.GunshotSoundPath;
        RebuildReloadSoundRows();
        _binding = false; RefreshState();
    }

    private void FilterList()
    {
        string term = _search.Text.Trim();
        foreach (ListBoxItem item in _list.Items)
        {
            var weapon = ((WeaponDraft)item.Tag).Saved;
            item.Visibility = $"{weapon.Id} {weapon.Name} {weapon.Category}".Contains(term, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshState()
    {
        if (Current == null) return;
        bool valid = Current.TryBuild(out var stats, out var errors);
        RefreshAudioState(errors);
        AmmoTypeInput.BorderBrush = errors.ContainsKey("ammoType") ? Error : Line;
        foreach (string key in ResourceInputs.Keys)
        {
            bool bad = errors.TryGetValue(key, out string? error);
            ResourceInputs[key].BorderBrush = bad ? Error : Line;
            _resourceMessages[key].Text = bad ? error! : "TSCN / SCN / GLB / GLTF；持有武器和背包显示都使用此场景。";
            _resourceMessages[key].Foreground = bad ? Error : Muted;
        }
        ModelPreview.SetScene(errors.ContainsKey("scenePath") ? null : Current.ScenePath);
        foreach (var field in WeaponFields.All)
        {
            bool bad = errors.TryGetValue(field.Key, out string? error);
            Inputs[field.Key].BorderBrush = bad ? Error : Line;
            _messages[field.Key].Text = bad ? error! : field.Hint;
            _messages[field.Key].Foreground = bad ? Error : Muted;
        }
        int dirty = Drafts.Count(d => d.IsDirty);
        int invalid = Drafts.Count(d => !d.TryBuild(out _, out _));
        foreach (var draft in Drafts) _listLabels[draft.Saved.Id].Text = draft.Saved.Name + (draft.IsDirty ? "  ●" : "");
        _save.IsEnabled = dirty > 0 && invalid == 0;
        _reset.IsEnabled = Current.IsDirty;
        _dirtyText.Text = dirty > 0 ? $"{dirty} 个武器有未保存修改" : "所有修改已保存";
        _summary.Text = valid
            ? $"参数预览   ·   射速 {stats.ShotsPerMinute():0.##} 发/分钟   ·   弹夹 {stats.MagazineCapacity} 发   ·   换弹 {stats.ReloadDuration:0.###} 秒   ·   持有者移速 {stats.MovementSpeedMultiplier() * 100:0.##}%   ·   敌人移速 {stats.EnemySpeedMultiplier() * 100:0.##}%\n"
                + $"转向系数 {stats.TurnFollowSpeed:0.###}   ·   散布 {stats.SpreadMin:0.###}° → {stats.SpreadMax:0.###}°   ·   每发 +{stats.SpreadPerShot:0.###}°   ·   停火 {stats.FireCooldown:0.###} 秒后以 {stats.SpreadRecovery:0.###}°/秒恢复"
            : "请修正红色字段后查看参数预览。";
        SetStatus(invalid > 0 ? $"{invalid} 个武器存在无效参数；请修正后再保存。" : dirty > 0 ? "修改暂存于当前窗口，点击“保存全部”写入游戏配置。" : "配置已就绪。选择武器开始编辑。", invalid > 0);
    }

    internal bool SaveAll()
    {
        if (_catalog == null) return false;
        if (Drafts.Any(d => !d.TryBuild(out _, out _))) { SetStatus("保存失败：请先修正无效参数。", true); return false; }
        if (!IsDirty) return true;
        try
        {
            var changed = _catalog with { Weapons = Drafts.Select(d => { d.TryBuild(out var stats, out _); return d.Saved with {
                Stats = stats, ScenePath = d.ScenePath, GunshotSoundPath = d.GunshotSoundPath, ReloadSounds = d.BuildReloadSounds() }; }).ToList() };
            _store.Save(changed);
            _catalog = changed;
            for (int i = 0; i < Drafts.Count; i++) Drafts[i].Accept(changed.Weapons[i]);
            if (Current != null) Select(Current);
            SetStatus($"保存成功 · {DateTime.Now:HH:mm:ss}   游戏下次启动读取新配置。", false);
            return true;
        }
        catch (Exception error) { SetStatus("保存失败：" + error.Message, true); return false; }
    }

    internal void ResetCurrent() { if (Current != null) { Current.Reset(); Select(Current); } }
    internal void SetMode(FireMode mode) => _mode.SelectedIndex = mode == FireMode.Automatic ? 1 : 0;
    internal void CloseForTest() { _skipClose = true; Close(); }

    private bool ConfirmDiscardOrSave()
    {
        if (!IsDirty) return true;
        var answer = MessageBox.Show(this, "有尚未保存的武器修改。\n\n是：保存全部并继续\n否：放弃修改并继续\n取消：返回编辑", "未保存的修改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        return answer == MessageBoxResult.No || (answer == MessageBoxResult.Yes && SaveAll());
    }

    private void SetStatus(string text, bool error) { _status.Text = text; _status.Foreground = error ? Error : Ink; }
    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    private static TextBlock Label(string text, double size, Brush color, FontWeight? weight = null) => new() {
        Text = text, FontSize = size, Foreground = color, FontWeight = weight ?? FontWeights.Normal
    };
}
