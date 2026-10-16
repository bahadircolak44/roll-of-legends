using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Slowly moves an object (e.g. a cloud) in one direction. When it has gone too far it jumps back
    /// to the other side, so it keeps drifting forever.
    /// </summary>
    public class Drift : MonoBehaviour
    {
        [SerializeField] private Vector3 _velocity = new Vector3(0.3f, 0f, 0f);

        [Tooltip("How far it may drift from its starting point before wrapping around to the other side.")]
        [SerializeField] private float _range = 20f;

        private Vector3 _start;
        private float _travelled;

        public void Setup(Vector3 velocity, float range)
        {
            _velocity = velocity;
            _range = range;
        }

        private void Start()
        {
            _start = transform.position;
        }

        private void Update()
        {
            _travelled += Time.deltaTime;
            float distance = _travelled * _velocity.magnitude;
            if (distance > _range) _travelled -= 2f * _range / Mathf.Max(0.001f, _velocity.magnitude);
            transform.position = _start + _velocity * _travelled;
        }
    }
}
