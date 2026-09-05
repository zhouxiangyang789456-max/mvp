using UnityEngine;
using UnityEngine.AI;

namespace SimpleMilitary.VehicleAnimation
{
    /// <summary>
    /// 载具运动状态提供者 —— 所有载具动画组件的速度来源。
    ///
    /// 挂在载具根节点上，自动识别你现有的移动方式（刚体 / NavMesh 寻路 / 直接位移），
    /// 统一对外提供速度，让轮子、履带、旋翼等动画跟着实际移动自动播放。
    ///
    /// 用法：挂在载具根节点，其他动画组件会自动查找它；找不到时会各自退化为自行计算。
    /// </summary>
    [AddComponentMenu("Simple Military/载具动画/Vehicle Motion")]
    public class VehicleMotion : MonoBehaviour
    {
        public enum SpeedSource
        {
            Auto,           // 自动检测：Rigidbody → NavMeshAgent → Transform 位移
            Rigidbody,      // 从 Rigidbody.velocity 读取
            NavMeshAgent,   // 从 NavMeshAgent.velocity 读取
            TransformDelta, // 从 Transform 帧间位移计算
            Manual          // 由外部代码通过 SetSpeed 手动写入
        }

        [Header("速度来源")]
        [Tooltip("Auto 会依次尝试 Rigidbody → NavMeshAgent → Transform 位移，推荐使用")]
        public SpeedSource source = SpeedSource.Auto;

        [Header("只读输出")]
        [Tooltip("速度大小（米/秒）")]
        [SerializeField] private float speed;

        [Tooltip("世界空间速度向量（米/秒）")]
        [SerializeField] private Vector3 velocity;

        [Tooltip("沿车头方向的速度分量：正=前进，负=倒车")]
        [SerializeField] private float forwardSpeed;

        [Header("调试")]
        [Tooltip("勾选后在控制台输出当前速度来源与数值，用于排查速度读不到的情况")]
        public bool debugLog;

        private Rigidbody _rb;
        private NavMeshAgent _agent;
        private Vector3 _lastPosition;
        private SpeedSource _resolvedSource;
        private bool _initialized;

        /// <summary>速度大小（米/秒）</summary>
        public float Speed => speed;

        /// <summary>世界空间速度向量（米/秒）</summary>
        public Vector3 Velocity => velocity;

        /// <summary>沿车头方向的速度分量，正为前进、负为倒车</summary>
        public float ForwardSpeed => forwardSpeed;

        /// <summary>实际生效的速度来源（Auto 模式下可查看自动检测结果）</summary>
        public SpeedSource ResolvedSource => _resolvedSource;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _agent = GetComponent<NavMeshAgent>();
            _lastPosition = transform.position;
            ResolveSource();
            _initialized = true;
        }

        private void ResolveSource()
        {
            if (source != SpeedSource.Auto)
            {
                _resolvedSource = source;
                return;
            }

            if (_rb != null)
                _resolvedSource = SpeedSource.Rigidbody;
            else if (_agent != null)
                _resolvedSource = SpeedSource.NavMeshAgent;
            else
                _resolvedSource = SpeedSource.TransformDelta;
        }

        private void Update()
        {
            if (!_initialized)
            {
                // 处理脚本在运行时被动态添加的情况
                _rb = GetComponent<Rigidbody>();
                _agent = GetComponent<NavMeshAgent>();
                _lastPosition = transform.position;
                ResolveSource();
                _initialized = true;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            switch (_resolvedSource)
            {
                case SpeedSource.Rigidbody:
                    if (_rb != null)
                    {
                        velocity = _rb.velocity;
                        // 忽略垂直分量，避免爬坡/下落时轮子狂转
                        velocity.y = 0f;
                    }
                    break;

                case SpeedSource.NavMeshAgent:
                    if (_agent != null)
                    {
                        velocity = _agent.velocity;
                        velocity.y = 0f;
                    }
                    break;

                case SpeedSource.TransformDelta:
                    Vector3 current = transform.position;
                    Vector3 delta = current - _lastPosition;
                    delta.y = 0f;
                    velocity = delta / deltaTime;
                    _lastPosition = current;
                    break;

                case SpeedSource.Manual:
                    // 速度由外部 SetSpeed 写入，此处不覆盖
                    break;
            }

            speed = velocity.magnitude;
            forwardSpeed = Vector3.Dot(velocity, transform.forward);

            if (debugLog)
                Debug.Log($"[VehicleMotion] {name} 来源={_resolvedSource} 速度={speed:F2} m/s 前向={forwardSpeed:F2}");
        }

        /// <summary>
        /// 手动写入速度（配合 SpeedSource.Manual 使用）。
        /// 适合载具移动由自定义逻辑驱动、不便从 Transform 或物理组件读取的场景。
        /// </summary>
        /// <param name="newSpeed">速度大小（米/秒），会自动沿车头方向分解</param>
        public void SetSpeed(float newSpeed)
        {
            speed = Mathf.Abs(newSpeed);
            forwardSpeed = newSpeed;
            velocity = transform.forward * newSpeed;
        }

        /// <summary>
        /// 手动写入世界空间速度向量（配合 SpeedSource.Manual 使用）。
        /// </summary>
        public void SetVelocity(Vector3 worldVelocity)
        {
            velocity = worldVelocity;
            velocity.y = 0f;
            speed = velocity.magnitude;
            forwardSpeed = Vector3.Dot(velocity, transform.forward);
        }

        private void OnValidate()
        {
            // 在 Inspector 中改动来源时立即重新解析，便于查看自动检测结果
            if (Application.isPlaying)
                ResolveSource();
        }
    }
}
