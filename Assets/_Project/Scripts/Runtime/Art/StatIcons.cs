using UnityEngine;

namespace RollOfLegends
{
    public enum StatIcon
    {
        Heart,  // hero health
        Sword,  // attack against heroes
        Spear,  // siege attack against bases
        Shield, // defense
    }

    /// <summary>Small stat icons for the badges, drawn in code with <see cref="Painter"/> (no image files).</summary>
    public static class StatIcons
    {
        private const int Size = 64;
        private static readonly Sprite[] Cache = new Sprite[4];

        private static readonly Color Outline = new Color(0.05f, 0.05f, 0.1f, 0.95f);
        private static readonly Color Steel = Painter.Hex("#e3ecf5");
        private static readonly Color SteelDark = Painter.Hex("#9fb1c4");
        private static readonly Color Gold = Painter.Hex("#ffc93c");
        private static readonly Color Wood = Painter.Hex("#a8693a");

        public static Sprite Get(StatIcon icon)
        {
            int index = (int)icon;
            if (Cache[index] == null)
            {
                var painter = new Painter(Size, Size);
                Draw(painter, icon);
                Texture2D texture = painter.ToTexture();
                Cache[index] = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            }
            return Cache[index];
        }

        private static void Draw(Painter painter, StatIcon icon)
        {
            switch (icon)
            {
                case StatIcon.Heart:
                {
                    Painter.Shape heart = Painter.Union(
                        Painter.Circle(new Vector2(22, 40), 13),
                        Painter.Circle(new Vector2(42, 40), 13),
                        Painter.Polygon(new Vector2(10.5f, 34), new Vector2(32, 8), new Vector2(53.5f, 34), new Vector2(32, 42)));
                    painter.Fill(Painter.Outline(heart, 7f), Outline);
                    painter.Fill(heart, Painter.VerticalGradient(8, Painter.Hex("#c81e3a"), 54, Painter.Hex("#ff6b7d")));
                    painter.Fill(Painter.Ellipse(new Vector2(20, 44), new Vector2(5, 3.5f), 35f), new Color(1f, 1f, 1f, 0.6f));
                    break;
                }

                case StatIcon.Sword:
                {
                    // Diagonal blade from bottom-left (grip) to top-right (tip).
                    Painter.Shape blade = Painter.Polygon(new Vector2(22, 26), new Vector2(26, 22), new Vector2(54, 50), new Vector2(57, 57), new Vector2(50, 54));
                    Painter.Shape guard = Painter.Line(new Vector2(14, 30), new Vector2(30, 14), 7f);
                    Painter.Shape grip = Painter.Line(new Vector2(10, 10), new Vector2(21, 21), 6f);
                    Painter.Shape pommel = Painter.Circle(new Vector2(9, 9), 4.5f);
                    Painter.Shape all = Painter.Union(blade, guard, grip, pommel);
                    painter.Fill(Painter.Outline(all, 6f), Outline);
                    painter.Fill(blade, Painter.VerticalGradient(20, SteelDark, 56, Steel));
                    painter.Fill(Painter.Line(new Vector2(26, 26), new Vector2(53, 53), 1.6f), Color.white);
                    painter.Fill(grip, Wood);
                    painter.Fill(guard, Gold);
                    painter.Fill(pommel, Gold);
                    break;
                }

                case StatIcon.Spear:
                {
                    // Long wooden shaft with a leaf-shaped steel head pointing to the top-right.
                    Painter.Shape shaft = Painter.Line(new Vector2(8, 8), new Vector2(42, 42), 5.5f);
                    Painter.Shape head = Painter.Polygon(new Vector2(38, 46), new Vector2(40, 36), new Vector2(46, 34), new Vector2(59, 59));
                    Painter.Shape binding = Painter.Line(new Vector2(36, 36), new Vector2(40, 40), 8.5f);
                    Painter.Shape all = Painter.Union(shaft, head, binding);
                    painter.Fill(Painter.Outline(all, 6f), Outline);
                    painter.Fill(shaft, Wood);
                    painter.Fill(head, Painter.VerticalGradient(34, SteelDark, 59, Steel));
                    painter.Fill(binding, Painter.Hex("#d23c3c"));
                    break;
                }

                default: // shield
                {
                    Painter.Shape shield = Painter.Union(
                        Painter.RoundedRect(new Rect(12, 26, 40, 30), 6f),
                        Painter.Polygon(new Vector2(12, 30), new Vector2(52, 30), new Vector2(32, 6)));
                    painter.Fill(Painter.Outline(shield, 7f), Outline);
                    painter.Fill(shield, Gold);
                    Painter.Shape inner = Painter.Union(
                        Painter.RoundedRect(new Rect(17, 31, 30, 21), 4f),
                        Painter.Polygon(new Vector2(17, 34), new Vector2(47, 34), new Vector2(32, 13)));
                    painter.Fill(inner, Painter.VerticalGradient(12, Painter.Hex("#2556b8"), 52, Painter.Hex("#5a9bff")));
                    painter.Fill(Painter.Line(new Vector2(32, 16), new Vector2(32, 48), 3f), Gold);
                    break;
                }
            }
        }
    }
}
