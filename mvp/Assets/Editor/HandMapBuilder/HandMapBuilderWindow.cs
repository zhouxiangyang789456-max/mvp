using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mvp.Battle.Map.Generation;

namespace Mvp.Editor.HandMapBuilder
{
    /// <summary>
    /// 3D 等距地图建造工具 — 阶段 2 (HandMapBuilder阶段2分类与层级改造方案).
    ///
    /// 设计师工作流：
    ///   1. 菜单 Tools/Map Generation/Hand Map Builder 打开工具
    ///   2. 选/新建一份 HandAuthoredMapData (ScriptableObject)
    ///   3. 上方 Scene 视图 = 实时 3D 预览（Unity 自带视角控制）
    ///   4. 顶部类别标签页切换 (Base/Path/Forest/.../Effect)
    ///   5. 调色板 8 个一排，可翻页
    ///   6. 鼠标移到格子 → 黄色高亮 → 单击放置 prefab
    ///   7. 手动管理 Z 层级：
    ///      - 默认 Z=0（第1 层）放地面
    ///      - 点 `⬇ 添加层级` → 新增 Z=1（第2 层），激活切到 Z=1
    ///      - 同格多层允许：(5,5) Z=0 草 + Z=1 桥 + Z=2 塔
    ///      - 上限 10 层（Z=0..9）
    ///   8. 右键点击格子 = 吸管：拾取 prefab 到调色板，自动切到对应 Z
    ///   9. 保存 → 选择关卡配置 → 点击“应用到关卡”
    /// </summary>
    public sealed class HandMapBuilderWindow : EditorWindow
    {
        const string DefaultAssetDir = "Assets/ScriptableObjects/HandMaps";
        const string DefaultAssetName = "HandMap_New";
        const int PalettePerPage = 8;

        [SerializeField] HandAuthoredMapData _mapData;
        [SerializeField] LevelMapGenerationProfile _targetProfile;

        // 当前选中的类别
        HandMapCategory _selectedCategory = HandMapCategory.Base;
        // 当前页（基于 _selectedCategory 的 prefab 列表）
        int _currentPage = 0;
        // 当前页内选中的 prefab 索引（0..PalettePerPage-1）
        int _selectedSlotIndex = 0;

        // 手动层级状态
        int _activeZ = 0;        // 当前激活的 Z（hover/click 命中这层）
        int _maxZInData = 0;     // 数据中已存在的最大 Z（用于"添加层级"按钮）
        // 用户主动保留的层数（即使数据中没有 tile 也不缩减，避免"添加层级"被 OnGUI 同步逻辑吞掉）
        int _userReservedMaxZ = 0;

        // 阶段 5：地图尺寸 pending（输入与提交分离，避免每次按键都 Rebuild）
        int _pendingWidth, _pendingHeight;
        bool _gridSizeInited;

        // 阶段 5：框选 + 复制粘贴 状态机
        enum BuilderState
        {
            Idle,            // 闲置（可画/可擦）
            BoxSelecting,    // 用户正在拖拽框选（拖拽距离 ≥2 格）
            BoxSelected,     // 用户已释放、框选被保留，可点"复制" / Delete
            ReadyToCopy,     // 已点复制，鼠标 hover 时显示 ghost preview
        }
        BuilderState _state = BuilderState.Idle;

        // 框选范围（min/max cell）
        Vector2Int _boxSelStart = new Vector2Int(-1, -1);
        Vector2Int _boxSelEnd = new Vector2Int(-1, -1);
        // 框选内的 tile 索引列表（指向 _mapData.Tiles）
        List<int> _selectedTileIndices = new List<int>();

        // 复制组数据（从原 tile 拷贝出来的）
        struct CopyGroupEntry
        {
            public int x, y;     // 复制组内坐标系（原 tile 减去复制组原点）
            public int z;        // 原 tile 的 Z
            public string prefabPath;
            public float rotY;
            public float hOff;
        }
        List<CopyGroupEntry> _clipboard = new List<CopyGroupEntry>();
        // 复制组相对原点（=复制前 tile 集合的最小 X / Y / Z）
        Vector3Int _clipboardOrigin;
        // 复制组相对范围（用于 ghost 外接框）
        Vector2Int _clipboardSize;

        // 笔刷大小（圆刷半径）：0=1×1 单格, 1=3×3 圆刷, 2=5×5 圆刷
        int _brushSize = 0;

        // ---- 阶段 4 新增：旋转 / 高度 / 层高缩放 ----
        float _currentRotationY = 0f;          // 当前放置 rotation（度），吸附时为 0/90/180/270
        float _currentHeightOffset = 0f;       // 当前放置的 height offset
        float _layerHeightScale = 1f;          // 全局层高缩放（0.5~2.0）
        bool _rotationSnapped = true;          // true 表示角度被吸附到 4 方向
        double _levelAddTime = -1;             // 最近一次添加层级的时间戳（用于闪烁）
        HandPlacedTile? _selectedTileForEdit;  // Shift+左键选中的 tile（Inspector 调整用）

        Vector2Int _hoverCell = new Vector2Int(-1, -1);
        bool _eraseMode;

        // 拖拽绘制去重：本帧（一次按下→松开）已画过的格子，避免重复 PlaceAt
        readonly HashSet<Vector2Int> _dragPaintedCells = new HashSet<Vector2Int>();

        // Scene 视图里的预览实例 — key 用 (X, Y, Z)
        readonly Dictionary<Vector3Int, GameObject> _spawnedByCell =
            new Dictionary<Vector3Int, GameObject>();
        SceneView _sv;

        [MenuItem("Tools/Map Generation/Hand Map Builder")]
        public static void Open()
        {
            GetWindow<HandMapBuilderWindow>("3D 地图建造");
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update += OnEditorUpdate;
            // EditorWindow fields survive window/domain reloads, but preview scene objects do not.
            // Restore the saved asset after Unity has finished rebuilding the editor UI.
            EditorApplication.delayCall += RestoreSavedMapPreviews;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.delayCall -= RestoreSavedMapPreviews;
            ClearSpawnedPreview();
        }

        void RestoreSavedMapPreviews()
        {
            if (this == null || _mapData == null) return;
            _layerHeightScale = _mapData.LayerHeightScale;
            _gridSizeInited = false;
            ClearSpawnedPreview();
            RebuildAllPreviews();
            _sv = SceneView.lastActiveSceneView;
            _sv?.Repaint();
            Repaint();
            Debug.Log($"[HandMapBuilder] 已恢复地图 {_mapData.name}: {_mapData.Tiles.Count} 个 tile");
        }

        void OnEditorUpdate()
        {
            if (_sv == null) _sv = SceneView.lastActiveSceneView;
            if (_sv == null) return;
            if (EditorWindow.mouseOverWindow == this) _sv.Repaint();
        }

        // ---- GUI 主入口 -----------------------------------------------------------

        void OnGUI()
        {
            DrawHeader();
            DrawMapAssetField();
            DrawGridSizeField();
            DrawCategoryTabs();
            DrawPalette();
            DrawDrawControls();
            DrawBoxSelectionControls();   // 阶段 5: 框选 + 复制粘贴
            DrawTransformControls();
            DrawLevelControls();
            DrawControls();
            DrawTileInspector();
            DrawHelp();

            // 数据变更后刷新 _maxZInData：只增不减（用户主动保留的层级不被覆盖）
            if (_mapData != null)
            {
                int newMax = 0;
                for (int i = 0; i < _mapData.Tiles.Count; i++)
                    if (_mapData.Tiles[i].Z > newMax) newMax = _mapData.Tiles[i].Z;
                // 用户保留的层级优先（即使数据里没 tile 也不缩减）
                if (newMax < _userReservedMaxZ) newMax = _userReservedMaxZ;
                if (newMax != _maxZInData)
                {
                    _maxZInData = newMax;
                    if (_activeZ > _maxZInData) _activeZ = _maxZInData;
                }
            }
        }

        void DrawHeader()
        {
            EditorGUILayout.LabelField("3D 等距地图建造工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "上方是 Unity Scene 视图（自带视角控制：Alt+左键转、滚轮缩放、F 聚焦）。\n" +
                "左键单击 = 放 1 格；按住拖拽 = 笔刷形状连续画；右键 = 吸管（自动跳到该 prefab 所在层级）。\n" +
                "下方依次：类别标签 → 调色板 → 笔刷+填充 → 层级控制。",
                MessageType.Info);
        }

        void DrawMapAssetField()
        {
            EditorGUILayout.BeginHorizontal();
            var newData = (HandAuthoredMapData)EditorGUILayout.ObjectField(
                "地图数据", _mapData, typeof(HandAuthoredMapData), false);
            if (newData != _mapData)
            {
                _mapData = newData;
                if (_mapData != null) _layerHeightScale = _mapData.LayerHeightScale;
                _gridSizeInited = false; // 让新 asset 重新同步 pending
                _state = BuilderState.Idle;
                _selectedTileIndices.Clear();
                _clipboard.Clear();
                _boxSelStart = new Vector2Int(-1, -1);
                _boxSelEnd = new Vector2Int(-1, -1);
                ClearSpawnedPreview();
                _activeZ = 0;
                _maxZInData = 0;
                _userReservedMaxZ = 0;
                EditorApplication.delayCall += RestoreSavedMapPreviews;
            }
            if (GUILayout.Button("新建", GUILayout.Width(48))) CreateNewAsset();
            if (GUILayout.Button("保存", GUILayout.Width(48))) SaveAsset();
            if (GUILayout.Button("新场景", GUILayout.Width(60))) CreateCleanWorkspaceScene();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _targetProfile = (LevelMapGenerationProfile)EditorGUILayout.ObjectField(
                "关卡配置", _targetProfile, typeof(LevelMapGenerationProfile), false);
            using (new EditorGUI.DisabledScope(_mapData == null || _targetProfile == null))
            {
                if (GUILayout.Button("应用到关卡", GUILayout.Width(90))) ApplyToLevelProfile();
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawGridSizeField()
        {
            if (_mapData == null) return;

            // 首次进入时把"待输入值"与"已应用值"同步
            if (!_gridSizeInited)
            {
                _pendingWidth = _mapData.Width;
                _pendingHeight = _mapData.Height;
                _gridSizeInited = true;
            }

            // 范围限制：1~200
            const int MinSize = 1, MaxSize = 200;

            _pendingWidth = EditorGUILayout.IntField("宽 (Width)", _pendingWidth);
            _pendingHeight = EditorGUILayout.IntField("高 (Height)", _pendingHeight);
            _pendingWidth = Mathf.Clamp(_pendingWidth, MinSize, MaxSize);
            _pendingHeight = Mathf.Clamp(_pendingHeight, MinSize, MaxSize);

            bool dirty = _pendingWidth != _mapData.Width || _pendingHeight != _mapData.Height;

            EditorGUILayout.BeginHorizontal();

            // 主按钮：只在有改动时可点
            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button(dirty ? "应用尺寸 (↩)" : "已是当前值", GUILayout.Width(110)))
                    ApplyGridSize();
            }

            // 恢复当前值（取消未提交修改）
            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button("恢复", GUILayout.Width(50)))
                {
                    _pendingWidth = _mapData.Width;
                    _pendingHeight = _mapData.Height;
                }
            }

            EditorGUILayout.EndHorizontal();

