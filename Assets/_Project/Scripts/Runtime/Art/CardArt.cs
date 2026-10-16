using System.Collections.Generic;
using UnityEngine;
using static RollOfLegends.Painter;

namespace RollOfLegends
{
    /// <summary>
    /// Themed card faces, drawn in code the first time they are needed:
    /// ODD = Sun of Ra (Egypt), EVEN = Zeus (Greece), FIXED = Dragon, RANDOM = World Tree,
    /// DOUBLE (rare) = Twin Moons.
    /// The card's title and numbers are UI text on top of this picture (see CardView).
    /// </summary>
    public static class CardArt
    {
        public const int Width = 256;
        public const int Height = 352;

        // Where the text goes on the card, as fractions of the card size (used by the scene builder).
        public static readonly Rect TitleArea = new Rect(28f / Width, 290f / Height, 200f / Width, 44f / Height);
        public static readonly Rect ValueArea = new Rect(40f / Width, 22f / Height, 176f / Width, 64f / Height);

        private static readonly Dictionary<DiceType, Sprite> Cache = new Dictionary<DiceType, Sprite>();
        private static Sprite _glow;

        public static Sprite Get(DiceType type)
        {
            if (!Cache.TryGetValue(type, out Sprite sprite))
            {
                Texture2D texture = Draw(type);
                sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = $"Card_{type}";
                Cache[type] = sprite;
            }
            return sprite;
        }

