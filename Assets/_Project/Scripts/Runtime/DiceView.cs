using System.Collections;
using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// The 3D die in the middle of the board. <see cref="Roll"/> drops it in while spinning
    /// and lands it with the rolled number facing up.
    /// </summary>
    public class DiceView : MonoBehaviour
    {
        [Tooltip("How long the spinning drop takes, in seconds.")]
        [SerializeField] private float _rollDuration = 1f;

        [Tooltip("How high above its resting place the die starts falling from.")]
        [SerializeField] private float _dropHeight = 4f;

        [Tooltip("How much the die spins before it lands, in degrees.")]
        [SerializeField] private float _spinDegrees = 1080f;

        private Vector3 _restPosition;

        private void Awake()
        {
            _restPosition = transform.position;
        }

        public IEnumerator Roll(int value)
        {
            gameObject.SetActive(true);

            Quaternion landed = Quaternion.FromToRotation(FaceNormal(value), Vector3.up);
            Vector3 spinAxis = Random.onUnitSphere;
            Vector3 start = _restPosition + Vector3.up * _dropHeight;

            for (float t = 0f; t < 1f; t += Time.deltaTime / _rollDuration)
            {
                float eased = 1f - Mathf.Pow(1f - t, 3f); // fast at first, slows down before landing
                transform.position = Vector3.Lerp(start, _restPosition, eased);
                // The leftover spin shrinks to zero, so the die ends exactly on the landed rotation.
                transform.rotation = landed * Quaternion.AngleAxis((1f - eased) * _spinDegrees, spinAxis);
                yield return null;
            }

            transform.SetPositionAndRotation(_restPosition, landed);
        }

        /// <summary>The number currently facing up (0 if the die is tilted).</summary>
        public int TopValue()
        {
            for (int value = 1; value <= 6; value++)
            {
                if (Vector3.Dot(transform.rotation * FaceNormal(value), Vector3.up) > 0.99f)
                {
                    return value;
                }
            }
            return 0;
        }

        /// <summary>Which side of the cube shows each number, like a real die (opposite sides add up to 7).</summary>
        public static Vector3 FaceNormal(int value)
        {
            switch (value)
            {
                case 1: return Vector3.up;
                case 6: return Vector3.down;
                case 2: return Vector3.back;
                case 5: return Vector3.forward;
                case 3: return Vector3.right;
                default: return Vector3.left; // 4
            }
        }
    }
}
