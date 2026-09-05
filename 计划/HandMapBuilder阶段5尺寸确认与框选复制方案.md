# HandMapBuilder 阶段 5 计划：尺寸确认 + 框选复制

> 状态：📝 **计划阶段**，等用户确认后实施

---

## 一、用户两个反馈的真因分析

### 问题 1：修改地图大小实时触发卡顿

**当前代码**（`HandMapBuilderWindow.cs` line 157-172）：

```csharp
void DrawGridSizeField()
{
    if (_mapData == null) return;
    EditorGUI.BeginChangeCheck();
    int w = EditorGUILayout.IntField("宽 (Width)", _mapData.Width);
    int h = EditorGUILayout.IntField("高 (Height)", _mapData.Height);
    if (EditorGUI.EndChangeCheck())   // ← 每次按键/退格都触发
    {
        Undo.RecordObject(_mapData, "调整地图尺寸");
        _mapData.Width = Mathf.Max(1, w);
        _mapData.Height = Mathf.Max(1, h);
        EditorUtility.SetDirty(_mapData);
        ClearSpawnedPreview();
        RebuildAllPreviews();          // ← 50x50 = 2500 个 prefab 销毁+重建
    }
}
```

**真因**：
- `EditorGUI.EndChangeCheck()` 把"输入 100"这个动作拆成 **3 次事件**（输完 1、输完 10、输完 100）
- 每次都 `DestroyImmediate` 全部 prefab 实例 + `InstantiatePrefab` 新一组
- 50×50 地图 = 2500 个 prefab 实例化，至少 **300ms × 3 = 900ms 卡死**
- 这就是你说的"一直会加载"

**正确做法**：把"输入"与"应用"分离。本地缓存 `_pendingWidth/_pendingHeight`，加 **「应用尺寸 (↩)」 按钮**，按下才修改。

### 问题 2：框选 + 复制粘贴完全缺失

**当前代码**：grep 整个文件，**没有任何** `Clipboard` / `Selection` / `Drag` / `Paste` 相关代码。

```bash
$ grep -n "Clipboard\|Selection\|Copy\|Paste" HandMapBuilderWindow.cs
（无输出）
```

只能"整层清空"或"单格画"，完全没有区域操作能力。

---

## 二、阶段 5 功能目标

### F1：地图尺寸改为"输入 + 确认"
- 拆为 **缓存字段** `_pendingWidth / _pendingHeight`，不直接写
- 加 **「应用尺寸 (↩)」** 按钮
- 加 **「恢复当前值」** 重置按钮
- 显示当前 pending vs 已应用差异
- 保留范围限制（1~200）

### F2：框选（Box Selection）
- **左键 + 拖拽** 在地图空白处（不影响画 tile）
- 框选范围 = `[min(startCell, endCell), max(startCell, endCell)]`
- 视觉：**半透明蓝色填充 + 白色描边**（沿激活层 Z 平面）
- 显示"已选 N 个 tile"
- 与现有功能不冲突：
  - 单击（左键按下不拖动）= 当前逻辑（画/擦）
  - Shift+左键 = 当前逻辑（Inspector 选中）
  - **识别为"拖拽框选"的判定**：按下→移动 ≥2 格 → 松开

### F3：复制 / 粘贴
- **Ctrl+C / Cmd+C**：复制当前框选内的所有 tile（不含层级跨 Z 还是按 Z 切分）
- **Ctrl+V / Cmd+V**：从当前 hover cell 起粘贴（按 Z 切分，原 Z 的 tile 放到目标 Z=激活层）
- **Ctrl+X / Cmd+X**：剪切（复制 + 立刻删除原 tile）
- **Escape**：清除当前框选
- **Home**：取消粘贴后的偏移，跳到原始 hover cell
- 剪贴板格式：JSON 序列化的 `List<HandPlacedTile>`（含 X/Y/RotationY/HeightOffset/PrefabPath）
- 撤销集成：每步粘贴/剪切单独 Undo