        /// <summary>A soft white halo around a card, tinted when the card is selected.</summary>
        public static Sprite Glow
        {
            get
            {
                if (_glow == null)
                {
                    const int margin = 28;
                    var painter = new Painter(Width + margin * 2, Height + margin * 2);
                    painter.Glow(RoundedRect(new Rect(margin, margin, Width, Height), 26f), Color.white, margin);
                    Texture2D texture = painter.ToTexture();
                    _glow = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
                return _glow;
            }
        }

        public static string Title(DiceType type)
        {
            switch (type)
            {
                case DiceType.Odd: return "ODD";
                case DiceType.Even: return "EVEN";
                case DiceType.Fixed: return "FIXED";
                case DiceType.Double: return "DOUBLE";
                default: return "RANDOM";
            }
        }

        public static string ThemeName(DiceType type)
        {
            switch (type)
            {
                case DiceType.Odd: return "Sun of Ra";
                case DiceType.Even: return "Zeus";
                case DiceType.Fixed: return "Dragon";
                case DiceType.Double: return "Twin Moons";
                default: return "World Tree";
            }
        }

        /// <summary>The theme's highlight color, also used for the selection glow and landing markers.</summary>
        public static Color AccentColor(DiceType type)
        {
            switch (type)
            {
                case DiceType.Odd: return Hex("#ffc94a");
                case DiceType.Even: return Hex("#8fd3ff");
                case DiceType.Fixed: return Hex("#ff7a45");
                case DiceType.Double: return Hex("#d59bff");
                default: return Hex("#9be15d");
            }
        }

        /// <summary>Text color for the title ribbon.</summary>
        public static Color TitleColor(DiceType type)
        {
            switch (type)
            {
                case DiceType.Odd: return Hex("#4a2a00");
                case DiceType.Even: return Hex("#123a6b");
                case DiceType.Fixed: return Hex("#fff1d6");
                case DiceType.Double: return Hex("#3a1660");
                default: return Hex("#1f4a12");
            }
        }

        public static Texture2D Draw(DiceType type)
        {
            switch (type)
            {
                case DiceType.Odd: return DrawSunOfRa();
                case DiceType.Even: return DrawZeus();
                case DiceType.Fixed: return DrawDragon();
                case DiceType.Double: return DrawTwinMoons();
                default: return DrawWorldTree();
            }
        }

        // ---------- Shared frame ----------

        private static readonly Rect Card = new Rect(0, 0, Width, Height);
        private static readonly Rect Inner = new Rect(9, 9, Width - 18, Height - 18);
        private static readonly Shape InnerShape = RoundedRect(Inner, 19f);

        /// <summary>Card body: metal border, background gradient, inner line.</summary>
        private static Painter StartCard(Color borderTop, Color borderBottom, Color backgroundTop, Color backgroundBottom)
        {
            var painter = new Painter(Width, Height);
            painter.Fill(RoundedRect(Card, 26f), VerticalGradient(0, borderBottom, Height, borderTop));
            painter.Fill(InnerShape, VerticalGradient(Inner.yMin, backgroundBottom, Inner.yMax, backgroundTop));
            return painter;
        }

        /// <summary>Title ribbon, value plaque and a thin inner line, drawn on top of the art.</summary>
        private static void FinishCard(Painter painter, Color accent, Color ribbonTop, Color ribbonBottom)
        {
            painter.Fill(Outline(RoundedRect(new Rect(17, 17, Width - 34, Height - 34), 14f), 2.5f), WithAlpha(accent, 0.75f));

            Rect ribbon = new Rect(TitleArea.x * Width, TitleArea.y * Height, TitleArea.width * Width, TitleArea.height * Height);
            painter.Fill(RoundedRect(new Rect(ribbon.x, ribbon.y - 3, ribbon.width, ribbon.height), 13f), WithAlpha(Color.black, 0.3f));
            painter.Fill(RoundedRect(ribbon, 13f), VerticalGradient(ribbon.yMin, ribbonBottom, ribbon.yMax, ribbonTop));
            painter.Fill(Outline(RoundedRect(ribbon, 13f), 2f), WithAlpha(Color.white, 0.5f));

            Rect plaque = new Rect(ValueArea.x * Width, ValueArea.y * Height, ValueArea.width * Width, ValueArea.height * Height);
            painter.Fill(RoundedRect(plaque, 16f), WithAlpha(Hex("#140c1e"), 0.62f));
            painter.Fill(Outline(RoundedRect(plaque, 16f), 2.5f), accent);
        }

        /// <summary>Happy closed eyes, rosy cheeks and a smile: the "cute" face used on every emblem.</summary>
        private static void CuteFace(Painter painter, Vector2 center, float size, Color ink, bool openEyes)
        {
            float eyeX = 13f * size;
            float eyeY = 6f * size;
            Rect area = new Rect(center.x - 40 * size, center.y - 30 * size, 80 * size, 50 * size);

            Color blush = Hex("#ff7b9c", 0.55f);
            painter.Fill(Ellipse(center + new Vector2(-eyeX - 9 * size, -8 * size), new Vector2(7, 4.5f) * size), blush, area);
            painter.Fill(Ellipse(center + new Vector2(eyeX + 9 * size, -8 * size), new Vector2(7, 4.5f) * size), blush, area);

            if (openEyes)
            {
                painter.Fill(Circle(center + new Vector2(-eyeX, eyeY), 4.6f * size), ink, area);
                painter.Fill(Circle(center + new Vector2(eyeX, eyeY), 4.6f * size), ink, area);
                painter.Fill(Circle(center + new Vector2(-eyeX + 1.5f * size, eyeY + 1.8f * size), 1.5f * size), Color.white, area);
                painter.Fill(Circle(center + new Vector2(eyeX + 1.5f * size, eyeY + 1.8f * size), 1.5f * size), Color.white, area);
            }
            else
            {
                painter.Fill(Arc(center + new Vector2(-eyeX, eyeY - 2 * size), 6f * size, 20f, 160f, 3.2f * size), ink, area);
                painter.Fill(Arc(center + new Vector2(eyeX, eyeY - 2 * size), 6f * size, 20f, 160f, 3.2f * size), ink, area);
            }

            painter.Fill(Arc(center + new Vector2(0, -2 * size), 9f * size, 210f, 330f, 3.2f * size), ink, area);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Rect Around(Vector2 center, float radius)
        {
            return new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        }

        // ---------- ODD: Sun of Ra ----------

        private static Texture2D DrawSunOfRa()
        {
            Painter painter = StartCard(Hex("#ffe59a"), Hex("#c98516"), Hex("#243a8c"), Hex("#f39c4a"));

            // Stars in the night sky.
            var random = new System.Random(7);
            for (int i = 0; i < 22; i++)
            {
                var star = new Vector2(20 + random.Next(216), 150 + random.Next(130));
                painter.Fill(Circle(star, 0.8f + (float)random.NextDouble() * 1.3f), WithAlpha(Color.white, 0.75f), Around(star, 4));
            }

            // Warm glow behind the sun.
            var sun = new Vector2(128, 196);
            painter.Glow(Circle(sun, 52f), WithAlpha(Hex("#ffd36b"), 0.55f), 60f, Around(sun, 115));

            // Pyramids on the horizon.
            Shape inner = InnerShape;
            painter.Fill(Intersect(Polygon(new Vector2(-10, 60), new Vector2(84, 150), new Vector2(178, 60)), inner), Hex("#d99a4e"));
            painter.Fill(Intersect(Polygon(new Vector2(84, 150), new Vector2(178, 60), new Vector2(84, 60)), inner), Hex("#b8742f"));
            painter.Fill(Intersect(Polygon(new Vector2(120, 60), new Vector2(196, 128), new Vector2(272, 60)), inner), Hex("#e5ab5f"));
            painter.Fill(Intersect(Polygon(new Vector2(196, 128), new Vector2(272, 60), new Vector2(196, 60)), inner), Hex("#c4823a"));
            painter.Fill(Intersect(RoundedRect(new Rect(0, 0, Width, 70), 0f), inner), VerticalGradient(10, Hex("#c98a45"), 70, Hex("#eab56a")));

            // Sun rays: long and short ones in turn.
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f + 11.25f;
                float length = i % 2 == 0 ? 88f : 74f;
                Vector2 tip = sun + Direction(angle) * length;
                Vector2 left = sun + Direction(angle - 7f) * 50f;
                Vector2 right = sun + Direction(angle + 7f) * 50f;
                painter.Fill(Polygon(left, tip, right), Hex(i % 2 == 0 ? "#ffcf3d" : "#ffe27a"), Around(sun, 92));
            }

            // Sun disk with a golden rim and a cute face.
            painter.Fill(Circle(sun, 54f), Hex("#e08e0b"), Around(sun, 56));
            painter.Fill(Circle(sun, 49f), RadialGradient(sun + new Vector2(-14, 16), 70f, Hex("#fff6c2"), Hex("#ffb627")), Around(sun, 50));
            CuteFace(painter, sun + new Vector2(0, -2), 1.35f, Hex("#7a3b00"), openEyes: false);

            // Little ankh symbols, a classic Egyptian sign.
            Ankh(painter, new Vector2(40, 238), Hex("#ffe59a", 0.85f));
            Ankh(painter, new Vector2(216, 238), Hex("#ffe59a", 0.85f));

            FinishCard(painter, Hex("#ffd56a"), Hex("#ffe9a8"), Hex("#e7a531"));
            return painter.ToTexture();
        }

        private static void Ankh(Painter painter, Vector2 at, Color color)
        {
            Rect area = Around(at, 22);
            painter.Fill(Outline(Ellipse(at + new Vector2(0, 9), new Vector2(5.5f, 7.5f)), 3.2f), color, area);
            painter.Fill(Line(at + new Vector2(0, 1), at + new Vector2(0, -18), 3.6f), color, area);
            painter.Fill(Line(at + new Vector2(-9, -2), at + new Vector2(9, -2), 3.6f), color, area);
        }

        // ---------- EVEN: Zeus ----------

        private static Texture2D DrawZeus()
        {
            Painter painter = StartCard(Hex("#f4f8ff"), Hex("#8aa4c4"), Hex("#2c66d1"), Hex("#9fd2ff"));

            // Greek key (meander) bands.
            MeanderBand(painter, 262f, WithAlpha(Color.white, 0.8f));
            MeanderBand(painter, 96f, WithAlpha(Color.white, 0.8f));

            // Marble columns on both sides.
            Column(painter, 38f);
            Column(painter, 218f);

            // Fluffy cloud.
            var cloud = new Vector2(128, 196);
            Shape cloudShape = Union(
                Circle(cloud + new Vector2(-30, -4), 27f),
                Circle(cloud + new Vector2(2, 12), 35f),
                Circle(cloud + new Vector2(34, -4), 25f),
                RoundedRect(new Rect(cloud.x - 58, cloud.y - 30, 116, 34), 16f));
            Rect cloudArea = Around(cloud, 75);
            painter.Fill(p => cloudShape(p - new Vector2(0, -5)), WithAlpha(Hex("#1a3f7a"), 0.25f), cloudArea);
            painter.Fill(cloudShape, VerticalGradient(cloud.y - 30, Hex("#d6e6f7"), cloud.y + 45, Color.white), cloudArea);
            painter.Fill(Outline(cloudShape, 2.5f), Hex("#a9c3df"), cloudArea);
            CuteFace(painter, cloud + new Vector2(0, -6), 1.15f, Hex("#1e3a66"), openEyes: true);

            // Zeus' lightning bolt.
            Vector2[] bolt =
            {
                new Vector2(137, 168), new Vector2(116, 132), new Vector2(132, 132),
                new Vector2(112, 98), new Vector2(156, 144), new Vector2(139, 144), new Vector2(156, 168),
            };
            Rect boltArea = new Rect(100, 90, 70, 85);
            painter.Glow(Polygon(bolt), WithAlpha(Hex("#fff3a0"), 0.6f), 10f, boltArea);
            painter.Fill(Polygon(bolt), VerticalGradient(98, Hex("#ffb703"), 168, Hex("#fff176")), boltArea);
            painter.Fill(Outline(Polygon(bolt), 2f), Hex("#c77d00"), boltArea);

            FinishCard(painter, Hex("#e8f2ff"), Hex("#ffffff"), Hex("#b9d3f0"));
            return painter.ToTexture();
        }

        private static void MeanderBand(Painter painter, float y, Color color)
        {
            const float cell = 18f;
            const float stroke = 2.6f;
            Rect band = new Rect(22, y, Width - 44, cell);
            Shape key = p =>
            {
                float x = Mathf.Repeat(p.x - band.x, cell);
                var q = new Vector2(x, p.y - y);
                float d = Line(new Vector2(0, 2), new Vector2(cell, 2), stroke)(q);
                d = Mathf.Min(d, Line(new Vector2(2, 2), new Vector2(2, 15), stroke)(q));
                d = Mathf.Min(d, Line(new Vector2(2, 15), new Vector2(14, 15), stroke)(q));
                d = Mathf.Min(d, Line(new Vector2(14, 15), new Vector2(14, 7), stroke)(q));
                d = Mathf.Min(d, Line(new Vector2(14, 7), new Vector2(8, 7), stroke)(q));
                return d;
            };
            painter.Fill(key, color, band);
        }

        private static void Column(Painter painter, float x)
        {
            Rect area = new Rect(x - 18, 112, 36, 150);
            Color marble = Hex("#f2f5fa");
            Color shade = Hex("#c8d3e2");
            painter.Fill(RoundedRect(new Rect(x - 9, 124, 18, 124), 2f), VerticalGradient(124, shade, 248, marble), area);
            painter.Fill(Line(new Vector2(x - 3, 128), new Vector2(x - 3, 244), 1.5f), WithAlpha(shade, 0.9f), area);
            painter.Fill(Line(new Vector2(x + 3, 128), new Vector2(x + 3, 244), 1.5f), WithAlpha(shade, 0.9f), area);
            painter.Fill(RoundedRect(new Rect(x - 15, 246, 30, 10), 3f), marble, area);
            painter.Fill(RoundedRect(new Rect(x - 14, 116, 28, 10), 3f), marble, area);
        }

        // ---------- FIXED: Dragon ----------

        private static Texture2D DrawDragon()
        {
            Painter painter = StartCard(Hex("#ffcf7a"), Hex("#a8481c"), Hex("#b0283f"), Hex("#3d0a18"));

            // Dragon scales in the background.
            const float cellX = 26f;
            const float cellY = 18f;
            Shape scales = p =>
            {
                float row = Mathf.Floor(p.y / cellY);
                float x = Mathf.Repeat(p.x + (row % 2f) * cellX / 2f, cellX) - cellX / 2f;
                float y = Mathf.Repeat(p.y, cellY);
                return Mathf.Abs(new Vector2(x, y).magnitude - cellX / 2f) - 1.2f;
            };
            painter.Fill(Intersect(scales, InnerShape), WithAlpha(Hex("#ff9a7a"), 0.16f));

            var head = new Vector2(128, 206);
            Rect area = Around(head, 100);
            Color green = Hex("#4fcf8f");
            Color darkGreen = Hex("#2a8f5e");

            // Little flames on both sides.
            Flame(painter, new Vector2(46, 118));
            Flame(painter, new Vector2(210, 118));

            // Horns.
            Color horn = Hex("#fff1c9");
            painter.Fill(Polygon(new Vector2(92, 238), new Vector2(70, 292), new Vector2(112, 250)), horn, area);
            painter.Fill(Polygon(new Vector2(164, 238), new Vector2(186, 292), new Vector2(144, 250)), horn, area);

            // Ear frills.
            painter.Fill(Polygon(new Vector2(76, 214), new Vector2(46, 236), new Vector2(80, 236)), darkGreen, area);
            painter.Fill(Polygon(new Vector2(180, 214), new Vector2(210, 236), new Vector2(176, 236)), darkGreen, area);

            // Spikes on top of the head.
            for (int i = -1; i <= 1; i++)
            {
                float x = 128 + i * 15;
                painter.Fill(Polygon(new Vector2(x - 8, 252), new Vector2(x, 272 - Mathf.Abs(i) * 6), new Vector2(x + 8, 252)), Hex("#ff8a4c"), area);
            }

            // Head and snout.
            Shape headShape = Union(Circle(head, 52f), Ellipse(head + new Vector2(0, -34), new Vector2(46, 30)));
            painter.Fill(p => headShape(p - new Vector2(0, -5)), WithAlpha(Color.black, 0.3f), area);
            painter.Fill(headShape, VerticalGradient(head.y - 64, darkGreen, head.y + 52, green), area);
            painter.Fill(Ellipse(head + new Vector2(0, -38), new Vector2(34, 21)), Hex("#b9f0cf"), area);

            // Nostrils.
            painter.Fill(Ellipse(head + new Vector2(-12, -30), new Vector2(4, 3)), Hex("#1d5c3c"), area);
            painter.Fill(Ellipse(head + new Vector2(12, -30), new Vector2(4, 3)), Hex("#1d5c3c"), area);

            // Big shiny eyes.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 eye = head + new Vector2(side * 22, 8);
                painter.Fill(Circle(eye, 16f), Color.white, area);
                painter.Fill(Circle(eye + new Vector2(-side * 2, -1), 10.5f), Hex("#2b1a0e"), area);
                painter.Fill(Ellipse(eye + new Vector2(-side * 2, -1), new Vector2(3f, 8.5f)), Hex("#ffb000"), area);
                painter.Fill(Circle(eye + new Vector2(-side * 2 + 4, 4), 3.4f), Color.white, area);
            }

            // Cheeks, smile and two tiny fangs.
            painter.Fill(Ellipse(head + new Vector2(-38, -16), new Vector2(8, 5)), Hex("#ff7b9c", 0.6f), area);
            painter.Fill(Ellipse(head + new Vector2(38, -16), new Vector2(8, 5)), Hex("#ff7b9c", 0.6f), area);
            painter.Fill(Arc(head + new Vector2(0, -40), 12f, 215f, 325f, 3f), Hex("#1d5c3c"), area);
            painter.Fill(Polygon(head + new Vector2(-8, -49), head + new Vector2(-5, -56), head + new Vector2(-2, -49)), Color.white, area);
            painter.Fill(Polygon(head + new Vector2(2, -49), head + new Vector2(5, -56), head + new Vector2(8, -49)), Color.white, area);

            FinishCard(painter, Hex("#ffb35c"), Hex("#d9532a"), Hex("#8c1f22"));
            return painter.ToTexture();
        }

