using UnityEngine;

namespace Mvp.Validation
{
    public sealed class CharacterAnimationPreview : MonoBehaviour
    {
        public float secondsPerAnimation = 3f;

        private static readonly string[] AnimationNames =
        {
            "Idle", "Walk", "Run", "Character_Auto_SingleShot", "Death_01"
        };

        private Animator _animator;
        private int _index;
        private float _nextChange;

        private void Start()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
                return;

            _animator.applyRootMotion = false;
            PlayCurrent();
        }

        private void Update()
        {
            if (_animator == null || Time.time < _nextChange)
                return;

            _index = (_index + 1) % AnimationNames.Length;
            PlayCurrent();
        }

        private void PlayCurrent()
        {
            _animator.Play(AnimationNames[_index], 0, 0f);
            _nextChange = Time.time + secondsPerAnimation;
        }
    }
}