### F4：边界处理 + 越界裁剪
- 复制内容如果超出地图，自动裁剪（不让越界 tile 落到 map 外）
- 复制内容如果在粘贴时跨过 map 边缘，弹 Dialog 确认
- 跨地图高度范围（`maxZInData`）粘贴时：所有 tile 都放到激活层 `_activeZ`（简化 UX）

---

## 三、UX 决策点（需用户拍板）

### 决策 F1：框选激活条件
**A**：左键按下+拖动 ≥2 格 = 框选；单击不动 = 画 tile
**B**：需要按住 **Shift 键 + 拖动** = 框选（不与画方块混淆）
**C**：增加一个 `选择模式` 按钮，切换到"框选"模式才生效

**推荐 A**（最自然的桌面编辑器习惯，符合 ProBuilder / Tile Palette 的通用约定）。

### 决策 F2：复制粘贴的层级策略
**A**：跨层级（所有 tile 都贴到当前激活 Z），简单
**B**：保持原 Z（复 Z=1 的 tile 仍到 Z=1），复杂但更准
**C**：弹 Dialog 让用户每次选

**推荐 A**（用户已经会切换激活层，复 Z=1 的东西时先切到 Z=1 再粘贴，符合直觉）。

### 决策 F3：复制时是否保留旋转/高度
**A**：保留（用户原意就是重复这一组配置）
**B**：默认都置 0，给"粘贴默认对齐"按钮，让用户事后批量调整

**推荐 A**（用户场景：复制一排栅栏，旋转+高度都要保持原样）。

### 决策 F4：地图尺寸超出存档保护
- 改宽 50→200 = 多 35000 个格子可用，但当前 tile 不动
- 改窄 200→50 = **可能裁掉现有 tile**
  - **A**：直接裁剪（保留范围内 + 弹警告）
  - **B**：阻止改窄 + 红色提示"会裁剪 X 个 tile"

**推荐 B**（保护用户数据）。

---

## 四、技术实现要点

### 文件改动
- `HandMapBuilderWindow.cs`（+约 250 行）

### 新增字段
```csharp
// 地图尺寸 pending
int _pendingWidth, _pendingHeight;
bool _gridSizeDirty => _pendingWidth != _mapData.Width || _pendingHeight != _mapData.Height;

// 框选状态
Vector2Int _boxSelectionStart = new Vector2Int(-1, -1);
Vector2Int _boxSelectionEnd = new Vector2Int(-1, -1);
bool _isBoxSelecting = false;
List<int> _selectedTileIndices = new List<int>();  // 指向 _mapData.Tiles 的索引

// 剪贴板
List<HandPlacedTile> _clipboard;
string _clipboardPreview;  // UI 显示用
```

### 新增方法
- `DrawGridSizeField`（**重写**） —— 加 `_pendingWidth/_pendingHeight` 缓存 + 「应用尺寸」按钮
- `ApplyGridSize()` —— 弹确认 + 真改 `_mapData.Width/Height`
- `OnSceneGUI` 加 `LateUpdate` 鼠标事件：检测拖拽范围
- `DetectBoxSelectionDrag()` —— 区分"画"和"框选"
- `DrawBoxSelectionVisual()` —— Scene 视图画蓝色半透矩形 + 白色描边
- `CopySelection()` / `PasteSelection()` / `CutSelection()` / `ClearSelection()` —— Ctrl+C/V/X/Esc
- `DrawBoxSelectionToolbar()` —— 工具窗口顶部加 4 个按钮 + 显示已选数量 + 粘贴预览
- `IsBoxSelectionActive` / `GetSelectedTiles()` / `GetBoxSelectionRect()` —— 辅助方法

### 关键交互改造

**OnSceneGUI 事件流升级**：

