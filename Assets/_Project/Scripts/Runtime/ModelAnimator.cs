using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Plays one animation clip on a loop (e.g. a hero's or monster's "Idle"), without needing an
    /// Animator Controller. Put it on the model's root object, the one the clip was imported with.
    /// </summary>
    public class ModelAnimator : MonoBehaviour
    {
        [SerializeField] private AnimationClip _clip;
        [SerializeField] private float _speed = 1f;

        private float _time;

        public AnimationClip Clip
        {
            get => _clip;
            set => _clip = value;
        }

        private void Start()
        {
            // Start at a random point so several copies of the same monster don't move in sync.
            if (_clip != null) _time = Random.Range(0f, _clip.length);
        }

        private void Update()
        {
            if (_clip == null || _clip.length <= 0f) return;
            _time = (_time + Time.deltaTime * _speed) % _clip.length;
            _clip.SampleAnimation(gameObject, _time);
        }
    }
}
