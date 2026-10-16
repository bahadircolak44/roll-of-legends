using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Plays one animation clip on a loop (e.g. a hero's or monster's "Idle"), without needing an
    /// Animator Controller. Put it on the model's root object, the one the clip was imported with.
    /// Other clips of the model (attack, hit, cheer...) can be played once with <see cref="Play"/>,
    /// after which the idle loop continues.
    /// </summary>
    public class ModelAnimator : MonoBehaviour
    {
        [SerializeField] private AnimationClip _clip;
        [SerializeField] private float _speed = 1f;

        [Tooltip("Clips that can be played once with Play(), e.g. attacks and hit reactions.")]
        [SerializeField] private AnimationClip[] _actions = new AnimationClip[0];

        private float _time;
        private AnimationClip _action;
        private float _actionTime;
        private float _actionSpeed;

        public AnimationClip Clip
        {
            get => _clip;
            set => _clip = value;
        }

        public AnimationClip[] Actions
        {
            get => _actions;
            set => _actions = value;
        }

        /// <summary>
        /// Plays the first clip whose name ends with one of <paramref name="names"/> once (e.g. "Hit_A" or
        /// "HitReact"). Returns how long it takes in seconds, or 0 if the model has none of them.
        /// </summary>
        public float Play(float speed, params string[] names)
        {
            foreach (string name in names)
            {
                foreach (AnimationClip clip in _actions)
                {
                    if (clip == null || !clip.name.EndsWith(name)) continue;
                    _action = clip;
                    _actionTime = 0f;
                    _actionSpeed = speed;
                    return clip.length / speed;
                }
            }
            return 0f;
        }

        public float Play(params string[] names)
        {
            return Play(1f, names);
        }

        /// <summary>Cancels a clip started with Play and goes straight back to the idle loop.</summary>
        public void Stop()
        {
            _action = null;
        }

        private void Start()
        {
            // Start at a random point so several copies of the same monster don't move in sync.
            if (_clip != null) _time = Random.Range(0f, _clip.length);
        }

        private void Update()
        {
            if (_action != null)
            {
                _actionTime += Time.deltaTime * _actionSpeed;
                if (_actionTime < _action.length)
                {
                    _action.SampleAnimation(gameObject, _actionTime);
                    return;
                }
                _action = null; // done: back to the idle loop
            }

            if (_clip == null || _clip.length <= 0f) return;
            _time = (_time + Time.deltaTime * _speed) % _clip.length;
            _clip.SampleAnimation(gameObject, _time);
        }
    }
}