```
鼠标在 SceneView:
├─ MouseDown(左键, !e.alt)
│   ├─ e.shift → PickTileAtHover (不变)
│   ├─ 否则: 记录 _boxSelectionStart = hoverCell，初始化 _dragPaintedCells
│   └─ _isBoxSelecting = true (待检测)
├─ MouseDrag(左键, !e.alt, !e.shift)
│   ├─ 检查 dist(_boxSelectionStart, hoverCell) ≥2 → 进入框选模式
│   │   ├─ _isBoxSelecting = true
│   │   ├─ _boxSelectionEnd = hoverCell
│   │   ├─ 更新 _selectedTileIndices
│   │   └─ 不画 tile（避免混淆）
│   └─ 否则: 保持原"拖拽画笔刷"行为
├─ MouseUp(左键)
│   ├─ 若 _isBoxSelecting：保留框选状态，等 Ctrl+C / Escape
│   └─ 否则：清空 _dragPaintedCells
└─ MouseMove: 更新 hoverCell (不变)
```

**快捷键系统扩展**（已在 `KeyCode` 块，加 4 行）：

```csharp
if (e.control || e.command)
{
    if (e.keyCode == KeyCode.C) { CopySelection(); e.Use(); }
    if (e.keyCode == KeyCode.V) { PasteSelection(); e.Use(); }
    if (e.keyCode == KeyCode.X) { CutSelection(); e.Use(); }
}
if (e.keyCode == KeyCode.Escape) { ClearSelection(); e.Use(); }
if (e.keyCode == KeyCode.Delete) { DeleteSelection(); e.Use(); }  // 顺手做：不用复制+删除也能删
```

### 视觉风格

**框选矩形**（激活层平面）：
- 填充：`new Color(0.3f, 0.7f, 1f, 0.25f)`（半透蓝）
- 描边：`new Color(0.7f, 0.95f, 1f, 0.9f)`（亮白蓝）
- 高度：`Y = activeZ * _layerHeightScale + 0.05f`（轻微抬升避免 Z-fighting）
- 4 角各加 5×5 的小角块强化"角点"

**选中 tile 高亮**（已选中的 tile）：
- 在原 prefab 顶部画一圈白色 wire cube（不破坏 prefab）
- 与"hover ghost"区分：hover 是半透选中色，selection 是白色描边

---

## 五、状态机汇总

```
工具状态机:
  Idle ──(左键单击)── Paint         (单格画)
       ──(左键拖拽)── DragPaint     (连续画笔刷)
       ──(Shift+左键)─ Select       (Inspector 选中)
       ──(右键单击)── Eyedropper    (吸管)

新增:
  Idle ──(左键拖拽 ≥2 格)── BoxSelecting ─(松开)── BoxSelected
  BoxSelected ──(Ctrl+C)── Copy ┌── BoxSelected (黏贴板有内容)
              ──(Ctrl+V)── Paste ─ 替换/合并到当前 hover cell
              ──(Ctrl+X)── Cut
              ──(Delete)── Delete
              ──(Escape)── Idle
              ──(左键拖拽再)── BoxSelected (覆盖)
```

---

## 六、验收清单

### F1 尺寸确认
- [ ] 输入 "100" 时**只在最终值敲完才触发应用**，敲的过程中无 RebuildAll
- [ ] 「应用尺寸」按钮在 pending == 当前时禁用，节省误触
- [ ] 缩小范围弹警告 + 显示即将裁剪的 tile 数
- [ ] 撤销记录改名「应用地图尺寸 50→100」

### F2 框选
- [ ] 拖拽 ≤ 1 格 = 画（不识别为框选）
- [ ] 拖拽 ≥ 2 格 = 框选（**取消画 tile**，避免误操作）
- [ ] Scene 视图显示蓝色半透矩形 + 白色描边
- [ ] 工具窗口状态栏显示"已选 N 个"
- [ ] Escape 清空框选

