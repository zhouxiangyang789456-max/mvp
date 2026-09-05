using UnityEngine;

namespace SimpleMilitary.VehicleAnimation
{
    /// <summary>
    /// 炮塔旋转 + 炮管俯仰瞄准动画。
    ///
    /// 挂在载具根节点上，拖入炮塔与炮管节点即可：
    ///   - 炮塔绕本地 Y 轴水平旋转
    ///   - 炮管绕本地 X 轴俯仰
    ///
    /// 支持三种驱动方式：跟随目标 Transform、跟随世界坐标点、手动输入角度。
    /// 所有旋转都带角度限位与平滑插值，避免出现穿模或瞬间甩头。
    ///
    /// 本资源包中车头朝 +Z，因此炮塔 Y 轴 0 度时朝向车头正前方。
    /// 若朝向不同，调整 turretForwardAxis 即可。
    /// </summary>
    [AddComponentMenu("Simple Military/载具动画/Vehicle Turret Aim")]
    public class VehicleTurretAim : MonoBehaviour
    {
        public enum AimMode { TargetTransform, WorldPoint, ManualAngles }

        [Header("部件节点")]
        [Tooltip("炮塔节点，通常是名称含 Turret 的子物体")]
        public Transform turret;

        [Tooltip("炮管节点，通常是名称含 Barrel 的子物体")]
        public Transform barrel;

        [Header("坐标轴")]
        [Tooltip("炮塔水平旋转轴，本资源包为 Y 轴")]
        public Vector3 turretYawAxis = Vector3.up;

        [Tooltip("炮管俯仰轴，本资源包为 X 轴")]
        public Vector3 barrelPitchAxis = Vector3.right;

        [Tooltip("炮管朝向：正值为沿 -Z（本资源包炮管朝车头 +Z，配合负号使用）")]
        public bool invertBarrelDirection;

        [Header("角度限位")]
        [Tooltip("炮塔是否限制转动范围。关闭则为 360 度全向")]
        public bool limitYaw;

        [Tooltip("炮塔左右最大转角（度），仅在开启限位时生效")]
        [Range(0f, 180f)] public float yawLimit = 120f;

        [Tooltip("炮管最大仰角（度）")]
        [Range(0f, 90f)] public float maxPitch = 20f;

        [Tooltip("炮管最大俯角（度），填正数")]
        [Range(0f, 90f)] public float minPitch = 10f;

        [Header("平滑")]
        [Tooltip("炮塔旋转速度（度/秒），越大越跟手")]
        public float yawSpeed = 90f;

        [Tooltip("炮管俯仰速度（度/秒）")]
        public float pitchSpeed = 60f;

        [Header("瞄准模式")]
        [Tooltip("TargetTransform：跟随目标；WorldPoint：瞄向世界坐标；ManualAngles：手动角度")]
        public AimMode aimMode = AimMode.WorldPoint;

        [Tooltip("瞄准模式为 TargetTransform 时的跟随目标")]
        public Transform target;

        [HideInInspector]
        [Tooltip("瞄准模式为 WorldPoint 时的目标点")]
        public Vector3 worldPoint;

        [Header("只读")]
        [SerializeField] private float currentYaw;
        [SerializeField] private float currentPitch;
        [SerializeField] private bool aimReady;

        private Quaternion _turretInitLocal;
        private Quaternion _barrelInitLocal;
        private bool _recorded;

        /// <summary>炮塔当前偏航角（度）</summary>
        public float CurrentYaw => currentYaw;

        /// <summary>炮管当前俯仰角（度）</summary>
        public float CurrentPitch => currentPitch;

        /// <summary>是否已对准目标（角度差小于阈值）</summary>
        public bool AimReady => aimReady;

        private void Awake()
        {
            RecordInitialRotation();
        }

        private void RecordInitialRotation()
        {
            if (turret != null) _turretInitLocal = turret.localRotation;
            if (barrel != null) _barrelInitLocal = barrel.localRotation;
            _recorded = true;
        }

