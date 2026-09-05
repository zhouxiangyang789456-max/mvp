using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Mvp.Battle.Map;
using Mvp.Battle.Map.Generation;
using Mvp.Shared;

namespace Mvp.Editor.MapGeneration
{
    /// <summary>
    /// 随机地图配置工具 (随机地图生成接入方案 §6/§8). Lets a designer tweak generator
    /// parameters, preview the battle grid, batch-verify a rule over many seeds, manage
    /// level-range rules inside a LevelMapGenerationProfile and save it as an asset.
    ///
    /// Batch verification runs on a background task (pure C# only) so the editor stays
    /// responsive and the job can be cancelled.
    /// </summary>
    public sealed class RandomMapToolWindow : EditorWindow
    {
        [MenuItem("Tools/Map Generation/Random Map Tool")]
        public static void Open()
        {
            GetWindow<RandomMapToolWindow>("随机地图配置工具");
        }

        // ---- configuration asset + selected rule -------------------------------
        LevelMapGenerationProfile _profile;
        int _selectedRuleIndex = -1;

        // ---- workspace parameters (left panel) ----------------------------------
        MapGenerationSettings _settings = new MapGenerationSettings();
        MapValidationSettings _validation = new MapValidationSettings();
        int _retryCount = 10;
        int _playerDeploymentGroups = 1;
        int _enemyDeploymentGroups = 2;
        [NonSerialized] Vector2 _parameterScroll;

        // ---- preview -------------------------------------------------------------
        [NonSerialized] TerrainType[,] _lastBattle;
        [NonSerialized] GeneratedMapData _lastData;
        [NonSerialized] GeneratedMapIdentity _lastIdentity;
        [NonSerialized] MapValidationResult _lastValidation;
        [NonSerialized] Texture2D _previewTex;
        [NonSerialized] Vector2 _previewScroll;
        bool _hasHover;
        Vector2Int _hoverCell;

        // ---- preview by level / reproduce ----------------------------------------
        int _previewLevel = 1;
        string _reproSeedText = "20260818";

        // ---- batch verification ----------------------------------------------------
        [NonSerialized] BatchJob _batchJob;
        [NonSerialized] List<string> _batchReport;
        int _batchSeedCount = 100;

        // ---- presets -----------------------------------------------------------------
        int _presetIndex = -1;
        static readonly string[] PresetNames =
        {
            "1-3 新手平原", "4-6 森林丘陵", "7-10 河流分割", "11+ 大地图混合",
            "16x14 默认(§16)", "24x18 稳定(§16)"
        };

        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            minSize = new Vector2(960, 540);
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnEditorUpdate()
        {
            if (_batchJob == null) return;
            if (!_batchJob.Completed) { Repaint(); return; }
            var job = _batchJob;
            _batchJob = null;
            _batchReport = BuildBatchReport(job);
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("随机地图配置工具", EditorStyles.boldLabel);
            DrawToolbar();
            DrawProfileHeader();

            GUILayout.BeginHorizontal();
            float leftPanelHeight = Mathf.Max(260f, position.height - 150f);
            GUILayout.BeginVertical("box", GUILayout.Width(290), GUILayout.Height(leftPanelHeight));
            _parameterScroll = EditorGUILayout.BeginScrollView(
                _parameterScroll,
                false,
                true,
                GUILayout.ExpandHeight(true));
            DrawParameterPanel();
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical("box");
            DrawPreviewPanel();
            DrawBatchPanel();
            DrawLevelPreview();
            GUILayout.EndVertical();

            GUILayout.BeginVertical("box", GUILayout.Width(330));
            DrawRulePanel();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            DrawReproducePanel();
        }

        // ---- top toolbar --------------------------------------------------------------

        void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重新生成", GUILayout.Width(92))) Regenerate();
            if (GUILayout.Button("随机种子", GUILayout.Width(92))) RandomSeed();
            if (GUILayout.Button("验证当前地图", GUILayout.Width(110))) RunValidation();
            if (GUILayout.Button("导出预览 PNG", GUILayout.Width(110))) ExportPng();
            if (GUILayout.Button("应用到 BattleScene 并运行", GUILayout.Width(180))) ApplyToBattleSceneAndPlay();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("batch: " + (_batchJob != null ? (_batchJob.Done + "/" + _batchJob.Total) : "空闲"));
            GUILayout.EndHorizontal();
        }

        // ---- profile header -----------------------------------------------------------

        void DrawProfileHeader()
        {
            EditorGUILayout.BeginHorizontal();
            var newProfile = (LevelMapGenerationProfile)EditorGUILayout.ObjectField(
                "配置资产", _profile, typeof(LevelMapGenerationProfile), false);
            if (newProfile != _profile)
            {
                _profile = newProfile;
                _selectedRuleIndex = -1;
            }
            if (GUILayout.Button("新建", GUILayout.Width(56))) CreateProfile();
            if (GUILayout.Button("保存", GUILayout.Width(56))) SaveProfile();
            EditorGUILayout.EndHorizontal();

            if (_profile != null)
            {
                string error = _profile.ValidateConfiguration();
                if (error != null)
                    EditorGUILayout.HelpBox("配置校验失败: " + error, MessageType.Error);
                else
                    EditorGUILayout.LabelField("ProfileId=" + _profile.ProfileId
                        + "  v" + _profile.ProfileVersion
                        + "  gen=" + _profile.GeneratorVersion
                        + "  salt=" + _profile.ProfileSalt);
                GUILayout.BeginHorizontal();
                _profile.ProfileId = EditorGUILayout.TextField("ProfileId", _profile.ProfileId);
                _profile.ProfileVersion = EditorGUILayout.IntField("版本", _profile.ProfileVersion);
                GUILayout.EndHorizontal();
                _profile.ProfileSalt = (uint)EditorGUILayout.LongField("盐", _profile.ProfileSalt);
            }
        }

