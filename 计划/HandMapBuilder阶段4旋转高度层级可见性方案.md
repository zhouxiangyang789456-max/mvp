# HandMapBuilder 阶段 4 — 旋转 / 高度 / 层级可见性方案

> 承接阶段 2（分类+层级）和阶段 3（笔刷+填充）后的进一步优化。
> 解决用户反馈的三个卡点：方向单一、没法调高度、添加层级看不到。

---

## 1. 问题诊断

### 1.1 "地形方向只有一个"

**当前状态**：

- `HandPlacedTile` 结构体里**已有** `RotationY` 字段（float），但代码里**从来没用过**：
  - `PlaceAt`、`EraseAt`、`FillLevel`、`ClearLevel` 都没读/写这个字段
  - 所有实例的 rotation 都保持默认（prefab 自带的旋转，多为 0°）
- UI 里没有任何旋转控件

**用户期望**：至少 4 个方向（0°/90°/180°/270°），最好精细到任意角度。

### 1.2 "没法调整地形高度"

**当前状态**：

- 网格在 `GridToWorld(x, y, z)` 处用 `Y = z * 1f`，每层正好 1 个 Unity 单位高
- `HandPlacedTile` 没有 `HeightOffset` 字段
- 所有 prefab 实例都贴在自己 Z 层的 Y 平面，不能微调
- 常见痛点：石头/树根陷进地面、山头悬空、桥面和桥墩对不上

**用户期望**：能够精细调整每个 tile 的高度，让叠放时不穿模/不悬空。

### 1.3 "添加层级之后没有反应"

**真相诊断**（已读完 `DrawGridHandles`）：

```csharp
for (int z = 0; z <= _maxZInData; z++) {
    float y = z * 1f;  // ← Z=1 网格画在 Y=1 平面
    Handles.DrawLine(GridToWorld(x, 0, y), GridToWorld(x, h, y));
}
```

Z=1 网格**确实**被画了，但是因为：

1. **遮挡问题**：Z=0 层 prefab 缩放后约 1 单位高，Z=1 网格在 Y=1 直接被 Z=0 prefab 完全挡住
2. **平面相机不低头看**：Scene 视图默认是斜俯视，Z=1 网格在头顶，没法看到
3. **没有层间引导线**：屏幕上看不到任何 Z 轴方向参考物体，用户不知道新层在哪

**用户期望**：添加层级后**立刻**看到"层级 2"出现一个明确标识，或者相机自动平移到新层视角。

---

## 2. 目标

| 编号 | 功能 | 验收标准 |
|------|------|---------|
| F1 | 旋转（4 方向） | 调色板上方有旋转按钮（0/90/180/270），快捷键 R +90°，放置时实例按当前旋转放置 |
| F2 | 自由旋转（细调） | 一个角度输入框，可手动输入任意角度；已有 tile 也能调整 |
| F3 | 高度微调（每 tile） | `HandPlacedTile.HeightOffset` 字段，hover 已放 tile 时 +/-0.1 可调整 |
| F4 | 层高缩放（全局） | `LayerHeightScale` 全局滑块（0.5 ~ 2.0），改动后所有 Z 层间距同步 |
| F5 | 层级可见性 | 添加层级后 Scene 视图自动抬高相机到新 Z 中心；Scene 里画 Z 轴参考线 |
| F6 | 旋转预览 | hover 鼠标时 ghost preview 按当前旋转渲染 |
| F7 | 吸管带旋转 | 吸管同时拾取 prefab 路径 + 旋转 + 高度偏移 |

---

## 3. 数据结构改造

### 3.1 `HandPlacedTile` 加字段

在 `HandAuthoredMapData.cs`：

```csharp
[Serializable]
public struct HandPlacedTile
{
    public int X, Y, Z;
    public string PrefabPath;
    public float RotationY;       // 已有字段，开始真正使用
    public float HeightOffset;    // ← 新增，[-2, +2]，默认 0
    public HandTileCategory Category;
}
```

**关键**：`HandPlacedTile` 是 `struct`，加字段**不破坏**已有数据（Unity 会用默认值填充缺失字段）——只要不在序列化里 `Required`。