### F3 复制 / 剪切 / 粘贴
- [ ] Ctrl+C 后 `Debug.Log` 显示复制的 tile 数 + 各自 Z
- [ ] 粘贴到不同 Z = 全部放到 `_activeZ`（按决策 F2-A）
- [ ] 粘出 map 边界自动裁剪 + Console 警告
- [ ] 一次撤销能整批撤销粘贴操作
- [ ] 复制内容含 RotationY/HeightOffset，按 F3-A 保留

### F4 性能
- [ ] 50×50 地图整图框选 + Ctrl+C + Ctrl+V = 至少 60fps 操作不卡
- [ ] 选区大的情况，保留 tile 索引列表（而不是每次循环扫）

---

## 七、实施步骤

1. **重写 `DrawGridSizeField`** —— 加 pending 字段 + 「应用尺寸」按钮（基础骨架）
2. **加 OnSceneGUI 框选事件检测** —— 拖拽距离判定为"画"还是"框选"
3. **加 `DrawBoxSelectionVisual`** —— 蓝色半透矩形 + 白色描边
4. **加 `_selectedTileIndices` 计算** —— 根据框选范围扫 `_mapData.Tiles`
5. **加快捷键 Ctrl+C/V/X + Escape/Delete** + **4 个 UI 按钮**
6. **加复制 / 粘贴 / 剪切 / 删除方法** —— 完整数据流 + Undo
7. **加越界裁剪 + Console 警告**
8. **验收清单跑一遍**

预计代码量：**+约 280 行**，主要新增 `BoxSelection` 类状态机和 4 个核心方法。

---

## ⚠️ 等用户决策

请回复以下 4 点，决策后开始开发：

1. **F1 框选激活条件**：A（拖拽 ≥2 格自动）/ B（必须 Shift）/ C（模式按钮）
2. **F2 层级策略**：A（都贴到 activeZ）/ B（保持原 Z）/ C（Dialog 选）
3. **F3 复制保留旋转高度**：A（保留）/ B（默认归 0）
4. **F4 缩小地图保护**：A（直接裁剪）/ B（阻止 + 警告）

---

## 八、UX 升级（P1 - 用户最新反馈）：复制按钮 → 待粘贴状态 → Ghost Preview → 单击落点

> **背景**：用户最新反馈 "复制的时候应该增加一个按钮，然后进入复制状态"
> **替代方案**：不再用 Ctrl+C/V 快速键模式，改用 **多步按钮交互**（接近 ProBuilder / DCC 软件的 drag-to-place 流程）

### 8.1 三态交互设计

```
[状态 1] Idle / BoxSelected
  ↓ 用户拖框选取内容
[状态 2] 待复制状态 (Ready-to-Copy)    ← 按"复制"按钮 或 拖框选后自动进入
  ↓ 鼠标 hover 时显示 ghost preview（半透蓝色）
  ↓ 鼠标 hover 在地图上 → ghost 跟着走
[状态 3] 待粘贴状态 (Ready-to-Paste)   ← 点击落点 或 "粘贴"按钮
  ↓ 单击 = 实贴
[状态 4] 粘贴完成（保留选区可继续粘贴）
  ↓ Escape / "退出"按钮 → 状态 1
```

### 8.2 UI 改造

**工具窗口新增"复制 / 粘贴"工具栏区**（放在 `DrawBoxSelectionToolbar` 之后）：

```csharp
// 状态展示 + 操作按钮
GUILayout.BeginHorizontal();

if (GUILayout.Button("复制 (⏎)", GUILayout.Width(80))) {
    if (IsBoxSelectionActive) EnterReadyToCopyState();
}

using (new EditorGUI.DisabledScope(_clipboard == null || _clipboard.Count == 0))
{
    if (GUILayout.Button("粘贴 (Space)", GUILayout.Width(80))) {
        PasteAtCursor();  // 用当前 hover cell 落点
    }
}

if (GUILayout.Button("✕ 退出 (Esc)", GUILayout.Width(80))) {
    ExitCopyPasteState();
}

GUILayout.EndHorizontal();

// 待粘贴状态指示
if (_state == HandMapBuilderState.ReadyToPaste) {
    EditorGUILayout.HelpBox(
        $"📋 待粘贴: {_clipboard.Count} 个 tile, 落点 ({_hoverCell.x},{_hoverCell.y}) Z={_activeZ}\n" +
        "• 单击 = 实贴\n" +
        "• 拖动 = 修改落点\n" +
        "• Space = 实贴 / Enter = 实贴 / Esc = 退出", MessageType.Info);
}
```