            // 实时展示待生效的尺寸差异
            if (dirty)
            {
                int dw = _pendingWidth - _mapData.Width;
                int dh = _pendingHeight - _mapData.Height;
                string arrow = (dw > 0 ? "+" : "") + dw + " × " + (dh > 0 ? "+" : "") + dh;
                EditorGUILayout.HelpBox(
                    $"待应用尺寸：{_pendingWidth} × {_pendingHeight} ({arrow})",
                    MessageType.Info);
            }
        }

        /// <summary>应用待生效的地图尺寸。会先扫描即将被裁掉的 tile 数量，让用户二次确认。</summary>
        void ApplyGridSize()
        {
            if (_mapData == null) return;
            int newW = Mathf.Max(1, _pendingWidth);
            int newH = Mathf.Max(1, _pendingHeight);
            int oldW = _mapData.Width;
            int oldH = _mapData.Height;
            if (newW == oldW && newH == oldH) return;

            // 计算会被裁掉的 tile 数（X >= newW 或 Y >= newH）
            int willRemove = 0;
            if (newW < oldW || newH < oldH)
            {
                for (int i = 0; i < _mapData.Tiles.Count; i++)
                {
                    var t = _mapData.Tiles[i];
                    if (t.X >= newW || t.Y >= newH) willRemove++;
                }
            }

            if (willRemove > 0)
            {
                if (!EditorUtility.DisplayDialog("缩窄地图",
                    $"新尺寸 {newW} × {newH} 会裁掉 {willRemove} 个 tile（超出新边界的部分）。\n\n" +
                    "确定要应用吗？此操作可撤销（Ctrl+Z）。",
                    "应用", "取消"))
                    return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"应用地图尺寸 {oldW}×{oldH} → {newW}×{newH}");
            Undo.RecordObject(_mapData, "应用地图尺寸");
            _mapData.Width = newW;
            _mapData.Height = newH;

            // 真正裁掉越界 tile（前面只算数量，现在动手）
            if (willRemove > 0)
            {
                _mapData.Tiles.RemoveAll(t => t.X >= newW || t.Y >= newH);

                // 销毁越界预览
                var keysToRemove = new List<Vector3Int>();
                foreach (var kv in _spawnedByCell)
                {
                    if (kv.Key.x >= newW || kv.Key.y >= newH)
                    {
                        if (kv.Value != null) DestroyImmediate(kv.Value);
                        keysToRemove.Add(kv.Key);
                    }
                }
                foreach (var k in keysToRemove) _spawnedByCell.Remove(k);

                // 缩窄地图后，可能选中的 tile 也越界了，清掉避免下次绘制拿失效索引抛异常
                _selectedTileIndices.Clear();
                _clipboard.Clear();
                if (_state == BuilderState.ReadyToCopy || _state == BuilderState.BoxSelected)
                    _state = BuilderState.Idle;
            }

            EditorUtility.SetDirty(_mapData);
            _pendingWidth = newW;
            _pendingHeight = newH;

            // 重生当前 hover 范围的预览（缩小后某些 tile 已不存在）
            ClearSpawnedPreview();
            RebuildAllPreviews();

            // 刷新 _maxZInData
            int newMax = 0;
            for (int i = 0; i < _mapData.Tiles.Count; i++)
                if (_mapData.Tiles[i].Z > newMax) newMax = _mapData.Tiles[i].Z;
            _maxZInData = Mathf.Max(newMax, _userReservedMaxZ);
            if (_activeZ > _maxZInData) _activeZ = _maxZInData;

            Undo.CollapseUndoOperations(undoGroup);
            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] 地图尺寸: {oldW}×{oldH} → {newW}×{newH}（裁掉 {willRemove} 个 tile）");
        }

        void DrawCategoryTabs()
        {
            EditorGUILayout.LabelField("类别", EditorStyles.boldLabel);
            var categories = (HandMapCategory[])System.Enum.GetValues(typeof(HandMapCategory));
            // 排成两行（每行 6 个）防止窗口太宽
            const int perRow = 6;
            int rows = (categories.Length + perRow - 1) / perRow;
            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < perRow; c++)
                {
                    int idx = r * perRow + c;
                    if (idx >= categories.Length) break;
                    var cat = categories[idx];
                    bool selected = (cat == _selectedCategory);
                    string label = CategoryLabel(cat);
                    var prev = GUI.backgroundColor;
                    if (selected) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                    if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(22)))
                    {
                        if (!selected)
                        {
                            _selectedCategory = cat;
                            _currentPage = 0;
                            _selectedSlotIndex = 0;
                            _eraseMode = (cat == HandMapCategory.Erase);
                            _sv?.Repaint();
                        }
                    }
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        static string CategoryLabel(HandMapCategory cat)
        {
            switch (cat)
            {
                case HandMapCategory.Base: return "基础地形";
                case HandMapCategory.Path: return "道路";
                case HandMapCategory.Forest: return "森林";
                case HandMapCategory.Plant: return "灌木";
                case HandMapCategory.Water: return "水";
                case HandMapCategory.Ramp: return "坡道";
                case HandMapCategory.Bridge: return "桥";
                case HandMapCategory.Mountain: return "山地";
                case HandMapCategory.Building: return "建筑";
                case HandMapCategory.Decoration: return "装饰";
                case HandMapCategory.Effect: return "特效";
                case HandMapCategory.Erase: return "擦除";
                default: return cat.ToString();
            }
        }

        void DrawPalette()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("调色板", EditorStyles.boldLabel);

            // 擦除类别：显示"擦除模式开启"提示，不画格子
            if (_selectedCategory == HandMapCategory.Erase)
            {
                EditorGUILayout.HelpBox("当前：擦除模式（在 Scene 视图点击格子清除当前层级的内容）", MessageType.Info);
                return;
            }

            var paths = HandMapPalette.Paths[_selectedCategory];
            if (paths == null || paths.Count == 0)
            {
                EditorGUILayout.HelpBox("该类别暂无 prefab", MessageType.Warning);
                return;
            }

            int totalPages = Mathf.CeilToInt(paths.Count / (float)PalettePerPage);
            if (_currentPage >= totalPages) _currentPage = totalPages - 1;
            if (_currentPage < 0) _currentPage = 0;

            // 上页/下页 + 页码
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_currentPage == 0))
            {
                if (GUILayout.Button("◀ 上页", GUILayout.Width(60))) _currentPage--;
            }
            EditorGUILayout.LabelField($"第 {_currentPage + 1} / {totalPages} 页",
                GUILayout.Width(80));
            using (new EditorGUI.DisabledScope(_currentPage >= totalPages - 1))
            {
                if (GUILayout.Button("下页 ▶", GUILayout.Width(60))) _currentPage++;
            }
            EditorGUILayout.LabelField($"共 {paths.Count} 个", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // 8 格调色板
            int startIdx = _currentPage * PalettePerPage;
            float slot = 72f;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < PalettePerPage; i++)
            {
                int pathIdx = startIdx + i;
                DrawPaletteSlot(i, pathIdx < paths.Count ? paths[pathIdx] : null, slot);
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "当前: " + DescribeSelected(),
                EditorStyles.miniLabel);
        }

        void DrawPaletteSlot(int slotIndex, string path, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size,
                GUILayout.Width(size), GUILayout.Height(size));
            bool selected = (slotIndex == _selectedSlotIndex);
            EditorGUI.DrawRect(rect, selected
                ? new Color(1f, 0.85f, 0.2f, 0.4f)
                : new Color(0.2f, 0.2f, 0.2f, 0.4f));
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2),
                new Color(0.15f, 0.15f, 0.15f, 0.6f));

            if (!string.IsNullOrEmpty(path))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
                if (prefab != null)
                {
                    var preview = AssetPreview.GetAssetPreview(prefab);
                    if (preview == null) preview = AssetPreview.GetMiniThumbnail(prefab);
                    if (preview != null)
                    {
                        GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8),
                            preview, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        EditorGUI.LabelField(rect, Path.GetFileNameWithoutExtension(path));
                    }
                }
                else
                {
                    EditorGUI.LabelField(rect, "(missing)");
                }
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _selectedSlotIndex = slotIndex;
                _sv?.Repaint();
            }
        }

        string DescribeSelected()
        {
            var paths = HandMapPalette.Paths[_selectedCategory];
            if (paths == null) return "(无)";
            int idx = _currentPage * PalettePerPage + _selectedSlotIndex;
            if (idx < 0 || idx >= paths.Count) return "(无)";
            return HandMapPalette.GetDisplayName(paths[idx]);
        }

        // ---- 笔刷 + 填充控件 ------------------------------------------------------

        void DrawDrawControls()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("绘制", EditorStyles.boldLabel);
            // 笔刷大小：3 选 1
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("笔刷:", GUILayout.Width(40));
            DrawBrushToggle(0, "1×1", GUILayout.Width(50));
            DrawBrushToggle(1, "3×3", GUILayout.Width(50));
            DrawBrushToggle(2, "5×5", GUILayout.Width(50));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                "(单击=放1格, 拖拽=笔刷连续画)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // 层级填充按钮 + 清空（按钮文字实时显示当前 Z 与 tile 数量）
            int tileCountAtActiveZ = CountTilesAtZ(_activeZ);
            int totalTileCount = _mapData != null ? _mapData.Tiles.Count : 0;
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_mapData == null || _eraseMode))
            {
                if (GUILayout.Button($"填充整个层级 (F)", GUILayout.Width(120)))
                    FillLevel(overwrite: false);
                if (GUILayout.Button($"覆盖填充 (⇧F)", GUILayout.Width(110)))
                    FillLevel(overwrite: true);
            }
            using (new EditorGUI.DisabledScope(_mapData == null))
            {
                if (GUILayout.Button($"清空当前层 (⌃⇧F)  Z={_activeZ} · {tileCountAtActiveZ}个",
                        GUILayout.Width(240)))
                    ClearLevel();
            }
            // 一键清空全部（防误操作：二次确认）
            using (new EditorGUI.DisabledScope(_mapData == null || totalTileCount == 0))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                if (GUILayout.Button($"清空所有层级 · {totalTileCount}个", GUILayout.Width(160)))
                    ClearAllLevels();
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawBrushToggle(int size, string label, params GUILayoutOption[] options)
        {
            bool sel = (_brushSize == size);
            var prev = GUI.backgroundColor;
            if (sel) GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Toggle(sel, label, "Button", options))
            {
                if (!sel) { _brushSize = size; _sv?.Repaint(); }
            }
            GUI.backgroundColor = prev;
        }

        /// <summary>
        /// 笔刷形状：以 (cx, cy) 为中心，按 _brushSize 半径展开的圆形格子集合。
        /// r=0: 1 格；r=1: 5 格（中心+上下左右）；r=2: 13 格（圆）。
        /// </summary>
        IEnumerable<Vector2Int> EnumerateBrushCells(int cx, int cy)
        {
            int r = _brushSize;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy <= r * r)
                        yield return new Vector2Int(cx + dx, cy + dy);
                }
            }
        }

        // ---- 框选 + 复制粘贴控件 -------------------------------------------------

        /// <summary>阶段 5：框选 + 复制粘贴工具栏 + 状态显示。</summary>
        void DrawBoxSelectionControls()
        {
            if (_mapData == null) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("框选 / 复制粘贴", EditorStyles.boldLabel);

            // 状态显示 + 已选数
            int selCount = _selectedTileIndices.Count;
            string stateText;
            Color stateColor;
            switch (_state)
            {
                case BuilderState.BoxSelecting: stateText = "拖拽框选中..."; stateColor = new Color(0.4f, 0.7f, 1f); break;
                case BuilderState.BoxSelected:  stateText = $"已选 {selCount} 个 tile"; stateColor = new Color(0.5f, 0.9f, 0.5f); break;
                case BuilderState.ReadyToCopy:  stateText = $"📋 待粘贴 {_clipboard.Count} 个 tile（按 Space 实贴 / Esc 退出）"; stateColor = new Color(1f, 0.85f, 0.3f); break;
                default: stateText = "未框选（拖 ≥2 格启动框选）"; stateColor = new Color(0.7f, 0.7f, 0.7f); break;
            }
            var prev = GUI.color;
            GUI.color = stateColor;
            EditorGUILayout.LabelField(stateText, EditorStyles.miniLabel);
            GUI.color = prev;

            // 复制 / 粘贴 / 删除 / 退出 按钮组
            EditorGUILayout.BeginHorizontal();

            bool hasSelection = (_state == BuilderState.BoxSelected || _state == BuilderState.ReadyToCopy)
                                && _selectedTileIndices.Count > 0;
            bool inPasteState = _state == BuilderState.ReadyToCopy && _clipboard.Count > 0;

            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("复制 (⏎)", GUILayout.Width(78)))
                    EnterReadyToCopyState();
            }

            using (new EditorGUI.DisabledScope(!inPasteState))
            {
                if (GUILayout.Button("粘贴 (Space)", GUILayout.Width(100)))
                    PasteAtCurrentHover();
            }

            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("删除 (⌫)", GUILayout.Width(70)))
                    DeleteSelection();
            }

            using (new EditorGUI.DisabledScope(!inPasteState))
            {
                if (GUILayout.Button("✕ 退出 (Esc)", GUILayout.Width(85)))
                    ExitCopyPasteState();
            }

            EditorGUILayout.EndHorizontal();

            // 复制组信息
            if (inPasteState)
            {
                EditorGUILayout.HelpBox(
                    $"待粘贴 { _clipboard.Count } 个 tile · 相对尺寸 {_clipboardSize.x}×{_clipboardSize.y} · 原点 ( {_clipboardOrigin.x},{_clipboardOrigin.y})\n" +
                    "• 鼠标 hover → ghost preview 跟随\n" +
                    "• Space / Enter / 单击 = 实贴\n" +
                    "• 切换激活层 Z → ghost 在新层显示\n" +
                    "• Esc / ✕ 退出 = 放弃复制",
                    MessageType.Info);
            }
        }

        // ---- 层级控件 -------------------------------------------------------------

        void DrawLevelControls()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("层级 (Z)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            // 添加层级按钮（上限 10）
            int nextZ = _maxZInData + 1;
            bool canAdd = _maxZInData < (HandMapPalette.MaxZ - 1);
            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("⬇ 添加层级", GUILayout.Width(100)))
                {
                    _activeZ = nextZ;
                    if (_activeZ > _maxZInData) _maxZInData = _activeZ;
                    // 标记用户主动保留这个层级，避免 OnGUI 末尾同步逻辑把它覆盖
                    if (_activeZ > _userReservedMaxZ) _userReservedMaxZ = _activeZ;
                    _levelAddTime = EditorApplication.timeSinceStartup;
                    // 自动 refocus 相机到新 Z 层中心（解决"添加层级看不到"问题）
                    FocusSceneViewOnLevel(_activeZ);
                    _sv?.Repaint();
                    Debug.Log($"[HandMapBuilder] 添加层级: 第 {_activeZ + 1} 层 (Z={_activeZ}), 当前 _maxZInData={_maxZInData}");
                }
            }
            // 移除最高层级按钮
            using (new EditorGUI.DisabledScope(_maxZInData <= 0))
            {
                if (GUILayout.Button("⬆ 移除最高层级", GUILayout.Width(120)))
                {
                    RemoveHighestZ();
                    // 移除后把相机也降回当前最高 Z
                    FocusSceneViewOnLevel(_maxZInData);
                }
            }

            // 重置视角按钮（保留 F 键 SceneView 自带聚焦，这里是"恢复等距默认"）
            if (GUILayout.Button(new GUIContent("🔄 重置视角",
                    "恢复等距俯视 + pivot 到当前激活 Z 层。点错了就用它回位。"),
                GUILayout.Width(60)))
            {
                ResetSceneView();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"当前激活: 第 {_activeZ + 1} 层 (Z={_activeZ})",
                EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            // 层级按钮 [第1层][第2层]...
            int totalLevels = Mathf.Max(1, _maxZInData + 1);
            const int perRow = 10;
            int rows = (totalLevels + perRow - 1) / perRow;
            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < perRow; c++)
                {
                    int z = r * perRow + c;
                    if (z >= totalLevels) break;
                    bool sel = (z == _activeZ);
                    var prev = GUI.backgroundColor;
                    if (sel) GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
                    if (GUILayout.Toggle(sel, $"第{z + 1}层 (Z={z})", "Button", GUILayout.Width(90)))
                    {
                        if (!sel)
                        {
                            _activeZ = z;
                            _sv?.Repaint();
                        }
                    }
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 状态提示：hover 当前层级
            if (_mapData != null && _hoverCell.x >= 0)
            {
                int countAtActiveZ = CountTilesAtCellZ(_hoverCell, _activeZ);
                EditorGUILayout.LabelField(
                    $"Hover: ({_hoverCell.x}, {_hoverCell.y}, Z={_activeZ})  本格本层已有 {countAtActiveZ} 个",
                    EditorStyles.miniLabel);
            }
        }

        // ---- 阶段 4 新增：变换控件（旋转 / 高度 / 层高缩放） -----------------------
        void DrawTransformControls()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("变换", EditorStyles.boldLabel);

            // —— 旋转 ——
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("方向:", GUILayout.Width(40));
            float[] snappedAngles = { 0f, 90f, 180f, 270f };
            string[] labels = { "0°", "90°", "180°", "270°" };
            for (int i = 0; i < 4; i++)
            {
                bool isSelected = _rotationSnapped && Mathf.Approximately(_currentRotationY, snappedAngles[i]);
                var prev = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
                if (GUILayout.Toggle(isSelected, labels[i], "Button", GUILayout.Width(55)))
                {
                    if (!isSelected)
                    {
                        _currentRotationY = snappedAngles[i];
                        _rotationSnapped = true;
                        UpdateHoverGhost();
                        Repaint();
                        _sv?.Repaint();
                    }
                }
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("旋转角:", GUILayout.Width(50));
            float newRot = EditorGUILayout.FloatField(_currentRotationY, GUILayout.Width(70));
            newRot = Mathf.Repeat(newRot, 360f);
            if (!Mathf.Approximately(newRot, _currentRotationY))
            {
                _currentRotationY = newRot;
                // 非 0/90/180/270 任意角 → 吸附失效，按钮全灭
                _rotationSnapped = Mathf.Approximately(newRot, 0f)
                                || Mathf.Approximately(newRot, 90f)
                                || Mathf.Approximately(newRot, 180f)
                                || Mathf.Approximately(newRot, 270f);
                UpdateHoverGhost();
            }
            if (GUILayout.Button("+90°", GUILayout.Width(55))) { AdjustRotation(90f); }
            if (GUILayout.Button("-90°", GUILayout.Width(55))) { AdjustRotation(-90f); }
            GUILayout.Label(_rotationSnapped ? "吸附" : "自由角", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // —— 高度偏移 ——
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("高度:", GUILayout.Width(50));
            float newH = EditorGUILayout.Slider(_currentHeightOffset, -2f, 2f);
            if (!Mathf.Approximately(newH, _currentHeightOffset))
            {
                _currentHeightOffset = newH;
            }
            if (GUILayout.Button("归零", GUILayout.Width(45))) { _currentHeightOffset = 0f; UpdateHoverGhost(); }
            GUILayout.Label($"当前 {_currentHeightOffset:F2}", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // —— 层高缩放 ——
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("层高:", GUILayout.Width(50));
            float newScale = EditorGUILayout.Slider(_layerHeightScale, 0.5f, 2f);
            if (!Mathf.Approximately(newScale, _layerHeightScale))
            {
                _layerHeightScale = newScale;
                if (_mapData != null)
                {
                    Undo.RecordObject(_mapData, "修改地图层高");
                    _mapData.LayerHeightScale = newScale;
                    EditorUtility.SetDirty(_mapData);
                }
                RelayoutAllPreviews();
                Repaint();
                _sv?.Repaint();
            }
            EditorGUILayout.LabelField($"{_layerHeightScale:F2}×", EditorStyles.miniLabel, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }

        void AdjustRotation(float delta)
        {
            _currentRotationY = Mathf.Repeat(_currentRotationY + delta, 360f);
            _rotationSnapped = Mathf.Approximately(_currentRotationY, 0f)
                            || Mathf.Approximately(_currentRotationY, 90f)
                            || Mathf.Approximately(_currentRotationY, 180f)
                            || Mathf.Approximately(_currentRotationY, 270f);
            UpdateHoverGhost();
        }

        // —— Tile Inspector（Shift+左键 选中后调整现有 tile 的 HeightOffset）——
        void DrawTileInspector()
        {
            if (!_selectedTileForEdit.HasValue) return;
            if (_mapData == null) return;
            var t = _selectedTileForEdit.Value;
            // 验证选中仍然有效（数据可能已被外部改动）
            int idx = -1;
            for (int i = 0; i < _mapData.Tiles.Count; i++)
            {
                var e = _mapData.Tiles[i];
                if (e.X == t.X && e.Y == t.Y && e.Z == t.Z) { idx = i; break; }
            }
            if (idx < 0)
            {
                _selectedTileForEdit = null;
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"Inspector — 选中 tile ({t.X}, {t.Y}, Z={t.Z})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Prefab: {System.IO.Path.GetFileNameWithoutExtension(t.PrefabPath)}");
            EditorGUILayout.LabelField($"RotationY: {t.RotationY:F1}°");
            float newH = EditorGUILayout.Slider("高度偏移", t.HeightOffset, -2f, 2f);
            if (!Mathf.Approximately(newH, t.HeightOffset))
            {
                Undo.RecordObject(_mapData, "调整 tile 高度");
                t.HeightOffset = newH;
                _mapData.Tiles[idx] = t;
                EditorUtility.SetDirty(_mapData);
                _selectedTileForEdit = t;
                // 重定位该格的预览实例
                var key = new Vector3Int(t.X, t.Y, t.Z);
                if (_spawnedByCell.TryGetValue(key, out var go) && go != null)
                {
                    var w = GridToWorld(t.X + 0.5f, t.Y + 0.5f, t.Z);
                    go.transform.position = new Vector3(w.x, w.y + t.HeightOffset, w.z);
                }
            }
            if (GUILayout.Button("清除选中", GUILayout.Width(80)))
            {
                _selectedTileForEdit = null;
                _sv?.Repaint();
            }
            EditorGUILayout.EndVertical();
        }

        // 重新定位所有 preview 实例（layerHeightScale 或 HeightOffset 改变时调用）
        void RelayoutAllPreviews()
        {
            if (_mapData == null) return;
            foreach (var kv in _spawnedByCell)
            {
                var go = kv.Value;
                if (go == null) continue;
                var key = kv.Key;
                float ho = 0f;
                for (int i = 0; i < _mapData.Tiles.Count; i++)
                {
                    var t = _mapData.Tiles[i];
                    if (t.X == key.x && t.Y == key.y && t.Z == key.z) { ho = t.HeightOffset; break; }
                }
                var w = GridToWorld(key.x + 0.5f, key.y + 0.5f, key.z);
                go.transform.position = new Vector3(w.x, w.y + ho, w.z);
            }
        }

        // 刷新 hover ghost 的旋转/高度
        void UpdateHoverGhost()
        {
            _sv?.Repaint();
        }

        // 把 Scene 视图相机锚定到指定 Z 层（解决"加了层级看不到"问题）
        void FocusSceneViewOnLevel(int z)
        {
            if (_mapData == null) return;
            var sv = _sv ?? SceneView.lastActiveSceneView;
            if (sv == null) return;
            float cx = _mapData.Width * 0.5f;
            float cz = _mapData.Height * 0.5f;
            float cy = z * _layerHeightScale;
            // 只抬高 pivot 到新 Z 层高度，**保留**用户原本的 size / rotation / 正交设置
            // 避免点一次添加层级就把用户的视角彻底改没了
            sv.pivot = new Vector3(cx, cy, cz);

            // 只有 size 太离谱时才纠正（>2× 理想值 或 <0.3× 理想值）
            float idealSize = Mathf.Max(_mapData.Width, _mapData.Height) * 1.0f;
            if (sv.size > idealSize * 2f || sv.size < idealSize * 0.3f)
                sv.size = idealSize;
        }

        /// <summary>一键恢复默认等距视角（F 键功能）。用户随时可点。</summary>
        void ResetSceneView()
        {
            if (_mapData == null) return;
            var sv = _sv ?? SceneView.lastActiveSceneView;
            if (sv == null) return;
            float cx = _mapData.Width * 0.5f;
            float cz = _mapData.Height * 0.5f;
            float cy = _activeZ * _layerHeightScale;
            sv.pivot = new Vector3(cx, cy, cz);
            sv.size = Mathf.Max(_mapData.Width, _mapData.Height) * 1.0f;
            sv.rotation = new Quaternion(0.35f, 0.45f, 0.15f, 0.81f); // 等距俯视
            sv.orthographic = true;
        }

        void DrawControls()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            bool newErase = GUILayout.Toggle(_eraseMode, "擦除模式 (E)", "Button");
            if (newErase != _eraseMode)
            {
                _eraseMode = newErase;
                _sv?.Repaint();
            }
            if (GUILayout.Button("清空所有层级", GUILayout.Width(110)))
            {
                if (_mapData != null && EditorUtility.DisplayDialog("清空地图",
                    "确认删除全部 " + _mapData.Tiles.Count + " 个放置？", "确认", "取消"))
                {
                    Undo.RecordObject(_mapData, "清空地图");
                    _mapData.Tiles.Clear();
                    EditorUtility.SetDirty(_mapData);
                    ClearSpawnedPreview();
                    _maxZInData = 0;
                    _activeZ = 0;
                    _userReservedMaxZ = 0;
                    _sv?.Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();

            // 键盘快捷
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.E) { _eraseMode = !_eraseMode; _sv?.Repaint(); e.Use(); }
                else if (e.keyCode == KeyCode.Equals || e.keyCode == KeyCode.Plus ||
                         e.keyCode == KeyCode.KeypadPlus)
                {
                    // + = 新建层级（若已达上限则不响应）
                    if (_maxZInData < HandMapPalette.MaxZ - 1)
                    {
                        _activeZ = ++_maxZInData;
                        if (_activeZ > _userReservedMaxZ) _userReservedMaxZ = _activeZ;
                        _sv?.Repaint();
                    }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Minus || e.keyCode == KeyCode.KeypadMinus)
                {
                    // - = 降一档激活层级
                    if (_activeZ > 0) { _activeZ--; _sv?.Repaint(); }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.LeftBracket)
                {
                    if (_currentPage > 0) { _currentPage--; Repaint(); }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.RightBracket)
                {
                    int totalPages = Mathf.CeilToInt(
                        HandMapPalette.Paths[_selectedCategory].Count / (float)PalettePerPage);
                    if (_currentPage < totalPages - 1) { _currentPage++; Repaint(); }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.B)
                {
                    // B = 笔刷循环 0→1→2→0
                    _brushSize = (_brushSize + 1) % 3;
                    _sv?.Repaint();
                    Repaint();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.F)
                {
                    // F = 填充整个层级（空格填，已有保留）
                    // Shift+F = 覆盖填充；Ctrl+Shift+F = 清空整个层级
                    if (e.shift && e.control) { ClearLevel(); }
                    else if (e.shift) { FillLevel(overwrite: true); }
                    else { FillLevel(overwrite: false); }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.R)
                {
                    // R = 旋转 +90°，Shift+R = 旋转 -90°
                    AdjustRotation(e.shift ? -90f : 90f);
                    Repaint();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.H)
                {
                    // H = 抬高当前 height offset 0.1，Shift+H = 下沉 0.1
                    _currentHeightOffset = Mathf.Clamp(
                        _currentHeightOffset + (e.shift ? -0.1f : 0.1f), -2f, 2f);
                    UpdateHoverGhost();
                    Repaint();
                    e.Use();
                }
                // ---- 阶段 5: 框选 + 复制粘贴 快捷键 ----
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    // Enter = 按当前选区进入"待粘贴"状态（仅在有选区时生效）
                    if (_state == BuilderState.BoxSelected && _selectedTileIndices.Count > 0)
                    {
                        EnterReadyToCopyState();
                        e.Use();
                    }
                }
                else if (e.keyCode == KeyCode.Space)
                {
                    // Space = 实贴（仅在 ReadyToCopy 状态下）
                    if (_state == BuilderState.ReadyToCopy)
                    {
                        PasteAtCurrentHover();
                        e.Use();
                    }
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    // Esc = 退出待粘贴 / 清除选区
                    if (_state == BuilderState.ReadyToCopy)
                    {
                        ExitCopyPasteState();
                        e.Use();
                    }
                    else if (_state == BuilderState.BoxSelected || _state == BuilderState.BoxSelecting)
                    {
                        ClearSelection();
                        e.Use();
                    }
                }
                else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                {
                    // Delete / Backspace = 删除选区
                    if ((_state == BuilderState.BoxSelected || _state == BuilderState.ReadyToCopy)
                        && _selectedTileIndices.Count > 0)
                    {
                        DeleteSelection();
                        e.Use();
                    }
                }
            }
        }

        void DrawHelp()
        {
            EditorGUILayout.LabelField("提示", EditorStyles.helpBox);
            EditorGUILayout.HelpBox(
                "• 上方预览 = Scene 视图，Unity 自带视角控制（Alt+左键转、滚轮缩放、F 聚焦）\n" +
                "• 单击 = 放 1 格；拖拽 ≤1 格 = 笔刷形状连续画；拖拽 ≥2 格 = 框选（不画 tile）\n" +
                "• 右键 = 吸管（自动切到 prefab 层级 + 回写旋转/高度）\n" +
                "• Shift+左键 = 选中该 tile → 下方面板显示 Inspector 可调高度偏移\n" +
                "• 「填充整个层级」空格填，已有保留；「覆盖填充」强制替换；「清空当前层」按钮显示当前 Z 与 tile 数（删 0 个会提示）；「清空所有层级」一键清空\n" +
                "• 旋转: 4 方向按钮 或 输入任意角；快捷键 R=+90°  ⇧R=-90°\n" +
                "• 高度: 滑块 [-2, +2]；快捷键 H=+0.1  ⇧H=-0.1\n" +
                "• 框选 → 点「复制 (⏎)」或 Enter → 进入「待粘贴」状态：鼠标 hover 显示 ghost，按 Space / Enter / 单击 实贴，Esc 退出\n" +
                "• Delete / Backspace = 删除当前框选\n" +
                "• 地图尺寸改大小后需点「应用尺寸」才生效（缩窄时弹警告显示即将裁掉的 tile 数）\n" +
                "• 其它: E=擦除  B=笔刷循环  F=填充  ⇧F=覆盖  ⌃⇧F=清空  [ / ] =翻页  + / - =层  ⌥+左键=转视角\n" +
                "• 选择关卡配置后点“应用到关卡”，进入战斗时会自动加载这张地图",
                MessageType.None);
        }

        // ---- Scene 视图绘制与交互 ------------------------------------------------

        void OnSceneGui(SceneView sv)
        {
            _sv = sv;
            if (_mapData == null) return;

            DrawGridHandles(sv);
            DrawPlacedTiles(sv);
            DrawHoverGhost(sv);
            DrawBoxSelectionVisual(sv);   // 阶段 5: 框选矩形视觉
            DrawPasteGhostPreview(sv);    // 阶段 5: 待粘贴 ghost 预览
            HandleSceneEvents(sv);
        }

        /// <summary>阶段 5：绘制框选进行中 / 已选中的矩形（蓝色半透 + 白色描边）。</summary>
        void DrawBoxSelectionVisual(SceneView sv)
        {
            if (_mapData == null) return;
            if (_selectedTileIndices.Count == 0 && _state != BuilderState.BoxSelecting) return;

            // 计算外接矩形
            int minX, minY, maxX, maxY;
            if (_state == BuilderState.BoxSelecting)
            {
                minX = Mathf.Min(_boxSelStart.x, _boxSelEnd.x);
                maxX = Mathf.Max(_boxSelStart.x, _boxSelEnd.x);
                minY = Mathf.Min(_boxSelStart.y, _boxSelEnd.y);
                maxY = Mathf.Max(_boxSelStart.y, _boxSelEnd.y);
            }
            else
            {
                if (!TryGetSelectionRect(out var rect)) return;
                minX = rect.x; minY = rect.y;
                maxX = rect.xMax - 1; maxY = rect.yMax - 1;
            }

            float y = _activeZ * _layerHeightScale + 0.06f;
            var center = GridToWorld((minX + maxX) * 0.5f + 0.5f, (minY + maxY) * 0.5f + 0.5f, _activeZ);
            var size = new Vector3(maxX - minX + 1, 0.05f, maxY - minY + 1);

            // 半透蓝色填充（用 4 个 fill quad 不如直接 DrawSolidRectangleWithOutline）
            Handles.color = new Color(0.3f, 0.7f, 1f, 0.28f);
            Vector3[] verts = new Vector3[4];
            verts[0] = GridToWorld(minX, minY, _activeZ); verts[0].y = y;
            verts[1] = GridToWorld(maxX + 1, minY, _activeZ); verts[1].y = y;
            verts[2] = GridToWorld(maxX + 1, maxY + 1, _activeZ); verts[2].y = y;
            verts[3] = GridToWorld(minX, maxY + 1, _activeZ); verts[3].y = y;
            Handles.DrawSolidRectangleWithOutline(verts, new Color(0.3f, 0.7f, 1f, 0.28f), new Color(0.85f, 0.97f, 1f, 0.9f));

            // 4 角小角块（强化角点）
            Handles.color = new Color(1f, 1f, 1f, 0.95f);
            float cornerSize = 0.12f;
            Vector3[] corners = { verts[0], verts[1], verts[2], verts[3] };
            foreach (var c in corners)
                Handles.SphereHandleCap(0, c, Quaternion.identity, cornerSize * 2, EventType.Repaint);

            // 已选 tile 高亮（白色 wire cube，包住每个 tile）
            if (_state != BuilderState.BoxSelecting)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.55f);
                // 倒序遍历同时剔除失效索引（防御 ClearAllLevels/ClearLevel 后残留）
                for (int n = _selectedTileIndices.Count - 1; n >= 0; n--)
                {
                    int idx = _selectedTileIndices[n];
                    if (idx < 0 || idx >= _mapData.Tiles.Count)
                    {
                        _selectedTileIndices.RemoveAt(n);
                        continue;
                    }
                    var t = _mapData.Tiles[idx];
                    var pos = GridToWorld(t.X + 0.5f, t.Y + 0.5f, t.Z);
                    pos.y += 0.05f;
                    Handles.DrawWireCube(pos, new Vector3(1f, 0.05f, 1f));
                }
            }
        }

        /// <summary>阶段 5：待粘贴状态下的 ghost preview。鼠标 hover 时在目标位置画半透矩形 + 外接亮黄框。</summary>
        void DrawPasteGhostPreview(SceneView sv)
        {
            if (_mapData == null || _state != BuilderState.ReadyToCopy) return;
            if (_clipboard.Count == 0) return;
            if (_hoverCell.x < 0 || _hoverCell.y < 0) return;

            // 落点 = 当前 hover cell 当作"复制组原点"
            int dropX = _hoverCell.x;
            int dropY = _hoverCell.y;
            int dropZ = _activeZ;

            // 复制组整体外接尺寸（在 _clipboard 里以最小 (relX, relY) 为原点，max 偏移为尺寸-1）
            int groupW = _clipboardSize.x;
            int groupH = _clipboardSize.y;

            // 落点在地图内？（鼠标移到地图外时）
            bool dropInsideMap = dropX >= 0 && dropX < _mapData.Width && dropY >= 0 && dropY < _mapData.Height;

            // 仅在落点完全在地图外时显示"无法粘贴"提示
            if (!dropInsideMap && (dropX + groupW <= 0 || dropY + groupH <= 0 || dropX >= _mapData.Width || dropY >= _mapData.Height))
            {
                Handles.color = new Color(1f, 0.4f, 0.4f, 0.85f);
                Handles.Label(_sv.camera.transform.position + _sv.camera.transform.forward * 5f,
                    $"⚠ 粘贴组 {groupW}×{groupH} 完全超出地图 {_mapData.Width}×{_mapData.Height}", EditorStyles.boldLabel);
                return;
            }

            // 标记超出边界的 tile 数（UX 提示）
            int outCount = 0, totalCount = _clipboard.Count;
            foreach (var entry in _clipboard)
            {
                int gx = dropX + entry.x;
                int gy = dropY + entry.y;
                if (gx < 0 || gx >= _mapData.Width || gy < 0 || gy >= _mapData.Height) outCount++;
            }

            // 每个复制组内 tile → 在 (dropX + relX, dropY + relY, dropZ) 位置画蓝色 wire cube
            Handles.color = new Color(0.35f, 0.78f, 1f, 0.7f);
            foreach (var entry in _clipboard)
            {
                int gx = dropX + entry.x;
                int gy = dropY + entry.y;
                int gz = dropZ; // 按 F7-A：所有 Z 都贴到 _activeZ

                // 越界裁剪：不画
                if (gx < 0 || gx >= _mapData.Width || gy < 0 || gy >= _mapData.Height) continue;

                var pos = GridToWorld(gx + 0.5f, gy + 0.5f, gz);
                pos.y += entry.hOff + 0.05f;
                Handles.DrawWireCube(pos, new Vector3(0.95f, 0.05f, 0.95f));
            }

            // 整体外接亮黄 wire box（视觉边界，clip 在地图内的部分）
            Handles.color = new Color(1f, 0.95f, 0.3f, 0.9f);
            float yCenter = dropZ * _layerHeightScale + 0.06f;
            var groupCenter = GridToWorld(
                dropX + groupW * 0.5f,
                dropY + groupH * 0.5f,
                dropZ);
            groupCenter.y = yCenter;
            var groupSize = new Vector3(groupW, 0.05f, groupH);
            Handles.DrawWireCube(groupCenter, groupSize);

            // 标"待粘贴" 标签（在原点旁）+ 越界提示
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(1f, 0.95f, 0.3f) },
                fontSize = 12
            };
            Handles.Label(GridToWorld(dropX, dropY - 0.5f, dropZ) + Vector3.up * 0.3f,
                $"📋 待粘贴 {totalCount - outCount}/{totalCount} @ Z={dropZ}" + (outCount > 0 ? $"  ⚠越界 {outCount}" : ""),
                style);
        }

        void DrawGridHandles(SceneView sv)
        {
            int w = _mapData.Width, h = _mapData.Height;
            // 每层网格都画（Y 用 _layerHeightScale 缩放）
            for (int z = 0; z <= _maxZInData; z++)
            {
                bool active = (z == _activeZ);
                Handles.color = active
                    ? new Color(1f, 0.95f, 0.3f, 0.55f)
                    : GridColorForZ(z);
                float y = z * _layerHeightScale;
                for (int x = 0; x <= w; x++)
                    Handles.DrawLine(GridToWorld(x, 0, z), GridToWorld(x, h, z));
                for (int yy = 0; yy <= h; yy++)
                    Handles.DrawLine(GridToWorld(0, yy, z), GridToWorld(w, yy, z));

                // 当前层额外绘制 Z 标签
                if (active)
                {
                    var style = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal = { textColor = new Color(1f, 0.95f, 0.3f) },
                        fontSize = 14
                    };
                    Handles.Label(GridToWorld(0, h + 0.3f, z),
                        $"第 {z + 1} 层 (Z={z})", style);
                }
            }

            // —— 阶段 4 新增：四角橙色 Z 轴引导线 + 圆点 ——
            // 让用户随时能看到 Z=0 到最高层的"高度尺"，解决"加了层级看不到"
            Handles.color = new Color(1f, 0.5f, 0.2f, 0.45f);
            float topY = _maxZInData * _layerHeightScale;
            Vector2Int[] corners = { new Vector2Int(0, 0), new Vector2Int(w, 0),
                                     new Vector2Int(w, h), new Vector2Int(0, h) };
            foreach (var c in corners)
            {
                Handles.DrawDottedLine(GridToWorld(c.x, c.y, 0), GridToWorld(c.x, c.y, _maxZInData), 4f);
                // 在每层画一个小圆点标识该层的实际高度
                for (int z = 0; z <= _maxZInData; z++)
                {
                    var p = GridToWorld(c.x, c.y, z);
                    Handles.SphereHandleCap(0, p, Quaternion.identity, 0.12f, EventType.Repaint);
                }
            }

            // —— 阶段 4 新增：刚添加的新层闪烁 2 秒 ——
            if (_levelAddTime > 0)
            {
                double t = EditorApplication.timeSinceStartup - _levelAddTime;
                if (t < 2.0)
                {
                    float alpha = Mathf.PingPong((float)t * 2f, 1f);
                    Handles.color = new Color(1f, 0.95f, 0.3f, alpha);
                    // 用 _activeZ 而不是 _maxZInData，确保画在新添加的那一层
                    var center = GridToWorld(w / 2f, h / 2f, _activeZ);
                    Handles.DrawWireCube(center, new Vector3(w * 0.9f, 0.1f, h * 0.9f));
                    // 必须用 SceneView.Repaint() 才能让 wire cube 持续闪烁
                    var sv2 = SceneView.lastActiveSceneView;
                    if (sv2 != null) sv2.Repaint();
                }
                else
                {
                    _levelAddTime = -1;
                }
            }
        }

        static Color GridColorForZ(int z)
        {
            // Z=0 白, Z=1 青, Z=2 橙, Z>=3 紫
            switch (z % 4)
            {
                case 0: return new Color(1f, 1f, 1f, 0.15f);
                case 1: return new Color(0.4f, 1f, 1f, 0.15f);
                case 2: return new Color(1f, 0.7f, 0.2f, 0.15f);
                default: return new Color(1f, 0.5f, 1f, 0.15f);
            }
        }

        void DrawPlacedTiles(SceneView sv)
        {
            // 笔刷预览由 DrawHoverGhost 绘制；本方法保留为空，后续可扩展
            // （如显示同格多层栈的彩色边框等）
        }

        void DrawHoverGhost(SceneView sv)
        {
            if (_hoverCell.x < 0) return;
            // 笔刷形状预览：在 hover 中心周围画所有笔刷覆盖格子
            Handles.color = _eraseMode || _selectedCategory == HandMapCategory.Erase
                ? new Color(1f, 0.3f, 0.3f, 0.35f)
                : new Color(1f, 0.95f, 0.3f, 0.35f);
            foreach (var c in EnumerateBrushCells(_hoverCell.x, _hoverCell.y))
            {
                if (c.x < 0 || c.y < 0) continue;
                Vector3 center = GridToWorld(c.x + 0.5f, c.y + 0.5f, _activeZ);
                Handles.DrawWireCube(center, new Vector3(0.95f, 0.05f, 0.95f));
            }
        }

        /// <summary>
