# HandMapBuilder 阶段 3：填充 + 拖拽绘制方案

## 1. 背景

阶段 2 已交付：分类标签页 + 调色板翻页 + 手动层级（上限 10）+ 多层网格 + 吸管。
但放置效率太低 —— 摆一张 20×15 的草地地图要点击 300 次。

新需求（用户提的）：
1. **一键填充整个层级** —— 不用一个一个点
2. **拖拽绘制** —— 鼠标按住拖动连续画线/画区域
3. （衍生）**笔刷形状** —— 1×1 / 3×3 / 5×5，控制单次拖拽的覆盖范围

## 2. 用户场景

设计师做一张"草地 + 几棵树 + 一条河 + 一个营地"的地图：

| 步骤 | 当前（阶段2） | 改进后（阶段3） |
| --- | --- | --- |
| 画满 20×15 草地 | 300 次点击 | 1 次「填充整个层级」 |
| 画一条横向河流 | 20 次点击 | 1 次拖拽（笔刷 1×1 横拖） |
| 画一条宽阔沙带 | ~60 次点击 | 1 次拖拽（笔刷 3×3 横拖） |
| 摆放 5 棵树 | 5 次点击 | 5 次点击（树没法批量摆，每棵位置不同） |

填充工具主要解决**重复大块地形**；**单点精确放置**（建筑、道具、桥等）保留点击。

## 3. 设计

### 3.1 绘制模式

**两种模式**：
- **拖拽模式**（默认）— 鼠标按住连续绘制，每帧沿路径画一个格子
- **单击模式** — 鼠标点一下只画一个格子（保留精确点选）

模式切换：`Tools → Drawing Mode → Click/Drag`，快捷键 `D`。

### 3.2 笔刷形状

```
笔刷大小 = 半径 r（中心格 + 周围一圈）

r=1 (1×1):         r=1 (3×3 圆刷):       r=2 (5×5 圆刷):
   □                   □ □ □                  □ □ □ □ □
                       □ □ □                □ □ □ □ □
                                                  □ □ □ □ □
                                            □ □ □ □ □
```

实现：对每个 (dx, dy) ∈ [-r, r]²，若 `dx*dx + dy*dy ≤ r*r` 就画 → 圆形笔刷。

| r | 笔刷格子数 | 形状 |
| - | --- | --- |
| 0 | 1 | 单格 |
| 1 | 5 | 十字（中心 + 上下左右） |
| 2 | 13 | 圆（去掉四角外的中空） |

**快捷键**：`B` 在 0→1→2→0 循环。

### 3.3 一键填充层级

**两种填充**：

| 行为 | 「填充整个层级」 | 「覆盖填充」 |
| --- | --- | --- |
| 空格 | 填当前 prefab | 填当前 prefab |
| 已有 prefab 的格子 | **保留**（跳过） | **替换**为当前 prefab |
| 用途 | 大块铺底色（保留特殊地形） | 整层重铺 |

**清空整个层级**：删除本 Z 层所有 tile + 预览实例（其他 Z 不动）。

快捷键：
- `F` = 填充整个层级（保留已有）
- `Shift+F` = 覆盖填充
- `Ctrl+Shift+F` = 清空整个层级

### 3.4 拖拽绘制逻辑

避免性能问题（每帧 PlaceAt 会卡顿 + Undo 噪声）：

```csharp
HashSet<Vector2Int> _dragPaintedCells;  // 本次拖拽已画过的格子

void HandleSceneEvents(sv):
    if MouseDown:
        _dragPaintedCells.Clear()
        PaintAt(_hoverCell)               // 起点立即画
    if MouseDrag && _dragMode:
        PaintAt(_hoverCell)               // 当前 hover 格
    if MouseUp:
        _dragPaintedCells.Clear()

void PaintAt(cell):
    foreach (dx, dy) in BrushShape(_brushSize):
        var c = cell + (dx, dy)
        if !_dragPaintedCells.Add(c): continue  // 已画过则跳过
        if c 在地图外: continue
        PlaceAt(c, currentPrefabPath)           // PlaceAt 内已包含 Undo.RecordObject
```

**关键**：`_dragPaintedCells` 用 HashSet，O(1) 去重，每帧只 PlaceAt 未画过的格子。

### 3.5 填充预览

「填充整个层级」按钮按下时不要立刻执行 —— 先**预览**要填的所有格子（绿色线框高亮），再次点击确认执行；点其他位置取消。

或者更简单：直接执行（Undo 可恢复）。**先做直接执行 + Undo**，预览作为可选优化留到阶段 4。

## 4. 数据模型

无改动。`HandPlacedTile` 已有 X/Y/Z/PrefabPath，填充就是把一组 `(x, y, z)` 全部加上同一个 tile 即可。

## 5. UI 布局

工具栏新增一栏（位于「层级控件」和「擦除模式」之间）：

```
┌─────────────────────────────────────────────────────────────┐
│ 绘制: ◉ 拖拽  ○ 单击                                          │
│ 笔刷: ◉ 1×1  ○ 3×3  ○ 5×5                                    │
│ 层级操作: [填充整个层级]  [覆盖填充]  [清空整个层级]            │
└─────────────────────────────────────────────────────────────┘
```

## 6. 实施步骤

1. **笔刷形状工具**（15 min）
   - `BrushSize` 枚举 + `EnumerateBrushCells(int cx, int cy, BrushSize bs)` 迭代器
2. **绘制模式 + 笔刷状态**（30 min）
   - `_dragMode: bool`、`_brushSize: BrushSize`
   - `_dragPaintedCells: HashSet<Vector2Int>` 拖拽去重
   - `HandleSceneEvents` 重写为 MouseDown/Drag/Up 三段
3. **填充层级方法**（30 min）
   - `FillLevel(bool overwrite)`：遍历 width×height，对每个格子根据 overwrite 决定是否放 prefab
   - `ClearLevel()`：删除本 Z 所有 tile + preview
   - 都用 `Undo.RecordObject` 包裹保证可撤销
4. **UI 控件 + 快捷键**（20 min）
   - `DrawDrawControls()` 新方法
   - `D` / `B` / `F` / `Shift+F` / `Ctrl+Shift+F` 快捷键
5. **验证**（10 min）
   - 拖拽不卡、Ctrl+Z 可撤销填充、笔刷 3×3 圆刷正确

## 7. 兼容性

- **旧 .asset** 兼容：`HandPlacedTile` 字段未改
- **旧 HandMapBuilderWindow 实例**：旧窗口会丢字段，强制重开

## 8. 验收

1. 默认拖拽模式：鼠标按住拖动 → 沿途格子都填上 prefab
2. 切换单击模式（D 键）→ 拖拽不再绘制，只点一下画一个
3. 笔刷 3×3（B 键循环）：拖拽画一个 5 格粗十字
4. 笔刷 5×5：拖拽画 13 格圆形
5. 「填充整个层级」（F 键）：当前 Z 层空格全填，已有保留
6. 「覆盖填充」（Shift+F）：当前 Z 层所有格子替换
7. 「清空整个层级」（Ctrl+Shift+F）：当前 Z 清空，其他 Z 不动
8. Ctrl+Z 撤销填充 → 地图恢复原状
9. 切换 Z 层（点层级按钮）→ 填充操作的是当前激活 Z，不影响其他层