        private void LateUpdate()
        {
            if (turret == null && barrel == null)
                return;
            if (!_recorded)
                RecordInitialRotation();

            float desiredYaw = currentYaw;
            float desiredPitch = currentPitch;

            switch (aimMode)
            {
                case AimMode.TargetTransform:
                    if (target != null)
                    {
                        worldPoint = target.position;
                        ComputeAngles(worldPoint, out desiredYaw, out desiredPitch);
                    }
                    break;

                case AimMode.WorldPoint:
                    ComputeAngles(worldPoint, out desiredYaw, out desiredPitch);
                    break;

                case AimMode.ManualAngles:
                    // 直接使用 currentYaw / currentPitch，由 SetManualAngles 写入
                    break;
            }

            // 限位
            if (limitYaw)
                desiredYaw = Mathf.Clamp(desiredYaw, -yawLimit, yawLimit);
            desiredPitch = Mathf.Clamp(desiredPitch, -minPitch, maxPitch);

            // 平滑逼近
            currentYaw = Mathf.MoveTowards(currentYaw, desiredYaw, yawSpeed * Time.deltaTime);
            currentPitch = Mathf.MoveTowards(currentPitch, desiredPitch, pitchSpeed * Time.deltaTime);

            aimReady = Mathf.Abs(desiredYaw - currentYaw) < 1f &&
                       Mathf.Abs(desiredPitch - currentPitch) < 1f;

            if (turret != null)
                turret.localRotation = _turretInitLocal * Quaternion.AngleAxis(currentYaw, turretYawAxis.normalized);

            if (barrel != null)
            {
                float pitch = invertBarrelDirection ? -currentPitch : currentPitch;
                barrel.localRotation = _barrelInitLocal * Quaternion.AngleAxis(pitch, barrelPitchAxis.normalized);
            }
        }

        private void ComputeAngles(Vector3 point, out float yaw, out float pitch)
        {
            yaw = currentYaw;
            pitch = currentPitch;

            Transform turretRef = turret != null ? turret : transform;

            // 把目标点转换到炮塔的父级空间，保证车体旋转时瞄准依然正确
            Transform parent = turretRef.parent != null ? turretRef.parent : transform;
            Vector3 localTarget = parent.InverseTransformPoint(point);

            // 车头朝 +Z：偏航为绕 Y 轴的角度
            float distXZ = Mathf.Sqrt(localTarget.x * localTarget.x + localTarget.z * localTarget.z);
            yaw = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

            // 高度差决定俯仰
            float heightDiff = localTarget.y - turretRef.localPosition.y;
            pitch = -Mathf.Atan2(heightDiff, distXZ) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 手动设置瞄准角度（配合 AimMode.ManualAngles 使用）。
        /// </summary>
        /// <param name="yaw">炮塔偏航角（度）</param>
        /// <param name="pitch">炮管俯仰角（度），正为抬高</param>
        public void SetManualAngles(float yaw, float pitch)
        {
            currentYaw = yaw;
            currentPitch = pitch;
        }

        /// <summary>
        /// 设置瞄准目标点（自动切换到 WorldPoint 模式）。
        /// </summary>
        public void SetAimPoint(Vector3 point)
        {
            aimMode = AimMode.WorldPoint;
            worldPoint = point;
        }

        /// <summary>
        /// 设置瞄准目标对象（自动切换到 TargetTransform 模式）。
        /// </summary>
        public void SetAimTarget(Transform newTarget)
        {
            aimMode = AimMode.TargetTransform;
            target = newTarget;
        }

        /// <summary>
        /// 获取炮口世界坐标，用于生成子弹或炮口火焰。
        /// </summary>
        public Vector3 GetMuzzlePosition()
        {
            if (barrel == null)
                return transform.position;
            // 默认取炮管末端：沿炮管本地 +Z 延伸，具体长度可按实际模型微调
            return barrel.TransformPoint(Vector3.forward * 0.5f);
        }

        private void OnValidate()
        {
            _recorded = false;
        }
    }
}
