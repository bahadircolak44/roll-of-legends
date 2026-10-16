using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>Shows one of the code-drawn stat icons (heart, sword, spear, shield) on an Image when the game runs.</summary>
    [RequireComponent(typeof(Image))]
    public class StatIconImage : MonoBehaviour
    {
        public StatIcon Icon;

        private void Awake()
        {
            var image = GetComponent<Image>();
            image.sprite = StatIcons.Get(Icon);
            image.preserveAspect = true;
        }
    }
}
