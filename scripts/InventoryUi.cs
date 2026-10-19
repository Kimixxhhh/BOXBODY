using Godot;
using System;
using System.Collections.Generic;
using FieldKit.Items;

// Weapon slots and individual ammunition packs reflect the player's live inventory.
public partial class InventoryUi : CanvasLayer
{
    public PlayerController Player { get; set; } = null!;
    public bool IsOpen => _root.Visible;
    public int SlotCount => AmmoBackpack.Capacity;
    public event Action<bool>? OpenChanged;
    private Control _root = null!;
    private Control _layout = null!;
    private Label _hint = null!;
    private Button _close = null!;
    private readonly List<WeaponModelPreview> _previews = new();
    private readonly List<Panel> _weaponCards = new();
    private Label _money = null!;
    private Label _storageCount = null!;
    private readonly Label?[] _magazines = new Label?[PlayerController.WeaponSlotCount];
    private readonly Button[] _itemSlots = new Button[AmmoBackpack.Capacity];
    private readonly Label[] _itemNames = new Label[AmmoBackpack.Capacity];
    private readonly Label[] _itemAmounts = new Label[AmmoBackpack.Capacity];
    private readonly InventoryIcon[] _itemIcons = new InventoryIcon[AmmoBackpack.Capacity];
    private readonly TextureRect[] _itemTextures = new TextureRect[AmmoBackpack.Capacity];
    private readonly Dictionary<string, WeaponModelPreview> _itemPreviews = new();
    private AmmoItem? _draggedItem;
    private int _dragSource = -1;
    private int _dragToken;
    private static readonly Color PanelColor = new(0.08f, 0.1f, 0.105f, 0.97f);
    private static readonly Color WellColor = new(0.045f, 0.06f, 0.065f, 1);
    private static readonly Color Border = new(0.28f, 0.34f, 0.33f);
    private static readonly Color Muted = new(0.6f, 0.67f, 0.65f);
    private static readonly Color Accent = new(0.72f, 0.79f, 0.64f);
    private const string DefaultHint = "拖动物品调整位置 · 放到已有物品上可交换";

