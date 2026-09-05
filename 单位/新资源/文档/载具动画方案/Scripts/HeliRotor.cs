using UnityEngine;

namespace SimpleMilitary.VehicleAnimation
{
    /// <summary>
    /// 直升机旋翼旋转动画。
    ///
    /// 主旋翼绕本地 Y 轴高速旋转，尾桨绕本地 X 轴旋转。
    /// 支持启动 / 停转的转速缓动，并可叠加高速时贴图的运动模糊感（可选）。
    ///
    /// 适用节点：
    ///   SK_Veh_Attack_Heli_01 → SK_Veh_Attack_Heli_Rotor_Main_01 / Rotor_Tail_01
    ///   SK_Veh_Small_Heli_01  → SK_Veh_Small_Heli_Rotor_Main_01  / Rotor_Tail_01
    ///   SK_Veh_Drone_01       → SK_Veh_Drone_Prop
    /// </summary>
    [AddComponentMenu("Simple Military/载具动画/Heli Rotor")]
    public class HeliRotor : MonoBehaviour
    {
        [Header("旋翼节点")]
        [Tooltip("主旋翼节点，名称通常含 Rotor_Main 或 Prop")]
        public Transform mainRotor;

        [Tooltip("尾桨节点，名称通常含 Rotor_Tail；无人机可留空")]
        public Transform tailRotor;

        [Header("转速")]
        [Tooltip("主旋翼转速（度/秒）。直升机约 2000~3000，无人机可更高")]
        public float mainRotorSpeed = 2200f;

        [Tooltip("尾桨转速（度/秒），通常高于主旋翼")]
        public float tailRotorSpeed = 3600f;

        [Tooltip("主旋翼旋转轴，本资源包为 Y 轴")]
        public Vector3 mainAxis = Vector3.up;

        [Tooltip("尾桨旋转轴，本资源包为 X 轴")]
        public Vector3 tailAxis = Vector3.right;

        [Header("启停")]
        [Tooltip("是否在 Start 时自动启动旋翼")]
        public bool autoStart = true;

        [Tooltip("转速从 0 升到目标值所需的加速时间（秒）")]
        public float spinUpTime = 2.5f;

        [Tooltip("转速从目标值降到 0 所需的减速时间（秒）")]
        public float spinDownTime = 4f;

        [Header("低频采样（可选）")]
        [Tooltip("开启后旋翼按固定帧率更新，降低高转速下的计算开销")]
        public bool useFixedRate;

        [Tooltip("固定更新频率（次/秒）")]
        public float updateRate = 60f;

        [Header("只读")]
        [SerializeField] private bool isRunning;
        [SerializeField] private float currentNormalizedSpeed;
        [SerializeField] private float mainAngle;
        [SerializeField] private float tailAngle;

        private float _accumulatedTime;
        private float _step;

        /// <summary>旋翼是否处于运转状态</summary>
        public bool IsRunning => isRunning;

        /// <summary>当前转速归一化值，0~1</summary>
        public float NormalizedSpeed => currentNormalizedSpeed;

        private void Start()
        {
            if (autoStart)
                isRunning = true;
            _step = useFixedRate && updateRate > 0f ? 1f / updateRate : 0f;
        }

        /// <summary>启动旋翼</summary>
        [ContextMenu("启动旋翼")]
        public void StartRotors() => isRunning = true;

        /// <summary>停止旋翼（会按 spinDownTime 缓慢减速）</summary>
        [ContextMenu("停止旋翼")]
        public void StopRotors() => isRunning = false;

        private void Update()
        {
            // 转速缓动
            float target = isRunning ? 1f : 0f;
            float duration = isRunning ? spinUpTime : spinDownTime;
            float rate = duration > 0f ? Time.deltaTime / duration : 1f;
            currentNormalizedSpeed = Mathf.MoveTowards(currentNormalizedSpeed, target, rate);

            if (currentNormalizedSpeed <= 0.0001f)
                return;

            // 固定频率模式：累计时间后一次性步进
            if (useFixedRate && _step > 0f)
            {
                _accumulatedTime += Time.deltaTime;
                if (_accumulatedTime < _step)
                    return;
                float dt = _accumulatedTime;
                _accumulatedTime = 0f;
                ApplyRotation(dt);
            }
            else
            {
                ApplyRotation(Time.deltaTime);
            }
        }

        private void ApplyRotation(float deltaTime)
        {
            if (mainRotor != null)
            {
                mainAngle += mainRotorSpeed * currentNormalizedSpeed * deltaTime;
                mainAngle = mainAngle % 360f;
                mainRotor.localRotation = Quaternion.AngleAxis(mainAngle, mainAxis.normalized);
            }

            if (tailRotor != null)
            {
                tailAngle += tailRotorSpeed * currentNormalizedSpeed * deltaTime;
                tailAngle = tailAngle % 360f;
                tailRotor.localRotation = Quaternion.AngleAxis(tailAngle, tailAxis.normalized);
            }
        }
    }
}
