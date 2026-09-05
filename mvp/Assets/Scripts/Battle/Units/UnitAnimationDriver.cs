using UnityEngine;
using Mvp.Shared;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Drives the infantry model's Animator from the unit's runtime state and
    /// toggles the capture flag. Lives on the infantry model prefab (child of the
    /// UnitView's ModelRoot) so it only runs for real 3D units, not placeholders.
    ///
    /// State mapping used by the infantry AnimatorController:
    ///   Idle/Deploying  -> "Idle"   (clip Idle)
    ///   Moving/Chasing  -> "Move"   (clip Move)
    ///   Attacking       -> "Attack" (clip Attack)
    ///   Capturing       -> "Occupy" (clip Occupy)
    /// </summary>
    public sealed class UnitAnimationDriver : MonoBehaviour
    {
        UnitView _unit;
        Animator _animator;
        GameObject _flagRoot;

        bool _capturing;
        bool _simpleMilitary;
        float _oneShotUntil;

        /// <summary>Resources path of the infantry AnimatorController asset.</summary>
        const string ControllerResource = "Battle/Units/InfantryAnimator";

        public void Initialize(UnitView unit)
        {
            _unit = unit;
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            _simpleMilitary = _animator != null &&
                _animator.runtimeAnimatorController != null &&
                _animator.runtimeAnimatorController.name.IndexOf("SimpleCharacter",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;

            // The prefab's serialized AnimatorController reference can fail to
            // resolve at runtime (the setup used to delete/recreate the controller
            // on every domain reload, churning its GUID). Load it from Resources
            // as a guaranteed fallback so the Animator always has a controller.
            if (_animator != null && _animator.runtimeAnimatorController == null)
            {
                var ctrl = Resources.Load<RuntimeAnimatorController>(ControllerResource);
                if (ctrl != null)
                {
                    _animator.runtimeAnimatorController = ctrl;
                    _animator.Rebind();
                }
                else
                {
                    Debug.LogWarning("[UnitAnimationDriver] Animator has no controller and Resources '" +
                        ControllerResource + "' not found; unit will not animate.", this);
                }
            }

            var t = transform.Find("Flag_Root");
            if (t != null) _flagRoot = t.gameObject;
        }

        void Update()
        {
            if (_unit == null || _unit.Data == null) return;
            if (_unit.Data.State == UnitState.Dead) return;

            if (_simpleMilitary)
            {
                bool moving = _unit.Data.State == UnitState.Moving ||
                    _unit.Data.State == UnitState.Chasing;
                bool capturing = _unit.Data.State == UnitState.Capturing || _capturing;
                _animator.SetFloat("Speed_f", moving ? 1f : 0f);
                _animator.SetBool("Static_b", !moving);
                _animator.SetBool("Crouch_b", capturing);
                _animator.SetBool("Shoot_b", Time.time < _oneShotUntil);
                if (_flagRoot != null) _flagRoot.SetActive(capturing);
                return;
            }

            string clip = null;
            switch (_unit.Data.State)
            {
                case UnitState.Capturing: clip = _simpleMilitary ? "Crouch_Idle" : "Occupy"; break;
                case UnitState.Attacking: clip = _simpleMilitary ? "Idle" : "Attack"; break;
                case UnitState.Moving:
                case UnitState.Chasing: clip = _simpleMilitary ? "Walk_Static" : "Move"; break;
                case UnitState.Deploying:
                case UnitState.Idle:
                default: clip = "Idle"; break;
            }

            if (Time.time < _oneShotUntil)
            {
                if (_flagRoot != null) _flagRoot.SetActive(_capturing);
                return;
            }

            if (clip != _lastClip)
            {
                _lastClip = clip;
                if (_animator != null)
                {
                    // Recover if the controller reference dropped (e.g. an asset
                    // rebuild mid-session). Only play once we actually have one so
                    // the "Animator does not have an AnimatorController" error does
                    // not spam every frame.
                    if (_animator.runtimeAnimatorController == null)
                        TryRestoreController();
                    if (_animator.runtimeAnimatorController == null) return;

                    // Integer param kept in sync for debugging/inspection.
                    _animator.SetInteger("State", (int)ClipToStateValue(clip));
                    _animator.Play(clip, 0, 0f);
                }
            }

            if (_flagRoot != null)
                _flagRoot.SetActive(_capturing);
        }

        void TryRestoreController()
        {
            var ctrl = Resources.Load<RuntimeAnimatorController>(ControllerResource);
            if (ctrl == null) return;
            _animator.runtimeAnimatorController = ctrl;
            _animator.Rebind();
        }

        public void SetCapturing(bool capturing)
        {
            _capturing = capturing;
            if (_flagRoot != null) _flagRoot.SetActive(capturing);
        }

        public void PlayAttack()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            if (_simpleMilitary)
            {
                _animator.SetBool("FullAuto_b", false);
                _animator.SetBool("Shoot_b", true);
                _oneShotUntil = Time.time + 0.42f;
                return;
            }
            string clip = "Attack";
            _animator.Play(clip, 0, 0f);
            _lastClip = clip;
            _oneShotUntil = Time.time + 0.25f;
        }

        static int ClipToStateValue(string clip)
        {
            switch (clip)
            {
                case "Move": return 1;
                case "Attack": return 2;
                case "Occupy": return 3;
                default: return 0;
            }
        }

        string _lastClip;
    }
}
