using System.Collections.Generic;
using UnityEngine;

namespace SimpleMilitary.VehicleAnimation
{
    /// <summary>
    /// 履带纹理滚动动画。
    ///
    /// 重要前提：本资源包的坦克（SK_Veh_Tank_01 / SK_Veh_tank_02）底盘与履带共用
    /// 同一套网格和材质，且只有 1 个材质槽。直接滚动会让整车贴图跟着动。
    /// 因此使用前必须先把履带拆成独立对象，三选一：
    ///   A. 在 Blender 中把履带面片分离为独立子物体（效果最好）
    ///   B. 在履带位置叠加独立的长条贴片（零改模，见文档）
    ///   C. 用顶点色遮罩 + 自定义 Shader 只滚动履带区域（见文档配套 Shader）
    ///
    /// 拆好后把履带 Renderer 填入本组件即可。
    /// </summary>
    [AddComponentMenu("Simple Military/载具动画/Tank Track Scroll")]
    public class TankTrackScroll : MonoBehaviour
    {
        public enum ScrollChannel { U, V }
        public enum ApplyMode { InstanceMaterial, PropertyBlock }

        [Header("履带对象")]
        [Tooltip("拖入履带的 MeshRenderer / SkinnedMeshRenderer。可多选（左右两条）")]
        public List<Renderer> trackRenderers = new List<Renderer>();

        [Header("滚动参数")]
        [Tooltip("沿哪个 UV 方向滚动。多数履带贴图沿 V（即 Y）方向")]
        public ScrollChannel channel = ScrollChannel.V;

        [Tooltip("纹理滚动系数：每米位移滚动多少 UV。贴图重复段数越多，此值应越大")]
        public float scrollPerMeter = 1f;

        [Tooltip("材质中控制主贴图偏移的属性名。Built-in/Standard 为 _MainTex，URP/Lit 为 _BaseMap")]
        public string texturePropertyName = "_MainTex";

        [Header("材质处理")]
        [Tooltip("InstanceMaterial：复制一份材质再改（安全，不污染原始资源）；" +
                 "PropertyBlock：不生成新材质，性能更好但 Inspector 中看不到变化")]
        public ApplyMode applyMode = ApplyMode.InstanceMaterial;

        [Tooltip("若发现目标 Renderer 的材质被其他对象共用，在控制台给出警告")]
        public bool warnOnSharedMaterial = true;

        [Header("速度来源")]
        [Tooltip("留空则自动在本对象上查找 VehicleMotion 组件")]
        public VehicleMotion motion;

        [Header("只读")]
        [SerializeField] private float currentOffset;

        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private readonly List<int> _materialIndices = new List<int>();
        private MaterialPropertyBlock _block;
        private bool _prepared;
        private bool _warned;

        private void Awake()
        {
            if (motion == null)
                motion = GetComponent<VehicleMotion>();
            _block = new MaterialPropertyBlock();
            PrepareMaterials();
        }

        private void OnDestroy()
        {
            // 清理运行期生成的材质实例，避免退出后残留
            foreach (var m in _runtimeMaterials)
            {
                if (m != null)
                    Destroy(m);
            }
            _runtimeMaterials.Clear();
        }

        /// <summary>
        /// 准备材质：按所选模式复制实例或记录索引，并检查材质是否被共用。
        /// </summary>
        [ContextMenu("重新准备材质")]
        public void PrepareMaterials()
        {
            _runtimeMaterials.Clear();
            _materialIndices.Clear();
            _prepared = true;

            var usage = new Dictionary<Material, int>();
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    usage.TryGetValue(m, out int c);
                    usage[m] = c + 1;
                }
            }

            foreach (var r in trackRenderers)
            {
                if (r == null) continue;

                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material src = mats[i];
                    if (src == null) continue;

                    if (warnOnSharedMaterial && usage.TryGetValue(src, out int count) && count > 1)
                    {
                        Debug.LogWarning(
                            $"[TankTrackScroll] 材质「{src.name}」被 {count} 个 Renderer 共用，" +
                            $"滚动它会影响其他部件（通常是车身）。请先把履带拆成独立对象并赋予单独材质。",
                            r);
                        _warned = true;
                    }

                    if (applyMode == ApplyMode.InstanceMaterial)
                    {
                        Material inst = new Material(src);
                        inst.name = src.name + " (TrackScroll)";
                        _runtimeMaterials.Add(inst);

                        var arr = r.sharedMaterials;
                        arr[i] = inst;
                        r.sharedMaterials = arr;
                    }
                }
                _materialIndices.Add(mats.Length);
            }
        }

        private void LateUpdate()
        {
            if (!_prepared)
                PrepareMaterials();

            float speed = motion != null ? motion.ForwardSpeed : 0f;
            currentOffset += speed * scrollPerMeter * Time.deltaTime;
            // 保持在 [0,1) 区间，避免长时间运行后浮点精度下降
            currentOffset = currentOffset - Mathf.Floor(currentOffset);

            bool isU = channel == ScrollChannel.U;

            foreach (var r in trackRenderers)
            {
                if (r == null) continue;

                if (applyMode == ApplyMode.PropertyBlock)
                {
                    r.GetPropertyBlock(_block);
                    Vector4 st = GetTextureST(r);
                    st.x = 1f; st.y = 1f; // tiling 保持原值需从材质读取，此处维持 1
                    if (isU) st.z = currentOffset; else st.w = currentOffset;
                    _block.SetVector(texturePropertyName + "_ST", st);
                    r.SetPropertyBlock(_block);
                }
                else
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (!m.HasProperty(texturePropertyName)) continue;
                        Vector2 offset = m.GetTextureOffset(texturePropertyName);
                        if (isU) offset.x = currentOffset; else offset.y = currentOffset;
                        m.SetTextureOffset(texturePropertyName, offset);
                    }
                }
            }
        }

        private Vector4 GetTextureST(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                string stName = texturePropertyName + "_ST";
                if (m.HasProperty(stName))
                    return m.GetVector(stName);
            }
            return new Vector4(1, 1, 0, 0);
        }

        /// <summary>
        /// 是否检测到材质共用问题（供编辑器工具读取）。
        /// </summary>
        public bool HasSharedMaterialWarning => _warned;

        private void OnValidate()
        {
            _prepared = false;
        }
    }
}
