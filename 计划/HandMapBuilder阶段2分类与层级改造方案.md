# HandMapBuilder 阶段 2：分类 + 层级改造方案

## 1. 背景

阶段 1 已交付最小可用工具：硬编码 8 个 prefab 的调色板 + Scene 视图 click-to-place。但用户截图 Demo 场景后发现：

1. **资产种类丰富**：`Isometric Pack 3d` 共 ~250 个 prefab（`Props/` 212 个 + `Tiles_Groups/` 43 个 + `Particles/` 6 个），按功能分类（基础地形/装饰/建筑/水/桥/坡道/森林/山丘等）才能让调色板可用。
2. **存在多层级资产**：基础地面 (`Tile_Grass`、`Tile_1_*`) 占一格；坡道 `Tile_Ramp*`、桥 `Bridge*`、楼梯 `Stairs_*`、二层建筑 `Camp1_Tower` 都是**跨格 / 跨高度**的元素（用户在格子 (0,0) 放坡道，往 (1,0) 方向升高）——需要引入 **Z 层级**概念做预览。
3. **小翻页**：原型图里下方一排约 8 格，靠上下页翻整页；同类别 prefab 多时（如 30 棵树）必须能翻页。

## 2. 资产清单分类（按功能）

参考 `Isometric Pack 3d/` 实际资产，整理出 **9 大功能类别** + 1 个擦除：

| 类别 ID | 显示名 | 数量 | 资产路径/示例 | 备注 |
| --- | --- | --- | --- | --- |
| `Base` | 基础地形 | 26 | `Props/Tile_Grass1-5`、`Props/Tile_1_A-H`、`Props/Tile_1_brick_A-H`、`Props/Tile1_Base`、`Tiles_Groups/Tile_Base1A-4` | 1×1 基础地面砖，默认 Z=0 |
| `Path` | 道路砖块 | 16 | `Props/Tile_1_brick_A-H`、`Tiles_Groups/Tile_Base1A-1H`、`Tiles_Groups/Tile_Group1-18`（部分） | 默认 Z=0；某些 Group 多格 |
| `Forest` | 森林 / 树 | 40+ | `Props/Tree1_1..5` + `Tree1_1b..5b`、`Tree2_1..8`、`Tree3_01..07`（含 `_aut` 动画版）、`Tree4_01..05` | 树根默认 Z=0，部分高大覆盖 2-3 格视觉 |
| `Plant` | 灌木 / 草 | 30+ | `Props/Plants_01A-24D`、`Props/Mushroom1A-4B`、`Props/Ground_leafs_1-2` | 小装饰，多数 1×1 |
| `Water` | 水体 | 5 | `Tiles_Groups/Tile_Water1A-1D`、`Tiles_Groups/Tile_Water2`、`Props/Water1` | 水面，默认 Z=0 |
| `Ramp` | 坡道 / 楼梯 | 7 | `Props/Tile_Ramp1A-1B`、`Props/Tile_Ramp2A-2B`、`Props/Stairs_1-2`、`Props/Stairs_Debris1-3` | 跨 Z 层级，Z=1 或 Z=2 |
| `Bridge` | 桥 | 2 | `Props/Bridge1`、`Props/Bridge2` | 跨 Z=1，桥面高于水面 |
| `Mountain` | 山地 / 岩石 | 11 | `Props/Rock_01-11` | 默认 Z=0，岩石可叠堆 |
| `Building` | 建筑 / 营地 | 17 | `Props/Camp1_Brick1-4`、`Camp1_Barricade1-2`、`Camp1_Shield`、`Camp1_Shooting_shield`、`Camp1_Tower`、`Camp2_Fierplace1-2`、`Camp2_tent`、`Props/Dungeon_Passage1`、`Props/Mine_Beam1`、`Mine_Box`、`Mine_Cart`、`Mine_Enter`、`Mine_Ore`、`Mine_tracks1-2` | 多数 Z=0，`Camp1_Tower` 跨多格高度 |
| `Decoration` | 装饰物 | 35+ | `Props/Banner_Pole1-4`、`Barrier1`、`Bench1-2`、`Bucket`、`Chest1A-4B`、`Fence1_1-3`、`Glow1`、`GoldBag1-2`、`Graveyard_01-17`、`Graveyard_Urn1-2`、`Lamp_01`、`Magic_Orb`、`Magic_Pillar1-5`、`Magic_Shrine1-2`、`Sign1-2`、`Waterfall1-2`、`Well1`、`Wood_el_01-06` | 装饰，不阻挡通行；Z 多数为 0 |
| `Effect` | 特效 | 5 | `Particles/Candleflame`、`Fog1-3`、`Glow1`、`Ripples` | 纯特效，运行时挂载用 |
| `Erase` | 擦除 | — | （占位） | 点击格子清除 |

