using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// A tiny drawing canvas for making art in code (card faces, rounded UI panels) without image files.
    /// Shapes are "signed distance" functions: they return how far a point is from the shape's edge
    /// (negative inside, positive outside), which gives smooth anti-aliased edges for free.
    /// Coordinates are in pixels with (0, 0) at the bottom-left.
    /// </summary>
    public class Painter
    {
        public delegate float Shape(Vector2 p);
        public delegate Color Paint(Vector2 p);

        public readonly int Width;
        public readonly int Height;
        private readonly Color[] _pixels;

        public Painter(int width, int height)
        {
            Width = width;
            Height = height;
            _pixels = new Color[width * height]; // starts fully transparent
        }

        public void Fill(Shape shape, Color color, Rect? bounds = null)
        {
            Fill(shape, _ => color, bounds);
        }

        /// <summary>Paints the inside of a shape. Pass bounds (in pixels) to skip work outside a small shape.</summary>
        public void Fill(Shape shape, Paint paint, Rect? bounds = null)
        {
            ForEachPixel(bounds, (p, index) =>
            {
                float coverage = Mathf.Clamp01(0.5f - shape(p));
                if (coverage > 0f) Blend(index, paint(p), coverage);
            });
        }

        /// <summary>A soft glow around a shape that fades out over <paramref name="radius"/> pixels.</summary>
        public void Glow(Shape shape, Color color, float radius, Rect? bounds = null)
        {
            ForEachPixel(bounds, (p, index) =>
            {
                float distance = shape(p);
                float strength = distance <= 0f ? 1f : 1f - Mathf.Clamp01(distance / radius);
                if (strength > 0f) Blend(index, color, strength * strength);
            });
        }

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(_pixels);
            texture.Apply(false);
            return texture;
        }

        private void ForEachPixel(Rect? bounds, System.Action<Vector2, int> action)
        {
            Rect area = bounds ?? new Rect(0, 0, Width, Height);
            int xMin = Mathf.Max(0, Mathf.FloorToInt(area.xMin));
            int yMin = Mathf.Max(0, Mathf.FloorToInt(area.yMin));
            int xMax = Mathf.Min(Width, Mathf.CeilToInt(area.xMax));
            int yMax = Mathf.Min(Height, Mathf.CeilToInt(area.yMax));
            for (int y = yMin; y < yMax; y++)
            {
                for (int x = xMin; x < xMax; x++)
                {
                    action(new Vector2(x + 0.5f, y + 0.5f), y * Width + x);
                }
            }
        }

        /// <summary>Standard "paint over" blending.</summary>
        private void Blend(int index, Color color, float coverage)
        {
            float alpha = color.a * coverage;
            Color below = _pixels[index];
            float outAlpha = alpha + below.a * (1f - alpha);
            if (outAlpha <= 0f) return;

            float keep = below.a * (1f - alpha);
            _pixels[index] = new Color(
                (color.r * alpha + below.r * keep) / outAlpha,
                (color.g * alpha + below.g * keep) / outAlpha,
                (color.b * alpha + below.b * keep) / outAlpha,
                outAlpha);
        }

        // ---------- Shapes ----------

        public static Shape Circle(Vector2 center, float radius)
        {
            return p => (p - center).magnitude - radius;
        }

        public static Shape Ellipse(Vector2 center, Vector2 radii, float angleDegrees = 0f)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, -angleDegrees);
            return p =>
            {
                Vector2 local = rotation * (p - center);
                return (new Vector2(local.x / radii.x, local.y / radii.y).magnitude - 1f) * Mathf.Min(radii.x, radii.y);
            };
        }

        public static Shape RoundedRect(Rect rect, float radius)
        {
            return p =>
            {
                Vector2 q = new Vector2(Mathf.Abs(p.x - rect.center.x), Mathf.Abs(p.y - rect.center.y))
                            - (rect.size / 2f - Vector2.one * radius);
                return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
            };
        }

        /// <summary>A thick line with round ends.</summary>
        public static Shape Line(Vector2 a, Vector2 b, float thickness)
        {
            return p =>
            {
                Vector2 pa = p - a;
                Vector2 ba = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                return (pa - ba * h).magnitude - thickness / 2f;
            };
        }

        /// <summary>Part of a circle's outline, from one angle to another (degrees, 0 = right, counter-clockwise).</summary>
        public static Shape Arc(Vector2 center, float radius, float fromDegrees, float toDegrees, float thickness)
        {
            Vector2 start = center + Direction(fromDegrees) * radius;
            Vector2 end = center + Direction(toDegrees) * radius;
            return p =>
            {
                Vector2 d = p - center;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (angle < fromDegrees) angle += 360f;
                float distance = angle <= toDegrees
                    ? Mathf.Abs(d.magnitude - radius)
                    : Mathf.Min((p - start).magnitude, (p - end).magnitude);
                return distance - thickness / 2f;
            };
        }

        /// <summary>Any polygon, given its corner points in order.</summary>
        public static Shape Polygon(params Vector2[] points)
        {
            return p =>
            {
                float distance = Vector2.Dot(p - points[0], p - points[0]);
                float sign = 1f;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i, i++)
                {
                    Vector2 edge = points[j] - points[i];
                    Vector2 w = p - points[i];
                    Vector2 b = w - edge * Mathf.Clamp01(Vector2.Dot(w, edge) / Vector2.Dot(edge, edge));
                    distance = Mathf.Min(distance, Vector2.Dot(b, b));
                    bool c1 = p.y >= points[i].y;
                    bool c2 = p.y < points[j].y;
                    bool c3 = edge.x * w.y > edge.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
                }
                return sign * Mathf.Sqrt(distance);
            };
        }

        /// <summary>Just the outline of a shape.</summary>
        public static Shape Outline(Shape shape, float thickness)
        {
            return p => Mathf.Abs(shape(p)) - thickness / 2f;
        }

        public static Shape Union(params Shape[] shapes)
        {
            return p =>
            {
                float distance = float.MaxValue;
                foreach (Shape shape in shapes) distance = Mathf.Min(distance, shape(p));
                return distance;
            };
        }

        public static Shape Intersect(Shape a, Shape b)
        {
            return p => Mathf.Max(a(p), b(p));
        }

        public static Shape Subtract(Shape shape, Shape cut)
        {
            return p => Mathf.Max(shape(p), -cut(p));
        }

        // ---------- Paints ----------

        public static Paint VerticalGradient(float bottomY, Color bottom, float topY, Color top)
        {
            return p => Color.Lerp(bottom, top, Mathf.InverseLerp(bottomY, topY, p.y));
        }

        public static Paint RadialGradient(Vector2 center, float radius, Color inner, Color outer)
        {
            return p => Color.Lerp(inner, outer, (p - center).magnitude / radius);
        }

        public static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            color.a = alpha;
            return color;
        }
    }
}