### 8.3 状态机 v2（替代原快捷键设计）

```
Idle ─(框选)─► BoxSelected ─(点"复制" 或 自动触发)─► ReadyToCopy
                                                       │
                                          hover: 显示 ghost 预览
                                          │
                                          单击左键 = 实贴
                                          Space / Enter = 实贴
                                          Esc = 退出
                                          拖动 = 修改落点
                                          ▼
                                       ReadyToPaste (粘贴后保留)
                                          │
                                          Esc / "退出"按钮
                                          ▼
                                          Idle
```

### 8.4 视觉：Ghost Preview（待粘贴预览）

当处于 `ReadyToPaste` 状态时，在鼠标 hover cell 位置绘制 **"将放置的内容预览"**：

```
┌─────────────────────┐
│  已选 tile 的轮廓图   │ ← 半透明蓝色 (0.3, 0.7, 1.0, 0.3)
│  （per-cell wire box）│    描边白色 (1, 1, 1, 0.8)
│                     │
│  整体外接框 (handles)│ ← 亮黄色 wire box 标识"组边界"
└─────────────────────┘
```

- **每个被复制 tile** 在目标位置显示一个**半透蓝色 wire cube**（不带 prefab，只画轮廓）
- **整体外接框**：最外层一个**亮黄 wire cube** 圈出整个复制内容外接矩形
- **坐标系偏移**：复制内容相对位置 = 复制组内坐标，原点 = 复制组最小 X/Y 角
- **落点**：以 `_hoverCell` 为复制组"原点"（=原复制最小 X/Y 角目标落点）

### 8.5 关键交互改造（不再依赖 Ctrl+C/V）

| 操作 | 旧版本（快捷键） | 新版本（按钮 + 多步状态） |
|------|----------------|------------------------|
| 进入复制 | Ctrl+C | 点 **`复制`** 按钮 |
| 进入粘贴 | Ctrl+V | 自动（进入待粘贴状态后） |
| 实贴 | (Ctrl+V 立即) | 点 **`粘贴`** 按钮 / Space / Enter |
| 预览落点 | (无) | **Ghost preview 跟鼠标走** ← 关键差异 |
| 切激活层 | 手动重 Ctrl+V | 切激活层 → ghost 重画在新 Z |
| 取消复制 | (无) | Esc / **`退出`** 按钮 |

**快捷键保留**（作为可选加速）：
- **Space / Enter** = 实贴（待粘贴状态下）
- **Esc** = 退出待粘贴状态 + 清除选区
- **Delete / X** = 删除当前选区（仍是 `ReadyToCopy` 之前的快速删除）

### 8.6 多步状态带来的额外好处

1. **复制前能改 Z**：进入待粘贴前，用户可以先切换到目标 Z 再点"复制"，省去"切 Z + Ctrl+V"两步
2. **预览对齐**：ghost preview 让用户**先看到**整个复制块的形状再决定落点（适合复杂形状复制）
3. **跨层复制明确**：复制前切 Z = 显式告诉系统"我要贴到这层"，比 Ctrl+V 后扫盲更清晰
4. **可放弃**：发现落点不对可以 Esc 取消，不会留下半个复制块

### 8.7 验收（F1-F4 + 升级后的额外项）

补充验收项：