    public override void _Ready()
    {
        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.Theme = new Theme { DefaultFont = new SystemFont {
            FontNames = new[] { "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif" }
        }, DefaultFontSize = 16 };
        var dim = new ColorRect { Color = new Color(0.025f, 0.04f, 0.045f, 0.72f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _layout = new Control { Name = "Layout", Size = new Vector2(1120, 660), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(_layout);
        Text(_layout, "背包", 0, 0, 200, 42, 30);
        Text(_layout, "FIELD KIT  /  装备与携行", 82, 11, 400, 28, 14, Muted);
        _money = Text(_layout, "", 620, 8, 360, 32, 19, Accent, HorizontalAlignment.Right);
        _money.Name = "Money";
        _close = new Button { Name = "Close", Text = "关闭  ×", FocusMode = Control.FocusModeEnum.None };
        Place(_layout, _close, 1010, 4, 110, 38);
        ButtonStyle(_close, false);
        _close.Pressed += Close;
        Rule(_layout, 0, 54, 1120, Border);
        RefreshWeapons();
        Player.LoadoutChanged += RefreshWeapons;
        BuildStorage();
        foreach (var item in ItemCatalog.Ammunition)
        {
            if (string.IsNullOrEmpty(item.IconScenePath)) continue;
            var preview = new WeaponModelPreview { Name = "ItemPreview_" + item.Id };
            AddChild(preview);
            preview.Initialize(item.IconScenePath, new Vector2I(384, 256), new Vector3(0, 1.6f, 1));
            _itemPreviews[item.Id] = preview;
        }
        _hint = Text(_layout, DefaultHint, 100, 627, 700, 28, 14, Muted);
        RefreshContents();
        Player.InventoryChanged += RefreshContents;
        Text(_layout, "TAB / ESC  关闭", 880, 627, 240, 28, 14, Muted, HorizontalAlignment.Right);
        _root.Resized += Fit;
        Fit();
        _root.Hide();
    }

    private void RefreshWeapons()
    {
        foreach (var card in _weaponCards)
        {
            _layout.RemoveChild(card);
            card.QueueFree();
        }
        _weaponCards.Clear();
        _previews.Clear();
        _money.Text = $"资金  {Player.Money}$";
        for (int slot = 0; slot < PlayerController.WeaponSlotCount; slot++) BuildWeaponCard(slot * 580, slot);
    }

    public override void _ExitTree()
    {
        Player.LoadoutChanged -= RefreshWeapons;
        Player.InventoryChanged -= RefreshContents;
    }

    private void RefreshContents()
    {
        if (_draggedItem != null && !ReferenceEquals(Player.Backpack.Slots[_dragSource], _draggedItem))
            GetViewport().GuiCancelDrag();
        _money.Text = $"资金  {Player.Money}$";
        _storageCount.Text = $"{Player.Backpack.Count} / {SlotCount}";
        for (int i = 0; i < SlotCount; i++)
        {
            var item = Player.Backpack.Slots[i];
            _itemNames[i].Text = item?.Definition.Name ?? "";
            _itemAmounts[i].Text = item == null ? "" : $"{item.RemainingAmmo} / {item.TotalAmmo}";
            _itemIcons[i].Visible = item == null;
            _itemTextures[i].Texture = item != null && _itemPreviews.TryGetValue(item.Definition.Id, out var preview) ? preview.GetTexture() : null;
            _itemTextures[i].Visible = item != null;
            _itemSlots[i].TooltipText = StorageHint(i);
        }
        for (int i = 0; i < PlayerController.WeaponSlotCount; i++)
            if (_magazines[i] is { } label) label.Text = MagazineText(i);
        _hint.Text = DefaultHint;
    }

    private string MagazineText(int slot) => Player.WeaponSlots[slot] is { } weapon
        ? $"{Player.MagazineAmmo(slot)} / {weapon.Stats.MagazineCapacity}" : "— / —";

    private string StorageHint(int slot) => Player.Backpack.Slots[slot] is { } item
        ? $"{item.Definition.Name} · 总弹药量 {item.TotalAmmo} 发 · 剩余弹药量 {item.RemainingAmmo} 发"
        : $"物品栏 {slot + 1:00} · 空栏位";

    private void BuildWeaponCard(float x, int index)
    {
        var weapon = Player.WeaponSlots[index];
        bool equipped = weapon != null;
        bool active = Player.ActiveWeaponSlot == index;
        var card = new Panel { Name = index == 0 ? "PrimaryWeapon" : "SecondaryWeapon" };
        Place(_layout, card, x, 76, 540, 280);
        _weaponCards.Add(card);
        card.AddThemeStyleboxOverride("panel", Box(PanelColor, Border, 10));
        var badge = new Panel();
        Place(card, badge, 20, 18, 28, 28);
        badge.AddThemeStyleboxOverride("panel", Box(active ? Accent : Border, Colors.Transparent, 4));
        Text(badge, (index + 1).ToString(), 0, 0, 28, 28, 17,
            active ? WellColor : Colors.White, HorizontalAlignment.Center);
        Text(card, "武器", 58, 18, 100, 28, 17);
        Text(card, active ? "持用中" : equipped ? "已装备" : "未装备", 400, 18, 118, 28, 13,
            equipped ? Accent : Muted, HorizontalAlignment.Right);
        Rule(card, 20, 57, 500, new Color(0.2f, 0.25f, 0.25f));
        if (weapon != null)
        {
            var preview = new WeaponModelPreview { Name = "WeaponPreview" };
            card.AddChild(preview);
            preview.Initialize(weapon.ScenePath);
            preview.RenderTargetUpdateMode = IsOpen ? SubViewport.UpdateMode.Once : SubViewport.UpdateMode.Disabled;
            _previews.Add(preview);
            var display = new TextureRect { Name = "WeaponModel", Texture = preview.GetTexture(),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            Place(card, display, 20, 65, 500, 100);
        }
        else
        {
            var icon = new InventoryIcon { Kind = InventoryIcon.Symbol.Rifle, Modulate = new Color(1, 1, 1, 0.28f) };
            Place(card, icon, 163, 81, 214, 84);
        }
        var title = Text(card, weapon != null ? $"{weapon.Name} {weapon.Category}" : "空武器栏", 22, 164, 496, 32, 23,
            equipped ? Colors.White : Muted);
        title.Name = "WeaponName";
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Text(card, equipped ? $"武器槽位 {index + 1}  /  {ItemCatalog.AmmoName(weapon!.Stats.AmmoType)}" : "前往武器箱购买后装备", 22, 196, 370, 23, 13, Muted);
        Text(card, "配件", 452, 198, 66, 20, 12, Muted, HorizontalAlignment.Right);
        string[] names = { "枪口", "弹匣", "瞄具", "枪托", "握把" };
        var symbols = new[] { InventoryIcon.Symbol.Muzzle, InventoryIcon.Symbol.Magazine,
            InventoryIcon.Symbol.Scope, InventoryIcon.Symbol.Stock, InventoryIcon.Symbol.Grip };
        for (int i = 0; i < names.Length; i++)
        {
            var slot = Slot(card, $"Attachment{i + 1}", 22 + i * 63, 230, 54, 36,
                $"武器槽位 {index + 1} · {names[i]}：未安装");
            var icon = new InventoryIcon { Kind = symbols[i], Modulate = new Color(1, 1, 1, 0.55f) };
            Place(slot, icon, 10, 5, 34, 26);
        }
        _magazines[index] = Text(card, MagazineText(index), 360, 232, 158, 30, 22, equipped ? Accent : Muted, HorizontalAlignment.Right);
        _magazines[index]!.Name = "MagazineAmmo";
    }

    private void BuildStorage()
    {
        Text(_layout, "随身物品", 100, 382, 300, 27, 18);
        _storageCount = Text(_layout, "", 820, 382, 200, 27, 15, Muted, HorizontalAlignment.Right);
        _storageCount.Name = "StorageCount";
        var tray = new Panel { Name = "Storage" };
        Place(_layout, tray, 100, 420, 920, 196);
        tray.AddThemeStyleboxOverride("panel", Box(PanelColor, Border, 10));
        for (int i = 0; i < SlotCount; i++)
        {
            int index = i;
            var slot = Slot(tray, $"Slot{i + 1:00}", 12 + i % 8 * 113, 12 + i / 8 * 91, 105, 81,
                "", () => StorageHint(index));
            _itemSlots[i] = slot;
            slot.SetDragForwarding(
                Callable.From<Vector2, Variant>(_ => BeginItemDrag(index)),
                Callable.From<Vector2, Variant, bool>((_, data) => CanDropItem(index, data)),
                Callable.From<Vector2, Variant>((_, data) => DropItem(index, data)));
            Text(slot, (i + 1).ToString("00"), 8, 4, 30, 17, 10, new Color(0.38f, 0.44f, 0.42f));
            var icon = new InventoryIcon { Kind = InventoryIcon.Symbol.Plus };
            Place(slot, icon, 43, 32, 18, 18);
            _itemIcons[i] = icon;
            var texture = new TextureRect { Name = "ItemIcon", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
            Place(slot, texture, 18, 3, 70, 40);
            _itemTextures[i] = texture;
            _itemNames[i] = Text(slot, "", 4, 42, 97, 19, 13, Colors.White, HorizontalAlignment.Center);
            _itemNames[i].Name = "ItemName";
            _itemAmounts[i] = Text(slot, "", 4, 61, 97, 18, 13, Accent, HorizontalAlignment.Center);
            _itemAmounts[i].Name = "AmmoAmount";
        }
    }

    private Variant BeginItemDrag(int slot)
    {
        if (!IsOpen || Player.Backpack.Slots[slot] is not { } item) return default;
        _dragSource = slot;
        _draggedItem = item;
        _dragToken++;
        _itemSlots[slot].Modulate = new Color(1, 1, 1, .45f);
        _hint.Text = "拖至目标物品栏后松开 · 放到背包外取消";
        var preview = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        var card = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Scale = _layout.Scale };
        Place(preview, card, 14, 14, 105, 81);
        card.AddThemeStyleboxOverride("panel", Box(PanelColor, Accent, 4));
        Place(card, new TextureRect { Texture = _itemTextures[slot].Texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore }, 18, 3, 70, 40);
        Text(card, item.Definition.Name, 4, 42, 97, 19, 13, Colors.White, HorizontalAlignment.Center);
        Text(card, $"{item.RemainingAmmo} / {item.TotalAmmo}", 4, 61, 97, 18, 13, Accent, HorizontalAlignment.Center);
        preview.Theme = _root.Theme;
        _itemSlots[slot].SetDragPreview(preview);
        return new Godot.Collections.Dictionary { { "inventory", this }, { "token", _dragToken } };
    }

    private bool CanDropItem(int target, Variant data)
    {
        if (!IsOpen || _draggedItem == null || data.VariantType != Variant.Type.Dictionary) return false;
        var payload = data.AsGodotDictionary();
        return target >= 0 && target < SlotCount && payload.TryGetValue("inventory", out var owner) &&
            owner.VariantType == Variant.Type.Object && owner.AsGodotObject() == this &&
            payload.TryGetValue("token", out var token) && token.VariantType == Variant.Type.Int && token.AsInt32() == _dragToken &&
            ReferenceEquals(Player.Backpack.Slots[_dragSource], _draggedItem);
    }

    private void DropItem(int target, Variant data)
    {
        if (!CanDropItem(target, data)) return;
        int source = _dragSource;
        var item = _draggedItem!;
        EndItemDrag();
        Player.TryMoveBackpackItem(source, target, item);
    }

    private void EndItemDrag()
    {
        if (_dragSource >= 0) _itemSlots[_dragSource].Modulate = Colors.White;
        _dragSource = -1;
        _draggedItem = null;
        _hint.Text = DefaultHint;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && _draggedItem != null) EndItemDrag();
    }

    private Button Slot(Control parent, string name, float x, float y, float width, float height, string hint, Func<string>? liveHint = null)
    {
        var button = new Button { Name = name, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        Place(parent, button, x, y, width, height);
        ButtonStyle(button, true);
        button.MouseEntered += () => _hint.Text = liveHint?.Invoke() ?? hint;
        button.MouseExited += () => _hint.Text = DefaultHint;
        button.Pressed += () => _hint.Text = liveHint?.Invoke() ?? hint;
        return button;
    }

    private static void ButtonStyle(Button button, bool inset)
    {
        button.AddThemeStyleboxOverride("normal", Box(inset ? WellColor : PanelColor, Border, 4));
        button.AddThemeStyleboxOverride("hover", Box(new Color(0.13f, 0.17f, 0.16f), Accent, 4));
        button.AddThemeStyleboxOverride("pressed", Box(new Color(0.18f, 0.22f, 0.19f), Accent, 4));
    }

    private static StyleBoxFlat Box(Color color, Color border, int radius) => new() {
        BgColor = color, BorderColor = border,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
    };

    private static void Place(Node parent, Control child, float x, float y, float width, float height)
    {
        parent.AddChild(child);
        child.Position = new Vector2(x, y);
        child.Size = new Vector2(width, height);
    }

    private static Label Text(Control parent, string text, float x, float y, float width, float height,
        int size, Color? color = null, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? new Color(0.92f, 0.94f, 0.93f));
        Place(parent, label, x, y, width, height);
        return label;
    }

    private static void Rule(Control parent, float x, float y, float width, Color color) =>
        Place(parent, new ColorRect { Color = color, MouseFilter = Control.MouseFilterEnum.Ignore }, x, y, width, 1);

    private void Fit()
    {
        Vector2 available = _root.Size;
        float scale = Mathf.Min((available.X - 64) / 1120, (available.Y - 48) / 660);
        scale = Mathf.Max(0.1f, scale);
        _layout.Scale = Vector2.One * scale;
        _layout.Position = (available - _layout.Size * scale) * 0.5f;
    }

    public void Open()
    {
        if (IsOpen) return;
        _hint.Text = DefaultHint;
        _root.Show();
        Fit();
        foreach (var preview in _previews) preview.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        foreach (var preview in _itemPreviews.Values) preview.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        OpenChanged?.Invoke(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        if (_draggedItem != null)
        {
            GetViewport().GuiCancelDrag();
            EndItemDrag();
        }
        _root.Hide();
        foreach (var preview in _previews) preview.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        foreach (var preview in _itemPreviews.Values) preview.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        OpenChanged?.Invoke(false);
    }
}

