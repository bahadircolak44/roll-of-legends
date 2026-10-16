using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>Gives an Image rounded corners when the game runs. Corner size can be tuned with Pixels Per Unit Multiplier.</summary>
    [RequireComponent(typeof(Image))]
    public class RoundedImage : MonoBehaviour
    {
        private void Awake()
        {
            var image = GetComponent<Image>();
            image.sprite = UiSprites.RoundedRect;
            image.type = Image.Type.Sliced;
        }
    }
}
