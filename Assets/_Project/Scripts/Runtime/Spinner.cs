using UnityEngine;

namespace RollOfLegends
{
    /// <summary>Slowly turns an object around, so pickups like swords and potions catch the eye.</summary>
    public class Spinner : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 90f;

        private void Update()
        {
            transform.Rotate(0f, _degreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
    }
}