但等等：Unity 序列化 struct 字段，**对于已存在的 .asset 不会自动补充新字段**。如果用户已有 11 个 tile 的 `HandMap_New.asset`，新加的 `HeightOffset` 字段在反序列化时会用 `default(float) = 0`，是预期行为（不影响）。

### 3.2 `HandMapPalette.cs` 加 `LayerHeightScale`

`HandMapPalette.cs` 已经有静态类，加一个常量：

```csharp
public const float DefaultLayerHeightScale = 1f;
```

`HandMapBuilderWindow.cs` 加字段：

```csharp
float _layerHeightScale = HandMapPalette.DefaultLayerHeightScale;  // 全局层高缩放
float _currentRotationY = 0f;                                       // 当前放置旋转
float _currentHeightOffset = 0f;                                    // 当前高度偏移
int _currentBrushRotation = 0;                                      // 笔刷旋转 0/90
```

`GridToWorld` 调整：

```csharp
Vector3 GridToWorld(float x, float y, float z)
{
    // 修改前：y_world = z
    // 修改后：y_world = z * layerHeightScale + 高度偏移在调用处加
    return new Vector3(x, z * _layerHeightScale, y);
}
```

实际高度偏移在 `PlaceAt` 调用时单独计算：`instance.transform.position = new Vector3(world.x, world.y + tile.HeightOffset, world.z)`。

---

## 4. UI 设计

### 4.1 新增 DrawTransformControls 面板（放在 DrawLevelControls 下）

```
┌──────────────────────────────────────────────────────────────┐
│ 方向: [0°] [90°] [180°] [270°]  旋转角: [___] 度  R 加 90°    │
│ 高度偏移: [-2.0 ━━●━━━━ +2.0]  当前: 0.00  可 +/-0.1         │
│ 层高缩放: [0.5 ━●━━━━━ 2.0]   当前: 1.0  (改变所有层间距)    │
└──────────────────────────────────────────────────────────────┘
```

- **方向按钮组** (Toggle)：点击切换当前 `_currentRotationY`（吸附到 0/90/180/270）
- **旋转角输入** (FloatField)：手动输任意角度，两个控件互相同步
- **高度偏移滑块** (Slider)：范围 [-2, +2]，同时显示当前值
- **层高缩放滑块**：调整 Z 层之间的物理间距

### 4.2 已有 tile 的高度调整

```csharp
// 在 DrawHoverGhost 或 DrawTileInspector 里：
if (已有 tile 在 hover 位置)
{
    int existingZ = ...;
    float existingH = tile.HeightOffset;
    if (GUILayout.Button("抬升 0.1")) tile.HeightOffset += 0.1f;
    if (GUILayout.Button("下沉 0.1")) tile.HeightOffset -= 0.1f;
}
```

更简单：选中 hover 的 tile 后按 `[` `]` 微调、也可直接拖滑块（编辑器 IMGUI 不太好做，简化成两个按钮）。

---

## 5. Scene 视图行为改动

### 5.1 层间 Z 轴引导线（解决 "看不到层级 2"）

在 `DrawGridHandles` 加：

```csharp
// 从地面到最高层画 4 根"Z 轴引导线"（四角）
float topY = _maxZInData * _layerHeightScale;
for (int corner = 0; corner < 4; corner++)
{
    var (x, y) = corner switch {
        0 => (0, 0), 1 => (w, 0), 2 => (w, h), 3 => (0, h),
    };
    Handles.color = new Color(1, 0.5f, 0.2f, 0.4f);
    Handles.DrawDottedLine(GridToWorld(x, y, 0), GridToWorld(x, y, topY), 4f);
    // 在每层画一个小立方体标记当前 Z
    for (int z = 0; z <= _maxZInData; z++) {
        var p = GridToWorld(x, y, z * _layerHeightScale);
        Handles.SphereHandleCap(0, p, Quaternion.identity, 0.1f, EventType.Repaint);
    }
}
```

视觉上用户在任何层都能看到**贯穿地图的橙色虚线 + 圆点**，标识 Z 高度。

### 5.2 添加层级时相机自动 refocus（解决 "看不到层级 2"）

