using Godot;
using System.Linq;

public partial class Prototype : Node3D
{
    public PlayerController Player { get; private set; } = null!;
    public FollowCamera Camera { get; private set; } = null!;
    public WeaponCrate Crate { get; private set; } = null!;
    public InteractionUi Ui { get; private set; } = null!;
    public InventoryUi Inventory { get; private set; } = null!;
    public FieldKit.Weapons.WeaponCatalog Weapons { get; private set; } = null!;

    public override void _EnterTree() => Weapons = WeaponDatabase.Load();

    public override void _Ready()
    {
        Player = GetNode<PlayerController>("Player");
        Camera = GetNode<FollowCamera>("Camera");
        Crate = GetNode<WeaponCrate>("WeaponCrate");
        Ui = GetNode<InteractionUi>("UI");
        Player.Camera = Camera;
        Camera.Target = Player;
        Camera.SnapToTarget();
        Inventory = new InventoryUi { Name = "Inventory", Layer = 20, Player = Player };
        AddChild(Inventory);
        Ui.Initialize(Player, Weapons);
        Ui.OpenChanged += _ => UpdateInputBlock();
        Inventory.OpenChanged += _ => UpdateInputBlock();
        GetNode<CrateOutline>("CrateOutline").Initialize(Camera, Crate.Visual, Player.Visual);
        Player.LoadoutChanged += GetNode<CrateOutline>("CrateOutline").RefreshPlayerEquipment;
        if (OS.GetCmdlineUserArgs().Contains("--smoke-test"))
            AddChild(new PrototypeSmokeTest { Name = "SmokeTest" });
    }

    public override void _Process(double delta)
    {
        if (Ui.IsOpen && !Crate.IsInReach(Player)) Ui.Close();
        Ui.ShowPrompt(!Inventory.IsOpen && Crate.IsInReach(Player) && !Camera.IsPositionBehind(Crate.PromptPosition), Camera.UnprojectPosition(Crate.PromptPosition));
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("inventory") && !@event.IsEcho())
        {
            if (Inventory.IsOpen) Inventory.Close();
            else
            {
                Ui.Close();
                Inventory.Open();
            }
            GetViewport().SetInputAsHandled();
        }
        else if (Inventory.IsOpen)
        {
            if (@event.IsActionPressed("ui_cancel")) Inventory.Close();
            // Consume gameplay keys, including Tab repeats, before GUI focus navigation.
            if (@event is InputEventKey) GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("interact") && !@event.IsEcho())
        {
            if (Ui.IsOpen) Ui.Close();
            else if (Crate.IsInReach(Player)) Ui.Open();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ui_cancel") && Ui.IsOpen)
        {
            Ui.Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateInputBlock()
    {
        Player.InputBlocked = Ui.IsOpen || Inventory.IsOpen;
        Ui.GetNode<Control>("Root/Instructions").Visible = !Inventory.IsOpen;
        Ui.GetNode<Control>("Root/Money").Visible = !Inventory.IsOpen;
        if (Inventory.IsOpen) Ui.GetNode<Control>("Root/Feedback").Hide();
    }
}