> Particles 文件夹虽叫特效，但 `Fog1-3`/`Glow1`/`Ripples` 实际是 GameObject prefab，可视作"装饰/特效"层使用。

## 3. 层级 (Z) 概念

### 3.1 为什么需要 Z

Isometric Pack 的几何语义：
- `Tile_Grass*`、`Tile_1_*` → 1×1 地面方块，**Y 轴高度 = 0**
- `Tile_Ramp*` → 1×1 斜坡，**Y 轴从 0 升到 0.5**（半个单位高度），落点 X/Z 方向不一致
- `Stairs_*` → 1×1 阶梯，**Y 轴多级升高**（如 `Stairs_1` 升 0.3）
- `Bridge*` → 2×1 桥，**桥面 Y = 1**（1 个单位高），盖在水面上
- `Camp1_Tower` → 1×1，**Y = 2**（塔身高 2 个单位），落地仍占 1 格
- `Tree1_*` → 1×1，**Y = 0 起，向上长到 Y ≈ 2-3**

> 多数 prefab 是 1×1 格，但 **视觉上跨多层**。

### 3.2 工具里的 Z 处理（**手动层级，用户驱动**）

> **设计原则**：层级由用户**自己创建**。工具不预判"桥应该在 Z=1"或"塔应该在 Z=2"——同一份 prefab（草地、桥、塔）用户想放哪层都行。

#### 概念

- **第 1 层级** = Z=0 = 地面层（最先建造）
- **第 2 层级** = Z=1 = 地面之上 1 个单位
- **第 3 层级** = Z=2 = 地面之上 2 个单位
- **第 N 层级** = Z=N-1
- 每个层级是**独立的"图层"**：每个格子（X, Y）在不同 Z 上可有不同 prefab，互不干扰

#### 状态机

```
┌──────────────────────────────────────────────────┐
│ 当前激活层级: Z=0                                  │
│ [⬇ 添加层级] 按钮 → 点击后激活 Z=1（第2层级）    │
│ [⬆ 移除最高层级] 按钮 → 删除 Z=Max 那层所有放置  │
│ 层级按钮 [1层][2层][3层]... 可点击切换激活层级    │
└──────────────────────────────────────────────────┘
```

- 首次打开工具：自动激活 Z=0（第 1 层级），用户开始放置地面
- 用户点 `⬇ 添加层级` → 新增 Z=1（第 2 层级），激活切到 Z=1
- 用户在 Z=1 上放桥面 → 桥渲染在 Y=1 高度
- 还能再 `⬇ 添加层级` → Z=2（第 3 层级）
- `⬆ 移除最高层级`：删除当前数据中 Z=Max 的所有 tile，连同 Scene 视图里的预览实例一起清掉
- **同格多层**：完全允许！(5,5) Z=0 放草地 + Z=1 放桥面 + Z=2 放瞭望塔，每个 Z 独立存

#### 视觉表现

- **每层网格都画**：Z=0 白色（半透明 0.15 alpha），Z=1 青色，Z=2 橙色，更高层继续用其他色
- **激活层级高亮**：当前激活层的网格线更粗、更亮；其他层网格线细、暗
- **hover/click 只命中激活层**：鼠标射线打 `Y = _activeZ` 平面（不是当前激活层）
- **同时显示所有层级已放置的 prefab**（preview instances），不需要切层才能看到其他层的内容

#### 数据结构

`HandAuthoredMapData` 已有的 `Tiles: List<HandPlacedTile>` 每个元素自带 `Z` 字段，**无需改动**。新增运行时状态：

```csharp
// HandMapBuilderWindow.cs
int _activeZ = 0;             // 当前操作的 Z（默认 0）
int _maxZ = 0;                // 数据中已存在的最大 Z，用于"添加层级"按钮的禁用/启用
```

`PlaceAt(cell, prefab)` 时：
```csharp
tile.Z = _activeZ;  // 直接用当前激活层级
```

#### 工具栏 UI（层级控件）

