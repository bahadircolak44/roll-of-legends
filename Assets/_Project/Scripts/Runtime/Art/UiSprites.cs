using UnityEngine;

namespace RollOfLegends
{
    /// <summary>Rounded shapes for UI panels and buttons, drawn in code (no image files).</summary>
    public static class UiSprites
    {
        private const int Size = 64;
        private const float Radius = 24f;
        private static Sprite _roundedRect;

        /// <summary>A white rounded rectangle that stretches without distorting its corners (9-sliced).</summary>
        public static Sprite RoundedRect
        {
            get
            {
                if (_roundedRect == null)
                {
                    var painter = new Painter(Size, Size);
                    painter.Fill(Painter.RoundedRect(new Rect(0, 0, Size, Size), Radius), Color.white);
                    Texture2D texture = painter.ToTexture();
                    float border = Radius + 2f;
                    _roundedRect = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f,
                        0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                }
                return _roundedRect;
            }
        }
    }
}