/// 处理 SceneView 事件。
/// 注意：不调用 HandleUtility.AddDefaultControl() —— 那是导致 SceneView
/// 鼠标右键 orbit/pan/zoom 失灵的元凶。改为只用 e.Use() 精确消费
/// 我们关心的左键事件（画/擦/吸管选中），让 SceneView 其他相机操作正常。
/// </summary>
        void HandleSceneEvents(SceneView sv)
        {
            var e = Event.current;

            // 鼠标移动 / 拖拽：更新 hover cell（不消费，让 SceneView 仍可相机操作）
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                Vector3 world = MouseToWorld(e.mousePosition, _activeZ);
                var cell = WorldToCell(world);
                if (cell != _hoverCell)
                {
                    _hoverCell = cell;
                    Repaint();
                    sv.Repaint();
                }
            }

            // 待粘贴状态：单击 = 实贴
            if (_state == BuilderState.ReadyToCopy && e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                PasteAtCurrentHover();
                e.Use();
                sv.Repaint();
                return;
            }

            // 左键按下：开始绘制 / 选中（Shift 模式）
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                if (e.shift)
                {
                    PickTileAtHover();
                }
                else
                {
                    // 启动可能的"画 tile"或"框选"双模式（拖拽距离决定）
                    if (_hoverCell.x < 0 || _hoverCell.y < 0
                        || _hoverCell.x >= _mapData.Width || _hoverCell.y >= _mapData.Height)
                    {
                        // 在地图外按的不算（保留相机控制）
                    }
                    else
                    {
                        _boxSelStart = _hoverCell;
                        _boxSelEnd = _hoverCell;
                        _dragPaintedCells.Clear();
                        _state = BuilderState.Idle; // 暂未确定，进入 Idle 等拖拽距离判定
                    }
                }
                e.Use();
                sv.Repaint();
            }

            // 左键拖拽：检测距离 → 画 tile 或 框选
            if (e.type == EventType.MouseDrag && e.button == 0 && !e.alt && !e.shift)
            {
                if (_boxSelStart.x >= 0 && _hoverCell.x >= 0)
                {
                    int dist = Mathf.Max(Mathf.Abs(_hoverCell.x - _boxSelStart.x),
                                         Mathf.Abs(_hoverCell.y - _boxSelStart.y));
                    if (dist >= 2)
                    {
                        // 大于等于 2 格 → 识别为框选，不再画 tile
                        _state = BuilderState.BoxSelecting;
                        _boxSelEnd = _hoverCell;
                    }
                }

                if (_state == BuilderState.BoxSelecting)
                {
                    // 更新框选矩形
                    _boxSelEnd = _hoverCell;
                    RecomputeSelectedTileIndices();
                    Repaint();
                    sv.Repaint();
                }
                else if (_boxSelStart.x >= 0)
                {
                    // 还不到 2 格 → 仍是画
                    PaintAtHover();
                    sv.Repaint();
                }
                e.Use();
            }

            // 左键松开：保留框选 / 结束绘制
            if (e.type == EventType.MouseUp && e.button == 0 && !e.alt)
            {
                if (_state == BuilderState.BoxSelecting)
                {
                    _state = BuilderState.BoxSelected;
                    _boxSelStart = new Vector2Int(-1, -1);
                    _boxSelEnd = new Vector2Int(-1, -1);
                }
                else
                {
                    _state = BuilderState.Idle;
                    _dragPaintedCells.Clear();
                    // 清掉起始点（避免下次按下误判）
                    if (_boxSelStart == _boxSelEnd && _boxSelStart.x >= 0)
                    {
                        // 单击未被识别为框选，重置 start
                    }
                    _boxSelStart = new Vector2Int(-1, -1);
                    _boxSelEnd = new Vector2Int(-1, -1);
                }
                e.Use();
                Repaint();
                sv.Repaint();
            }

            // 右键单击：吸管（不消费右键 Drag，让 SceneView 仍可 orbit）
            if (e.type == EventType.MouseDown && e.button == 1 && !e.alt)
            {
                if (_hoverCell.x < 0 || _hoverCell.y < 0) return;
                if (_hoverCell.x >= _mapData.Width || _hoverCell.y >= _mapData.Height) return;

                var tile = _mapData.FindTile(_hoverCell.x, _hoverCell.y, _activeZ);
                if (tile == null)
                {
                    for (int z = 0; z <= _maxZInData; z++)
                    {
                        var t = _mapData.FindTile(_hoverCell.x, _hoverCell.y, z);
                        if (t != null) { tile = t; break; }
                    }
                }
                if (tile != null)
                {
                    var t = tile.Value;
                    _activeZ = t.Z;
                    _currentRotationY = t.RotationY;
                    _rotationSnapped = Mathf.Approximately(t.RotationY, 0f)
                                    || Mathf.Approximately(t.RotationY, 90f)
                                    || Mathf.Approximately(t.RotationY, 180f)
                                    || Mathf.Approximately(t.RotationY, 270f);
                    _currentHeightOffset = t.HeightOffset;
                    if (SelectByPath(t.PrefabPath))
                    {
                        _eraseMode = false;
                        Repaint();
                        sv.Repaint();
                        Debug.Log($"[HandMapBuilder] 吸管: 切到第 {_activeZ + 1} 层, " +
                                  $"rot={t.RotationY:F1}°, hOff={t.HeightOffset:F2}, " +
                                  $"prefab = {Path.GetFileNameWithoutExtension(t.PrefabPath)}");
                    }
                }
                e.Use();
                sv.Repaint();
            }
        }

        /// <summary>根据当前 _boxSelStart / _boxSelEnd 计算选中的 tile 索引（指向 _mapData.Tiles）。</summary>
        void RecomputeSelectedTileIndices()
        {
            _selectedTileIndices.Clear();
            if (_mapData == null) return;
            int minX = Mathf.Min(_boxSelStart.x, _boxSelEnd.x);
            int maxX = Mathf.Max(_boxSelStart.x, _boxSelEnd.x);
            int minY = Mathf.Min(_boxSelStart.y, _boxSelEnd.y);
            int maxY = Mathf.Max(_boxSelStart.y, _boxSelEnd.y);

            for (int i = 0; i < _mapData.Tiles.Count; i++)
            {
                var t = _mapData.Tiles[i];
                if (t.X >= minX && t.X <= maxX && t.Y >= minY && t.Y <= maxY)
                {
                    // 任何层（跨层选择）
                    _selectedTileIndices.Add(i);
                }
            }
        }

        /// <summary>统一方法：在 tile 数据大幅变更（清空 / 缩窄 / 移除层级）后主动清空选区相关状态，
        /// 防止 SceneView 重绘时 OnSceneGUI 内的 TryGetSelectionRect / DrawBoxSelectionVisual
        /// 用失效的 _selectedTileIndices 索引访问 _mapData.Tiles 抛 ArgumentOutOfRangeException。</summary>
        void InvalidateSelectionReferences()
        {
            _selectedTileIndices.Clear();
            _clipboard.Clear();
            if (_state == BuilderState.ReadyToCopy || _state == BuilderState.BoxSelected)
                _state = BuilderState.Idle;
        }

        /// <summary>取得当前框选的归一化矩形（min/max cell）。
        /// 内部对失效索引做防御性清理（ClearLevel/ClearAllLevels/ApplyGridSize 后
        /// _selectedTileIndices 里可能残留已不存在 tile 的索引，导致 OnSceneGUI
        /// 重绘时 ArgumentOutOfRangeException 被刷屏）。</summary>
        bool TryGetSelectionRect(out RectInt rect)
        {
            rect = default;
            if (_mapData == null)
            {
                _selectedTileIndices.Clear();
                return false;
            }
            if (_selectedTileIndices.Count == 0)
            {
                rect = default;
                return false;
            }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            // 倒序遍历，同时剔除失效索引（避免 List 遍历时修改）
            for (int n = _selectedTileIndices.Count - 1; n >= 0; n--)
            {
                int idx = _selectedTileIndices[n];
                if (idx < 0 || idx >= _mapData.Tiles.Count)
                {
                    _selectedTileIndices.RemoveAt(n);
                    continue;
                }
                var t = _mapData.Tiles[idx];
                if (t.X < minX) minX = t.X;
                if (t.Y < minY) minY = t.Y;
                if (t.X > maxX) maxX = t.X;
                if (t.Y > maxY) maxY = t.Y;
            }
            if (_selectedTileIndices.Count == 0)
            {
                rect = default;
                return false;
            }
            rect = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        /// <summary>把当前选区转化为复制组，进入"待粘贴"状态。</summary>
        void EnterReadyToCopyState()
        {
            if (_mapData == null || _selectedTileIndices.Count == 0)
            {
                Debug.LogWarning("[HandMapBuilder] 没有框选内容可复制");
                return;
            }

            _clipboard.Clear();
            int minX = int.MaxValue, minY = int.MaxValue;
            // 第一轮：找原点
            foreach (int idx in _selectedTileIndices)
            {
                var t = _mapData.Tiles[idx];
                if (t.X < minX) minX = t.X;
                if (t.Y < minY) minY = t.Y;
            }
            _clipboardOrigin = new Vector3Int(minX, minY, 0);

            // 第二轮：写入相对坐标
            foreach (int idx in _selectedTileIndices)
            {
                var t = _mapData.Tiles[idx];
                _clipboard.Add(new CopyGroupEntry
                {
                    x = t.X - minX,
                    y = t.Y - minY,
                    z = t.Z,
                    prefabPath = t.PrefabPath,
                    rotY = t.RotationY,
                    hOff = t.HeightOffset,
                });
            }

            // 计算复制组尺寸（=外接矩形）
            int maxRelX = 0, maxRelY = 0;
            foreach (var e in _clipboard)
            {
                if (e.x > maxRelX) maxRelX = e.x;
                if (e.y > maxRelY) maxRelY = e.y;
            }
            _clipboardSize = new Vector2Int(maxRelX + 1, maxRelY + 1);

            _state = BuilderState.ReadyToCopy;
            Repaint();
            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] 进入待粘贴: {_clipboard.Count} 个 tile, " +
                      $"组尺寸 {_clipboardSize.x}×{_clipboardSize.y}, " +
                      $"按 Space/Enter 实贴，Esc 取消");
        }

        /// <summary>退出"待粘贴"状态。保留框选（用户可以再点复制）。</summary>
        void ExitCopyPasteState()
        {
            if (_state == BuilderState.ReadyToCopy)
            {
                _clipboard.Clear();
                _state = _selectedTileIndices.Count > 0 ? BuilderState.BoxSelected : BuilderState.Idle;
                Repaint();
                _sv?.Repaint();
            }
        }

        /// <summary>删除当前选区所有 tile（带 Undo）。</summary>
        void DeleteSelection()
        {
            if (_mapData == null || _selectedTileIndices.Count == 0) return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"删除 {_selectedTileIndices.Count} 个 tile");
            Undo.RecordObject(_mapData, "删除选区");

            // 倒序索引，逐个删除
            var sorted = new List<int>(_selectedTileIndices);
            sorted.Sort();
            sorted.Reverse();
            foreach (int idx in sorted)
            {
                if (idx < 0 || idx >= _mapData.Tiles.Count) continue;
                var key = new Vector3Int(_mapData.Tiles[idx].X,
                                         _mapData.Tiles[idx].Y,
                                         _mapData.Tiles[idx].Z);
                if (_spawnedByCell.TryGetValue(key, out var go) && go != null)
                    DestroyImmediate(go);
                _spawnedByCell.Remove(key);
                _mapData.Tiles.RemoveAt(idx);
            }
            EditorUtility.SetDirty(_mapData);
            Undo.CollapseUndoOperations(undoGroup);

            // 清理选区
            ClearSelection();
            Repaint();
            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] 已删除选区，剩余 {_mapData.Tiles.Count} 个 tile");
        }

        /// <summary>清空选区（不删除数据）。</summary>
        void ClearSelection()
        {
            _selectedTileIndices.Clear();
            if (_state == BuilderState.BoxSelected || _state == BuilderState.BoxSelecting)
                _state = BuilderState.Idle;
            if (_state == BuilderState.ReadyToCopy)
                _clipboard.Clear();
            _state = _state == BuilderState.ReadyToCopy ? BuilderState.Idle : _state;
            _boxSelStart = new Vector2Int(-1, -1);
            _boxSelEnd = new Vector2Int(-1, -1);
            Repaint();
            _sv?.Repaint();
        }

        /// <summary>在当前 hover cell 位置粘贴 _clipboard 中的内容（按 F7-A：所有 Z 都贴到 _activeZ）。
        /// hover 在地图外 → 全部裁掉的情况下，拦截掉避免刷"放置 0 个"。</summary>
        void PasteAtCurrentHover()
        {
            if (_mapData == null || _clipboard.Count == 0) return;
            if (_state != BuilderState.ReadyToCopy) return;

            // 落点完全越界拦截（保留部分越界仍能贴的设计）
            int groupW = _clipboardSize.x, groupH = _clipboardSize.y;
            int dx = _hoverCell.x, dy = _hoverCell.y;
            if (dx + groupW <= 0 || dy + groupH <= 0 || dx >= _mapData.Width || dy >= _mapData.Height)
            {
                Debug.LogWarning($"[HandMapBuilder] 粘贴组 {groupW}×{groupH} 完全超出地图 {_mapData.Width}×{_mapData.Height}，" +
                    $"当前落点 ({dx},{dy}) 不在地图内，无法粘贴。请把鼠标移到地图内再试");
                return;
            }
            int dropX = _hoverCell.x;
            int dropY = _hoverCell.y;
            int dropZ = _activeZ;

            int placed = 0, clipped = 0;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"粘贴 {_clipboard.Count} 个 tile @ Z={dropZ}");
            Undo.RecordObject(_mapData, "粘贴");

            foreach (var entry in _clipboard)
            {
                int gx = dropX + entry.x;
                int gy = dropY + entry.y;
                // 越界：跳过
                if (gx < 0 || gx >= _mapData.Width || gy < 0 || gy >= _mapData.Height)
                {
                    clipped++;
                    continue;
                }

                // 检查目标 Z 上是否已有同坐标 tile（避免重复）—— 已存在则跳过
                bool conflict = false;
                for (int i = 0; i < _mapData.Tiles.Count; i++)
                {
                    var t = _mapData.Tiles[i];
                    if (t.X == gx && t.Y == gy && t.Z == dropZ) { conflict = true; break; }
                }
                if (conflict) { clipped++; continue; }

                _mapData.Tiles.Add(new HandPlacedTile
                {
                    X = gx,
                    Y = gy,
                    Z = dropZ,
                    PrefabPath = entry.prefabPath,
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath),
                    RotationY = entry.rotY,
                    HeightOffset = entry.hOff,
                    Category = GuessCategoryFromPath(entry.prefabPath),
                });
                placed++;
            }
            EditorUtility.SetDirty(_mapData);
            Undo.CollapseUndoOperations(undoGroup);

            // 重建预览（单 Z，重建轻量）
            RefreshLayerPreviews(dropZ);

            Repaint();
            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] 粘贴: 落点 ({dropX},{dropY}) Z={dropZ}, " +
                      $"放置 {placed} 个, 越界/冲突跳过 {clipped} 个（剩余复制组 {_clipboard.Count} 个，仍可继续粘）");

            // 保留 ReadyToCopy 状态：用户可继续粘贴 / 切层后粘贴 / Esc 退出
        }

        /// <summary>仅刷新某一 Z 层的预览（粘贴只需要重新实例化该层 tile）。</summary>
        void RefreshLayerPreviews(int z)
        {
            if (_mapData == null) return;
            // 先销毁该层所有现存 preview
            var keysToRemove = new List<Vector3Int>();
            foreach (var kv in _spawnedByCell)
            {
                if (kv.Key.z == z && kv.Value != null)
                {
                    DestroyImmediate(kv.Value);
                    keysToRemove.Add(kv.Key);
                }
            }
            foreach (var k in keysToRemove) _spawnedByCell.Remove(k);
            // 再重新实例化该层所有 tile
            for (int i = 0; i < _mapData.Tiles.Count; i++)
            {
                var tile = _mapData.Tiles[i];
                if (tile.Z != z) continue;
                if (string.IsNullOrEmpty(tile.PrefabPath)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(tile.PrefabPath);
                if (prefab == null) continue;
                EnsurePreviewFor(new Vector3Int(tile.X, tile.Y, tile.Z), prefab);
            }
        }

        /// <summary>从 prefab 路径粗判类别（用于粘贴后 Cat 字段正确）。</summary>
        HandTileCategory GuessCategoryFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return HandTileCategory.Base;
            // HandMapPalette.Paths key 是 HandMapCategory enum，转 HandTileCategory 用 (HandTileCategory)(int)kv.Key
            foreach (var kv in HandMapPalette.Paths)
            {
                if (kv.Value.Contains(path)) return (HandTileCategory)(int)kv.Key;
            }
            return HandTileCategory.Base;
        }

        /// <summary>
        /// 在当前 hover cell 用笔刷形状绘制（放置或擦除）。
        /// 拖拽时通过 _dragPaintedCells 去重，避免每帧重复 PlaceAt。
        /// </summary>
        void PaintAtHover()
        {
            if (_mapData == null) return;
            if (_hoverCell.x < 0 || _hoverCell.y < 0) return;
            if (_hoverCell.x >= _mapData.Width || _hoverCell.y >= _mapData.Height) return;

            bool erasing = _eraseMode || _selectedCategory == HandMapCategory.Erase;
            if (erasing)
            {
                foreach (var c in EnumerateBrushCells(_hoverCell.x, _hoverCell.y))
                {
                    if (c.x < 0 || c.y < 0 || c.x >= _mapData.Width || c.y >= _mapData.Height) continue;
                    if (!_dragPaintedCells.Add(c)) continue; // 已擦过
                    EraseAt(c);
                }
                return;
            }

            var paths = HandMapPalette.Paths[_selectedCategory];
            if (paths == null) return;
            int idx = _currentPage * PalettePerPage + _selectedSlotIndex;
            if (idx < 0 || idx >= paths.Count) return;
            string path = paths[idx];

            foreach (var c in EnumerateBrushCells(_hoverCell.x, _hoverCell.y))
            {
                if (c.x < 0 || c.y < 0 || c.x >= _mapData.Width || c.y >= _mapData.Height) continue;
                if (!_dragPaintedCells.Add(c)) continue; // 已画过
                PlaceAt(new Vector3Int(c.x, c.y, _activeZ), path);
            }
        }

        bool SelectByPath(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return false;
            string trimmed = prefabPath.EndsWith(".prefab")
                ? prefabPath.Substring(0, prefabPath.Length - ".prefab".Length)
                : prefabPath;
            foreach (var kv in HandMapPalette.Paths)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == trimmed)
                    {
                        _selectedCategory = kv.Key;
                        _currentPage = i / PalettePerPage;
                        _selectedSlotIndex = i % PalettePerPage;
                        return true;
                    }
                }
            }
            return false;
        }

        // ---- 数据操作 -------------------------------------------------------------

        void PlaceAt(Vector3Int cell, string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[HandMapBuilder] 找不到 prefab: " + prefabPath);
                return;
            }

            Undo.RecordObject(_mapData, "放置地块");
            // 同格同 Z 已有则替换
            _mapData.RemoveTile(cell.x, cell.y, cell.z);
            _mapData.Tiles.Add(new HandPlacedTile
            {
                X = cell.x,
                Y = cell.y,
                Z = cell.z,
                PrefabPath = prefabPath + ".prefab",
                Prefab = prefab,
                RotationY = _currentRotationY,
                HeightOffset = _currentHeightOffset,
                Category = GuessCategory(prefabPath),
            });
            EditorUtility.SetDirty(_mapData);

            EnsurePreviewFor(cell, prefab);
        }

        void EraseAt(Vector2Int cell)
        {
            Undo.RecordObject(_mapData, "擦除地块");
            bool removed = false;
            // 删除当前激活 Z 的 tile
            if (_mapData.RemoveTile(cell.x, cell.y, _activeZ))
            {
                EditorUtility.SetDirty(_mapData);
                removed = true;
            }
            var key = new Vector3Int(cell.x, cell.y, _activeZ);
            if (_spawnedByCell.TryGetValue(key, out var go) && go != null)
            {
                Undo.DestroyObjectImmediate(go);
                _spawnedByCell.Remove(key);
            }
            if (!removed)
            {
                Debug.Log($"[HandMapBuilder] ({cell.x}, {cell.y}, Z={_activeZ}) 没有放置物");
            }
        }

        void RemoveHighestZ()
        {
            if (_mapData == null || _maxZInData <= 0) return;
            Undo.RecordObject(_mapData, "移除最高层级");
            int zToRemove = _maxZInData;
            _mapData.Tiles.RemoveAll(t => t.Z == zToRemove);
            EditorUtility.SetDirty(_mapData);

            // 销毁该层的所有 preview 实例
            var keysToRemove = new List<Vector3Int>();
            foreach (var kv in _spawnedByCell)
            {
                if (kv.Key.z == zToRemove && kv.Value != null)
                {
                    DestroyImmediate(kv.Value);
                    keysToRemove.Add(kv.Key);
                }
            }
            foreach (var k in keysToRemove) _spawnedByCell.Remove(k);

            _maxZInData--;
            // 用户主动保留的层级也跟着降
            if (_userReservedMaxZ > 0) _userReservedMaxZ--;
            if (_activeZ > _maxZInData) _activeZ = _maxZInData;
            // 移除层级后，被移除层的 tile 索引全部失效，清掉避免下次绘制抛异常
            InvalidateSelectionReferences();
            // 移除后相机跟回到新最高层（保留用户视角，不强制旋转）
            FocusSceneViewOnLevel(_maxZInData);
            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] 已移除第 {zToRemove + 1} 层");
        }

        /// <summary>
        /// 用当前选中 prefab 填充当前激活 Z 层。
        /// overwrite=false: 空格填，已有保留（推荐默认）；
        /// overwrite=true: 强制覆盖所有格子。
        /// </summary>
        void FillLevel(bool overwrite)
        {
            if (_mapData == null) return;
            if (_eraseMode)
            {
                Debug.LogWarning("[HandMapBuilder] 擦除模式下不可填充，请先关闭擦除模式");
                return;
            }
            var paths = HandMapPalette.Paths[_selectedCategory];
            if (paths == null) return;
            int idx = _currentPage * PalettePerPage + _selectedSlotIndex;
            if (idx < 0 || idx >= paths.Count) return;
            string prefabPath = paths[idx] + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[HandMapBuilder] 填充失败: prefab 不存在 " + prefabPath);
                return;
            }

            // 把整批操作合并到一个 Undo group（按一次 Ctrl+Z 可整体撤销）
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(overwrite ? "覆盖填充层级" : "填充层级");
            Undo.RecordObject(_mapData, overwrite ? "覆盖填充层级" : "填充层级");

            int filled = 0, skipped = 0;
            int w = _mapData.Width, h = _mapData.Height;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var existing = _mapData.FindTile(x, y, _activeZ);
                    if (existing != null)
                    {
                        if (!overwrite) { skipped++; continue; }
                        _mapData.RemoveTile(x, y, _activeZ);
                        var keyOld = new Vector3Int(x, y, _activeZ);
                        if (_spawnedByCell.TryGetValue(keyOld, out var goOld) && goOld != null)
                        {
                            DestroyImmediate(goOld);
                            _spawnedByCell.Remove(keyOld);
                        }
                    }
                    _mapData.Tiles.Add(new HandPlacedTile
                    {
                        X = x, Y = y, Z = _activeZ,
                        PrefabPath = prefabPath,
                        Prefab = prefab,
                        RotationY = _currentRotationY,
                        HeightOffset = _currentHeightOffset,
                        Category = GuessCategory(paths[idx]),
                    });
                    EnsurePreviewFor(new Vector3Int(x, y, _activeZ), prefab);
                    filled++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.SetDirty(_mapData);
            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] {(overwrite ? "覆盖填充" : "填充")} 第 {_activeZ + 1} 层: " +
                      $"填 {filled} 个, 跳过 {skipped} 个");
        }

        /// <summary>删除当前激活 Z 层所有 tile + 预览实例，其他 Z 不动。
        /// Dialog 显示当前 Z 和 tile 数量；删 0 个时弹提示，避免"点了没反应"的困惑。</summary>
        void ClearLevel()
        {
            if (_mapData == null) return;
            int tileCount = CountTilesAtZ(_activeZ);
            if (tileCount == 0)
            {
                EditorUtility.DisplayDialog("清空层级",
                    $"当前激活的第 {_activeZ + 1} 层（Z={_activeZ}）是空的，没有可删除的 tile。\n\n" +
                    $"• 想清掉截图里的地形？切到对应层级后再点\n" +
                    $"• 想一次性清掉所有 Z？按下面的『清空所有层级』",
                    "知道了");
                Debug.LogWarning($"[HandMapBuilder] 清空层级失败: Z={_activeZ} 没有 tile（共有 {_mapData.Tiles.Count} 个分布在其他层）");
                return;
            }
            if (!EditorUtility.DisplayDialog("清空层级",
                $"确认删除第 {_activeZ + 1} 层（Z={_activeZ}）的 {tileCount} 个 tile？\n" +
                $"其他层级（{_mapData.Tiles.Count - tileCount} 个 tile）不受影响。",
                "清空", "取消"))
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"清空第 {_activeZ + 1} 层");
            Undo.RecordObject(_mapData, "清空层级");
            int removedCount = _mapData.Tiles.RemoveAll(t => t.Z == _activeZ);
            EditorUtility.SetDirty(_mapData);
            InvalidateSelectionReferences();

            // 销毁预览实例
            var keysToRemove = new List<Vector3Int>();
            foreach (var kv in _spawnedByCell)
            {
                if (kv.Key.z == _activeZ && kv.Value != null)
                {
                    DestroyImmediate(kv.Value);
                    keysToRemove.Add(kv.Key);
                }
            }
            foreach (var k in keysToRemove) _spawnedByCell.Remove(k);
            Undo.CollapseUndoOperations(undoGroup);

            // 更新 _maxZInData（保留用户主动保留的层级）
            int newMax = 0;
            for (int i = 0; i < _mapData.Tiles.Count; i++)
                if (_mapData.Tiles[i].Z > newMax) newMax = _mapData.Tiles[i].Z;
            _maxZInData = Mathf.Max(newMax, _userReservedMaxZ);
            if (_activeZ > _maxZInData) _activeZ = _maxZInData;

            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] 已清空第 {_activeZ + 1} 层: 删除 {removedCount} 个 tile（剩余 {_mapData.Tiles.Count} 个）");
        }

        /// <summary>一键清空所有 Z 层（不管当前激活哪一层）。用于"全删重来"场景。
        /// 二次弹窗防误操作。</summary>
        void ClearAllLevels()
        {
            if (_mapData == null || _mapData.Tiles.Count == 0) return;
            if (!EditorUtility.DisplayDialog("清空所有层级",
                $"确认清空整张地图的 {_mapData.Tiles.Count} 个 tile？\n" +
                $"此操作影响所有 Z 层，无法撤销前的备份请提前 Ctrl+S 保存。",
                "清空全部", "取消"))
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("清空所有层级");
            Undo.RecordObject(_mapData, "清空所有层级");
            int removedCount = _mapData.Tiles.Count;
            _mapData.Tiles.Clear();
            EditorUtility.SetDirty(_mapData);
            InvalidateSelectionReferences(); // 清空后所有 tile 索引失效，清掉避免 SceneView 重绘抛异常

            // 销毁所有预览实例
            var keysToRemove = new List<Vector3Int>();
            foreach (var kv in _spawnedByCell)
            {
                if (kv.Value != null)
                {
                    DestroyImmediate(kv.Value);
                    keysToRemove.Add(kv.Key);
                }
            }
            foreach (var k in keysToRemove) _spawnedByCell.Remove(k);
            Undo.CollapseUndoOperations(undoGroup);

            _maxZInData = 0;
            _activeZ = 0;
            _userReservedMaxZ = 0;  // 清空所有数据时同时清空用户保留的层级
            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] 已清空所有层级: 删除 {removedCount} 个 tile");
        }

        void EnsurePreviewFor(Vector3Int cell, GameObject prefab, bool registerUndo = true)
        {
            var key = new Vector3Int(cell.x, cell.y, cell.z);
            if (_spawnedByCell.TryGetValue(key, out var existing) && existing != null)
            {
                DestroyImmediate(existing);
                _spawnedByCell.Remove(key);
            }
            // 从数据源读取该格的 rotation 和 height offset（而不是全局当前值）
            // 因为 FillLevel 后需要重建预览、要忠实反映实际数据
            float rotY = _currentRotationY;
            float hOff = _currentHeightOffset;
            var t = _mapData?.FindTile(cell.x, cell.y, cell.z);
            if (t != null)
            {
                rotY = t.Value.RotationY;
                hOff = t.Value.HeightOffset;
            }

            Vector3 world = GridToWorld(cell.x + 0.5f, cell.y + 0.5f, cell.z * 1f);
            world.y += hOff;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) return;
            instance.transform.position = world;
            instance.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            instance.transform.localScale = Vector3.one * 0.5f;
            instance.name = $"HandMapTile_{cell.x}_{cell.y}_Z{cell.z}";
            instance.hideFlags = HideFlags.DontSaveInEditor;
            var cols = instance.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (Application.isPlaying) Destroy(cols[i]);
                else DestroyImmediate(cols[i]);
            }
            _spawnedByCell[key] = instance;
            // 默认注册 Undo（单格放置），FillLevel 批量时由调用方用 IncrementCurrentGroup 合并
            if (registerUndo) Undo.RegisterCreatedObjectUndo(instance, "放置地块预览");
        }

        void RebuildAllPreviews()
        {
            // 调整尺寸时调用：从 _mapData.Tiles 重建预览
            if (_mapData == null) return;
            foreach (var t in _mapData.Tiles)
            {
                if (string.IsNullOrEmpty(t.PrefabPath)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(t.PrefabPath);
                if (prefab == null) continue;
                EnsurePreviewFor(new Vector3Int(t.X, t.Y, t.Z), prefab, false);
            }
        }

        void ClearSpawnedPreview()
        {
            foreach (var kv in _spawnedByCell)
            {
                if (kv.Value != null) DestroyImmediate(kv.Value);
            }
            _spawnedByCell.Clear();
        }

        int CountTilesAtCellZ(Vector2Int cell, int z)
        {
            if (_mapData == null) return 0;
            int n = 0;
            for (int i = 0; i < _mapData.Tiles.Count; i++)
            {
                var t = _mapData.Tiles[i];
                if (t.X == cell.x && t.Y == cell.y && t.Z == z) n++;
            }
            return n;
        }

        /// <summary>统计整个 Z 层有多少 tile（无视格子），给按钮文字/Dialog 提示用。</summary>
        int CountTilesAtZ(int z)
        {
            if (_mapData == null) return 0;
            int n = 0;
            for (int i = 0; i < _mapData.Tiles.Count; i++)
                if (_mapData.Tiles[i].Z == z) n++;
            return n;
        }

        HandTileCategory GuessCategory(string path)
        {
            string low = path.ToLowerInvariant();
            if (low.Contains("bridge")) return HandTileCategory.Bridge;
            if (low.Contains("water") || low.Contains("waterfall")) return HandTileCategory.Water;
            if (low.Contains("ramp") || low.Contains("stair")) return HandTileCategory.Ramp;
            if (low.Contains("rock")) return HandTileCategory.Mountain;
            if (low.Contains("tree") || low.Contains("forest")) return HandTileCategory.Forest;
            if (low.Contains("plant") || low.Contains("mushroom") || low.Contains("ground_leafs"))
                return HandTileCategory.Plant;
            if (low.Contains("camp") || low.Contains("mine") || low.Contains("magic")
                || low.Contains("graveyard") || low.Contains("dungeon"))
                return HandTileCategory.Building;
            if (low.Contains("tile_group") || low.Contains("path")) return HandTileCategory.Path;
            if (low.Contains("fog") || low.Contains("glow") || low.Contains("ripple")
                || low.Contains("candle") || low.Contains("particle"))
                return HandTileCategory.Effect;
            if (low.Contains("grass") || low.Contains("brick") || low.Contains("tile_1")
                || low.Contains("tile_base") || low.Contains("tile1_base")) return HandTileCategory.Base;
            return HandTileCategory.Decoration;
        }

        // ---- 坐标转换 -------------------------------------------------------------

        Vector3 GridToWorld(float x, float y, float z)
        {
            // 阶段4：Z 层高度按 _layerHeightScale 缩放（0.5~2.0）
            return new Vector3(x, z * _layerHeightScale, y);
        }

        Vector2Int WorldToCell(Vector3 world)
        {
            int x = Mathf.FloorToInt(world.x);
            int y = Mathf.FloorToInt(world.z);
            return new Vector2Int(x, y);
        }

        Vector3 MouseToWorld(Vector2 mousePos, int z)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mousePos);
            // 用激活层所在 Y 平面试射线
            Plane plane = new Plane(Vector3.up, new Vector3(0, -z, 0));
            if (plane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }
            return Vector3.zero;
        }

        // ---- 资产与场景 ------------------------------------------------------------

        void CreateNewAsset()
        {
            if (!AssetDatabase.IsValidFolder(DefaultAssetDir))
            {
                Directory.CreateDirectory(DefaultAssetDir);
                AssetDatabase.Refresh();
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(DefaultAssetDir, DefaultAssetName + ".asset").Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<HandAuthoredMapData>();
            asset.LayerHeightScale = _layerHeightScale;
            asset.DefaultPrefabScale = 0.5f;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _mapData = asset;
            ClearSpawnedPreview();
            _activeZ = 0;
            _maxZInData = 0;
            _userReservedMaxZ = 0;
            _sv?.Repaint();
            Debug.Log("[HandMapBuilder] 新建地图数据: " + path);
        }

        void SaveAsset()
        {
            if (_mapData == null) return;
            _mapData.LayerHeightScale = _layerHeightScale;
            RefillRuntimePrefabReferences(_mapData);
            EditorUtility.SetDirty(_mapData);
            AssetDatabase.SaveAssetIfDirty(_mapData);
            Debug.Log($"[HandMapBuilder] 已保存: {AssetDatabase.GetAssetPath(_mapData)} ({_mapData.Tiles.Count} 个 tile)");
        }

        void ApplyToLevelProfile()
        {
            SaveAsset();
            Undo.RecordObject(_targetProfile, "应用手作地图到关卡");
            _targetProfile.HandMapOverride = _mapData;
            EditorUtility.SetDirty(_targetProfile);
            AssetDatabase.SaveAssetIfDirty(_targetProfile);

            const string runtimeConfigPath = "Assets/Resources/Battle/Map/HandMapRuntimeConfig.asset";
            var runtimeConfig = AssetDatabase.LoadAssetAtPath<HandMapRuntimeConfig>(runtimeConfigPath);
            if (runtimeConfig == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(runtimeConfigPath));
                runtimeConfig = CreateInstance<HandMapRuntimeConfig>();
                AssetDatabase.CreateAsset(runtimeConfig, runtimeConfigPath);
            }
            Undo.RecordObject(runtimeConfig, "激活手作战斗地图");
            runtimeConfig.ActiveMap = _mapData;
            runtimeConfig.ActiveProfile = _targetProfile;
            EditorUtility.SetDirty(runtimeConfig);
            AssetDatabase.SaveAssetIfDirty(runtimeConfig);
            Debug.Log("[HandMapBuilder] 已应用地图 " + _mapData.name + " 到关卡配置 " + _targetProfile.name);
        }

        static int RefillRuntimePrefabReferences(HandAuthoredMapData map)
        {
            int filled = 0;
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                var tile = map.Tiles[i];
                if (tile.Prefab != null || string.IsNullOrEmpty(tile.PrefabPath)) continue;
                tile.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(tile.PrefabPath);
                if (tile.Prefab == null) continue;
                map.Tiles[i] = tile;
                filled++;
            }
            return filled;
        }

        void CreateCleanWorkspaceScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "新建工作区场景",
                    "将关闭当前场景、新建一个空的 Basic(Built-in) 场景并保存到 Assets/Scenes/。\n继续？",
                    "新建", "取消"))
            {
                return;
            }

            var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(8f, 12f, -10f);
                cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = 6f;
            }

            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                float cx = _mapData != null ? _mapData.Width * 0.5f : 8f;
                float cz = _mapData != null ? _mapData.Height * 0.5f : 8f;
                sv.LookAt(new Vector3(cx, 0f, cz), Quaternion.Euler(45f, 0f, 0f), 12f);
                sv.Repaint();
            }

            const string scenesFolder = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(scenesFolder))
            {
                Directory.CreateDirectory(scenesFolder);
                AssetDatabase.Refresh();
            }
            string scenePath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(scenesFolder, "HandMapWorkspace.unity").Replace('\\', '/'));
            bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, scenePath);
            if (saved)
            {
                Debug.Log("[HandMapBuilder] 已创建工作区场景: " + scenePath);
            }
            else
            {
                Debug.LogWarning("[HandMapBuilder] 工作区场景保存失败");
            }

            ClearSpawnedPreview();
        }

        // Shift+左键 → 选中该格的 tile 进 Inspector
        void PickTileAtHover()
        {
            if (_mapData == null) return;
            if (_hoverCell.x < 0 || _hoverCell.y < 0) return;
            // 优先 active z，其次扫其他层
            var tile = _mapData.FindTile(_hoverCell.x, _hoverCell.y, _activeZ);
            if (tile == null)
            {
                for (int z = 0; z <= _maxZInData; z++)
                {
                    var t = _mapData.FindTile(_hoverCell.x, _hoverCell.y, z);
                    if (t != null) { tile = t; break; }
                }
            }
            if (tile != null)
            {
                _selectedTileForEdit = tile;
                Repaint();
                Debug.Log($"[HandMapBuilder] 选中 tile: ({tile.Value.X}, {tile.Value.Y}, Z={tile.Value.Z}) " +
                          $"hOff={tile.Value.HeightOffset:F2}");
            }
        }
    }
}