        private static void Flame(Painter painter, Vector2 at)
        {
            Rect area = Around(at, 30);
            Shape outer = Union(Circle(at, 14f), Polygon(at + new Vector2(-14, 2), at + new Vector2(0, 40), at + new Vector2(14, 2)));
            Shape inner = Union(Circle(at + new Vector2(0, -2), 8f), Polygon(at + new Vector2(-8, 0), at + new Vector2(0, 24), at + new Vector2(8, 0)));
            painter.Glow(outer, WithAlpha(Hex("#ffb347"), 0.5f), 10f, area);
            painter.Fill(outer, Hex("#ff6a2b"), area);
            painter.Fill(inner, Hex("#ffd166"), area);
        }

        // ---------- DOUBLE (rare): Twin Moons ----------

        private static Texture2D DrawTwinMoons()
        {
            Painter painter = StartCard(Hex("#f7e6ff"), Hex("#8d5cc4"), Hex("#2a1457"), Hex("#6b3fb3"));

            // Starry night with a soft glow.
            var random = new System.Random(11);
            for (int i = 0; i < 30; i++)
            {
                var star = new Vector2(20 + random.Next(216), 96 + random.Next(190));
                float size = 0.8f + (float)random.NextDouble() * 1.6f;
                painter.Fill(Circle(star, size), WithAlpha(Color.white, 0.8f), Around(star, 5));
            }
            painter.Glow(Circle(new Vector2(128, 196), 40f), WithAlpha(Hex("#e4c6ff"), 0.4f), 80f, new Rect(9, 90, Width - 18, 200));

            // Two crescent moons facing each other, each with a sleepy smile.
            DrawCrescent(painter, new Vector2(92, 208), 42f, facingRight: true);
            DrawCrescent(painter, new Vector2(164, 180), 42f, facingRight: false);

            // Sparkly four-pointed stars between them.
            Sparkle(painter, new Vector2(128, 238), 12f);
            Sparkle(painter, new Vector2(60, 130), 8f);
            Sparkle(painter, new Vector2(200, 250), 7f);

            // Clouds along the bottom.
            Shape clouds = Union(
                Circle(new Vector2(40, 98), 22f), Circle(new Vector2(78, 92), 18f), Circle(new Vector2(128, 96), 24f),
                Circle(new Vector2(178, 92), 18f), Circle(new Vector2(216, 98), 22f), RoundedRect(new Rect(9, 76, Width - 18, 22), 0f));
            painter.Fill(Intersect(clouds, InnerShape), VerticalGradient(76, Hex("#a17fd6"), 120, Hex("#e9dcff")));

            FinishCard(painter, Hex("#e8c8ff"), Hex("#f7e8ff"), Hex("#c49ae8"));
            return painter.ToTexture();
        }

