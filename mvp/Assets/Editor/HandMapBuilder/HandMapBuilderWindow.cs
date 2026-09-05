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
            // 模式 1:Idle        编辑器地形模式（默认）。鼠标左键单击/拖动 = 笔刷画/擦;Ctrl+左键拖 = 框选;Shift+左键 = 模式 2。右键 = 吸管。Esc 无操作。
            // 模式 2:InspectingTile 选中已放置 tile。鼠标左键/拖动全部 noop(避免误画),Inspector 调整高度/旋转/偏移。Esc → Idle。右键吸管仍可用。
            // 模式 3:ReadyToCopy 复制粘贴模式。鼠标左键单击 = 实贴;Space / Enter 也实贴。Esc → Idle(保留框选)。
            // 注:user 原话"不能出现框选框" — 模式 1 左键拖绝不变框选;BoxSelecting/BoxSelected 仅在 Ctrl+左键拖时出现,作为 Idle 的子状态(拖框中的中间态)。
            Idle,
            BoxSelecting,    // Idle 子状态:Ctrl+左键拖框中(任何距离)
            BoxSelected,     // Idle 子状态:Ctrl+左键拖完成,等待点"复制 (⏎)" / Enter / 删除 / Esc
            InspectingTile,  // 模式 2:Shift+左键 选中已放置 tile。鼠标左键啥都不做。Esc → Idle。
            ReadyToCopy,     // 模式 3:点"复制"或 Enter 后进入。鼠标左键单击 = 实贴。Esc → Idle。
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

        // Esc 后到下一次 MouseUp 期间，吞掉所有鼠标事件（防止"按住左键 Esc 后继续 MouseDrag 重新进 BoxSelecting"）
        bool _suppressMouseUntilUp;

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
            // Domain reload / Undo can make _spawnedByCell lose entries while the
            // preview GameObjects are still alive.  Closing the tool must remove
            // every preview, not just the instances still present in the dictionary.
            ClearAllPreviewObjects();
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
                "左键单击/拖动 = 笔刷连续画；Ctrl+左键拖 = 框选；Shift+左键 = 模式2 Inspector；右键 = 吸管（自动跳到该 prefab 所在层级）。\n" +
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
                        if (kv.Value != null) Undo.DestroyObjectImmediate(kv.Value);
                        keysToRemove.Add(kv.Key);
                    }
                }
                foreach (var k in keysToRemove) _spawnedByCell.Remove(k);
                CleanOrphanedPreviews(-1);

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
                // 模式 1 / 子状态
                case BuilderState.BoxSelecting: stateText = "🟦 模式1·编辑器 — Ctrl+左键拖框选中..."; stateColor = new Color(0.4f, 0.7f, 1f); break;
                case BuilderState.BoxSelected:  stateText = $"🟦 模式1·编辑器 — 已选 {selCount} 个 tile（点「复制 (⏎)」进入模式3,Esc 清选）"; stateColor = new Color(0.5f, 0.9f, 0.5f); break;
                // 模式 2
                case BuilderState.InspectingTile:
                    stateText = _selectedTileForEdit.HasValue
                        ? $"🟨 模式2·Inspector — 已选 tile ({_selectedTileForEdit.Value.X},{_selectedTileForEdit.Value.Y},Z={_selectedTileForEdit.Value.Z}) | 调整高度/旋转/平移 | Esc 退回模式1"
                        : "🟨 模式2·Inspector — Esc 退回模式1";
                    stateColor = new Color(1f, 0.95f, 0.3f);
                    break;
                // 模式 3
                case BuilderState.ReadyToCopy:  stateText = $"🟩 模式3·复制粘贴 — 待粘贴 {_clipboard.Count} 个 tile（Space / 单击 实贴 | Esc 退出）"; stateColor = new Color(0.5f, 1f, 0.5f); break;
                // Idle (默认 / 模式1)
                default: stateText = "🟦 模式1·编辑器 — 左键拖=笔刷(画/擦),Ctrl+左键拖=框选,Shift+左键=模式2,右键=吸管"; stateColor = new Color(0.7f, 0.7f, 0.7f); break;
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
                // 非 0/90/180/270 任意角 → 吸附失效,按钮全灭
                _rotationSnapped = Mathf.Approximately(newRot, 0f)
                                || Mathf.Approximately(newRot, 90f)
                                || Mathf.Approximately(newRot, 180f)
                                || Mathf.Approximately(newRot, 270f);
                _sv?.Repaint();
            }
            if (GUILayout.Button("+90°", GUILayout.Width(55))) { AdjustRotation(90f); }
            if (GUILayout.Button("-90°", GUILayout.Width(55))) { AdjustRotation(-90f); }
            GUILayout.Label(_rotationSnapped ? "吸附" : "自由角", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // —— 高度偏移（这里控制"新放置 tile 的默认高度"，不是已放置的）——
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("高度:", "这个滑块 = 新放置 tile 的默认 HeightOffset。\n" +
                                       "只影响接下来新放的地块；已放置的不动。\n" +
                                       "想改已放置 tile 的高度？看下方『Inspector — 调整已放置 tile』区，Shift+左键先选中。"),
                GUILayout.Width(50));
            float newH = EditorGUILayout.Slider(_currentHeightOffset, -2f, 2f);
            if (!Mathf.Approximately(newH, _currentHeightOffset))
            {
                _currentHeightOffset = newH;
            }
            if (GUILayout.Button("归零", GUILayout.Width(45))) { _currentHeightOffset = 0f; _sv?.Repaint(); }
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
            _sv?.Repaint();
        }

        // —— Tile Inspector（Shift+左键 选中后调整现有 tile 的 HeightOffset）——
        void DrawTileInspector()
        {
            if (_mapData == null) return;

            // —— 未选中态：占位提示，告诉用户怎么进入选中 ——
            if (!_selectedTileForEdit.HasValue)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Inspector — 调整已放置 tile", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var prev = GUI.color;
                GUI.color = new Color(0.6f, 0.85f, 1f);  // 浅蓝提示色
                EditorGUILayout.LabelField(
                    "💡 想调整某一块已放置 tile 的高度/数据？",
                    EditorStyles.boldLabel);
                GUI.color = prev;
                EditorGUILayout.LabelField(
                    "  在 Scene 视图里按住 Shift，单击该 tile → 选中后这里会滑块化显示。",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    "  选中后：旋转 / 高度偏移 / 删除 全部在下方操作。",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

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
                    go.transform.position = new Vector3(w.x + t.OffsetX, w.y + t.HeightOffset, w.z + t.OffsetY);
                }
            }

            // —— 水平偏移(X / Y)：手动微调,默认 0 = 完全跟随自动 pivot 修正 ——
            // 滑条范围 [-1, +1] 单位 = 整 1 格,允许用户让 tile 偏移最多 1 格。
            EditorGUILayout.LabelField(
                "水平偏移 (手动微调)",
                EditorStyles.miniBoldLabel);
            float newOX = EditorGUILayout.Slider(
                new GUIContent("X:",
                    "X 方向手动偏移。让 tile 在格子里挪动 X 方向最多 1 格。\n" +
                    "0 = 默认居中;需要 tile 偏离格子中心时(例如伸进邻格或避开 Z-fighting)用。"),
                t.OffsetX, -1f, 1f);
            float newOY = EditorGUILayout.Slider(
                new GUIContent("Y:",
                    "Y 方向手动偏移(= 世界 Z 轴)。同上。"),
                t.OffsetY, -1f, 1f);
            if (!Mathf.Approximately(newOX, t.OffsetX) || !Mathf.Approximately(newOY, t.OffsetY))
            {
                Undo.RecordObject(_mapData, "调整 tile 水平偏移");
                t.OffsetX = newOX;
                t.OffsetY = newOY;
                _mapData.Tiles[idx] = t;
                EditorUtility.SetDirty(_mapData);
                _selectedTileForEdit = t;
                // 直接挪该格预览位置
                var key2 = new Vector3Int(t.X, t.Y, t.Z);
                if (_spawnedByCell.TryGetValue(key2, out var go2) && go2 != null)
                {
                    var w = GridToWorld(t.X + 0.5f, t.Y + 0.5f, t.Z);
                    go2.transform.position = new Vector3(w.x + t.OffsetX, w.y + t.HeightOffset, w.z + t.OffsetY);
                }
            }

            if (GUILayout.Button("清除选中", GUILayout.Width(80)))
            {
                _selectedTileForEdit = null;
                _state = BuilderState.Idle;
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
                float offX = 0f, offY = 0f;
                for (int i = 0; i < _mapData.Tiles.Count; i++)
                {
                    var t = _mapData.Tiles[i];
                    if (t.X == key.x && t.Y == key.y && t.Z == key.z)
                    {
                        ho = t.HeightOffset;
                        offX = t.OffsetX;
                        offY = t.OffsetY;
                        break;
                    }
                }
                var w = GridToWorld(key.x + 0.5f, key.y + 0.5f, key.z);
                // 同时保留用户的水平偏移,否则改 layerHeightScale 会清掉 OffsetX/Y
                go.transform.position = new Vector3(w.x + offX, w.y + ho, w.z + offY);
            }
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
                    // 走 Undo 通道销毁所有 preview，再扫描兜底清孤儿
                    foreach (var kv in _spawnedByCell)
                        if (kv.Value != null) Undo.DestroyObjectImmediate(kv.Value);
                    _spawnedByCell.Clear();
                    CleanOrphanedPreviews(-1);
                    _maxZInData = 0;
                    _activeZ = 0;
                    _userReservedMaxZ = 0;
                    _sv?.Repaint();
                }
            }
            // 强制清理孤儿 preview 按钮:不删数据,只清场景里那些"数据已删但实例还残留"的 HandMapTile_*。
            // 是 user 报告"清空层级还有地形"那种顽固情况的兜底工具。
            int orphanCount = _mapData != null ? CountOrphanedPreviews(-1) : 0;
            var orphanLabel = orphanCount > 0
                ? $"🧹 强制清理残留 ({orphanCount})"
                : "🧹 强制清理残留";
            using (new EditorGUI.DisabledScope(orphanCount == 0))
            {
                if (GUILayout.Button(new GUIContent(orphanLabel,
                    "扫描场景里所有 HandMapTile_* GameObject,删除那些 _mapData.Tiles 里已经没有对应 cell 的孤儿。"),
                    GUILayout.Width(160)))
                {
                    CleanOrphanedPreviews(-1);
                    _sv?.Repaint();
                    Debug.Log($"[HandMapBuilder] 强制清理完成: 清理了 {orphanCount} 个孤儿 preview");
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
                    _sv?.Repaint();
                    Repaint();
                    e.Use();
                }
                else
                {
                    // 其他 4 键（Esc/Enter/Space/Delete）的状态切换逻辑抽到了 HandleShortcuts
                    // —— OnGUI 焦点 和 SceneView 焦点 两个事件路径都共用同一份逻辑。
                    if (HandleShortcuts(e)) Repaint();
                }
            }
        }

        /// <summary>4 键共享快捷键逻辑（Esc/Enter/Space/Delete）。
        /// 从 OnGUI 和 HandleSceneEvents 两个事件入口调用,确保 窗口焦点 / Scene 焦点 都能响应。
        /// 返回 true 表示本函数已消费事件（外层可继续 Repaint）。</summary>
        bool HandleShortcuts(Event e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                // Enter = 按当前选区进入"待粘贴"状态（仅在有选区时生效）
                if (_state == BuilderState.BoxSelected && _selectedTileIndices.Count > 0)
                {
                    EnterReadyToCopyState();
                    e.Use();
                    return true;
                }
            }
            else if (e.keyCode == KeyCode.Space)
            {
                // Space = 实贴（仅在 ReadyToCopy 状态下）
                if (_state == BuilderState.ReadyToCopy)
                {
                    PasteAtCurrentHover();
                    e.Use();
                    return true;
                }
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                // Esc = 退出待粘贴 / 清除选区 / 退出 Inspector 编辑模式
                // 同时标记"到下一次 MouseUp 之前吞掉所有鼠标事件"——防止"按住左键 Esc 后继续 MouseDrag 重新进入 BoxSelecting"
                if (_state == BuilderState.ReadyToCopy)
                {
                    ExitCopyPasteState();
                    _suppressMouseUntilUp = true;
                    e.Use();
                    return true;
                }
                else if (_state == BuilderState.BoxSelected || _state == BuilderState.BoxSelecting)
                {
                    ClearSelection();
                    _suppressMouseUntilUp = true;
                    e.Use();
                    return true;
                }
                else if (_state == BuilderState.InspectingTile)
                {
                    // 退出 Inspector 编辑模式 = 清除选中 tile，回到 Idle
                    _selectedTileForEdit = null;
                    _state = BuilderState.Idle;
                    _suppressMouseUntilUp = true;
                    _sv?.Repaint();
                    e.Use();
                    return true;
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
                    return true;
                }
            }
            return false;
        }

        void DrawHelp()
        {
            EditorGUILayout.LabelField("提示", EditorStyles.helpBox);
            EditorGUILayout.HelpBox(
                "• 上方预览 = Scene 视图，Unity 自带视角控制（Alt+左键转、滚轮缩放、F 聚焦）\n" +
                "• 单击 / 拖拽左键 = 笔刷画(任何距离都画,不会变成框选);Ctrl+左键拖 = 框选(进入复制粘贴)\n" +
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

            // Claim ordinary SceneView input for the map editor. Without a default
            // control Unity's built-in object selection wins the mouse-down, so the
            // window often never receives Ctrl+left-drag or erase clicks. Alt-based
            // camera navigation remains handled by SceneView.
            if (Event.current.type == EventType.Layout && !Event.current.alt)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            DrawGridHandles(sv);
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
        /// 处理 SceneView 事件。三种模式:
        ///   模式 1 (Idle 子状态):       鼠标左键单击/拖动 = 笔刷画/擦;Ctrl+左键拖 = 框选;Shift+左键 = 模式 2;右键 = 吸管
        ///   模式 2 (InspectingTile):    鼠标左键/拖动全部 noop(避免误画/误框选);Inspector 调整高度/旋转/平移;右键吸管仍可用
        ///   模式 3 (ReadyToCopy):      鼠标左键单击 = 实贴;Space / Enter 也实贴
        /// Esc 三种模式都退出 → 回 Idle (模式 1)。
        /// <para/>Layout 阶段注册默认控件以接收编辑鼠标事件；Alt 相机操作仍交给 SceneView。
        /// </summary>
        void HandleSceneEvents(SceneView sv)
        {
            var e = Event.current;

            // Keyboard shortcuts must run before the per-mode early returns below.
            // Previously ReadyToCopy and InspectingTile returned first, which meant
            // Esc could never exit those modes while the SceneView had focus.
            if (e.type == EventType.KeyDown && !e.alt && HandleShortcuts(e))
            {
                Repaint();
                sv.Repaint();
                return;
            }

            // 鼠标移动 / 拖拽:更新 hover cell(不消费,SceneView 仍可相机操作)
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

            // ====== 模式 3:ReadyToCopy 实贴 ======
            if (_state == BuilderState.ReadyToCopy)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    PasteAtCurrentHover();
                    e.Use();
                    sv.Repaint();
                }
                // 模式 3 下右键仍走吸管逻辑(下方统一处理)
                HandlePipetteClick(sv, e);
                return; // 模式 3 完全屏蔽模式 1/2 的左键逻辑
            }

            // ====== 模式 2:InspectingTile 编辑选中 tile ======
            // 左键/拖动/松开全部 noop,只更新 hover cell(已在开头做了)。
            // Esc 在快捷键段退回 Idle。
            if (_state == BuilderState.InspectingTile)
            {
                if (e.type == EventType.MouseDown && e.button == 1 && !e.alt)
                {
                    // 模式 2 下右键吸管继续可用,方便从地图上挑 prefab 切到画笔
                    HandlePipetteClick(sv, e);
                }
                return;
            }

            // ====== Esc 抑制窗口:到下一次 MouseUp 之前,吞掉所有鼠标 MouseDown/MouseDrag/MouseMove(右键除外) ======
            // 防止"按住左键 Esc 后,后续 MouseDrag 重新进入 BoxSelecting/PlaceAt"。
            // MouseMove 也要吞,否则 Repaint 会被它不停触发。
            if (_suppressMouseUntilUp && (e.type == EventType.MouseMove
                || e.type == EventType.MouseDrag
                || (e.type == EventType.MouseDown && e.button == 0)))
            {
                e.Use();
                return;
            }
            if (_suppressMouseUntilUp && e.type == EventType.MouseUp && e.button == 0)
            {
                _suppressMouseUntilUp = false; // 用户松开了,恢复正常
                e.Use();
                return;
            }

            // ====== 模式 1:Idle 编辑器地形模式 ======
        // 鼠标左键单击/拖动 = 笔刷画图(任何距离都画,不切换成框选)
        // 鼠标右键单击 = 吸管
        // Shift+左键 = 切模式 2 (InspectingTile)
        // Ctrl+左键 拖动 = 框选(进入复制粘贴的唯一入口)
        // 注:user 原话"不能出现框选框" — 模式 1 必须永远是左键=笔刷;框选只在 Ctrl 按下时出现。

        // 左键按下
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (e.shift)
            {
                // Shift+左键 = 进入模式 2 (InspectingTile)
                PickTileAtHover();
            }
            else if (e.control || e.command)
            {
                // Ctrl/Cmd + 左键 = 启动框选(画/擦都不做)
                if (_hoverCell.x < 0 || _hoverCell.y < 0
                    || _hoverCell.x >= _mapData.Width || _hoverCell.y >= _mapData.Height)
                {
                    // 地图外 → 当作相机操作
                }
                else
                {
                    _boxSelStart = _hoverCell;
                    _boxSelEnd = _hoverCell;
                    _state = BuilderState.BoxSelecting;
                }
            }
            else
            {
                // 普通左键 = 画/擦 1 格
                if (_hoverCell.x >= 0 && _hoverCell.y >= 0
                    && _hoverCell.x < _mapData.Width && _hoverCell.y < _mapData.Height)
                {
                    PaintAtHover();
                    _dragPaintedCells.Add(_hoverCell);
                    _state = BuilderState.Idle;
                }
            }
            e.Use();
            sv.Repaint();
        }

        // 左键拖动
        if (e.type == EventType.MouseDrag && e.button == 0 && !e.alt && !e.shift && !(e.control || e.command))
        {
            // 非 Shift/Ctrl 左键拖动 = 笔刷连续画图(任何距离都画)
            if (_state == BuilderState.BoxSelecting)
            {
                // 极少见:鼠标按下时不是 Ctrl 但拖动起来变成 Ctrl(修饰键变化)
                _boxSelEnd = _hoverCell;
                RecomputeSelectedTileIndices();
                Repaint();
                sv.Repaint();
            }
            else if (_hoverCell.x >= 0 && _hoverCell.y >= 0
                     && _hoverCell.x < _mapData.Width && _hoverCell.y < _mapData.Height)
            {
                PaintAtHover();
                _dragPaintedCells.Add(_hoverCell);
                sv.Repaint();
            }
            e.Use();
        }

        // 左键 Ctrl 拖动 = 框选
        if (e.type == EventType.MouseDrag && e.button == 0 && (e.control || e.command) && !e.alt && !e.shift)
        {
            if (_state == BuilderState.BoxSelecting)
            {
                _boxSelEnd = _hoverCell;
                RecomputeSelectedTileIndices();
                Repaint();
                sv.Repaint();
            }
            else if (_hoverCell.x >= 0)
            {
                // 拖动过程中才按住 Ctrl — 启动框选
                _boxSelStart = _hoverCell;
                _boxSelEnd = _hoverCell;
                _state = BuilderState.BoxSelecting;
                Repaint();
                sv.Repaint();
            }
            e.Use();
        }

        // 左键松开
        if (e.type == EventType.MouseUp && e.button == 0 && !e.alt)
        {
            if (_state == BuilderState.BoxSelecting)
            {
                _state = BuilderState.BoxSelected;
                _boxSelStart = new Vector2Int(-1, -1);
                _boxSelEnd = new Vector2Int(-1, -1);
            }
            // 否则保持 Idle(刚画完)
            _dragPaintedCells.Clear();
            e.Use();
            Repaint();
            sv.Repaint();
        }

        // 右键 = 吸管(MouseDown 时触发;MouseDrag 时自然不会)
        HandlePipetteClick(sv, e);

    }

        /// <summary>吸管:右键单击时,根据当前 _hoverCell 已有 tile 回写 palette/旋转/高度。
        /// 模式 1/2/3 都可触发(由调用方控制)。</summary>
        void HandlePipetteClick(SceneView sv, Event e)
        {
            if (e.type != EventType.MouseDown || e.button != 1 || e.alt) return;
            if (_hoverCell.x < 0 || _hoverCell.y < 0) return;
            if (_hoverCell.x >= _mapData.Width || _hoverCell.y >= _mapData.Height) return;
            if (_mapData == null) return;

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

        /// <summary>彻底退出复制粘贴和框选状态，回到普通绘制模式。</summary>
        void ExitCopyPasteState()
        {
            if (_state == BuilderState.ReadyToCopy)
            {
                _clipboard.Clear();
                _selectedTileIndices.Clear();
                _boxSelStart = new Vector2Int(-1, -1);
                _boxSelEnd = new Vector2Int(-1, -1);
                _state = BuilderState.Idle;
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
                    Undo.DestroyObjectImmediate(go);
                _spawnedByCell.Remove(key);
                _mapData.Tiles.RemoveAt(idx);
            }
            EditorUtility.SetDirty(_mapData);
            Undo.CollapseUndoOperations(undoGroup);

            // 兜底：扫描场景里这些 key 对应的 HandMapTile_* 孤儿清理
            CleanOrphanedPreviews(-1);

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
            // 框选和 Inspector 编辑模式共存时,Esc 一并清掉——简化状态机,避免两个"选中区"叠加。
            if (_selectedTileForEdit.HasValue)
            {
                _selectedTileForEdit = null;
                // _state 已被上面设回 Idle
            }
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
                // 优先用序列化字段 Prefab（避免每次都 AssetDatabase.LoadAssetAtPath）
                var prefab = tile.Prefab;
                if (prefab == null && !string.IsNullOrEmpty(tile.PrefabPath))
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(tile.PrefabPath);
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
            // 同格同 Z 已有则替换：先删数据 + 旧 preview,再加新数据 + 新 preview,
            // 否则会出现"两个 prefab 重叠在同一格"的视觉假象(用户感觉"替换不上")。
            if (_mapData.RemoveTile(cell.x, cell.y, cell.z))
            {
                var keyOld = new Vector3Int(cell.x, cell.y, cell.z);
                if (_spawnedByCell.TryGetValue(keyOld, out var goOld) && goOld != null)
                    Undo.DestroyObjectImmediate(goOld);
                _spawnedByCell.Remove(keyOld);
            }
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
            // Older assets can contain duplicate entries at one cell/Z. Removing
            // only the first entry makes the terrain appear impossible to delete.
            int removedCount = _mapData.Tiles.RemoveAll(t =>
                t.X == cell.x && t.Y == cell.y && t.Z == _activeZ);
            if (removedCount > 0)
            {
                EditorUtility.SetDirty(_mapData);
            }
            var key = new Vector3Int(cell.x, cell.y, _activeZ);
            if (_spawnedByCell.TryGetValue(key, out var go) && go != null)
            {
                Undo.DestroyObjectImmediate(go);
            }
            _spawnedByCell.Remove(key);
            DestroyPreviewObjectsAt(cell.x, cell.y, _activeZ);
            if (removedCount == 0)
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
                    Undo.DestroyObjectImmediate(kv.Value);
                    keysToRemove.Add(kv.Key);
                }
            }
            foreach (var k in keysToRemove) _spawnedByCell.Remove(k);
            CleanOrphanedPreviews(zToRemove);

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
                            Undo.DestroyObjectImmediate(goOld);
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

            // 销毁该层所有预览实例。
            // ⚠️ 必须用 Undo.DestroyObjectImmediate 而不是裸 DestroyImmediate——
            // 这些 instance 在 EnsurePreviewFor 里有 Undo.RegisterCreatedObjectUndo 注册过，
            // 裸 Destroy 会让 Undo 系统的记录与场景状态不一致（极端场景下 Undo 重做会把 instance 复活）。
            // Undo.DestroyObjectImmediate 让 Undo 系统也记录这次销毁，整体一致。
            var keysToRemove = new List<Vector3Int>();
            foreach (var kv in _spawnedByCell)
            {
                if (kv.Key.z == _activeZ && kv.Value != null)
                {
                    Undo.DestroyObjectImmediate(kv.Value);
                    keysToRemove.Add(kv.Key);
                }
            }
            foreach (var k in keysToRemove) _spawnedByCell.Remove(k);

            // 兜底：上一步的 Destroy 基于 _spawnedByCell 字典，但若字典里没记录到这些 instance
            // (域重载/外部脚本/Undo redo 复活等极端情况),会残留孤儿。下面用命名约定扫一遍场景,把任何
            // 不在数据里的 HandMapTile_* 视为孤儿一并清理。这是 user 报告"清空层级还残留地形"的根因保险。
            CleanOrphanedPreviews(_activeZ);
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

            // 销毁所有预览实例（同 ClearLevel：走 Undo 通道避免 Undo redo 复活）
            var keysToRemove = new List<Vector3Int>();
            foreach (var kv in _spawnedByCell)
            {
                if (kv.Value != null)
                {
                    Undo.DestroyObjectImmediate(kv.Value);
                    keysToRemove.Add(kv.Key);
                }
            }
            foreach (var k in keysToRemove) _spawnedByCell.Remove(k);

            // 兜底：扫描场景里所有 HandMapTile_* 孤儿，一并清理
            CleanOrphanedPreviews(-1);
            Undo.CollapseUndoOperations(undoGroup);

            _maxZInData = 0;
            _activeZ = 0;
            _userReservedMaxZ = 0;  // 清空所有数据时同时清空用户保留的层级
            _sv?.Repaint();
            Debug.Log($"[HandMapBuilder] 已清空所有层级: 删除 {removedCount} 个 tile");
        }

        /// <summary>
        /// 扫场景里所有名字为 HandMapTile_X_Y_Z 的 GameObject,如果它对应的 cell 不在 _mapData.Tiles 里
        /// 就视为孤儿删掉。这是"清空层级还残留地形"这种顽固 bug 的兜底修复,跟 _spawnedByCell 字典无关。
        /// <para/>zFilter&lt;0 表示不限层;zFilter&gt;=0 表示只清该层。
        /// </summary>
        void CleanOrphanedPreviews(int zFilter)
        {
            if (_mapData == null) return;
            // 收集当前数据应有的 cell
            var validCells = new HashSet<long>();
            for (int i = 0; i < _mapData.Tiles.Count; i++)
            {
                var t = _mapData.Tiles[i];
                if (zFilter >= 0 && t.Z != zFilter) continue;
                long k = ((long)t.X << 40) | ((long)t.Y << 20) | (uint)t.Z;
                validCells.Add(k);
            }

            // 用 SceneView 的 root 或 active scene 找所有 HandMapTile_* (它们不在层级中,只在 SceneView 里出现——
            // 但 Unity 实例化的 GameObject 会在当前场景的 hierarchy 里)
            var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
            int cleaned = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                var go = candidates[i];
                if (go == null) continue;
                if (!go.name.StartsWith("HandMapTile_")) continue;
                // 跳过 asset (如 prefab asset 自身) —— 只清场景中实例
                if (EditorUtility.IsPersistent(go)) continue;
                // 解析名字尾段 'HandMapTile_X_Y_Z{n}'
                int zStart = go.name.LastIndexOf('_');
                if (zStart < 0) continue;
                if (!int.TryParse(go.name.Substring(zStart + 2), out int z)) continue;  // 跳 'Z'
                int yStart = go.name.LastIndexOf('_', zStart - 1);
                if (yStart < 0) continue;
                if (!int.TryParse(go.name.Substring(yStart + 1, zStart - yStart - 1), out int y)) continue;
                int xStart = "HandMapTile_".Length;
                int xEnd = go.name.IndexOf('_', xStart);
                if (xEnd < 0) continue;
                if (!int.TryParse(go.name.Substring(xStart, xEnd - xStart), out int x)) continue;

                if (zFilter >= 0 && z != zFilter) continue;
                long k = ((long)x << 40) | ((long)y << 20) | (uint)z;
                if (validCells.Contains(k)) continue;  // 数据里有,合法

                Undo.DestroyObjectImmediate(go);
                cleaned++;
            }
            if (cleaned > 0)
                Debug.Log($"[HandMapBuilder] 兜底清理: 删除 {cleaned} 个孤儿 preview (zFilter={zFilter})");
        }

        void EnsurePreviewFor(Vector3Int cell, GameObject prefab, bool registerUndo = true)
        {
            var key = new Vector3Int(cell.x, cell.y, cell.z);
            if (_spawnedByCell.TryGetValue(key, out var existing) && existing != null)
            {
                DestroyImmediate(existing);
                _spawnedByCell.Remove(key);
            }
            // 从数据源读取该格的 rotation / height offset / 水平偏移（而不是全局当前值）
            // 因为 FillLevel 后需要重建预览、要忠实反映实际数据
            float rotY = _currentRotationY;
            float hOff = _currentHeightOffset;
            float offX = 0f, offY = 0f;
            var t = _mapData?.FindTile(cell.x, cell.y, cell.z);
            if (t != null)
            {
                rotY = t.Value.RotationY;
                hOff = t.Value.HeightOffset;
                offX = t.Value.OffsetX;
                offY = t.Value.OffsetY;
            }

            Vector3 world = GridToWorld(cell.x + 0.5f, cell.y + 0.5f, cell.z * 1f);
            // 应用 tile 上的水平偏移:OffsetX 走世界 X,OffsetY 走世界 Z(因为网格 y 映射到世界 z 轴)
            world.x += offX;
            world.z += offY;
            world.y += hOff;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) return;
            instance.transform.position = world;
            instance.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            instance.transform.localScale = Vector3.one * 0.5f;
            instance.name = $"HandMapTile_{cell.x}_{cell.y}_Z{cell.z}";
            // Preview objects are editor-only transient state. HideAndDontSave also
            // protects the scene if Unity closes/reloads the window unexpectedly.
            instance.hideFlags = HideFlags.HideAndDontSave;
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

        /// <summary>关闭工具时清理所有地图预览，包括字典因域重载而遗失的对象。</summary>
        void ClearAllPreviewObjects()
        {
            ClearSpawnedPreview();
            var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < candidates.Length; i++)
            {
                var go = candidates[i];
                if (go == null || EditorUtility.IsPersistent(go)) continue;
                if (!go.name.StartsWith("HandMapTile_")) continue;
                DestroyImmediate(go);
            }
        }

        /// <summary>删除指定格/Z 的全部预览，兼容旧版本留下的重复或孤儿实例。</summary>
        void DestroyPreviewObjectsAt(int x, int y, int z)
        {
            string expectedName = $"HandMapTile_{x}_{y}_Z{z}";
            var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < candidates.Length; i++)
            {
                var go = candidates[i];
                if (go == null || EditorUtility.IsPersistent(go)) continue;
                if (go.name != expectedName) continue;
                Undo.DestroyObjectImmediate(go);
            }
        }

        /// <summary>统计"孤儿 preview"数量:场景里有 HandMapTile_*,但 _mapData.Tiles 里没有对应 cell。
        /// 用于按钮文字显示,以及手动触发 CleanOrphanedPreviews 前让用户有数。</summary>
        int CountOrphanedPreviews(int zFilter)
        {
            if (_mapData == null) return 0;
            var validCells = new HashSet<long>();
            for (int i = 0; i < _mapData.Tiles.Count; i++)
            {
                var t = _mapData.Tiles[i];
                if (zFilter >= 0 && t.Z != zFilter) continue;
                long k = ((long)t.X << 40) | ((long)t.Y << 20) | (uint)t.Z;
                validCells.Add(k);
            }
            var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
            int count = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                var go = candidates[i];
                if (go == null) continue;
                if (!go.name.StartsWith("HandMapTile_")) continue;
                if (EditorUtility.IsPersistent(go)) continue;
                int zStart = go.name.LastIndexOf('_');
                if (zStart < 0) continue;
                if (!int.TryParse(go.name.Substring(zStart + 2), out int z)) continue;
                int yStart = go.name.LastIndexOf('_', zStart - 1);
                if (yStart < 0) continue;
                if (!int.TryParse(go.name.Substring(yStart + 1, zStart - yStart - 1), out int y)) continue;
                int xStart = "HandMapTile_".Length;
                int xEnd = go.name.IndexOf('_', xStart);
                if (xEnd < 0) continue;
                if (!int.TryParse(go.name.Substring(xStart, xEnd - xStart), out int x)) continue;

                if (zFilter >= 0 && z != zFilter) continue;
                long k = ((long)x << 40) | ((long)y << 20) | (uint)z;
                if (validCells.Contains(k)) continue;
                count++;
            }
            return count;
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
            // 用激活层所在 Y 平面试射线,跟 GridToWorld(x, y, z) = (x, z*scale, y) 保持一致。
            // 之前写 (-z) 当成 plane point 错了一个方向:Unity Plane(normal, point) 等价于 normal·X + dot(normal, point) = 0;
            // 所以想要 plane = "y = z*scale",需要 point = (0, z*scale, 0) (不是 -)。
            // 当 _layerHeightScale = 1 时 Z=1 平面在 y=+1,旧代码把 plane 设在 y=-1 → 鼠标射线穿过的是错的 Y,
            // 导致 Z>=1 时定位偏移 1~2 格 (在 LayerHeightScale 不同的情况下偏差更大)。
            Plane plane = new Plane(Vector3.up, new Vector3(0f, z * _layerHeightScale, 0f));
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
                // 选中后进入 InspectingTile 状态：避免左键拖动时误触 PlaceAt/框选
                // Esc 退回 Idle（见 HandleSceneEvents + 快捷键段）
                _state = BuilderState.InspectingTile;
                Repaint();
                Debug.Log($"[HandMapBuilder] 选中 tile: ({tile.Value.X}, {tile.Value.Y}, Z={tile.Value.Z}) " +
                          $"hOff={tile.Value.HeightOffset:F2}");
            }
        }
    }
}