```
[⬇ 添加层级]  [⬆ 移除最高层级]   当前: 第 1 层 (Z=0)
层级:  [1层]  [2层]  [3层]   ← 高亮当前激活
```

键盘：`+` / `-` 增加/降低激活层级（在已有层级范围内）；`Shift+`+ 新建层级。

#### 为什么这样做

1. **用户直觉控制**——不同游戏对"层级"定义不同（塔、防线、塔楼、二层走廊……），工具不该越俎代庖
2. **简化心智模型**——"我现在工作在第几层" vs "这个 prefab 应该被自动分到第几层"，前者更清楚
3. **支持艺术化构图**——同样一块草地可以放在 Z=0（地面）或 Z=2（屋顶花园）
4. **数据模型零改动**——`Z` 字段已存在，只调整 UI 和交互

> **简化版（前方案已废弃）**：prefab 类别自动推断 Z（桥→Z=1、塔→Z=2）。用户拒绝此方案，理由是"我想自己来建造层级"。

### 3.3 UI 布局调整

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ Tools/Map Generation/Hand Map Builder                                          │
├──────────────────────────────────────────────────────────────────────────────┤
│ 地图数据: [HandMap_001 ▼]  [新建] [保存] [新场景]                              │
│ 尺寸: 宽[16] 高[14]                                                            │
├──────────────────────────────────────────────────────────────────────────────┤
│ [基础地形] [道路] [森林] [灌木] [水] [坡道] [桥] [山地] [建筑] [装饰] [特效]    │
│  ← 类别标签页（点击切换）                                                      │
├──────────────────────────────────────────────────────────────────────────────┤
│ ◀ 上页  1/4  ▶ 下页       [擦除 (E)]                                          │
│ ┌──┐┌──┐┌──┐┌──┐┌──┐┌──┐┌──┐┌──┐                                              │
│ │图││图││图││图││图││图││图││图│  ← 调色板（8 格一排）                          │
│ └──┘└──┘└──┘└──┘└──┘└──┘└──┘└──┘                                              │
├──────────────────────────────────────────────────────────────────────────────┤
│ 层级: [⬇ 添加层级] [⬆ 移除最高层级]                                            │
│        [第1层 (Z=0)] [第2层 (Z=1)] [第3层 (Z=2)]...   ← 当前激活高亮            │
├──────────────────────────────────────────────────────────────────────────────┤
│  Scene 视图（Unity 自带）：                                                       │
│  - 每层都画网格（非激活层细线、激活层粗亮线）                                   │
│  - hover/click 只命中激活层                                                     │
│  - 所有已放置的 prefab 都按各自 Z 渲染到对应 Y 高度                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

## 4. 数据建模改动

### 4.1 `HandAuthoredMapData` 已存在，无需改

字段 `Width/Height/List<HandPlacedTile>` 已满足 Z 概念（每个 tile 自带 `Z`）。

### 4.2 新增 `HandMapPalette.cs`（配置类）

放 `Assets/Editor/HandMapBuilder/HandMapPalette.cs`：

```csharp
public enum HandMapCategory
{
    Base, Path, Forest, Plant, Water, Ramp, Bridge,
    Mountain, Building, Decoration, Effect, Erase
}

public sealed class HandMapCategoryEntry
{
    public HandMapCategory Category;
    public string DisplayName;
    public List<string> PrefabPaths;
    public int DefaultZ;
}
```

工具启动时构造一个静态字典：`HandMapCategory → HandMapCategoryEntry`。

### 4.3 `HandAuthoredMapData.HandTileCategory` 枚举已存在，**扩展**：

当前：`Base, Forest, Hill, Mountain, Water, Bridge, Road, Building, Decoration, Erase`
新版（对齐 `HandMapCategory`）：`Base, Path, Forest, Plant, Water, Ramp, Bridge, Mountain, Building, Decoration, Effect, Erase`

> 原 `Road` 改名 `Path`，`Hill` 并入 `Mountain`，`Forest/Plant` 拆分；旧数据兼容见 §6。

## 5. 实施步骤

1. **阶段 2.1 静态分类字典**（30 min）
   - 新建 `HandMapPalette.cs` 静态类，按 §2 表格把所有 prefab 路径枚举出来
   - 改 `HandTileCategory` 枚举扩展
   - 在 `HandMapBuilderWindow` 里 `_selectedCategory` 替换原硬编码 `_selectedPaletteIndex`

