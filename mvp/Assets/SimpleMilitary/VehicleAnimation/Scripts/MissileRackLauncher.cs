using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleMilitary.VehicleAnimation
{
    /// <summary>
    /// 导弹发射架起竖 + 导弹发射动画。
    ///
    /// 适用于 SK_Veh_ScudTruck_01（飞毛腿导弹车）等带发射架的载具。
    /// 注意：本资源包中节点名拼作 "Missle"（少了 i），本组件两种拼写都能自动识别。
    ///
    /// 起竖：发射架绕本地 X 轴从水平位抬升到发射角（默认 90 度）。
    /// 发射：导弹沿发射方向飞出，支持间隔连发，发射后导弹对象隐藏。
    /// </summary>
    [AddComponentMenu("Simple Military/载具动画/Missile Rack Launcher")]
    public class MissileRackLauncher : MonoBehaviour
    {
        [Header("节点")]
        [Tooltip("发射架节点。飞毛腿导弹车上为 SK_Veh_ScudTruck_MissleRack_01")]
        public Transform rack;

        [Tooltip("导弹节点列表。留空则自动收集架下名称含 missile/missle 的子物体")]
        public List<Transform> missiles = new List<Transform>();

        [Header("起竖")]
        [Tooltip("起竖旋转轴，本资源包为 X 轴")]
        public Vector3 raiseAxis = Vector3.right;

        [Tooltip("起竖目标角（度）。90 为垂直发射，通常在 45~90 之间")]
        [Range(0f, 120f)] public float raiseAngle = 90f;

        [Tooltip("起竖耗时（秒）")]
        public float raiseDuration = 3f;

        [Tooltip("起竖完成后是否自动开始发射")]
        public bool autoFireAfterRaise;

        [Header("发射")]
        [Tooltip("每发之间的间隔（秒）")]
        public float fireInterval = 0.6f;

        [Tooltip("导弹初速度（米/秒）")]
        public float missileSpeed = 40f;

        [Tooltip("导弹飞行重力（米/秒²）。设为 0 则为直线飞行")]
        public float missileGravity = 9.81f;

        [Tooltip("导弹存活时间（秒），超时自动销毁")]
        public float missileLifetime = 6f;

        [Tooltip("发射方向本地轴。发射架起竖后通常沿其本地 +Z")]
        public Vector3 fireDirection = Vector3.forward;

        [Header("状态")]
        [SerializeField] private bool isRaised;
        [SerializeField] private bool isFiring;
        [SerializeField] private int missilesLeft;

        private Quaternion _rackInitLocal;
        private readonly List<Vector3> _missileInitPos = new List<Vector3>();
        private readonly List<Quaternion> _missileInitRot = new List<Quaternion>();
        private bool _recorded;
        private Coroutine _raiseRoutine;
        private Coroutine _fireRoutine;

        /// <summary>发射架是否已起竖到位</summary>
        public bool IsRaised => isRaised;

        /// <summary>剩余导弹数</summary>
        public int MissilesLeft => missilesLeft;

        private void Awake()
        {
            CollectMissiles();
            RecordInitial();
        }

        /// <summary>
        /// 自动收集导弹节点（兼容 Missile / Missle 两种拼写）。
        /// </summary>
        [ContextMenu("收集导弹节点")]
        public void CollectMissiles()
        {
            missiles.Clear();
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == transform) continue;
                string n = t.name.ToLowerInvariant();
                if (n.Contains("missile") || n.Contains("missle"))
                    missiles.Add(t);
            }
            missilesLeft = missiles.Count;
        }

        private void RecordInitial()
        {
            if (rack != null)
                _rackInitLocal = rack.localRotation;

            _missileInitPos.Clear();
            _missileInitRot.Clear();
            foreach (var m in missiles)
            {
                if (m == null) continue;
                _missileInitPos.Add(m.localPosition);
                _missileInitRot.Add(m.localRotation);
            }
            _recorded = true;
        }

        /// <summary>
        /// 开始起竖。重复调用无效（已在起竖中或已起竖）。
        /// </summary>
        [ContextMenu("起竖发射架")]
        public void Raise()
        {
            if (rack == null)
            {
                Debug.LogWarning("[MissileRackLauncher] 未指定发射架节点 rack。", this);
                return;
            }
            if (isRaised || _raiseRoutine != null)
                return;

            if (!_recorded) RecordInitial();
            _raiseRoutine = StartCoroutine(RaiseRoutine());
        }

        /// <summary>
        /// 放平发射架，回到初始角度。
        /// </summary>
        public void Lower()
        {
            if (rack == null) return;
            if (_raiseRoutine != null)
            {
                StopCoroutine(_raiseRoutine);
                _raiseRoutine = null;
            }
            _raiseRoutine = StartCoroutine(LowerRoutine());
        }

        /// <summary>
        /// 开始发射。若尚未起竖会先起竖再发射。
        /// </summary>
        [ContextMenu("开始发射")]
        public void Fire()
        {
            if (missiles.Count == 0)
                CollectMissiles();

            if (!isRaised && rack != null)
            {
                StartCoroutine(RaiseThenFire());
                return;
            }
            StartFiring();
        }

        private IEnumerator RaiseThenFire()
        {
            yield return RaiseRoutine();
            StartFiring();
        }

        private void StartFiring()
        {
            if (isFiring) return;
            _fireRoutine = StartCoroutine(FireRoutine());
        }

        private IEnumerator RaiseRoutine()
        {
            float elapsed = 0f;
            Quaternion from = rack.localRotation;
            Quaternion to = _rackInitLocal * Quaternion.AngleAxis(raiseAngle, raiseAxis.normalized);

            while (elapsed < raiseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / raiseDuration);
                // 用 SmoothStep 做出起步与到位的缓动
                rack.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            rack.localRotation = to;
            isRaised = true;
            _raiseRoutine = null;

            if (autoFireAfterRaise)
                StartFiring();
        }

        private IEnumerator LowerRoutine()
        {
            float elapsed = 0f;
            Quaternion from = rack.localRotation;

            while (elapsed < raiseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / raiseDuration);
                rack.localRotation = Quaternion.Slerp(from, _rackInitLocal, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            rack.localRotation = _rackInitLocal;
            isRaised = false;
            _raiseRoutine = null;
        }

        private IEnumerator FireRoutine()
        {
            isFiring = true;

            for (int i = 0; i < missiles.Count; i++)
            {
                var m = missiles[i];
                if (m == null || !m.gameObject.activeSelf)
                    continue;

                LaunchOne(m);
                missilesLeft--;

                if (fireInterval > 0f)
                    yield return new WaitForSeconds(fireInterval);
            }

            isFiring = false;
            _fireRoutine = null;
        }

        private void LaunchOne(Transform missile)
        {
            // 与发射架解除父子关系，避免跟随发射架旋转
            missile.SetParent(null, true);

            var projectile = missile.gameObject.AddComponent<MissileProjectile>();
            projectile.Initialize(
                rack != null ? rack.TransformDirection(fireDirection.normalized) : transform.forward,
                missileSpeed,
                missileGravity,
                missileLifetime
            );
        }

        /// <summary>
        /// 重置：发射架放平、所有导弹回到初始位置并显示。
        /// 适合做成"补给后重新装填"的效果。
        /// </summary>
        [ContextMenu("重新装填")]
        public void Reload()
        {
            if (_fireRoutine != null)
            {
                StopCoroutine(_fireRoutine);
                _fireRoutine = null;
            }
            isFiring = false;

            if (rack != null)
                rack.localRotation = _rackInitLocal;
            isRaised = false;

            for (int i = 0; i < missiles.Count; i++)
            {
                var m = missiles[i];
                if (m == null) continue;

                var proj = m.GetComponent<MissileProjectile>();
                if (proj != null)
                    Destroy(proj);

                if (rack != null && m.parent != rack)
                    m.SetParent(rack, false);

                if (i < _missileInitPos.Count)
                {
                    m.localPosition = _missileInitPos[i];
                    m.localRotation = _missileInitRot[i];
                }
                m.gameObject.SetActive(true);
            }
            missilesLeft = missiles.Count;
        }
    }

    /// <summary>
    /// 简易导弹飞行体。发射时由 MissileRackLauncher 动态挂上。
    /// 只做视觉飞行；命中判定请在你自己的游戏逻辑中处理。
    /// </summary>
    public class MissileProjectile : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _gravity;
        private float _lifetime;
        private bool _launched;
        private bool _orientToVelocity = true;

        public void Initialize(Vector3 direction, float speed, float gravity, float lifetime)
        {
            _velocity = direction.normalized * speed;
            _gravity = gravity;
            _lifetime = lifetime;
            _launched = true;
        }

        private void Update()
        {
            if (!_launched) return;

            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                _launched = false;
                gameObject.SetActive(false);
                return;
            }

            if (_gravity > 0f)
                _velocity += Vector3.down * _gravity * Time.deltaTime;

            Vector3 prev = transform.position;
            transform.position += _velocity * Time.deltaTime;

            if (_orientToVelocity && _velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(_velocity);
        }

        /// <summary>
        /// 改为抛射模式后，可在命中回调中调用本方法播放爆炸等效果。
        /// 这里仅提供销毁接口。
        /// </summary>
        public void Explode()
        {
            Destroy(gameObject);
        }
    }
}