        void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "新建地图生成配置", "MapGenerationProfile", "asset", "保存到项目 (建议 Assets/ScriptableObjects/MapGeneration)");
            if (string.IsNullOrEmpty(path)) return;
            _profile = ScriptableObject.CreateInstance<LevelMapGenerationProfile>();
            _profile.ProfileId = "profile_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _profile.ProfileVersion = 1;
            long ticks = DateTime.UtcNow.Ticks;
            _profile.ProfileSalt = unchecked((uint)(ticks ^ (ticks >> 32)));
            AssetDatabase.CreateAsset(_profile, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(_profile);
            _selectedRuleIndex = -1;
            Debug.Log("[RandomMapTool] 新建配置 " + path);
        }

        void SaveProfile()
        {
            if (_profile == null) return;
            string error = _profile.ValidateConfiguration();
            if (error != null)
            {
                EditorUtility.DisplayDialog("无法保存", "配置校验失败: " + error, "确定");
                return;
            }
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[RandomMapTool] 已保存 " + AssetDatabase.GetAssetPath(_profile));
        }

        // ---- left parameter panel -----------------------------------------------------

        void DrawParameterPanel()
        {
            EditorGUILayout.LabelField("参数", EditorStyles.boldLabel);

            int preset = EditorGUILayout.Popup("预设", _presetIndex, PresetNames);
            if (preset != _presetIndex)
            {
                _presetIndex = preset;
                ApplyPreset(preset);
            }

            GUILayout.Space(4);
            _settings.Width = Mathf.Max(8, EditorGUILayout.IntField("宽", _settings.Width));
            _settings.Height = Mathf.Max(8, EditorGUILayout.IntField("高", _settings.Height));
            _settings.Seed = (uint)Math.Max(0, EditorGUILayout.LongField("种子", _settings.Seed));

            _settings.SeaLevel = EditorGUILayout.Slider("海平面", _settings.SeaLevel, 0f, 1f);
            _settings.MountainLevel = EditorGUILayout.Slider("山地高度线", _settings.MountainLevel, 0f, 1f);
            _settings.ForestMoisture = EditorGUILayout.Slider("森林湿度线", _settings.ForestMoisture, 0f, 1f);
            _settings.Rivers = EditorGUILayout.IntField("河流数", _settings.Rivers);
            _settings.BridgeSpan = EditorGUILayout.IntField("桥跨格数", _settings.BridgeSpan);
            EditorGUILayout.HelpBox(
                "道路按邻接自动使用直路、弯道、T 字和十字贴图；跨河道路自动显示桥梁。",
                MessageType.None);
            _settings.SmoothRounds = EditorGUILayout.IntField("平滑轮数", _settings.SmoothRounds);
            _settings.Mirror = EditorGUILayout.Toggle("180° 对称", _settings.Mirror);
            _settings.Buildings = EditorGUILayout.Toggle("生成建筑", _settings.Buildings);
            if (_settings.Buildings)
            {
                _settings.Roads = EditorGUILayout.Toggle("生成道路与桥梁", _settings.Roads);
                _settings.HouseCount = Mathf.Max(0,
                    EditorGUILayout.IntField("楼房数量", _settings.HouseCount));
                _settings.ArmoryCount = Mathf.Max(0,
                    EditorGUILayout.IntField("兵工厂数量", _settings.ArmoryCount));
            }

            GUILayout.Space(4);
            _settings.Ocean = EditorGUILayout.Toggle("海洋", _settings.Ocean);
            _settings.Beach = EditorGUILayout.Toggle("沙滩", _settings.Beach);
            _settings.River = EditorGUILayout.Toggle("河流", _settings.River);
            _settings.Forest = EditorGUILayout.Toggle("森林", _settings.Forest);
            _settings.Mountain = EditorGUILayout.Toggle("山地", _settings.Mountain);

            GUILayout.Space(4);
            EditorGUILayout.LabelField("校验", EditorStyles.miniBoldLabel);
            _validation.MinWalkableRatio = EditorGUILayout.Slider("最小可通行比例", _validation.MinWalkableRatio, 0f, 1f);
            _validation.MaxWalkableRatio = EditorGUILayout.Slider("最大可通行比例", _validation.MaxWalkableRatio, 0f, 1f);
            _validation.MinWalkableComponentRatio = EditorGUILayout.Slider("最小连通分量占比", _validation.MinWalkableComponentRatio, 0f, 1f);
            _validation.MinDeploymentArea = EditorGUILayout.IntField("最小部署面积", _validation.MinDeploymentArea);
            _playerDeploymentGroups = Mathf.Max(1,
                EditorGUILayout.IntField("玩家编队数", _playerDeploymentGroups));
            _enemyDeploymentGroups = Mathf.Max(0,
                EditorGUILayout.IntField("敌方编队数", _enemyDeploymentGroups));
            _retryCount = Mathf.Max(1, EditorGUILayout.IntField("最大重试次数", _retryCount));

            GUILayout.Space(6);
            if (_profile != null && _selectedRuleIndex >= 0 && _selectedRuleIndex < _profile.Rules.Count)
            {
                EditorGUILayout.HelpBox("正在编辑规则: " + _profile.Rules[_selectedRuleIndex].DisplayName, MessageType.Info);
                if (GUILayout.Button("写入选中规则")) WriteWorkspaceToSelectedRule();
            }

            GUILayout.Space(6);
            EditorGUILayout.LabelField("限时传送门撤离 (目标类型)", EditorStyles.miniBoldLabel);
            _settings.EnableExtractionPortal = EditorGUILayout.Toggle(
                new GUIContent("启用撤离传送门",
                    "勾选后此关卡会成为“限时撤离”目标；预览图会标记传送门位置与距离玩家路径步数。"),
                _settings.EnableExtractionPortal);
            using (new EditorGUI.DisabledScope(!_settings.EnableExtractionPortal))
            {
                _settings.ExtractionTimeLimitSeconds = Mathf.Clamp(
                    EditorGUILayout.IntField("撤离时限 (秒)", _settings.ExtractionTimeLimitSeconds), 5, 1800);
                _settings.ExtractionZoneWidth = Mathf.Clamp(
                    EditorGUILayout.IntField("撤离区宽 (格)", _settings.ExtractionZoneWidth), 1, 4);
                _settings.ExtractionZoneHeight = Mathf.Clamp(
                    EditorGUILayout.IntField("撤离区高 (格)", _settings.ExtractionZoneHeight), 1, 4);
                _settings.MinPortalPathDistanceFromPlayer = Mathf.Clamp(
                    EditorGUILayout.IntField("距玩家最近路径 (min)", _settings.MinPortalPathDistanceFromPlayer), 0, 64);
                _settings.MaxPortalPathDistanceFromPlayer = Mathf.Clamp(
                    EditorGUILayout.IntField("距玩家最近路径 (max)", _settings.MaxPortalPathDistanceFromPlayer), 1, 96);
                if (_settings.MaxPortalPathDistanceFromPlayer < _settings.MinPortalPathDistanceFromPlayer)
                    _settings.MaxPortalPathDistanceFromPlayer = _settings.MinPortalPathDistanceFromPlayer;
                _settings.PortalOpeningDelaySeconds = Mathf.Clamp(
                    EditorGUILayout.FloatField("开门延迟 (秒)", _settings.PortalOpeningDelaySeconds), 0f, 10f);
                EditorGUILayout.HelpBox(
                    "传送门会被放到“远处角落”：与最近玩家部署格的最短路径步数落在 [min, max] 区间，再按距离带中点 + 地图中心距离择优。\n" +
                    "想靠近角落就调高 min；想避开中线就减小 max。",
                    MessageType.None);
            }
        }

        void ApplyPreset(int index)
        {
            switch (index)
            {
                case 0: SetSettings(16, 14, 0.30f, 0.72f, 0.64f, 0, false, false, 1); break;   // 1-3 新手平原
                case 1: SetSettings(16, 14, 0.34f, 0.68f, 0.58f, 1, false, false, 4); break;   // 4-6 森林丘陵
                case 2: SetSettings(20, 16, 0.36f, 0.66f, 0.56f, 1, false, false, 7); break;   // 7-10 河流分割
                case 3: SetSettings(24, 18, 0.38f, 0.66f, 0.58f, 2, true, false, 11); break;   // 11+ 大地图混合
                case 4: SetSettings(16, 14, 0.36f, 0.68f, 0.60f, 1, false, false, 1); break;   // §16 默认
                case 5: SetSettings(24, 18, 0.38f, 0.66f, 0.58f, 2, true, false, 1); break;    // §16 稳定
                default: return;
            }
            Regenerate();
        }

        void SetSettings(int w, int h, float sea, float mountain, float forest, int rivers, bool mirror, bool buildings,
            int rangeStart)
        {
            _settings = new MapGenerationSettings
            {
                Width = w, Height = h,
                Seed = _settings != null ? _settings.Seed : 20260818u,
                SeaLevel = sea, MountainLevel = mountain, ForestMoisture = forest,
                Rivers = rivers, BridgeSpan = 3, SmoothRounds = 2,
                Mirror = mirror, Buildings = buildings
            };
            _previewLevel = rangeStart;
        }

        // ---- preview ------------------------------------------------------------------

        void DrawPreviewPanel()
        {
            EditorGUILayout.LabelField("地图预览", EditorStyles.boldLabel);
            if (_lastBattle == null || _previewTex == null)
            {
                EditorGUILayout.HelpBox("点击“重新生成”预览地图", MessageType.Info);
                return;
            }

            int w = _lastBattle.GetLength(1);
            int h = _lastBattle.GetLength(0);
            const float cell = 14f;
            float pxW = w * cell, pxH = h * cell;

            _previewScroll = GUILayout.BeginScrollView(_previewScroll);
            var rect = GUILayoutUtility.GetRect(pxW, pxH, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            GUI.DrawTexture(rect, _previewTex);

            var ev = Event.current;
            if (ev != null && rect.Contains(ev.mousePosition))
            {
                int cx = Mathf.FloorToInt((ev.mousePosition.x - rect.x) / cell);
                int cz = Mathf.FloorToInt((ev.mousePosition.y - rect.y) / cell);
                _hasHover = cx >= 0 && cx < w && cz >= 0 && cz < h;
                _hoverCell = new Vector2Int(cx, cz);
                if (_hasHover) Repaint();
            }

            if (_hasHover && _hoverCell.x >= 0 && _hoverCell.x < w && _hoverCell.y >= 0 && _hoverCell.y < h)
            {
                var hr = new Rect(rect.x + _hoverCell.x * cell, rect.y + _hoverCell.y * cell, cell, cell);
                EditorGUI.DrawRect(hr, new Color(1f, 1f, 0f, 0.35f));
            }
            DrawDeploymentOverlay(rect, cell);
            DrawBuildingOverlay(rect, cell);
            DrawPortalOverlay(rect, cell);

            GUILayout.EndScrollView();

            if (_hasHover && _hoverCell.x >= 0 && _hoverCell.x < w && _hoverCell.y >= 0 && _hoverCell.y < h)
            {
                var t = _lastBattle[_hoverCell.y, _hoverCell.x];
                EditorGUILayout.LabelField("(" + _hoverCell.x + ", " + _hoverCell.y + ") "
                    + TerrainCatalog.GetDisplayName(t)
                    + (TerrainCatalog.IsWalkable(t) ? " 可走" : " 阻挡"));
            }

            DrawBuildingStats();

            if (_lastData != null)
            {
                EditorGUILayout.LabelField("尺寸 " + _lastData.Width + "x" + _lastData.Height
                    + "  seed=" + _lastData.Seed
                    + "  哈希=" + _lastData.MapHash);
            }
            if (_lastValidation != null)
                EditorGUILayout.LabelField(_lastValidation.Passed
                    ? "校验通过"
                    : "校验失败: " + _lastValidation.ToString());
            DrawPortalStats();
        }

        void DrawDeploymentOverlay(Rect rect, float cell)
        {
            if (_lastData == null) return;
            foreach (var c in _lastData.PlayerDeploymentCells)
                EditorGUI.DrawRect(new Rect(rect.x + c.X * cell, rect.y + c.Y * cell, cell, cell), new Color(0.2f, 0.6f, 1f, 0.4f));
            foreach (var c in _lastData.EnemyDeploymentCells)
                EditorGUI.DrawRect(new Rect(rect.x + c.X * cell, rect.y + c.Y * cell, cell, cell), new Color(1f, 0.3f, 0.3f, 0.4f));
        }

        static readonly Color HouseMarkerColor = new Color(0.949f, 0.788f, 0.298f, 0.95f); // #F2C94C
        static readonly Color ArmoryMarkerColor = new Color(0.851f, 0.294f, 0.271f, 0.95f); // #D94B45

        /// <summary>Draws a distinct inset marker for each placed building on the preview.</summary>
        void DrawBuildingOverlay(Rect rect, float cell)
        {
            if (_lastData == null || _lastData.BuildingSpawnData == null) return;
            float inset = Mathf.Max(1f, cell * 0.22f);
            for (int i = 0; i < _lastData.BuildingSpawnData.Count; i++)
            {
                var a = _lastData.BuildingSpawnData[i].AnchorCell;
                Color c = _lastData.BuildingSpawnData[i].DefinitionId == "building_armory"
                    ? ArmoryMarkerColor
                    : HouseMarkerColor;
                EditorGUI.DrawRect(new Rect(rect.x + a.x * cell + inset, rect.y + a.y * cell + inset,
                    cell - inset * 2f, cell - inset * 2f), c);
            }
        }

        /// <summary>计划/实际数量、非平原/越界/重叠校验与实际不足警告。</summary>
        void DrawBuildingStats()
        {
            if (_lastData == null || _lastData.Buildings == null || _lastData.BuildingReport == null) return;

            var report = _lastData.BuildingReport;
            EditorGUILayout.LabelField("建筑统计 (楼房 / 兵工厂):", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("  计划 " + report.RequestedHouse + " / " + report.RequestedArmory
                + "   实际 " + report.PlacedHouse + " / " + report.PlacedArmory, EditorStyles.miniLabel);

            bool valid = report.IsValid;
            EditorGUILayout.LabelField(valid
                ? "  非平原格=0  越界=0  重叠=0"
                : "  非平原格=" + report.NonPlainCells
                    + "  越界=" + report.OutOfBoundsCells
                    + "  重叠=" + report.OverlapCells, EditorStyles.miniLabel);

            if (!valid)
                EditorGUILayout.HelpBox("建筑放置违规：建筑所在格必须为平原（非平原/越界/重叠须为 0）", MessageType.Error);
            else if (report.PlacedHouse < report.RequestedHouse || report.PlacedArmory < report.RequestedArmory)
                EditorGUILayout.HelpBox("平原不足：实际建筑数量少于计划数量", MessageType.Warning);
        }

        // ---- extraction portal overlay + stats ---------------------------------------

        static readonly Color PortalFillColor   = new Color(0.62f, 0.30f, 0.92f, 0.45f); // #9F4DEB 半透明
        static readonly Color PortalBorderColor = new Color(0.85f, 0.55f, 1.00f, 0.95f);
        static readonly Color PortalDistanceColor = new Color(1f, 1f, 1f, 0.85f);
        static GUIStyle _portalLabelStyle;
        static GUIStyle PortalLabelStyle
        {
            get
            {
                if (_portalLabelStyle == null)
                {
                    _portalLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                    _portalLabelStyle.alignment = TextAnchor.MiddleCenter;
                    _portalLabelStyle.fontSize = 12;
                    _portalLabelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
                }
                return _portalLabelStyle;
            }
        }

        /// <summary>Draws the extraction portal footprint with a coloured fill, border
        /// and a center "P" marker so designers can spot it on the grid preview.</summary>
        void DrawPortalOverlay(Rect rect, float cell)
        {
            if (_lastData == null || _lastData.Portal == null) return;
            var p = _lastData.Portal;
            var footprint = new Rect(rect.x + p.AnchorCell.x * cell,
                rect.y + p.AnchorCell.y * cell, p.Width * cell, p.Height * cell);
            EditorGUI.DrawRect(footprint, PortalFillColor);
            DrawRectBorder(footprint, PortalBorderColor, 2f);
            GUI.Label(footprint, "P", PortalLabelStyle);
        }

        static void DrawRectBorder(Rect r, Color c, float thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }

        /// <summary>Shows portal anchor/size + shortest path distance to the nearest
        /// player deployment cell so designers can verify it lands in the "far corner".</summary>
        void DrawPortalStats()
        {
            if (_lastData == null) return;
            EditorGUILayout.LabelField("目标类型", EditorStyles.miniBoldLabel);
            if (_lastData.Portal == null)
            {
                EditorGUILayout.LabelField(_settings.EnableExtractionPortal
                    ? "  撤离 (本次生成失败——已自动重试仍无可用位置，可放宽 min/max 或开关后再生)"
                    : "  消灭 (未启用撤离传送门)", EditorStyles.miniLabel);
                return;
            }

            var p = _lastData.Portal;
            int dist = ShortestPathFromPortalToPlayer(p);
            int minD = Mathf.Max(0, _settings.MinPortalPathDistanceFromPlayer);
            int maxD = Mathf.Max(minD, _settings.MaxPortalPathDistanceFromPlayer);
            bool inBand = dist >= minD && dist <= maxD;

            EditorGUILayout.LabelField("  模式: 限时撤离  目标=" + p.TimeLimitSeconds + "s", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("  锚点: (" + p.AnchorCell.x + ", " + p.AnchorCell.y
                + ")  尺寸: " + p.Width + "x" + p.Height + "  开门延迟: "
                + p.OpeningDelaySeconds.ToString("0.0") + "s", EditorStyles.miniLabel);

            string bandMsg = inBand
                ? "  距最近玩家格 = " + dist + " 步  (落在 [" + minD + ", " + maxD + "] 区间 ✓)"
                : "  距最近玩家格 = " + dist + " 步  (超出 [" + minD + ", " + maxD + "] 区间 ✗)";
            EditorGUILayout.LabelField(bandMsg, EditorStyles.miniLabel);
            if (!inBand)
                EditorGUILayout.HelpBox(
                    "传送门距离玩家不在期望区间。在角落地图上想拉远就调高 min，玩家活跃区域太靠地图中心就调低 max。",
                    MessageType.Warning);
        }

        /// <summary>8-direction BFS from any portal footprint cell to the nearest player
        /// deployment cell, restricted to walkable terrain. Returns -1 if unreachable.</summary>
        int ShortestPathFromPortalToPlayer(PortalSpawnData portal)
        {
            if (_lastBattle == null || portal == null) return -1;
            int h = _lastBattle.GetLength(0);
            int w = _lastBattle.GetLength(1);

            var start = new System.Collections.Generic.List<Vector2Int>();
            for (int dy = 0; dy < portal.Height; dy++)
            for (int dx = 0; dx < portal.Width; dx++)
            {
                int x = portal.AnchorCell.x + dx;
                int y = portal.AnchorCell.y + dy;
                if (x >= 0 && x < w && y >= 0 && y < h
                    && TerrainCatalog.IsWalkable(_lastBattle[y, x]))
                    start.Add(new Vector2Int(x, y));
            }
            if (start.Count == 0) return -1;

            var goal = new bool[h, w];
            for (int i = 0; i < _lastData.PlayerDeploymentCells.Count; i++)
            {
                var c = _lastData.PlayerDeploymentCells[i];
                if (c.Y >= 0 && c.Y < h && c.X >= 0 && c.X < w) goal[c.Y, c.X] = true;
            }
            if (!AnyTrue(goal)) return -1;

            var dist = new int[h, w];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) dist[y, x] = -1;
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            for (int i = 0; i < start.Count; i++)
            {
                dist[start[i].y, start[i].x] = 0;
                queue.Enqueue(start[i]);
            }

            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 1), new Vector2Int(1, -1),
                new Vector2Int(-1, 1), new Vector2Int(-1, -1),
            };

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (goal[c.y, c.x]) return dist[c.y, c.x];
                for (int d = 0; d < dirs.Length; d++)
                {
                    int nx = c.x + dirs[d].x;
                    int ny = c.y + dirs[d].y;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    if (dist[ny, nx] >= 0) continue;
                    if (!TerrainCatalog.IsWalkable(_lastBattle[ny, nx])) continue;
                    dist[ny, nx] = dist[c.y, c.x] + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return -1;
        }

        static bool AnyTrue(bool[,] g)
        {
            for (int y = 0; y < g.GetLength(0); y++)
            for (int x = 0; x < g.GetLength(1); x++)
                if (g[y, x]) return true;
            return false;
        }

        void Regenerate()
        {
            try
            {
                var request = BuildRequestFromWorkspace();
                _lastBattle = ProceduralBattleMapProvider.CreateBattleMap(request, out _lastData, out _lastIdentity);
                RunValidation();
                RebuildPreview();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void RandomSeed()
        {
            _settings.Seed = unchecked((uint)(Environment.TickCount & 0x7fffffff) | 1u);
            Debug.Log("[RandomMapTool] 已随机生成 seed=" + _settings.Seed + "。点'应用到 BattleScene 并运行'生效。");
            Regenerate();
        }

        void RunValidation()
        {
            if (_lastBattle == null) return;
            _lastValidation = MapGenerationValidator.Validate(
                _lastBattle, _lastBattle.GetLength(1), _lastBattle.GetLength(0),
                requireMirror: _settings.Mirror,
                minWalkableRatio: _validation.MinWalkableRatio,
                maxWalkableRatio: _validation.MaxWalkableRatio,
                minWalkableComponentRatio: _validation.MinWalkableComponentRatio,
                TerrainCatalog.IsWalkable);
            if (_lastValidation.Passed)
            {
                var deployment = DeploymentAreaPlanner.Plan(_lastBattle,
                    _playerDeploymentGroups, _enemyDeploymentGroups,
                    TerrainCatalog.IsWalkable);
                if (!deployment.Passed)
                {
                    _lastValidation.Passed = false;
                    _lastValidation.Failures.Add(deployment.FailureReason);
                }
            }
        }

        BattleMapRequest BuildRequestFromWorkspace()
        {
            return new BattleMapRequest
            {
                ProfileId = _profile != null ? _profile.ProfileId : "tool",
                ProfileVersion = _profile != null ? _profile.ProfileVersion : 0,
                RuleId = "tool",
                LevelIndex = _previewLevel,
                SeedMode = SeedMode.Fixed,
                FixedSeed = _settings.Seed,
                ProfileSalt = _profile != null ? _profile.ProfileSalt : 0u,
                RetryCount = _retryCount,
                Settings = _settings.Clone(),
                MinWalkableRatio = _validation.MinWalkableRatio,
                MaxWalkableRatio = _validation.MaxWalkableRatio,
                MinWalkableComponentRatio = _validation.MinWalkableComponentRatio,
                PlayerDeploymentGroupCount = _playerDeploymentGroups,
                EnemyDeploymentGroupCount = _enemyDeploymentGroups,
            };
        }

        void RebuildPreview()
        {
            if (_previewTex != null) { DestroyImmediate(_previewTex); _previewTex = null; }
            if (_lastBattle == null) return;
            int w = _lastBattle.GetLength(1);
            int h = _lastBattle.GetLength(0);
            _previewTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var colors = new Color32[w * h];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                    colors[z * w + x] = TerrainCatalog.GetColor(_lastBattle[z, x]);
            _previewTex.SetPixels32(colors);
            _previewTex.filterMode = FilterMode.Point;
            _previewTex.wrapMode = TextureWrapMode.Clamp;
            _previewTex.Apply();
            _hasHover = false;
        }

        // ---- batch verification -----------------------------------------------------------

        void DrawBatchPanel()
        {
            EditorGUILayout.LabelField("批量验证", EditorStyles.boldLabel);
            _batchSeedCount = Mathf.Clamp(EditorGUILayout.IntField("每规则种子数", _batchSeedCount), 1, 5000);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("验证当前规则")) StartBatch(_batchSeedCount, currentRuleOnly: true);
            if (GUILayout.Button("验证全部规则")) StartBatch(_batchSeedCount, currentRuleOnly: false);
            if (_batchJob != null) { if (GUILayout.Button("取消")) _batchJob.Canceled = true; }
            EditorGUILayout.EndHorizontal();

            if (_batchJob != null)
            {
                float p = _batchJob.Total > 0 ? (float)_batchJob.Done / _batchJob.Total : 0f;
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(0f, 18f), p, _batchJob.Done + "/" + _batchJob.Total);
            }
            if (_batchReport != null)
            {
                foreach (var line in _batchReport) EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
        }

        void StartBatch(int seedsPerRule, bool currentRuleOnly)
        {
            var settingsList = new List<MapGenerationSettings>();
            var validationList = new List<MapValidationSettings>();
            var nameList = new List<string>();

            if (!currentRuleOnly && _profile != null && _profile.Rules.Count > 0)
            {
                foreach (var r in _profile.Rules)
                {
                    if (r == null) continue;
                    settingsList.Add(r.Settings != null ? r.Settings.Clone() : new MapGenerationSettings());
                    validationList.Add(r.Validation != null ? r.Validation.Clone() : new MapValidationSettings());
                    nameList.Add(string.IsNullOrEmpty(r.DisplayName) ? r.RuleId : r.DisplayName);
                }
            }
            else
            {
                settingsList.Add(_settings.Clone());
                validationList.Add(_validation.Clone());
                nameList.Add("当前参数");
            }

            if (settingsList.Count == 0)
            {
                Debug.LogWarning("[RandomMapTool] 没有可验证的规则");
                return;
            }

            _batchReport = null;
            var job = new BatchJob();
            _batchJob = job;
            int perRule = Mathf.Max(1, seedsPerRule);
            int playerGroups = _playerDeploymentGroups;
            int enemyGroups = _enemyDeploymentGroups;
            Task.Run(() => BatchWorker.Run(job, settingsList, validationList, nameList,
                perRule, playerGroups, enemyGroups));
        }

        static List<string> BuildBatchReport(BatchJob job)
        {
            var lines = new List<string>
            {
                "批量完成  成功=" + job.SuccessCount + "  失败=" + job.ErrorCount
                    + "  耗时=" + job.ElapsedMs.ToString("0") + "ms"
                    + (job.Canceled ? "  (已取消)" : "")
            };
            if (job.Failures.Count > 0)
            {
                lines.Add("失败样例 (最多 50 条):");
                int shown = Mathf.Min(job.Failures.Count, 50);
                for (int i = 0; i < shown; i++) lines.Add(job.Failures[i].ToString());
                if (job.Failures.Count > shown) lines.Add("... 共 " + job.Failures.Count + " 条失败");
            }
            return lines;
        }

        // ---- preview by level --------------------------------------------------------------

        void DrawLevelPreview()
        {
            EditorGUILayout.LabelField("按关卡预览", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _previewLevel = Mathf.Max(1, EditorGUILayout.IntField("关卡", _previewLevel));
            if (GUILayout.Button("按关卡生成", GUILayout.Width(100))) GenerateByLevel(_previewLevel);
            EditorGUILayout.EndHorizontal();

            if (_profile == null)
            {
                EditorGUILayout.HelpBox("未加载配置资产", MessageType.Info);
            }
            else
            {
                var matched = _profile.FindRule(_previewLevel);
                EditorGUILayout.LabelField("命中规则: " + (matched != null ? (matched.DisplayName ?? matched.RuleId) : "(无 - 使用 fallback)"));

                if (_lastIdentity != null && _lastIdentity.RuleId != "tool")
                {
                    EditorGUILayout.LabelField("身份: " + _lastIdentity, EditorStyles.miniLabel);
                }
            }
        }

        void GenerateByLevel(int level)
        {
            if (_profile == null)
            {
                Debug.LogWarning("[RandomMapTool] 请先加载或新建配置资产");
                return;
            }
            try
            {
                var request = _profile.BuildRequest(level);
                _lastBattle = ProceduralBattleMapProvider.CreateBattleMap(request, out _lastData, out _lastIdentity);
                _lastValidation = MapGenerationValidator.Validate(
                    _lastBattle, _lastData.Width, _lastData.Height,
                    requireMirror: _lastData.Mirror,
                    minWalkableRatio: request.MinWalkableRatio,
                    maxWalkableRatio: request.MaxWalkableRatio,
                    minWalkableComponentRatio: request.MinWalkableComponentRatio,
                    TerrainCatalog.IsWalkable);
                RebuildPreview();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // ---- reproduce a reported seed -------------------------------------------------------

        void DrawReproducePanel()
        {
            EditorGUILayout.LabelField("复现失败地图", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _reproSeedText = EditorGUILayout.TextField("Seed", _reproSeedText);
            if (GUILayout.Button("按种子复现", GUILayout.Width(100))) ReproduceSeed();
            EditorGUILayout.EndHorizontal();
        }

        void ReproduceSeed()
        {
            if (!uint.TryParse(_reproSeedText.Trim(), out uint seed))
            {
                Debug.LogWarning("[RandomMapTool] 无法解析 seed: " + _reproSeedText);
                return;
            }
            try
            {
                var request = new BattleMapRequest
                {
                    ProfileId = _profile != null ? _profile.ProfileId : "tool",
                    ProfileVersion = _profile != null ? _profile.ProfileVersion : 0,
                    RuleId = "reproduce",
                    LevelIndex = _previewLevel,
                    SeedMode = SeedMode.Fixed,
                    FixedSeed = seed,
                    RetryCount = _retryCount,
                    Settings = _settings.Clone(),
                    MinWalkableRatio = _validation.MinWalkableRatio,
                    MaxWalkableRatio = _validation.MaxWalkableRatio,
                    MinWalkableComponentRatio = _validation.MinWalkableComponentRatio,
                };
                _lastBattle = ProceduralBattleMapProvider.CreateBattleMap(request, out _lastData, out _lastIdentity);
                RunValidation();
                RebuildPreview();
                Debug.Log("[RandomMapTool] 复现 seed=" + seed + " 哈希=" + _lastData.MapHash
                    + "  校验=" + (_lastValidation != null && _lastValidation.Passed ? "通过" : _lastValidation));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // ---- export / apply -----------------------------------------------------------------

        void ExportPng()
        {
            if (_previewTex == null)
            {
                Debug.LogWarning("[RandomMapTool] 无预览可导出");
                return;
            }
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "MapGenPreviews");
            Directory.CreateDirectory(dir);
            string path = EditorUtility.SaveFilePanel("导出预览 PNG", dir,
                "map_" + (_lastData != null ? _lastData.Seed.ToString() : "preview") + ".png", "png");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllBytes(path, _previewTex.EncodeToPNG());
            Debug.Log("[RandomMapTool] 已导出 " + path);
        }

        void ApplyToBattleSceneAndPlay()
        {
            // 1. 先退出当前 Play 模式,避免 Unity 编辑器在 Play 模式下 SaveScene 时
            //    截断 OnDestroy/OnApplicationQuit 导致单例泄漏和 NRE。
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                // 等待一帧让 OnDestroy 跑完再继续
                EditorApplication.delayCall += () => { DoApplyAndPlay(); };
                return;
            }
            DoApplyAndPlay();
        }

        void DoApplyAndPlay()
        {
            // 1. 先把当前参数写到 BattleGridController 的 _proceduralSettings（保持原逻辑）
            var grid = FindObjectOfType<BattleGridController>();
            if (grid == null)
            {
                // 当前场景没有 BattleGridController，先打开 BattleScene
                const string battleScenePath = "Assets/Scenes/BattleScene.unity";
                if (!System.IO.File.Exists(battleScenePath))
                {
                    Debug.LogWarning("[RandomMapTool] 找不到 Assets/Scenes/BattleScene.unity");
                    return;
                }
                EditorSceneManager.OpenScene(battleScenePath, OpenSceneMode.Single);
                grid = FindObjectOfType<BattleGridController>();
                if (grid == null)
                {
                    Debug.LogWarning("[RandomMapTool] BattleScene 中没有 BattleGridController");
                    return;
                }
            }

            var mapSourceField = typeof(BattleGridController).GetField("_mapSource", BindingFlags.NonPublic | BindingFlags.Instance);
            var settingsField = typeof(BattleGridController).GetField("_proceduralSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            var levelField = typeof(BattleGridController).GetField("_proceduralLevel", BindingFlags.NonPublic | BindingFlags.Instance);
            var toolOverrideField = typeof(BattleGridController).GetField("_useAppliedToolSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            var profileField = typeof(BattleGridController).GetField("_proceduralProfile", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mapSourceField == null || settingsField == null || levelField == null || toolOverrideField == null)
            {
                Debug.LogWarning("[RandomMapTool] 找不到 BattleGridController 序列化字段");
                return;
            }

            mapSourceField.SetValue(grid, BattleMapSource.Procedural);
            settingsField.SetValue(grid, _settings.Clone());
            levelField.SetValue(grid, Mathf.Max(1, _previewLevel));
            toolOverrideField.SetValue(grid, true);

            // 关键:把 _proceduralProfile 置空,避免 profile 字段覆盖 settings。
            // 当 _proceduralProfile 为空且 BattleStartContext.MapProfile 也为空时,
            // BattleGridController.ResolveMap 会走 fallback 到 _proceduralSettings。
            if (profileField != null) profileField.SetValue(grid, null);

            // Enter Play Mode Options may disable domain reload, so old static hand-off data
            // can survive from the preceding battle. Clear it here as well as persisting the
            // scene override above.
            BattleMapContext.PendingRequest = null;
            BattleStartContext.MapProfile = null;
            BattleStartContext.LevelIndex = Mathf.Max(1, _previewLevel);

            EditorUtility.SetDirty(grid);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[RandomMapTool] 已将当前参数应用到 BattleScene 并保存（工具配置优先）。 settings.EnableExtractionPortal=" + _settings.EnableExtractionPortal
                + " seed=" + _settings.Seed + " Width=" + _settings.Width + " Height=" + _settings.Height);

            // 2. 直接进入 Play 模式,让用户立刻看到地图跑起来
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
                Debug.Log("[RandomMapTool] 已进入 Play 模式。");
            }
        }

        // ---- rule list (right panel) ----------------------------------------------------------

        void DrawRulePanel()
        {
            EditorGUILayout.LabelField("关卡规则", EditorStyles.boldLabel);
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("先加载或新建配置资产", MessageType.Info);
                return;
            }

            if (GUILayout.Button("新增规则(从当前参数)")) AddRuleFromWorkspace();

            for (int i = 0; i < _profile.Rules.Count; i++)
            {
                var rule = _profile.Rules[i];
                if (rule == null) continue;
                bool selected = i == _selectedRuleIndex;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField((selected ? "▶ " : "") + (rule.DisplayName ?? rule.RuleId)
                    + "  [" + rule.StartLevel + "-" + rule.EndLevel + "]", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Seed=" + rule.FixedSeed + "  " + rule.SeedMode
                    + "  retry=" + rule.RetryCount, EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("选择")) SelectRule(i);
                if (GUILayout.Button("复制")) DuplicateRule(i);
                if (GUILayout.Button("删除")) DeleteRule(i);
                if (GUILayout.Button("上")) MoveRule(i, -1);
                if (GUILayout.Button("下")) MoveRule(i, +1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        void AddRuleFromWorkspace()
        {
            if (_profile == null) return;
            var rule = new LevelMapGenerationRule
            {
                RuleId = Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = "新规则 " + (_profile.Rules.Count + 1),
                StartLevel = Mathf.Max(1, _previewLevel),
                EndLevel = Mathf.Max(_previewLevel, _previewLevel + 2),
                Settings = _settings.Clone(),
                Validation = _validation.Clone(),
                SeedMode = SeedMode.LevelBased,
                FixedSeed = _settings.Seed,
                RetryCount = _retryCount,
            };
            _profile.Rules.Add(rule);
            _selectedRuleIndex = _profile.Rules.Count - 1;
            EditorUtility.SetDirty(_profile);
            Debug.Log("[RandomMapTool] 新增规则 " + rule.DisplayName + "  [" + rule.StartLevel + "-" + rule.EndLevel + "]");
        }

        void SelectRule(int index)
        {
            if (_profile == null || index < 0 || index >= _profile.Rules.Count) return;
            _selectedRuleIndex = index;
            var rule = _profile.Rules[index];
            _settings = rule.Settings != null ? rule.Settings.Clone() : new MapGenerationSettings();
            _validation = rule.Validation != null ? rule.Validation.Clone() : new MapValidationSettings();
            _retryCount = rule.RetryCount;
            _previewLevel = Mathf.Max(1, rule.StartLevel);
            Regenerate();
        }

        void WriteWorkspaceToSelectedRule()
        {
            if (_profile == null || _selectedRuleIndex < 0 || _selectedRuleIndex >= _profile.Rules.Count) return;
            var rule = _profile.Rules[_selectedRuleIndex];
            rule.Settings = _settings.Clone();
            rule.Validation = _validation.Clone();
            rule.RetryCount = _retryCount;
            EditorUtility.SetDirty(_profile);
            Debug.Log("[RandomMapTool] 已写入选中规则 " + rule.DisplayName);
        }

        void DuplicateRule(int index)
        {
            if (_profile == null || index < 0 || index >= _profile.Rules.Count) return;
            var copy = _profile.Rules[index].Clone();
            copy.RuleId = Guid.NewGuid().ToString("N").Substring(0, 8);
            copy.DisplayName = _profile.Rules[index].DisplayName + " 副本";
            _profile.Rules.Insert(index + 1, copy);
            _selectedRuleIndex = index + 1;
            EditorUtility.SetDirty(_profile);
        }

        void DeleteRule(int index)
        {
            if (_profile == null || index < 0 || index >= _profile.Rules.Count) return;
            _profile.Rules.RemoveAt(index);
            if (_selectedRuleIndex == index) _selectedRuleIndex = -1;
            else if (_selectedRuleIndex > index) _selectedRuleIndex--;
            EditorUtility.SetDirty(_profile);
        }

        void MoveRule(int index, int delta)
        {
            if (_profile == null || index < 0 || index >= _profile.Rules.Count) return;
            int to = index + delta;
            if (to < 0 || to >= _profile.Rules.Count) return;
            var tmp = _profile.Rules[index];
            _profile.Rules[index] = _profile.Rules[to];
            _profile.Rules[to] = tmp;
            _selectedRuleIndex = to;
            EditorUtility.SetDirty(_profile);
        }

        // ---- batch job types -------------------------------------------------------------------

        sealed class BatchJob
        {
            public volatile bool Canceled;
            public int Total;
            public int Done;
            public volatile bool Completed;
            public int SuccessCount;
            public int ErrorCount;
            public double ElapsedMs;
            public readonly List<BatchFailure> Failures = new List<BatchFailure>();
        }

        readonly struct BatchFailure
        {
            public readonly int RuleIndex;
            public readonly string RuleName;
            public readonly uint Seed;
            public readonly int Attempt;
            public readonly string Reason;
            public readonly string Hash;

            public BatchFailure(int ruleIndex, string ruleName, uint seed, int attempt, string reason, string hash)
            {
                RuleIndex = ruleIndex;
                RuleName = ruleName;
                Seed = seed;
                Attempt = attempt;
                Reason = reason;
                Hash = hash;
            }

            public override string ToString()
            {
                return "[" + RuleName + " seed=" + Seed + " attempt=" + Attempt + "] " + Reason + " hash=" + Hash;
            }
        }

        static class BatchWorker
        {
            public static void Run(BatchJob job,
                IReadOnlyList<MapGenerationSettings> settingsList,
                IReadOnlyList<MapValidationSettings> validationList,
                IReadOnlyList<string> nameList,
                int seedsPerRule,
                int playerGroups,
                int enemyGroups)
            {
                var sw = Stopwatch.StartNew();
                job.Total = settingsList.Count * seedsPerRule;
                int done = 0;

                for (int ri = 0; ri < settingsList.Count && !job.Canceled; ri++)
                {
                    var settings = settingsList[ri];
                    var validation = validationList[ri];
                    string name = nameList[ri];

                    for (int s = 1; s <= seedsPerRule && !job.Canceled; s++)
                    {
                        var attempt = settings.Clone();
                        attempt.Seed = unchecked((uint)s);

                        var generated = ProceduralMapGenerator.Generate(attempt);
                        var battle = GeneratedTerrainMapper.ToBattleGrid(generated);
                        var result = MapGenerationValidator.Validate(
                            battle, generated.Width, generated.Height,
                            requireMirror: generated.Mirror,
                            minWalkableRatio: validation.MinWalkableRatio,
                            maxWalkableRatio: validation.MaxWalkableRatio,
                            minWalkableComponentRatio: validation.MinWalkableComponentRatio,
                            TerrainCatalog.IsWalkable);

                        if (result.Passed)
                        {
                            var deployment = DeploymentAreaPlanner.Plan(battle,
                                playerGroups, enemyGroups,
                                TerrainCatalog.IsWalkable);
                            if (!deployment.Passed)
                            {
                                result.Passed = false;
                                result.Failures.Add(deployment.FailureReason);
                            }
                        }

                        // 建筑放置校验 (建筑平原约束): 建筑所在单格必须为平原, 且不越界、不重叠。
                        if (result.Passed && generated.BuildingReport != null && !generated.BuildingReport.IsValid)
                        {
                            result.Passed = false;
                            result.Failures.Add("建筑放置违规: 非平原=" + generated.BuildingReport.NonPlainCells
                                + " 越界=" + generated.BuildingReport.OutOfBoundsCells
                                + " 重叠=" + generated.BuildingReport.OverlapCells);
                        }

                        if (result.Passed) job.SuccessCount++;
                        else
                        {
                            job.ErrorCount++;
                            job.Failures.Add(new BatchFailure(
                                ri, name, attempt.Seed, 1, result.ToString(), generated.MapHash));
                        }

                        done++;
                        job.Done = done;
                    }
                }

                sw.Stop();
                job.ElapsedMs = sw.Elapsed.TotalMilliseconds;
                job.Completed = true;
            }
        }
    }
}