```csharp
void AddLevel()
{
    int newZ = _maxZInData + 1;
    _maxZInData = newZ;
    _activeZ = newZ;

    // Scene 视图相机自动平移 Y 到新层中心
    var sv = _sv ?? SceneView.lastActiveSceneView;
    if (sv != null)
    {
        var pivot = GridToWorld(_mapData.Width / 2f, _mapData.Height / 2f, newZ * _layerHeightScale);
        sv.pivot = pivot;
        sv.size = Mathf.Max(_mapData.Width, _mapData.Height) * 1.5f;
        sv.Repaint();
    }
}
```

加层级时直接把相机拉到新 Z 平面，**用户立刻看到新层**。

### 5.3 激活层闪烁高亮（防止用户找不到当前位置）

```csharp
// _levelAddTime 记录 AddLevel 时间戳
float t = (float)(EditorApplication.timeSinceStartup - _levelAddTime);
if (t < 2.0f)
{
    float alpha = Mathf.PingPong(t * 2f, 1f);
    Handles.color = new Color(1, 0.95f, 0.3f, alpha);
    Handles.DrawWireCube(activeZCenter, size);
    Repaint();
    SceneView.RepaintAll();
}
```

新层网格闪烁两秒，**视觉上立即反馈"层级 2 已经创建"**。

### 5.4 ghost preview 按当前旋转渲染

在 `DrawHoverGhost` 里，prefab 实例化后立即 `instance.transform.rotation = Quaternion.Euler(0, _currentRotationY, 0)`，这样鼠标 hover 时预览已经带旋转。

---

## 6. 旋转功能详细交互

### 6.1 状态

```csharp
float _currentRotationY;  // 当前放置的 rotation
```

### 6.2 写入

- `PlaceAt`：写入 `tile.RotationY = _currentRotationY;`
- `FillLevel`：写入 `tile.RotationY = _currentRotationY;`
- `EraseAt`：不动

### 6.3 读取

- 吸管（右键）：从已有 tile 读 `RotationY` 回写到 `_currentRotationY`，UI 自动更新按钮高亮
- DrawHoverGhost：prefab 实例的 `transform.rotation = Quaternion.Euler(0, _currentRotationY, 0)`

### 6.4 UI 行为

- 0/90/180/270 四个 Toggle 按钮：高亮那个 = 当前 `_currentRotationY`（吸附）
- 角度输入框：可以输入 15.5° 这种自由值；输入后四个按钮全不高亮（表示非吸附）
- 快捷键 `R`：`RotationY = (RotationY + 90) % 360`
- 快捷键 `Shift+R`：`RotationY -= 90`

### 6.5 旋转 + 笔刷配合

拖拽绘制时每个格子都用 `_currentRotationY`，所以一条直线上的所有 tile 都是同方向。如果想做"沿线扭转"——超出本阶段范围。

---

## 7. 高度功能详细交互

### 7.1 全局层高缩放

```csharp
float _layerHeightScale = 1f;  // 范围 [0.5, 2.0]
```

- UI Slider：0.5 ~2.0，默认 1
- 改 `_layerHeightScale` 后**所有 Z 层的 Y 位置**重新计算，Scene 视图**所有已放 prefab 也跟随重新 Y 定位**
- **关键实现**：监听 `_layerHeightScale` 变化 → 遍历 `_spawnedByCell` 用值 → `instance.transform.position.y = key.z * _layerHeightScale + HeightOffset`

### 7.2 单 tile 高度偏移

新增字段：

```csharp
float _currentHeightOffset = 0f;  // 当前放置时的默认 height offset
float _inspectHeightOffset;       // 选中已放 tile 时调它的 height offset
```

放置新 tile：写入 `tile.HeightOffset = _currentHeightOffset`

调整已有 tile 高度：

```
// 选中模式（按住 Shift 单击）：
if (Event.current.shift && _hoverCell.x >= 0)
{
    // 进入选中模式
    var tile = _mapData.FindTile(_hoverCell.x, _hoverCell.y, _activeZ);
    if (tile != null) {
        // 显示在 Inspector 区域
        tile = tile.Value;
        float newH = EditorGUILayout.FloatField("高度偏移", tile.HeightOffset);
        if (!Mathf.Approximately(newH, tile.HeightOffset)) {
            Undo.RecordObject(_mapData, "调整高度");
            var idx = _mapData.Tiles.IndexOf(tile);
            tile.HeightOffset = newH;
            _mapData.Tiles[idx] = tile;
            // 重定位预览实例
            UpdateInstancePosition(idx);
        }
    }
}
```