2. **阶段 2.2 类别标签页 + 调色板翻页**（30 min）
   - `DrawCategoryTabs()`：11 个标签页按钮（类别名）
   - `DrawPalette()`：根据 `_selectedCategory` 拿 prefab 列表，按 8 个一页分页显示
   - 上页/下页按钮 + 页码 `1/4`
   - 键盘快捷 `[` `]` 翻页、`Tab` 切类别、`E` 擦除模式（已存在）

3. **阶段 2.3 手动层级（核心新增）**（45 min）
   - 字段：`_activeZ`（int，默认 0）、`_maxZ`（数据中已存在的最大 Z）
   - `DrawLevelControls()`：3 个控件
     - `[⬇ 添加层级]` 按钮：`_activeZ = ++_maxZ`（同时新建最高层）
     - `[⬆ 移除最高层级]` 按钮：删除所有 Z==_maxZ 的 tile，_maxZ--；若 _maxZ < 0 则禁用
     - 层级按钮 `[1层] [2层] ...`：动态生成 1..(_maxZ+1) 个按钮，点击切 `_activeZ`
   - `DrawGridHandles`：循环 z = 0.._maxZ，在 `Y = z * 1.0` 平面画网格
     - 非激活层：白/青/橙色细线（alpha 0.12）
     - 激活层：粗线 + 高 alpha 0.4
   - hover/click 只命中 `_activeZ` 层：`new Plane(Vector3.up, -_activeZ * 1f)`
   - `PlaceAt`：`tile.Z = _activeZ`（用户当前激活层）
   - 键盘：`+` = 添加层级、`-` = 降低激活层级（在 0.._maxZ 范围）

4. **阶段 2.4 工具体验打磨**（30 min）
   - DrawHelp 更新（讲清层级概念）
   - DrawHeader 提示信息更新
   - Scene 视图默认相机角度（俯视等距 45°）
   - 右键吸管（拾取该格的 prefab 进调色板选中；**带层级**：拾取时把激活层切到 prefab 所在 Z）
   - 鼠标 hover 时 Scene 视图状态栏显示 `(X, Y, Z)` 坐标
   - 预览实例按各自 Z 渲染（在 Y = Z 高度），跨层叠放时不需要切层也能看到全部

5. **阶段 2.5 运行时集成预演**（不在本次范围，留待后续）
   - `LevelMapGenerationProfile.HandMapOverride` 字段
   - `ProceduralBattleMapProvider` 集成

## 6. 兼容与风险

- **旧 `HandTileCategory` 值兼容**：旧 `.asset` 数据里 `Road → Path`、`Hill → Mountain`，加载时做迁移（或写 `OnAfterDeserialize` 转换）。
- **prefab 路径硬编码**：若用户重命名/移动 prefab，路径失效；先期用 `[FormerlySerializedAs]` 或路径查找函数（按名字扫描）兜底——本阶段先用硬编码。
- **Z=2 视觉**：塔身 2 单位高，若场景里有相机从 12 米高度俯视，Z=2 网格在 Y=2 平面仍能看到，OK。

## 7. 验收

1. **分类**：打开工具，默认显示「基础地形」类别的 8 个 prefab；点击其他类别标签页 → 调色板切换；点击 `[` `]` 翻页（树有 40+）
2. **第 1 层级**：默认激活 Z=0，选个草地 prefab → 在地图上点格子 → 出现草地 preview
3. **第 2 层级**：点 `⬇ 添加层级` → 激活切到 Z=1，Scene 视图多画一层青色网格；选桥 prefab → 在地图上点同一格子 → 既有 Z=0 草、又有 Z=1 桥（视觉上桥面浮在草地上方）
4. **第 3 层级**：再次 `⬇ 添加层级` → Z=2 橙色网格出现；切回 Z=2 层级 → 选塔 prefab 摆上去
5. **切换激活层**：点 `第2层` 按钮 → hover 高亮 Z=1 网格；点 `第1层` → hover 回到 Z=0；其他层已放置的 prefab 仍可见
6. **移除层级**：点 `⬆ 移除最高层级` → 删除 Z=2 所有放置 + preview 实例；按钮自动禁用（无 Z>0 数据）
7. **保存**：保存后 `HandAuthoredMapData.asset` 里 `Tiles` 列表每个元素带正确 Z 字段（Z=0/1/2 混合）