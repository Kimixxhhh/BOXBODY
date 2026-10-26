using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using FieldKit.Weapons;

namespace WeaponWorkbench;

public sealed class ThrowableDraft
{
    public ThrowableDefinition Saved { get; private set; }
    public bool IsNew { get; private set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public Dictionary<string, string> Values { get; } = new();
    public string ItemScenePath { get; set; } = "";
    public string ExplosionSoundPath { get; set; } = "";
    private readonly string _project;

    public ThrowableDraft(ThrowableDefinition saved, string project, bool isNew = false)
    {
        Saved = saved;
        _project = project;
        IsNew = isNew;
        Reset();
    }

    public void Reset()
    {
        Id = Saved.Id;
        Name = Saved.Name;
        Category = Saved.Category;
        foreach (var field in ThrowableFields.All)
            Values[field.Key] = field.Read(Saved).ToString("G", CultureInfo.InvariantCulture);
        ItemScenePath = Saved.ItemScenePath;
        ExplosionSoundPath = Saved.ExplosionSoundPath;
    }

    public bool TryBuild(out ThrowableDefinition definition, out Dictionary<string, string> errors)
    {
        definition = Saved with {
            Id = Id, Name = Name, Category = Category,
            ItemScenePath = ItemScenePath, ExplosionSoundPath = ExplosionSoundPath
        };
        errors = new();
        if (string.IsNullOrWhiteSpace(Id)) errors["id"] = "请填写投掷物编号。";
        else if (IsNew && !Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
            errors["id"] = "编号只能包含英文字母、数字、下划线和短横线。";
        if (string.IsNullOrWhiteSpace(Name)) errors["name"] = "请填写投掷物名称。";
        if (string.IsNullOrWhiteSpace(Category)) errors["category"] = "请填写投掷物分类。";
        foreach (var field in ThrowableFields.All)
        {
            if (!double.TryParse(Values[field.Key], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                !double.IsFinite(value) || value < field.Min || value > field.Max ||
                (field.Integer && value != Math.Truncate(value)))
                errors[field.Key] = $"请输入 {field.Min}–{field.Max}{(field.Integer ? " 的整数" : " 的数值")}";
            else field.Write(definition, value);
        }
        if (WeaponResources.ItemSceneFileError(_project, ItemScenePath) is string sceneError)
            errors["itemScenePath"] = sceneError;
        if (WeaponResources.SoundFileError(_project, ExplosionSoundPath, optional: true) is string soundError)
            errors["explosionSoundPath"] = soundError;
        return errors.Count == 0;
    }

    public bool IsDirty => IsNew || !TryBuild(out var definition, out _) || definition != Saved;

    public void Accept(ThrowableDefinition saved)
    {
        Saved = saved;
        IsNew = false;
        Reset();
    }
}

public sealed partial class MainWindow
{
    internal readonly List<ThrowableDraft> ThrowableDrafts = new();
    internal readonly Dictionary<string, TextBox> ThrowableInputs = new();
    internal readonly Dictionary<string, TextBox> ThrowableResourceInputs = new();
    internal readonly TextBox ExplosionSoundInput = new() { FontSize = 12, Height = 34 };
    private readonly Dictionary<string, TextBlock> _throwableMessages = new();
    private readonly Dictionary<string, TextBlock> _throwableResourceMessages = new();
    private readonly Dictionary<string, TextBox> _throwableIdentityInputs = new();
    private readonly Dictionary<string, TextBlock> _throwableIdentityMessages = new();
    private readonly Dictionary<ThrowableDraft, TextBlock> _throwableListLabels = new();
    private readonly Dictionary<ThrowableDraft, TextBlock> _throwableCategoryLabels = new();
    private StackPanel _throwableEditor = null!;
    private readonly TextBlock _throwableTitle = new(), _throwableSubtitle = new(), _throwableSummary = new();
    private TextBlock _throwableSoundMessage = new();
    private readonly Button _throwableReset = new();
    private Border _throwableIdentityCard = null!;
    internal WeaponScenePreview ThrowablePreview { get; private set; } = null!;
    internal ThrowableDraft? CurrentThrowable { get; private set; }
    internal bool ThrowablesMode { get; private set; }
    internal IReadOnlyDictionary<string, TextBox> ThrowableIdentityInputs => _throwableIdentityInputs;
    internal void ScrollThrowableEditorToTop() => _scroll?.ScrollToTop();
    internal IEnumerable<string> VisibleThrowableIds =>
        _list.Items.Cast<ListBoxItem>().Where(item => item.Visibility == Visibility.Visible && item.Tag is ThrowableDraft)
            .Select(item => ((ThrowableDraft)item.Tag).Id);

    private void BuildThrowableEditor()
    {
        _throwableEditor = new StackPanel { Margin = new Thickness(28, 22, 28, 24) };
        var identity = new DockPanel { Margin = new Thickness(0,0,0,18) };
        _throwableReset.Content = "撤销本投掷物修改";
        _throwableReset.VerticalAlignment = VerticalAlignment.Center;
        _throwableReset.Click += (_, _) => {
            if (CurrentThrowable?.IsNew == true) { ResetCurrentThrowable(); return; }
            if (CurrentThrowable != null && MessageBox.Show(this, "撤销当前投掷物所有未保存的修改？", "撤销修改",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                ResetCurrentThrowable();
        };
        DockPanel.SetDock(_throwableReset, Dock.Right);
        identity.Children.Add(_throwableReset);
        var identityText = new StackPanel();
        identity.Children.Add(identityText);
        _throwableTitle.FontSize = 25; _throwableTitle.FontWeight = FontWeights.SemiBold; _throwableTitle.TextWrapping = TextWrapping.Wrap;
        _throwableSubtitle.FontSize = 11; _throwableSubtitle.Foreground = Muted; _throwableSubtitle.Margin = new Thickness(0,5,0,0);
        _throwableSubtitle.TextWrapping = TextWrapping.Wrap;
        identityText.Children.Add(_throwableTitle);
        identityText.Children.Add(_throwableSubtitle);
        _throwableEditor.Children.Add(identity);
        BuildThrowableIdentityCard();
        BuildThrowableResourceCard();
        var cards = new Grid();
        cards.ColumnDefinitions.Add(new ColumnDefinition());
        cards.ColumnDefinitions.Add(new ColumnDefinition());
        cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _throwableEditor.Children.Add(cards);
        AddThrowableCard(cards, 0, "01", "爆炸效果",
            new[] { "damage", "range", "enemySlowPercent", "slowDuration", "knockbackSpeed", "fuseTime" });
        AddThrowableCard(cards, 1, "02", "购买与使用", new[] { "price", "slotCapacity" });
        BuildThrowableSoundCard();
        var summaryPanel = new Border { Background = Brush("#E5EEE7"), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(18,13,18,13), Margin = new Thickness(0,8,0,0) };
        _throwableSummary.Foreground = Teal; _throwableSummary.TextWrapping = TextWrapping.Wrap; _throwableSummary.LineHeight = 23;
        summaryPanel.Child = _throwableSummary;
        _throwableEditor.Children.Add(summaryPanel);
        var scope = Label("投掷物模板可供游戏购买并放入背包物品槽位；一次购买把一格填满模板配置的“每格数量”。游戏内按 G 拿出手雷，左键投掷，模板属性在爆炸时全部生效。", 11, Muted);
        scope.TextWrapping = TextWrapping.Wrap; scope.Margin = new Thickness(2,12,2,0);
        _throwableEditor.Children.Add(scope);
    }

    private void BuildThrowableIdentityCard()
    {
        var content = new StackPanel();
        _throwableIdentityCard = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Margin = new Thickness(0,0,0,16), Child = content };
        _throwableEditor.Children.Add(_throwableIdentityCard);
        content.Children.Add(Label("基本信息", 16, Ink, FontWeights.SemiBold));
        var grid = new Grid { Margin = new Thickness(0,12,0,0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        content.Children.Add(grid);
        foreach (var (key, title, column, row) in new[] {
                     ("name", "投掷物名称", 0, 0), ("category", "分类", 1, 0), ("id", "投掷物编号", 0, 1) })
        {
            var panel = new StackPanel { Margin = new Thickness(column == 1 ? 14 : 0, row == 1 ? 10 : 0, column == 0 ? 14 : 0, 0) };
            Grid.SetColumn(panel, column); Grid.SetRow(panel, row); grid.Children.Add(panel);
            panel.Children.Add(Label(title, 12, Ink));
            var input = new TextBox { Height = 36, Margin = new Thickness(0,6,0,0) };
            _throwableIdentityInputs[key] = input; panel.Children.Add(input);
            var message = Label("", 10, Muted); message.TextWrapping = TextWrapping.Wrap; message.Margin = new Thickness(0,5,0,0);
            _throwableIdentityMessages[key] = message; panel.Children.Add(message);
            input.TextChanged += (_, _) => {
                if (_binding || CurrentThrowable == null) return;
                string text = input.Text.Trim();
                if (key == "id" && CurrentThrowable.IsNew) CurrentThrowable.Id = text;
                else if (key == "name") CurrentThrowable.Name = text;
                else if (key == "category") CurrentThrowable.Category = text;
                RefreshThrowableState();
            };
        }
    }

    private void BuildThrowableResourceCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var card = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Margin = new Thickness(0,0,0,16), Child = grid };
        _throwableEditor.Children.Add(card);
        var preview = new StackPanel { Margin = new Thickness(0,0,22,0), VerticalAlignment = VerticalAlignment.Center };
        preview.Children.Add(ThrowablePreview);
        preview.Children.Add(Label("3D 模型 · 拖动旋转 / 滚轮缩放", 10, Muted));
        grid.Children.Add(preview);
        var rows = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(rows, 1); grid.Children.Add(rows);
        const string key = "itemScenePath";
        rows.Children.Add(Label("物品场景 · Node3D", 12, Ink, FontWeights.SemiBold));
        var row = new DockPanel { Margin = new Thickness(0,5,0,0) };
        var browse = new Button { Content = "选择文件", Padding = new Thickness(12,7,12,7), Margin = new Thickness(8,0,0,0) };
        DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
        var input = new TextBox { FontSize = 12, Height = 34 };
        ThrowableResourceInputs[key] = input; row.Children.Add(input); rows.Children.Add(row);
        var message = Label("", 10, Muted); message.TextWrapping = TextWrapping.Wrap; message.Margin = new Thickness(0,4,0,10);
        _throwableResourceMessages[key] = message; rows.Children.Add(message);
        input.TextChanged += (_, _) => {
            if (_binding || CurrentThrowable == null) return;
            CurrentThrowable.ItemScenePath = input.Text.Trim();
            RefreshThrowableState();
        };
        browse.Click += (_, _) => {
            var dialog = new OpenFileDialog { InitialDirectory = _project, CheckFileExists = true,
                Filter = "Godot 物品场景|*.tscn;*.scn;*.glb;*.gltf" };
            if (dialog.ShowDialog(this) != true) return;
            string relative = Path.GetRelativePath(_project, dialog.FileName).Replace('\\','/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            { SetStatus("请选择游戏工程内的资源文件。外部文件请先放入工程目录。", true); return; }
            input.Text = "res://" + relative;
        };
    }

    private void AddThrowableCard(Grid parent, int column, string index, string title, string[] keys)
    {
        var card = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(18),
            Margin = new Thickness(column == 0 ? 0 : 8,0,column == 0 ? 8 : 0,16) };
        Grid.SetColumn(card, column); parent.Children.Add(card);
        var content = new StackPanel(); card.Child = content;
        var heading = Label($"{index}   {title}", 16, Ink, FontWeights.SemiBold);
        heading.Margin = new Thickness(0,0,0,17); content.Children.Add(heading);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < (keys.Length + 1) / 2; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(grid);
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            var fieldPanel = new StackPanel { Margin = new Thickness(i % 2 == 0 ? 0 : 8,0,i % 2 == 0 ? 8 : 0,12) };
            Grid.SetRow(fieldPanel, i / 2); Grid.SetColumn(fieldPanel, i % 2); grid.Children.Add(fieldPanel);
            var field = ThrowableFields.All.First(f => f.Key == key);
            fieldPanel.Children.Add(Label(field.Label, 12, Ink));
            var valueRow = new Grid { Margin = new Thickness(0,6,0,0) };
            valueRow.ColumnDefinitions.Add(new ColumnDefinition());
            valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var input = new TextBox { Height = 36, ToolTip = field.Hint, VerticalContentAlignment = VerticalAlignment.Center };
            ThrowableInputs[key] = input; valueRow.Children.Add(input);
            var unit = Label(field.Unit, 10, Muted); unit.Margin = new Thickness(7,0,0,0); unit.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(unit,1); valueRow.Children.Add(unit); fieldPanel.Children.Add(valueRow);
            var message = Label(field.Hint, 10, Muted); message.TextWrapping = TextWrapping.Wrap; message.MinHeight = 31;
            message.Margin = new Thickness(0,5,0,0); fieldPanel.Children.Add(message); _throwableMessages[key] = message;
            input.TextChanged += (_, _) => {
                if (!_binding && CurrentThrowable != null) { CurrentThrowable.Values[key] = input.Text.Trim(); RefreshThrowableState(); }
            };
        }
        if (index == "02")
        {
            var help = Label("购买价格：在武器箱购买一份（填满一个物品槽位）的费用，0 为免费。每格数量：一个物品槽位可容纳并一次放入的枚数，与子弹的每份数量相同。购买后放入背包下方物品槽位，不占用两个武器栏。", 11, Muted);
            help.TextWrapping = TextWrapping.Wrap; help.LineHeight = 21; help.Margin = new Thickness(0,2,0,0);
            content.Children.Add(help);
        }
    }

    private void BuildThrowableSoundCard()
    {
        var content = new StackPanel();
        var card = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Margin = new Thickness(0,0,0,16), Child = content };
        _throwableEditor.Children.Add(card);
        var heading = Label("03   爆炸音效", 16, Ink, FontWeights.SemiBold);
        heading.Margin = new Thickness(0,0,0,17); content.Children.Add(heading);
        content.Children.Add(Label("音效文件", 12, Ink, FontWeights.SemiBold));
        var row = new DockPanel { Margin = new Thickness(0,6,0,0) };
        var clear = new Button { Content = "清空", Padding = new Thickness(12,7,12,7), Margin = new Thickness(8,0,0,0) };
        clear.Click += (_, _) => ExplosionSoundInput.Text = "";
        DockPanel.SetDock(clear, Dock.Right); row.Children.Add(clear);
        var browse = new Button { Content = "选择文件", Padding = new Thickness(12,7,12,7), Margin = new Thickness(8,0,0,0) };
        DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
        row.Children.Add(ExplosionSoundInput); content.Children.Add(row);
        _throwableSoundMessage = Label("", 10, Muted); _throwableSoundMessage.TextWrapping = TextWrapping.Wrap;
        _throwableSoundMessage.Margin = new Thickness(0,5,0,0); content.Children.Add(_throwableSoundMessage);
        ExplosionSoundInput.TextChanged += (_, _) => {
            if (_binding || CurrentThrowable == null) return;
            CurrentThrowable.ExplosionSoundPath = ExplosionSoundInput.Text.Trim();
            RefreshThrowableState();
        };
        browse.Click += (_, _) => {
            var dialog = new OpenFileDialog { InitialDirectory = _project, CheckFileExists = true,
                Filter = "音效文件|*.wav;*.ogg;*.mp3" };
            if (dialog.ShowDialog(this) != true) return;
            string relative = Path.GetRelativePath(_project, dialog.FileName).Replace('\\','/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            { SetStatus("请选择游戏工程内的音效文件。外部音效请先放入工程目录。", true); return; }
            ExplosionSoundInput.Text = "res://" + relative;
        };
    }

    internal void AddNewThrowable()
    {
        if (_catalog == null) return;
        SwitchMode(true);
        int number = 1;
        while (ThrowableDrafts.Any(d => d.Id == $"throwable_{number}" || d.Saved.Id == $"throwable_{number}")) number++;
        var definition = new ThrowableDefinition {
            Id = $"throwable_{number}", Name = "新投掷物", Category = "投掷物",
            Price = 500, SlotCapacity = 3, Damage = 100, Range = 4, EnemySlowPercent = 30, SlowDuration = 2,
            KnockbackSpeed = 6, FuseTime = 2.5, ItemScenePath = "", ExplosionSoundPath = ""
        };
        var draft = new ThrowableDraft(definition, _project, isNew: true);
        ThrowableDrafts.Add(draft);
        RebuildThrowableList();
        _search.Text = "";
        SelectThrowableById(draft.Id);
        _throwableEditor.BringIntoView(new Rect(0, 0, 1, 1));
        _throwableIdentityInputs["name"].Focus();
        _throwableIdentityInputs["name"].SelectAll();
        SetStatus("已创建新投掷物草稿。填写名称与分类、选择物品场景并调整属性后保存；可点击“取消新增”放弃。", false);
    }

    internal void SelectThrowableById(string id)
    {
        var item = _list.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.Tag is ThrowableDraft d && d.Id == id)
            ?? (_list.Items.Count > 0 ? (ListBoxItem)_list.Items[0] : null);
        if (item == null) return;
        _list.SelectedItem = item;
        if (item.Tag is ThrowableDraft draft) SelectThrowable(draft);
    }

    private void SelectThrowable(ThrowableDraft draft)
    {
        CurrentThrowable = draft; _binding = true;
        _throwableIdentityInputs["id"].Text = draft.Id;
        _throwableIdentityInputs["id"].IsReadOnly = !draft.IsNew;
        _throwableIdentityInputs["name"].Text = draft.Name;
        _throwableIdentityInputs["category"].Text = draft.Category;
        ThrowableResourceInputs["itemScenePath"].Text = draft.ItemScenePath;
        ExplosionSoundInput.Text = draft.ExplosionSoundPath;
        foreach (var field in ThrowableFields.All) ThrowableInputs[field.Key].Text = draft.Values[field.Key];
        _binding = false;
        RefreshThrowableState();
    }

    internal void ResetCurrentThrowable()
    {
        if (CurrentThrowable == null) return;
        if (CurrentThrowable.IsNew)
        {
            int index = ThrowableDrafts.IndexOf(CurrentThrowable);
            ThrowableDrafts.Remove(CurrentThrowable);
            RebuildThrowableList();
            if (ThrowableDrafts.Count > 0) SelectThrowableById(ThrowableDrafts[Math.Max(0, index - 1)].Id);
            else { CurrentThrowable = null; _throwableEditor.IsEnabled = false; }
            SetStatus("已取消新增投掷物。", false);
        }
        else { CurrentThrowable.Reset(); SelectThrowable(CurrentThrowable); }
    }

    private bool TryBuildThrowableDraft(ThrowableDraft draft, out ThrowableDefinition definition, out Dictionary<string, string> errors)
    {
        draft.TryBuild(out definition, out errors);
        if (ThrowableDrafts.Count(other => string.Equals(other.Id, draft.Id, StringComparison.Ordinal)) > 1)
            errors["id"] = "编号已被其他投掷物使用，请换一个。";
        return errors.Count == 0;
    }

    private void RefreshThrowableState()
    {
        if (CurrentThrowable == null || _catalog == null) return;
        bool valid = TryBuildThrowableDraft(CurrentThrowable, out var definition, out var errors);
        _throwableTitle.Text = CurrentThrowable.Name.Length > 0 ? CurrentThrowable.Name : "未命名投掷物";
        _throwableSubtitle.Text = $"{CurrentThrowable.Category}   /   {CurrentThrowable.Id}" + (CurrentThrowable.IsNew ? "   /   尚未保存" : "");
        foreach (var (key, input) in _throwableIdentityInputs)
        {
            bool bad = errors.TryGetValue(key, out string? error);
            input.BorderBrush = bad ? Error : Line;
            _throwableIdentityMessages[key].Foreground = bad ? Error : Muted;
            _throwableIdentityMessages[key].Text = bad ? error! : key == "id"
                ? CurrentThrowable.IsNew ? "自动生成，可修改；必须唯一，保存后固定。" : "已保存的编号固定，避免影响游戏引用。"
                : key == "name" ? "显示在武器箱购买列表和背包物品槽中。" : "例如：投掷物、手雷、闪光弹。";
        }
        foreach (string key in ThrowableResourceInputs.Keys)
        {
            bool bad = errors.TryGetValue(key, out string? error);
            ThrowableResourceInputs[key].BorderBrush = bad ? Error : Line;
            _throwableResourceMessages[key].Text = bad ? error! : "TSCN / SCN / GLB / GLTF；背包图标与投掷物网格都使用此场景。";
            _throwableResourceMessages[key].Foreground = bad ? Error : Muted;
        }
        ThrowablePreview.SetScene(errors.ContainsKey("itemScenePath") ? null : CurrentThrowable.ItemScenePath);
        foreach (var field in ThrowableFields.All)
        {
            bool bad = errors.TryGetValue(field.Key, out string? error);
            ThrowableInputs[field.Key].BorderBrush = bad ? Error : Line;
            _throwableMessages[field.Key].Text = bad ? error! : field.Hint;
            _throwableMessages[field.Key].Foreground = bad ? Error : Muted;
        }
        bool badSound = errors.TryGetValue("explosionSoundPath", out string? soundError);
        ExplosionSoundInput.BorderBrush = badSound ? Error : Line;
        _throwableSoundMessage.Foreground = badSound ? Error : Muted;
        _throwableSoundMessage.Text = badSound ? soundError!
            : "WAV / OGG / MP3；留空则爆炸时静音。文件需先在 Godot 中导入。";
        _throwableSummary.Text = valid
            ? $"爆炸预览   ·   伤害 {definition.Damage:0.##}   ·   范围 {definition.Range:0.##} 米   ·   " +
              $"减速 {definition.EnemySlowPercent:0.##}%（{definition.SlowDuration:0.##} 秒）   ·   " +
              $"击退 {definition.KnockbackSpeed:0.##} 米/秒   ·   引信 {definition.FuseTime:0.##} 秒\n" +
              $"购买价格 {definition.Price}$ / 份   ·   每格 {definition.SlotCapacity} 枚   ·   投掷后 {definition.FuseTime:0.##} 秒爆炸，范围内的目标都会受到完整效果"
            : "请修正红色字段后查看投掷物参数预览。";
        foreach (var draft in ThrowableDrafts)
        {
            if (_throwableListLabels.TryGetValue(draft, out var label))
                label.Text = (draft.Name.Length > 0 ? draft.Name : "未命名投掷物") + (draft.IsNew ? "  [新增]" : draft.IsDirty ? "  ●" : "");
            if (_throwableCategoryLabels.TryGetValue(draft, out var category)) category.Text = draft.Category;
        }
        RefreshSaveState();
        FilterList();
        _throwableReset.IsEnabled = CurrentThrowable.IsDirty;
        _throwableReset.Content = CurrentThrowable.IsNew ? "取消新增" : "撤销本投掷物修改";
        SetStatus(valid && !CurrentThrowable.IsDirty ? "投掷物模板已就绪，选择其他模板或保存修改。"
            : valid ? "修改暂存于当前窗口，点击“保存全部”写入游戏配置。" : "请修正红色字段后再保存。", !valid);
    }

    internal void RefreshSaveState()
    {
        int dirty = Drafts.Count(d => d.IsDirty) + ThrowableDrafts.Count(d => d.IsDirty);
        int invalid = Drafts.Count(d => !TryBuildDraft(d, out _, out _)) + ThrowableDrafts.Count(d => !TryBuildThrowableDraft(d, out _, out _));
        _save.IsEnabled = dirty > 0 && invalid == 0;
        _reset.IsEnabled = Current?.IsDirty ?? false;
        _throwableReset.IsEnabled = CurrentThrowable?.IsDirty ?? false;
        _dirtyText.Text = dirty > 0 ? $"{dirty} 个条目有未保存修改" : "所有修改已保存";
        _resourceCount.Text = ThrowablesMode
            ? $"{ThrowableDrafts.Count} 个投掷物模板 · {Drafts.Count} 个武器\n● 表示尚未保存的修改"
            : $"{Drafts.Count} 个武器 · {ThrowableDrafts.Count} 个投掷物模板\n● 表示尚未保存的修改";
        _resourceCount.Text += invalid > 0 ? $"\n{invalid} 个条目参数无效" : "";
    }

    private void RebuildThrowableList()
    {
        _binding = true;
        _list.Items.Clear(); _throwableListLabels.Clear(); _throwableCategoryLabels.Clear();
        foreach (var draft in ThrowableDrafts)
        {
            var text = Label(draft.Name, 14, Ink, FontWeights.SemiBold);
            text.TextWrapping = TextWrapping.Wrap;
            var category = Label(draft.Category, 10, Muted); category.TextWrapping = TextWrapping.Wrap;
            _throwableListLabels[draft] = text; _throwableCategoryLabels[draft] = category;
            var stack = new StackPanel { Margin = new Thickness(8) };
            stack.Children.Add(text); stack.Children.Add(category);
            _list.Items.Add(new ListBoxItem { Content = stack, Tag = draft, Margin = new Thickness(0,0,0,4) });
        }
        _binding = false;
    }
}