        private static void DrawCrescent(Painter painter, Vector2 center, float radius, bool facingRight)
        {
            float side = facingRight ? 1f : -1f;
            Shape moon = Subtract(Circle(center, radius), Circle(center + new Vector2(side * radius * 0.55f, radius * 0.15f), radius * 0.85f));
            Rect area = Around(center, radius + 12);
            painter.Glow(moon, WithAlpha(Hex("#fff4b8"), 0.45f), 10f, area);
            painter.Fill(moon, RadialGradient(center - new Vector2(side * 18, -14), radius * 1.6f, Hex("#fffbe0"), Hex("#ffd45c")), area);

            // A sleepy closed eye and a small smile on the thick part of the crescent.
            Vector2 face = center - new Vector2(side * radius * 0.52f, 0);
            painter.Fill(Arc(face + new Vector2(0, 6), 5f, 200f, 340f, 3f), Hex("#7a4a00"), area);
            painter.Fill(Arc(face + new Vector2(side * 2, -10), 6f, 210f, 330f, 2.8f), Hex("#7a4a00"), area);
            painter.Fill(Ellipse(face + new Vector2(-side * 4, -3), new Vector2(5, 3)), Hex("#ff7b9c", 0.55f), area);
        }

        private static void Sparkle(Painter painter, Vector2 at, float size)
        {
            Shape star = Polygon(
                at + new Vector2(0, size), at + new Vector2(size * 0.25f, size * 0.25f), at + new Vector2(size, 0),
                at + new Vector2(size * 0.25f, -size * 0.25f), at + new Vector2(0, -size), at + new Vector2(-size * 0.25f, -size * 0.25f),
                at + new Vector2(-size, 0), at + new Vector2(-size * 0.25f, size * 0.25f));
            painter.Glow(star, WithAlpha(Color.white, 0.5f), 6f, Around(at, size + 8));
            painter.Fill(star, Hex("#fffbe8"), Around(at, size + 2));
        }

