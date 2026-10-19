using Godot;

// Resolution-independent line icons match the prototype's restrained panel style.
public partial class InventoryIcon : Control
{
    public enum Symbol { Plus, Rifle, Muzzle, Magazine, Scope, Stock, Grip }
    public Symbol Kind { get; set; }
    private static readonly Color Ink = new(0.60f, 0.67f, 0.65f);
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        Vector2 P(float x, float y) => new(x * Size.X, y * Size.Y);
        void Line(float x, float y, float a, float b) => DrawLine(P(x, y), P(a, b), Ink, 1.5f, true);
        void Outline(params Vector2[] points)
        {
            for (int i = 0; i < points.Length; i++)
                DrawLine(P(points[i].X, points[i].Y), P(points[(i + 1) % points.Length].X,
                    points[(i + 1) % points.Length].Y), Ink, 1.5f, true);
        }
        switch (Kind)
        {
            case Symbol.Plus:
                Line(.25f, .5f, .75f, .5f); Line(.5f, .25f, .5f, .75f); break;
            case Symbol.Muzzle:
                Outline(new(.12f,.36f),new(.88f,.36f),new(.88f,.64f),new(.12f,.64f));
                Line(.28f,.37f,.28f,.63f); Line(.7f,.37f,.7f,.63f); break;
            case Symbol.Magazine:
                Outline(new(.4f,.14f),new(.74f,.24f),new(.53f,.88f),new(.2f,.77f));
                Line(.47f,.3f,.33f,.7f); Line(.59f,.34f,.45f,.74f); break;
            case Symbol.Scope:
                DrawCircle(P(.5f,.47f),Size.Y*.28f,Ink,false,1.5f,true);
                Line(.5f,.25f,.5f,.68f); Line(.35f,.47f,.65f,.47f); Line(.2f,.86f,.8f,.86f); break;
            case Symbol.Stock:
                Outline(new(.12f,.3f),new(.86f,.3f),new(.86f,.8f),new(.66f,.8f),new(.5f,.52f),new(.12f,.52f)); break;
            case Symbol.Grip:
                Outline(new(.25f,.18f),new(.75f,.18f),new(.75f,.34f),new(.61f,.34f),new(.61f,.84f),new(.39f,.84f),new(.39f,.34f),new(.25f,.34f)); break;
            case Symbol.Rifle:
                Outline(new(.05f,.37f),new(.28f,.37f),new(.28f,.3f),new(.63f,.3f),new(.63f,.4f),new(.94f,.4f),new(.94f,.48f),new(.62f,.48f),new(.59f,.57f),new(.52f,.57f),new(.57f,.82f),new(.47f,.84f),new(.39f,.57f),new(.32f,.57f),new(.29f,.73f),new(.21f,.7f),new(.24f,.5f),new(.05f,.61f));
                Line(.38f,.3f,.38f,.21f); Line(.34f,.21f,.46f,.21f); break;
        }
    }
}