`Shift+左键单击` = 进入"调整模式"，显示 Inspector 区域让用户调 HeightOffset。

### 7.3 高度微调按钮

UI 加两个按钮：

```
[抬起 0.1]   [下沉 0.1]   （针对当前选中 tile 或当前新放置默认值）
```

---

## 8. 实施步骤

### 步骤 1：数据结构（10 min）

- `HandAuthoredMapData.cs` 给 `HandPlacedTile` 加 `HeightOffset`（默认 0，不破坏已有数据）
- `HandMapPalette.cs` 加 `DefaultLayerHeightScale` 常量

### 步骤 2：字段 + GridToWorld 改造（10 min）

- `HandMapBuilderWindow.cs` 加 `_layerHeightScale`、`_currentRotationY`、`_currentHeightOffset`
- `GridToWorld` 加入对 `_layerHeightScale` 的读取

### 步骤 3：旋转 UI + PlaceAt 写入（20 min）

- `DrawTransformControls()` 新方法
- `PlaceAt` 写入 `RotationY`
- `FillLevel` 写入 `RotationY`
- `DrawHoverGhost` 实例应用当前旋转
- `EraseAt` 吸管读 `RotationY` 回写

### 步骤 4：高度 UI + PlaceAt 写入（20 min）

- 高度滑块 + 抬起/下沉按钮
- `PlaceAt` 写入 `HeightOffset`
- 选中 tile 时显示 Inspector 区域 + 滑块可改
- 实时重定位预览实例

### 步骤 5：层高缩放全局联动（15 min）

- Slider 改动 → 触发重定位所有已放预览实例
- `EnsurePreviewFor` 里加 Y 偏移计算

### 步骤 6：添加层级相机自动 refocus（10 min）

- `AddLevel` 加相机 pivot + size 重置

### 步骤 7：Scene 视图 Z 轴引导线（10 min）

- `DrawGridHandles` 加四角橙色虚线 + 圆点

### 步骤 8：闪烁高亮（5 min）

- `_levelAddTime` 时间戳 + PingPong alpha

### 步骤 9：快捷键（5 min）

- `R` / `Shift+R` / `[` / `]`（已有 `[`/`]` 是翻页，重新分配：`Ctrl+R`、`Ctrl+Shift+R`）

---

## 9. 验收

| 编号 | 测试 |
|------|------|
| V1 | 选中「草地」类目，点 R：rotation 跳到 90°，Scene 视图 ghost 旋转 90° |
| V2 | 放置 10 个草地 tile，所有 tile rotation 都是 90° |
| V3 | 在「草地」类目输入旋转角 45°，按钮全部不高亮（吸附失效），放置 5 个，所有 tile 旋转 45° |
| V4 | 右键吸管已放 45° tile，rotation 自动切回 45°，按钮状态同步 |
| V5 | 拉「高度偏移」到 0.5，放置新 tile，tile 半浮空（高出 0.5） |
| V6 | 拉「层高缩放」从 1.0 到 1.5，**所有已放 tile** 的 Y 位置同步缩放 1.5 倍 |
| V7 | 点「添加层级」，Scene 视图相机自动抬高到新 Z 中心，看到层级 2 网格清晰可见 |
| V8 | 新层网格闪烁 2 秒后稳定 |
| V9 | Scene 视图四个角可见橙色点状 Z 轴引导线 |
| V10 | Ctrl+Z 能撤销整批操作（包括旋转 / 高度调整） |

---

## 10. 不在本阶段

- **运行时集成**（让 HandMapData 真正进入 BattleScene）— 留给阶段 5
- **可视化 Inspector**（更复杂的 tile 属性面板）— 留给阶段 6
- **批量旋转 / 旋转层叠**（同格多方向合成复杂造型）— 太复杂，暂不
- **每个 prefab 自带默认旋转**（prefab 元数据）— 暂用全局当前 rotation 即可
