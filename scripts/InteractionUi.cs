using Godot;
using FieldKit.Weapons;
using FieldKit.Items;
using System.Linq;

public partial class InteractionUi : CanvasLayer
{
    public bool IsOpen => _modal.Visible;
    public ItemList Items { get; private set; } = null!;
    public event System.Action<bool>? OpenChanged;
    private Control _modal = null!;
    private PanelContainer _prompt = null!;
    private Button _close = null!;
    public Button BuyButton { get; private set; } = null!;
    private Label _wallet = null!, _hudMoney = null!, _status = null!;
    private PanelContainer _feedback = null!;
    private Label _feedbackText = null!;
    private double _feedbackTime;
    private PlayerController _player = null!;
    private WeaponCatalog _catalog = null!;
    private const string ColumnPath = "Root/Modal/Center/Panel/Margin/Column";

    public override void _Ready()
    {
        _modal = GetNode<Control>("Root/Modal");
        _prompt = GetNode<PanelContainer>("Root/Prompt");
        _close = GetNode<Button>("Root/Modal/Center/Panel/Margin/Column/Header/Close");
        Items = GetNode<ItemList>("Root/Modal/Center/Panel/Margin/Column/Items");
        BuyButton = GetNode<Button>(ColumnPath + "/Buy");
        _wallet = GetNode<Label>(ColumnPath + "/Wallet");
        _hudMoney = GetNode<Label>("Root/Money/Text");
        _status = GetNode<Label>(ColumnPath + "/Status");
        _feedback = GetNode<PanelContainer>("Root/Feedback");
        _feedbackText = GetNode<Label>("Root/Feedback/Text");
        _feedback.Hide();
        Items.ItemSelected += _ => Refresh();
        BuyButton.Pressed += PurchaseSelected;
        _close.Pressed += Close;
        _modal.Hide();
        _prompt.Hide();
    }

    public void Initialize(PlayerController player, WeaponCatalog catalog)
    {
        _player = player;
        _catalog = catalog;
        foreach (var weapon in catalog.Weapons)
        {
            int index = Items.AddItem($"{weapon.Name}   ·   {weapon.Category}     {weapon.Stats.Price}$");
            Items.SetItemTooltip(index, $"{weapon.Name}\n价格：{weapon.Stats.Price}$\n购买后自动装备至空闲武器槽位");
        }
        foreach (var item in ItemCatalog.Ammunition)
        {
            int index = Items.AddItem($"{item.Name}   ·   {item.TotalAmmo} 发     {item.Price}$");
            Items.SetItemTooltip(index, $"{item.Name}\n总弹药量：{item.TotalAmmo} 发\n剩余弹药量：{item.TotalAmmo} 发\n价格：{item.Price}$\n每份占用一个背包物品槽位");
        }
        if (Items.ItemCount > 0) Items.Select(0);
        _player.LoadoutChanged += Refresh;
        _player.InventoryChanged += Refresh;
        _player.ReloadFeedback += ShowFeedback;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_player != null)
        {
            _player.LoadoutChanged -= Refresh;
            _player.InventoryChanged -= Refresh;
            _player.ReloadFeedback -= ShowFeedback;
        }
    }

    private void ShowFeedback(string message)
    {
        _feedbackText.Text = message;
        _feedbackTime = 3;
        _feedback.Visible = !GetParent<Prototype>().Inventory.IsOpen;
    }

    public override void _Process(double delta)
    {
        if (_feedbackTime <= 0) return;
        _feedbackTime -= delta;
        if (_feedbackTime <= 0) _feedback.Hide();
    }

    private AmmoDefinition? SelectedAmmo()
    {
        var selected = Items.GetSelectedItems();
        int index = selected.Length == 1 ? selected[0] - _catalog.Weapons.Count : -1;
        return index >= 0 && index < ItemCatalog.Ammunition.Count ? ItemCatalog.Ammunition[index] : null;
    }

    private WeaponDefinition? SelectedWeapon()
    {
        var selected = Items.GetSelectedItems();
        return selected.Length == 1 && selected[0] >= 0 && selected[0] < _catalog.Weapons.Count
            ? _catalog.Weapons[selected[0]] : null;
    }

    private void Refresh()
    {
        _hudMoney.Text = $"资金  {_player.Money}$";
        if (_player.EquippedWeapon is { } held)
            _hudMoney.Text += $"\n{held.Name}  {_player.MagazineAmmo(_player.ActiveWeaponSlot)} / {held.Stats.MagazineCapacity}   ·   备弹 {_player.Backpack.Available(held.Stats.AmmoType)}";
        int occupied = _player.WeaponSlots.Count(w => w != null);
        _wallet.Text = $"资金 {_player.Money}$   ·   武器 {occupied} / 2   ·   物品 {_player.Backpack.Count} / {AmmoBackpack.Capacity}";
        if (SelectedAmmo() is { } ammo)
        {
            string? block = _player.PurchaseBlockReason(ammo);
            BuyButton.Disabled = block != null;
            BuyButton.Text = $"购买并放入背包  ·  {ammo.Price}$";
            _status.Text = block ?? $"每份 {ammo.TotalAmmo} 发 · 占用一个物品槽位";
            return;
        }
        var selected = SelectedWeapon();
        string? reason = selected == null ? "请选择要购买的武器或弹药" : _player.PurchaseBlockReason(selected);
        BuyButton.Disabled = reason != null;
        BuyButton.Text = selected == null ? "购买并装备" : $"购买并装备  ·  {selected.Stats.Price}$";
        _status.Text = reason ?? $"购买后自动装备至武器槽位 {occupied + 1}";
    }

    private void PurchaseSelected()
    {
        string message;
        if (SelectedAmmo() is { } ammo) _player.TryPurchaseAmmo(ammo.Id, out message);
        else if (SelectedWeapon() is { } weapon) _player.TryPurchaseWeapon(weapon.Id, out message);
        else return;
        Refresh();
        _status.Text = message;
    }

    public void ShowPrompt(bool reachable, Vector2 position)
    {
        _prompt.Visible = reachable && !IsOpen;
        _prompt.Position = position - new Vector2(_prompt.Size.X * 0.5f, _prompt.Size.Y);
    }

    public void Open()
    {
        if (IsOpen) return;
        Refresh();
        _modal.Show();
        _prompt.Hide();
        _close.GrabFocus();
        OpenChanged?.Invoke(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        _modal.Hide();
        _close.ReleaseFocus();
        OpenChanged?.Invoke(false);
    }
}
