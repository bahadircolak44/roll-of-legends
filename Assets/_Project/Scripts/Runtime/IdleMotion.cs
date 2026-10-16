using UnityEngine;

namespace RollOfLegends
{
    /// <summary>Gentle idle animation: spinning and/or bobbing up and down, so things on the board feel alive.</summary>
    public class IdleMotion : MonoBehaviour
    {
        [SerializeField] private float _spinDegreesPerSecond = 0f;
        [SerializeField] private float _bobHeight = 0.05f;
        [SerializeField] private float _bobSpeed = 2.5f;

        private Vector3 _startLocalPosition;
        private float _phase;

        private void Start()
        {
            _startLocalPosition = transform.localPosition;
            _phase = Random.Range(0f, Mathf.PI * 2f); // so neighbours don't move in sync
        }

        private void Update()
        {
            if (_spinDegreesPerSecond != 0f)
            {
                transform.Rotate(0f, _spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            }
            float bob = Mathf.Sin(Time.time * _bobSpeed + _phase) * _bobHeight;
            transform.localPosition = _startLocalPosition + new Vector3(0f, bob, 0f);
        }
    }
}
