using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FieldKit.Weapons;
using FieldKit.Items;

namespace WeaponWorkbench;

public sealed partial class MainWindow
{
    internal readonly Button AddWeaponButton = new() { IsEnabled = false };
    internal readonly Dictionary<string, TextBox> IdentityInputs = new();
    private readonly Dictionary<string, TextBlock> _identityMessages = new();
    internal Border IdentityCard { get; private set; } = null!;
    internal TextBox SearchInput => _search;
    internal IEnumerable<string> VisibleWeaponIds => _list.Items.Cast<ListBoxItem>()
        .Where(item => item.Visibility == Visibility.Visible && item.Tag is WeaponDraft)
        .Select(item => ((WeaponDraft)item.Tag).Id);

    private void BuildIdentityCard()
    {
        var content = new StackPanel();
        IdentityCard = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Margin = new Thickness(0,0,0,16), Child = content };
        _editor.Children.Add(IdentityCard);
        content.Children.Add(Label("基本信息", 16, Ink, FontWeights.SemiBold));
        var grid = new Grid { Margin = new Thickness(0,12,0,0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        content.Children.Add(grid);
        foreach (var (key, title, column, row) in new[] { ("name", "武器名称", 0, 0), ("category", "分类", 1, 0), ("id", "武器编号", 0, 1) })
        {
            var panel = new StackPanel { Margin = new Thickness(column == 1 ? 14 : 0, row == 1 ? 10 : 0, column == 0 ? 14 : 0, 0) };
            Grid.SetColumn(panel, column); Grid.SetRow(panel, row); grid.Children.Add(panel);
            panel.Children.Add(Label(title, 12, Ink));
            var input = new TextBox { Height = 36, Margin = new Thickness(0,6,0,0) };
            IdentityInputs[key] = input; panel.Children.Add(input);
            var message = Label("", 10, Muted); message.TextWrapping = TextWrapping.Wrap; message.Margin = new Thickness(0,5,0,0);
            _identityMessages[key] = message; panel.Children.Add(message);
            input.TextChanged += (_, _) => {
                if (_binding || Current == null) return;
                string text = input.Text.Trim();
                if (key == "id" && Current.IsNew) Current.Id = text;
                else if (key == "name") Current.Name = text;
                else if (key == "category") Current.Category = text;
                RefreshState();
            };
        }
    }

    internal void AddNewWeapon()
    {
        if (_catalog == null) return;
        SwitchMode(false);
        int number = 1;
        while (Drafts.Any(d => d.Id == $"weapon_{number}" || d.Saved.Id == $"weapon_{number}")) number++;
        var definition = new WeaponDefinition {
            Id = $"weapon_{number}", Name = "新武器", Category = "未分类", ScenePath = "",
            Stats = new WeaponStats {
                AmmoType = AmmoType.Medium, Price = 1000, Damage = 30, EnemySlowPercent = 0, KnockbackSpeed = 0,
                FireCooldown = .2, FireMode = FireMode.SemiAuto, MagazineCapacity = 30, ReloadDuration = 2.65,
                EquipDuration = .35, HolsterDuration = .25,
                MovementInertiaPercent = 10, TurnFollowSpeed = 10, ProjectileSpeed = 100,
                SpreadMin = 1, SpreadMax = 8, SpreadPerShot = .5, SpreadRecovery = 4 } };
        var draft = new WeaponDraft(definition, _project, isNew: true);
        Drafts.Add(draft);
        RebuildWeaponList();
        _search.Text = "";
        SelectById(draft.Id);
        _editor.BringIntoView(new Rect(0, 0, 1, 1));
        IdentityInputs["name"].Focus(); IdentityInputs["name"].SelectAll();
        SetStatus("已创建新武器草稿。填写名称与分类、选择模型后保存；可点击“取消新增”放弃。", false);
    }

    private void RebuildWeaponList()
    {
        _binding = true;
        _list.Items.Clear(); _listLabels.Clear(); _categoryLabels.Clear();
        foreach (var draft in Drafts)
        {
            var text = Label(draft.Name, 14, Ink, FontWeights.SemiBold);
            text.TextWrapping = TextWrapping.Wrap;
            var category = Label(draft.Category, 10, Muted); category.TextWrapping = TextWrapping.Wrap;
            _listLabels[draft] = text; _categoryLabels[draft] = category;
            var stack = new StackPanel { Margin = new Thickness(8) };
            stack.Children.Add(text); stack.Children.Add(category);
            _list.Items.Add(new ListBoxItem { Content = stack, Tag = draft, Margin = new Thickness(0,0,0,4) });
        }
        _binding = false;
    }

    private void RebuildCurrentList()
    {
        if (ThrowablesMode) RebuildThrowableList();
        else RebuildWeaponList();
    }

    internal void SwitchMode(bool throwables)
    {
        if (ThrowablesMode == throwables) return;
        ThrowablesMode = throwables;
        _search.Text = "";
        if (throwables) ModelPreview.SetScene(null);
        else ThrowablePreview.SetScene(null);
        RefreshModeButtons();
        RebuildCurrentList();
        _scroll.Content = throwables ? _throwableEditor : _editor;
        if (throwables)
        {
            _editor.IsEnabled = true;
            _throwableEditor.IsEnabled = ThrowableDrafts.Count > 0;
            if (CurrentThrowable != null) SelectThrowableById(CurrentThrowable.Id);
            else if (ThrowableDrafts.Count > 0) SelectThrowableById(ThrowableDrafts[0].Id);
            else { CurrentThrowable = null; RefreshSaveState(); }
        }
        else
        {
            _throwableEditor.IsEnabled = true;
            _editor.IsEnabled = true;
            if (Current != null) SelectById(Current.Id);
            else if (Drafts.Count > 0) SelectById(Drafts[0].Id);
        }
    }

    private void RefreshModeButtons()
    {
        StyleModeButton(_weaponModeButton, !ThrowablesMode);
        StyleModeButton(_throwableModeButton, ThrowablesMode);
        if (_sidebarTitle != null) _sidebarTitle.Text = ThrowablesMode ? "投掷物模板" : "武器资源";
        AddWeaponButton.Content = ThrowablesMode ? "＋ 新增投掷物" : "＋ 新增武器";
        AddWeaponButton.ToolTip = ThrowablesMode ? "新增投掷物草稿（Ctrl+N）" : "新增武器草稿（Ctrl+N）";
        if (_searchPlaceholder != null)
            _searchPlaceholder.Text = ThrowablesMode ? "搜索投掷物名称、分类或 ID" : "搜索名称、分类或 ID";
        _search.ToolTip = ThrowablesMode ? "按名称、分类或 ID 搜索投掷物模板" : "按名称、分类或 ID 搜索";
    }

    private void StyleModeButton(Button button, bool active)
    {
        button.Background = active ? Teal : Brushes.White;
        button.Foreground = active ? Brushes.White : Ink;
        button.BorderBrush = active ? Teal : Line;
    }

    private bool TryBuildDraft(WeaponDraft draft, out WeaponStats stats, out Dictionary<string, string> errors)
    {
        draft.TryBuild(out stats, out errors);
        if (Drafts.Count(other => string.Equals(other.Id, draft.Id, StringComparison.Ordinal)) > 1)
            errors["id"] = "编号已被其他武器使用，请换一个。";
        return errors.Count == 0;
    }

    private void RefreshIdentityState(Dictionary<string, string> errors)
    {
        _title.Text = Current!.Name.Length > 0 ? Current.Name : "未命名武器";
        _subtitle.Text = $"{Current.Category}   /   {Current.Id}" + (Current.IsNew ? "   /   尚未保存" : "");
        foreach (var (key, input) in IdentityInputs)
        {
            bool bad = errors.TryGetValue(key, out string? error);
            input.BorderBrush = bad ? Error : Line;
            _identityMessages[key].Foreground = bad ? Error : Muted;
            _identityMessages[key].Text = bad ? error! : key == "id"
                ? Current.IsNew ? "自动生成，可修改；必须唯一，保存后固定。" : "已保存的编号固定，避免影响游戏引用。"
                : key == "name" ? "显示在游戏商店和背包中。" : "例如：突击步枪、手枪、霰弹枪。";
        }
    }
}