- [ ] 点 **`复制`** 按钮后，UI 显示"📋 待粘贴: N 个 tile"
- [ ] 待粘贴状态下，鼠标 hover 不同位置，ghost 跟随
- [ ] 待粘贴状态下，**切激活 Z** 后 ghost 在新层显示
- [ ] **Space / Enter / 单击** 任一都能实贴
- [ ] **Esc / 退出按钮** 退出后回到 Idle，下次进入不会留下残留
- [ ] 旋转 / 高度（按决策 F3-A）保留：ghost 显示正确的旋转
- [ ] 复制前切 Z，下个 Z 的 ghost 位置不重叠（避免误盖）
- [ ] 退出后 state = Idle，鼠标单击仍然是画 tile（不进入重复粘贴）

### 8.8 实施步骤调整

原 7 步 → 新 9 步（多了状态机和预览绘制）：

1. F1 尺寸确认（重写 `DrawGridSizeField`）
2. F2 框选（OnSceneGUI 拖拽事件 + 视觉）
3. F3 **状态机枚举** `HandMapBuilderState {Idle, BoxSelecting, BoxSelected, ReadyToCopy, ReadyToPaste}`
4. F4 **`EnterReadyToCopyState`** / **`ExitCopyPasteState`**（新方法）
5. F5 **复制方法**：`CopySelectionToClipboard()` —— 框选 → 剪贴板
6. F6 **`DrawBoxSelectionToolbar`** + **`复制/粘贴/退出`** 3 个按钮 + 状态 HelpBox
7. F7 **`DrawPasteGhostPreview`** —— Scene 视图画 ghost（半透蓝 wire cube + 外接亮黄 box）
8. F8 **`PasteAtCursor`** + 越界裁剪 + Undo 集成
9. F9 验收清单

### 8.9 代码量预估变化

| 部分 | 行数 |
|------|------|
| 原 Ctrl+C/V 快捷键 | -15 |
| 新状态机枚举 + 字段 + 切换 | +35 |
| `DrawBoxSelectionToolbar` 升级（多按钮 + HelpBox） | +40 |
| `DrawPasteGhostPreview`（Scene 视图绘制） | +50 |
| `EnterReadyToCopyState` / `ExitCopyPasteState` | +30 |
| `PasteAtCursor`（带 ghost 落点） | +30 |
| **净增** | **+170 行** |
| **总代码量** | **~+280 → ~+450 行** |

### 8.10 用户决策（v2 新增）

5. **F5 复制进入方式**：
   - **A**：必须按 **`复制`** 按钮才能进入待粘贴（推荐，符合用户最新反馈）
   - **B**：拖框选后自动进入待粘贴（更便捷，但没法"看一眼再决定"）
   - **C**：两者都支持（推荐 A 为默认，B 拖框选完成后单击"复制"按钮）

6. **F6 实贴触发条件**（一旦在待粘贴态）：
   - **A**：单击 = 实贴（与画 tile 冲突，按钮粘也行）
   - **B**：必须 **`粘贴`** 按钮 / Space / Enter（避免误单击）
   - **C**：单击 = 实贴 + 拖动 = 改落点（用户无意识 click=贴，有意识 click+drag=预览）

7. **F7 复制组高度策略**：
   - **A**：所有 Z 都贴到 activeZ（v1 的 A）
   - **B**：保持原 Z 相对值（activeZ = 原最低 Z 时正好对齐）
   - **C**：弹 Dialog 显示"原组跨 X 层，贴到 activeZ=Y 层？"

### 8.11 决策汇总（等你回复 v1 + v2 两批）

**v1（原有）**：
1. F1 框选激活条件
2. F2 层级策略（A/B/C）
3. F3 复制保留旋转高度
4. F4 缩小地图保护

**v2（新增，因为你的最新反馈）**：
5. F5 复制进入方式
6. F6 实贴触发条件
7. F7 复制组高度策略

合计 **7 个决策**，确认后我开始编码。
