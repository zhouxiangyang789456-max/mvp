using System.Collections.Generic;
using UnityEngine;

namespace SimpleMilitary.VehicleAnimation
{
    /// <summary>
    /// 车轮滚动与转向动画。
    ///
    /// 挂在载具根节点上，自动收集名称含 "wheel" 的子节点作为车轮，
    /// 按实际行驶速度驱动轮子绕本地 X 轴滚动，并可选让前轮跟随转向输入偏转。
    ///
    /// 本资源包中车轮沿 X 轴左右分布、车头朝 +Z，因此滚动轴为 X，转向轴为 Y。
    /// 若你的载具朝向与此不同，可在 Inspector 中调整轴向。
    /// </summary>
    [AddComponentMenu("Simple Military/载具动画/Vehicle Wheels")]
    public class VehicleWheels : MonoBehaviour
    {
        [Header("车轮节点")]
        [Tooltip("开启后自动收集子节点中名称包含关键词的对象作为车轮")]
        public bool autoFindWheels = true;

        [Tooltip("自动匹配时使用的名称关键词，不区分大小写")]
        public string wheelNameFilter = "wheel";

        [Tooltip("手动指定的车轮节点；留空且未开启自动收集时不会有任何车轮转动")]
        public List<Transform> wheels = new List<Transform>();

        [Header("滚动参数")]
        [Tooltip("车轮半径（米）。半径越小，相同速度下转得越快。设为 0 时按 0.5 处理")]
        public float wheelRadius = 0.5f;

        [Tooltip("滚动轴。本资源包为 X 轴（车头朝 +Z）")]
        public Vector3 spinAxis = Vector3.right;

        [Tooltip("转向轴。本资源包为 Y 轴")]
        public Vector3 steerAxis = Vector3.up;

        [Tooltip("倒车时轮子反转。关闭则倒车时轮子仍正向转")]
        public bool reverseWithVelocity = true;

        [Header("转向（可选）")]
        [Tooltip("开启前轮转向。需要外部调用 SetSteerInput 提供转向量")]
        public bool enableSteering;

        [Tooltip("参与转向的车轮；留空则自动取名称含 front/fl/fr 或 z 坐标最大的车轮")]
        public List<Transform> steeringWheels = new List<Transform>();

        [Range(0f, 90f)]
        [Tooltip("最大转向角（度）")]
        public float maxSteerAngle = 30f;

        [Range(0.1f, 30f)]
        [Tooltip("转向跟随速度，越大越灵敏")]
        public float steerLerpSpeed = 8f;

        [Header("速度来源")]
        [Tooltip("留空则自动在本对象上查找 VehicleMotion 组件")]
        public VehicleMotion motion;

        private float _currentSteerAngle;
        private float _targetSteerInput;
        private float _spinAngle;
        private bool _collected;
        private readonly Dictionary<Transform, Quaternion> _initialRotations =
            new Dictionary<Transform, Quaternion>();

        /// <summary>当前累积滚动角度，调试用</summary>
        public float SpinAngle => _spinAngle;

        private void Awake()
        {
            CollectWheels();
            if (motion == null)
                motion = GetComponent<VehicleMotion>();
        }

        /// <summary>
        /// 收集车轮节点。可在 Inspector 修改参数后手动调用以刷新。
        /// </summary>
        [ContextMenu("收集车轮节点")]
        public void CollectWheels()
        {
            if (!autoFindWheels)
            {
                _collected = true;
                return;
            }

            wheels.Clear();
            _initialRotations.Clear();
            string filter = (wheelNameFilter ?? "wheel").ToLowerInvariant();
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == transform)
                    continue;
                if (t.name.ToLowerInvariant().Contains(filter))
                {
                    wheels.Add(t);
                    _initialRotations[t] = t.localRotation;
                }
            }

            _collected = true;

            if (enableSteering && steeringWheels.Count == 0)
                AutoPickSteeringWheels();
        }

        /// <summary>
        /// 自动挑选前轮：优先按名称含 front/fl/fr，否则取本地 Z 坐标最大的两个。
        /// </summary>
        [ContextMenu("自动挑选前轮")]
        public void AutoPickSteeringWheels()
        {
            steeringWheels.Clear();
            if (wheels.Count == 0)
                return;

            // 先按名称匹配
            foreach (var w in wheels)
            {
                string n = w.name.ToLowerInvariant();
                if (n.Contains("front") || n.Contains("_fl") || n.Contains("_fr"))
                    steeringWheels.Add(w);
            }
            if (steeringWheels.Count > 0)
                return;

            // 名称匹配失败时，按本地 Z 坐标（车头方向）挑最靠前的两个
            var sorted = new List<Transform>(wheels);
            sorted.Sort((a, b) => b.localPosition.z.CompareTo(a.localPosition.z));
            int count = Mathf.Min(2, sorted.Count);
            for (int i = 0; i < count; i++)
                steeringWheels.Add(sorted[i]);
        }

        private void LateUpdate()
        {
            if (!_collected)
                CollectWheels();

            float speed = motion != null ? motion.ForwardSpeed : 0f;
            if (!reverseWithVelocity)
                speed = Mathf.Abs(speed);

            // 角速度 ω = v / r，转换为角度后逐帧累加
            float radius = wheelRadius <= 0f ? 0.5f : wheelRadius;
            float angularSpeed = speed / radius;                 // 弧度/秒
            float deltaAngle = angularSpeed * Mathf.Rad2Deg * Time.deltaTime;
            _spinAngle += deltaAngle;

            Vector3 axis = spinAxis.normalized;
            for (int i = 0; i < wheels.Count; i++)
            {
                var w = wheels[i];
                if (w == null)
                    continue;

                if (enableSteering && steeringWheels.Contains(w))
                {
                    // 转向轮：先应用转向角（Y 轴），再叠加滚动（X 轴）
                    Quaternion initial = GetInitialRotation(w);
                    w.localRotation = initial
                                      * Quaternion.AngleAxis(_currentSteerAngle, steerAxis.normalized)
                                      * Quaternion.AngleAxis(_spinAngle, axis);
                }
                else
                {
                    w.localRotation = GetInitialRotation(w) * Quaternion.AngleAxis(_spinAngle, axis);
                }
            }

            if (enableSteering)
            {
                float target = _targetSteerInput * maxSteerAngle;
                _currentSteerAngle = Mathf.Lerp(_currentSteerAngle, target,
                    Time.deltaTime * steerLerpSpeed);
            }
        }

        /// <summary>
        /// 设置转向输入，范围 [-1, 1]，-1 为左满舵、1 为右满舵。
        /// 由你的载具控制脚本每帧调用，或接到输入轴上。
        /// </summary>
        public void SetSteerInput(float input)
        {
            _targetSteerInput = Mathf.Clamp(input, -1f, 1f);
        }

        private Quaternion GetInitialRotation(Transform wheel)
        {
            if (!_initialRotations.TryGetValue(wheel, out Quaternion initial))
            {
                initial = wheel.localRotation;
                _initialRotations[wheel] = initial;
            }
            return initial;
        }

        private void OnValidate()
        {
            // 编辑器中改动参数时标记需重新收集
            _collected = false;
        }
    }
}