        // ---------- RANDOM: World Tree ----------

        private static Texture2D DrawWorldTree()
        {
            Painter painter = StartCard(Hex("#e3f59a"), Hex("#5e9b2c"), Hex("#2f9b7a"), Hex("#123f2c"));

            // Soft light from above.
            painter.Glow(Circle(new Vector2(128, 300), 10f), WithAlpha(Hex("#e9ffd0"), 0.35f), 150f, new Rect(9, 140, Width - 18, Height - 149));

            // Vines climbing both sides.
            Vine(painter, 30f, 0f);
            Vine(painter, 226f, Mathf.PI);

            // Grass hill.
            painter.Fill(Intersect(Ellipse(new Vector2(128, 96), new Vector2(110, 26)), InnerShape), VerticalGradient(70, Hex("#2f7d32"), 120, Hex("#6abf4b")));

            // Trunk and roots.
            Color bark = Hex("#8a5a35");
            painter.Fill(Polygon(new Vector2(104, 104), new Vector2(120, 186), new Vector2(136, 186), new Vector2(152, 104)), VerticalGradient(100, Hex("#6b4226"), 186, bark));
            painter.Fill(Line(new Vector2(110, 106), new Vector2(92, 100), 6f), bark);
            painter.Fill(Line(new Vector2(146, 106), new Vector2(164, 100), 6f), bark);
            painter.Fill(Line(new Vector2(124, 160), new Vector2(98, 186), 7f), bark);
            painter.Fill(Line(new Vector2(132, 164), new Vector2(160, 190), 7f), bark);

            // Round leafy crown.
            var crown = new Vector2(128, 222);
            Shape crownShape = Union(
                Circle(crown, 46f),
                Circle(crown + new Vector2(-38, -14), 32f),
                Circle(crown + new Vector2(38, -14), 32f),
                Circle(crown + new Vector2(-24, 24), 30f),
                Circle(crown + new Vector2(24, 24), 30f));
            Rect area = Around(crown, 90);
            painter.Fill(p => crownShape(p - new Vector2(0, -5)), WithAlpha(Color.black, 0.25f), area);
            painter.Fill(crownShape, RadialGradient(crown + new Vector2(-20, 30), 110f, Hex("#c5f27a"), Hex("#3f8f2a")), area);
            painter.Fill(Outline(crownShape, 2.5f), Hex("#2f6f1f"), area);

            // Golden apples.
            foreach (Vector2 apple in new[] { new Vector2(86, 214), new Vector2(170, 214), new Vector2(104, 252), new Vector2(156, 252), new Vector2(128, 262) })
            {
                painter.Fill(Circle(apple, 6.5f), Hex("#ffcc33"), Around(apple, 8));
                painter.Fill(Circle(apple + new Vector2(-2, 2), 2f), WithAlpha(Color.white, 0.8f), Around(apple, 8));
            }

            CuteFace(painter, crown + new Vector2(0, -8), 1.15f, Hex("#1f4a12"), openEyes: true);

            FinishCard(painter, Hex("#d8f27a"), Hex("#eaffb0"), Hex("#9ccc4f"));
            return painter.ToTexture();
        }

        private static void Vine(Painter painter, float x, float phase)
        {
            Rect area = new Rect(x - 22, 100, 44, 190);
            Shape stem = p =>
            {
                float wave = Mathf.Sin(p.y / 22f + phase);
                float slope = 9f / 22f * Mathf.Cos(p.y / 22f + phase);
                return Mathf.Abs(p.x - (x + 9f * wave)) / Mathf.Sqrt(1f + slope * slope) - 1.8f;
            };
            painter.Fill(stem, Hex("#3e7d2a"), area);

            for (float y = 110f; y < 286f; y += 22f)
            {
                float stemX = x + 9f * Mathf.Sin(y / 22f + phase);
                float side = Mathf.Sin(y / 22f + phase) > 0 ? -1f : 1f;
                var leaf = new Vector2(stemX + side * 8f, y + 4f);
                painter.Fill(Ellipse(leaf, new Vector2(8f, 4f), side * 35f), Hex("#8bd35a"), area);
            }
        }
    }
}
