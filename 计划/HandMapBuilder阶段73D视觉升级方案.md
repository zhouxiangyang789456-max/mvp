# HandMapBuilder 阶段 7 — BattleScene 3D 等距视觉升级方案（修订版）

> 阶段 1-5：HandMapBuilder 编辑器工具完成。
> 阶段 6：HandMap 数据接入 BattleScene，见 `HandMapBuilder阶段6运行时集成方案.md`。
> 阶段 7：把 BattleScene 的 2D 地形贴片升级为 3D Mesh Prefab，同时保证手作地图真正所见即所得。
>
> 本版修订日期：2026-09-05。修订重点：拆分 Procedural/HandAuthored 视觉管线，补齐高度、缩放、道路、水面、回退、性能与验收规则。

---

## 1. 结论与范围

阶段 7 不再用一套随机 `TerrainPrefabCatalog` 同时覆盖 Procedural 和 HandAuthored 地图，而是采用两条明确的视觉管线：

```text
Procedural
  -> TerrainType[,]
  -> TerrainPrefabCatalog
  -> 按地图 Seed + 坐标确定性选取 3D Prefab

HandAuthored
  -> HandPlacedTile 原始视觉数据
  -> 直接实例化原 Prefab
  -> 保留 RotationY / Z / HeightOffset / 固定缩放

共同基础设施
  -> 统一高度接口
  -> Collider/Physics 清理
  -> 旧 Sprite fallback
  -> 统一视觉根节点和生命周期
```

这样同时满足两个目标：

1. Procedural 地图从 2D Sprite 升级到稳定、多样的 3D 地形。
2. HandAuthored 地图不被重新随机映射，BattleScene 忠实复现 Builder 中手工选择和调整的视觉。

### 本阶段包含

- 3D Ground Prefab 运行时渲染。
- HandAuthored 原始 Prefab 的忠实复现。
- Procedural 地形的确定性 Prefab Catalog。
- Road/Bridge 连接规则、Water 渲染策略、统一高度和 Sprite 回退。
- EditMode 测试、PlayMode/视觉检查与性能基线。

### 本阶段不包含

- Orthographic 改 Perspective。
- 重做 UnitView、HUD、指挥官 UI 或编队玩法。
- 自研水 Shader、DOTS、运行时网格合并、完整 LOD 系统。
- 新建或实质修改 3D 模型、网格、材质、骨骼或动画。

若后续发现缺少合适的 Road/Water Prefab，需要制作或修改 3D 资产，必须另开资产任务并遵守 `规则/BlenderMCP工作规则.md`。本阶段优先复用已有 IsoPack，不以 Unity 基础几何体冒充正式资产。

---

## 2. 已确认现状

| 系统 | 当前情况 |
|---|---|
| `BattleCellView` | 每格创建 `SpriteRenderer`；存在相机朝向 Sprite、颜色方块和 Road/Bridge Sprite 拼接 |
| `TerrainVisualCatalog` | 纯 Sprite 查询和连接图逻辑，适合作为 fallback，不应承担 3D Catalog 职责 |
| `BattleGridController` | 创建全图 `BaseGround`、逐格 `BattleCellView`，最后运行 3D DecorationSpawner |
| `TerrainDecorationSpawner` | 已实例化 3D Prefab，并用整组 Bounds 做 `NormalizeAndGround` |
| HandMapBuilder | 直接实例化指定 Prefab，应用 `RotationY`、`HeightOffset`，预览固定缩放为 `0.5` |
| HandMap 数据 | 已有 `PrefabPath`、`RotationY`、`HeightOffset`、`Category`；阶段 6 计划增加直接 `GameObject` 引用和运行时 Z 层参数 |
| IsoPack | 已确认有 `Tile_Grass*`、`Tile_Group*`、`Tile_Water*`、`Bridge1/2`、树和石头等 Prefab |
| 相机 | BattleScene 使用 Orthographic 斜俯视，继续保留 |

### 必须修正的原方案假设

- HandAuthored 的 Base/Path/Water/Mountain 不能跳过后再按 `TerrainType` 随机重建，否则会丢失原 Prefab、旋转和高度，不是所见即所得。
- `NormalizeAndGround` 不能无条件用于 `Tile_Group*`。整组 Bounds 含树木等高物体，会把整个地块异常缩小。
- Mesh Ground 不应被统一改到透明队列。Opaque Ground 保持深度写入，只有水面使用透明材质策略。
- `BaseGround` 是全图单对象，不能实现“个别格 fallback 时才保留”；必须采用明确的全图策略。

---

## 3. 最终设计决策

### 决策 1：按地图来源分流视觉

| 地图来源 | Ground 视觉来源 | 选择规则 |
|---|---|---|
| `TestMap` | `TerrainPrefabCatalog` | 与 Procedural 相同，便于直接开场景验证 |
| `Procedural` | `TerrainPrefabCatalog` | `mapSeed + cell + terrain + variantSalt` 确定性选择 |
| `HandAuthored` | `HandPlacedTile.Prefab` | 原样实例化；路径仅作为旧数据迁移依据，不能用于 Player Build 动态加载 |

HandAuthored 只在 Prefab 为空、引用失效、Tile 明确无视觉或旧资产尚未回填时，使用 Catalog/Sprite 回退。

### 决策 2：Catalog 独立于 Sprite Catalog

新增 `TerrainPrefabCatalog : ScriptableObject`，不向 `TerrainVisualCatalog` 塞入 `HasPrefab()` 或转发逻辑。

- `TerrainPrefabCatalog`：3D Prefab、权重、缩放、基准旋转、连接变体。
- `TerrainVisualCatalog`：保留现有 2D Sprite fallback。
- `BattleCellView`：根据视觉上下文选择 3D、HandAuthored 或 Sprite 路径。

Catalog 放在 `Assets/Resources/Battle/Terrain/Generated/TerrainPrefabCatalog.asset`，其中直接引用 IsoPack Prefab。直接引用会让 Unity 收集依赖，不要求把原 Prefab 复制进 Resources。

### 决策 3：确定性选择，不使用全局 Random

Procedural/TestMap 的变体选择必须满足：

```text
同一地图 Seed + 同一坐标 + 同一 TerrainType + 同一 Catalog 版本
=> 永远得到相同 Prefab 和旋转
```

不得调用或污染 `UnityEngine.Random.state`。建议复用项目稳定哈希/随机实现；若无现成接口，再增加局部纯函数哈希。

### 决策 4：缩放按模式配置

```csharp
public enum TerrainScaleMode
{
    KeepPrefabScale,
    FixedScale,
    FitFootprint
}
```

| 用途 | 默认模式 | 说明 |
|---|---|---|
| HandAuthored | `FixedScale` | 使用 HandMap 持久化的全局/单 Tile 缩放；默认与 Builder 当前 `0.5` 一致 |
| 单纯 Ground Tile | `FixedScale` 或 `FitFootprint` | 只按 XZ footprint 缩放，不按整体高度缩放 |
| `Tile_Group*` | `FixedScale` | 含树木/道具，禁止按整组高度归一 |
| 独立 Decoration | 保留现有 Bounds 归一 | 行为不在阶段 7 强制改变 |

新增几何辅助接口时必须拆分为 `ApplyScale`、`TryGetRendererBounds`、`GroundToY` 和 `DisablePhysicsAndColliders`。不要继续用一个 `NormalizeAndGround` 同时决定缩放和贴地。

### 决策 5：统一高度公式

```text
baseY = TerrainCatalog.GetElevation(terrain)

Procedural visualY = baseY + catalogEntry.GroundOffset

HandAuthored visualY = baseY
                     + tile.Z * handMap.LayerHeightScale
                     + tile.HeightOffset
                     + prefabGroundOffset
```

阶段 6 必须把 Builder 的 `LayerHeightScale` 持久化到 `HandAuthoredMapData`，不能继续只存在于 EditorWindow 私有字段。

`BattleGridController` 应提供统一查询：

```csharp
float GetSurfaceY(Vector2Int cell);
Vector3 GridToSurfaceWorld(Vector2Int cell);
```

单位、装饰、指挥官锚点和地形视觉逐步共用该接口。阶段 7 至少保证新 Ground、现有 Decoration 和单位站立点不产生明显分离。

### 决策 6：Road/Bridge 分来源处理

- HandAuthored：使用用户手放的原始 Path/Bridge Prefab、旋转和高度，不执行自动替换。
- Procedural/TestMap：保留 `BuildRoadMask`，通过经过验证的 3D 连接表选型。
- 3D 连接表未配置完整时：该格走现有 Sprite Road/Bridge fallback，不允许猜 Prefab。

连接表必须覆盖：

| 连接数 | 形态 |
|---|---|
| 0 | isolated |
| 1 | end/dead-end |
| 2 且相对 | straight |
| 2 且相邻 | corner |
| 3 | T |
| 4 | cross |

每项包含 `mask -> prefab -> prefabBaseYaw -> runtimeYaw`。正式配置前先在 Unity 中人工确认候选资源的外观、原点和正方向。仅凭 `Tile_1_A~H` 名字不得自动推断语义。

Bridge 还必须验证相邻 Road/Water 方向、桥面高度与 `GetSurfaceY` 一致，以及单位逻辑仍走 2D 格，不因 Mesh Collider 参与寻路。

### 决策 7：水保持透明，Ground 保持 Opaque

- Ground 材质保持原始 Opaque/Geometry 队列和深度写入。
- Water 使用 IsoPack 自带透明材质；必要时只调整水面几何高度或专用材质资产。
- 禁止在运行时直接修改共享材质的 `renderQueue`。
- 若确需不同参数，创建项目内专用材质资产或使用 `MaterialPropertyBlock`；不要为每格实例化材质。
- 水需要验证正交相机移动、相邻水格、岸边、单位遮挡和阴影表现。

若现有 Water Prefab 无法稳定工作，本阶段允许 Water 暂时走 Sprite fallback；自研水 Shader 不阻塞其他 TerrainType 的 3D 升级。

### 决策 8：保留 BaseGround 作为低位兜底

全图 `BaseGround` 暂不删除：

- 仅作为地图边缘、Tile 缝隙、缺失资源时的背景；
- 保持低于最低地形表面；
- 不承担逐格 fallback；
- 不改为透明队列。

具体 Tile 的 fallback 仍由 `BattleCellView` 创建 Sprite。全部 3D 视觉稳定后，再用独立性能数据决定是否移除 BaseGround。

### 决策 9：相机、单位、UI 暂不联动重构

- 保留 Orthographic 相机和现有 RTS 构图。
- UnitView、HUD、Tooltip、指挥官和编队控制语义不在本阶段修改。
- 只允许为地表高度适配增加最小接口调用。
- 地形 Mesh 和清理后的 Collider 不得改变点击优先级或退回单兵直接控制。

---

## 4. 数据结构建议

```csharp
[CreateAssetMenu(menuName = "Battle/Terrain Prefab Catalog")]
public sealed class TerrainPrefabCatalog : ScriptableObject
{
    public int CatalogVersion = 1;
    public List<TerrainPrefabEntry> Entries = new();

    public bool TryGetEntry(TerrainType terrain, out TerrainPrefabEntry entry);
    public bool TryPick(TerrainType terrain, uint mapSeed, Vector2Int cell,
        int connectionMask, out TerrainPrefabVariant variant, out float yaw);
}

[Serializable]
public sealed class TerrainPrefabEntry
{
    public TerrainType Terrain;
    public TerrainScaleMode ScaleMode = TerrainScaleMode.FixedScale;
    public float FixedScale = 0.5f;
    public float MaxFootprint = 1f;
    public float GroundOffset;
    public List<TerrainPrefabVariant> Variants = new();
    public List<TerrainConnectionVariant> Connections = new();
}

[Serializable]
public sealed class TerrainPrefabVariant
{
    public GameObject Prefab;
    [Min(1)] public int Weight = 1;
    public float BaseYaw;
}
```

补充约束：

- 不使用原方案中的 `Vector3 tint`；需要颜色时使用 `Color`，但阶段 7 默认尊重 Prefab 原材质。
- `TryPick` 返回失败而不是抛异常；调用方进入 Sprite fallback。
- Catalog 校验重复 `TerrainType`、空 Prefab、非正权重、连接表缺项和不支持的 Collider。
- 阶段 6 的 `HandAuthoredMapData` 至少持久化 `LayerHeightScale` 与 `DefaultPrefabScale`（建议默认 0.5）。
- 单 Tile 继续保存 `Prefab`、`PrefabPath`、`RotationY`、`HeightOffset`、`Z` 和 `Category`。

---

## 5. 运行时职责

### `BattleGridController`

- 解析地图逻辑数据。
- 创建 `GridVisual` 和低位 `BaseGround`。
- 为每格创建 `BattleCellView`，传入地图来源、map seed 和视觉上下文。
- HandAuthored 模式下调用 HandMap 渲染器，并用统一所有权规则防止重复。
- 提供 `GetSurfaceY` / `GridToSurfaceWorld`。
- 构建完成后只保留必要引用，不每帧查找对象。

### `BattleCellView`

- TestMap/Procedural：Prefab-first，失败时 Sprite fallback。
- HandAuthored：默认不替换已有手作视觉；仅创建逻辑底板或缺失视觉 fallback。
- 清晰暴露 `VisualMode`，不要使用含义模糊的 `_lastIsPrefab`。
- `SetDecorationBase` 只影响 Sprite fallback，不能覆盖或旋转 3D 根节点。

### `HandMapVisualRenderer`

- 直接实例化所有有视觉的 HandPlacedTile，包括 Base、Path、Water、Mountain、Building、Decoration、Effect。
- 忠实应用原 Prefab、旋转、Z、HeightOffset 和持久化缩放。
- 不再使用按 Category 跳过 Base/Path/Water/Mountain 的白名单。
- 对每格建立视觉所有权：存在有效 HandPlacedTile 时，`BattleCellView` 不再为该格生成同层 3D Ground。
- Building/不可走语义仍由阶段 6 Provider 负责，Renderer 只负责视觉。

### `TerrainGeometryHelper`

- 统一关闭/移除 Collider、Rigidbody 和非必要物理组件。
- 统一层级设置，避免干扰地图/单位拾取。
- 按明确的 `TerrainScaleMode` 缩放和贴地。
- 不改 Prefab 资源本体，不修改共享材质。

---

## 6. 实施顺序

### P0 — 阶段 6 契约修订

1. 在阶段 6 文档同步修订：HandMap 渲染器保留原始 Ground Tile，不采用 Category 白名单删除视觉。
2. 将 `LayerHeightScale`、`DefaultPrefabScale` 持久化到 `HandAuthoredMapData`。
3. 确定每格视觉所有权规则与空格 fallback 规则。
4. 阶段 6 先完成逻辑映射、Prefab 回填和 2D fallback 下的运行时验证。

### P1 — 公共几何与高度基础设施

5. 新增 `TerrainGeometryHelper.cs`，拆分 Bounds、Scale、Ground、Physics 四类职责。
6. `TerrainDecorationSpawner` 迁移到 helper，但保持现有表现不变。
7. `BattleGridController` 增加统一地表高度查询。
8. 添加几何 helper 的 EditMode 测试。

### P2 — Procedural 3D Catalog

9. 新增 `TerrainPrefabCatalog.cs` 和 `.asset`。
10. 新增 Catalog 校验/初始化 Editor 工具。
11. 初始化工具只生成候选和校验报告；存在同名资源或语义不明时要求人工选择，不做 fuzzy 静默匹配。
12. 先接入 Plain，再逐种接入 Forest/Hill/Mountain/SnowMountain/Desert。
13. 每接入一种都保留 Sprite fallback，并完成确定性测试。

### P3 — HandAuthored 忠实渲染

14. 实现/修订 `HandMapVisualRenderer`，直接使用 `HandPlacedTile.Prefab`。
15. 应用与 Builder 一致的坐标、缩放、RotationY、Z 和 HeightOffset。
16. 实现 HandPlacedTile 与 BattleCellView 的视觉所有权，禁止双重 Ground。
17. 用包含自由旋转、Z 层、HeightOffset 和 Tile_Group 的地图做编辑器/运行时对照。

### P4 — Road、Bridge、Water

18. 在 Unity 中人工检查 Road/Bridge 候选 Prefab，形成完整连接映射表。
19. 只有完整覆盖 isolated/end/straight/corner/T/cross 后，Procedural Road 才切到 3D；否则继续 Sprite fallback。
20. 接入 Water Prefab，保持 Ground Opaque；不运行时修改共享材质。
21. 验证 Bridge 表面高度、连接方向和单位通过表现。

### P5 — 回归与性能

22. 完成 TestMap、Procedural、HandAuthored 三条路径回归。
23. 用 20x20 做功能基线，使用项目目标最大地图（至少包含 80x80 档）做压力测试。
24. 记录首次构建耗时、GameObject 数、Renderer 数、Batches、SetPass、CPU、内存与 GC。
25. 对比 Development Build 前后体积，记录实测差值，不预写估算结论。

---

## 7. 文件改动清单

### 新增

| 路径 | 作用 |
|---|---|
| `Assets/Scripts/Battle/Map/TerrainGeometryHelper.cs` | 几何 Bounds、缩放、贴地、物理清理 |
| `Assets/Scripts/Battle/Map/TerrainPrefabCatalog.cs` | Procedural/TestMap 3D Prefab 配置与确定性选择 |
| `Assets/Resources/Battle/Terrain/Generated/TerrainPrefabCatalog.asset` | Catalog 数据 |
| `Assets/Editor/HandMapBuilder/TerrainPrefabCatalogSetup.cs` | 候选生成、校验报告和显式写入 |
| `Assets/Editor/Battle/TerrainPrefabCatalogTests.cs` | Catalog 完整性与确定性测试 |
| `Assets/Editor/Battle/TerrainGeometryHelperTests.cs` | 缩放、贴地和物理清理测试 |

### 修改

| 路径 | 主要改动 |
|---|---|
| `Assets/Scripts/Battle/Map/BattleCellView.cs` | 视觉模式分支、3D Prefab、Sprite fallback、连接变体 |
| `Assets/Scripts/Battle/Map/BattleGridController.cs` | 视觉上下文、统一表面高度、HandMap 所有权、BaseGround 低位兜底 |
| `Assets/Scripts/Battle/Map/HandMapVisualRenderer.cs` | 原始 HandPlacedTile 忠实渲染，不按类别随机替换 |
| `Assets/Scripts/Battle/Map/Generation/HandAuthoredMapData.cs` | 持久化层高和默认 Prefab 缩放 |
| `Assets/Scripts/Battle/Map/Decorations/TerrainDecorationSpawner.cs` | 复用 helper，保持视觉行为 |

### 明确不修改

- `TerrainVisualCatalog.cs` 的 Sprite 职责保持独立，仅在确需复用小型连接辅助函数时做最小整理。
- `UnitView`、指挥官编队控制、HUD 和相机控制器不做视觉重构。

---

## 8. 验收标准

### 自动化测试

- [ ] 相同 Seed、坐标、TerrainType 和 CatalogVersion 永远选择相同变体。
- [ ] 变体选择不改变 `UnityEngine.Random.state`。
- [ ] Catalog 空条目、null Prefab、非法权重和 Road 连接缺项都有明确校验结果。
- [ ] HandAuthored 原 Prefab、RotationY、Z、HeightOffset 和缩放序列化后保持一致。
- [ ] `Tile_Group*` 不会因树木高度被整体压缩。
- [ ] 运行时地形实例不保留 Collider/Rigidbody，不参与地图点击和寻路。
- [ ] Sprite fallback 在 Catalog 缺失或 Hand Prefab 失效时正常出现。

### 视觉与运行时

- [ ] TestMap、Procedural、HandAuthored 均可进入 BattleScene，无 MissingReference 和异常日志。
- [ ] HandMapBuilder 与 BattleScene 对照时，手作 Tile 的具体 Prefab、旋转、层高和微调一致。
- [ ] HandAuthored 同一格不存在 HandMap Ground 与 Catalog Ground 重复渲染。
- [ ] Plain/Forest/Hill/Mountain/SnowMountain/Desert 能逐项启用或回退。
- [ ] Road 六类连接形态方向正确；未完成时明确走 Sprite fallback。
- [ ] Ground 保持 Opaque；Water 不穿帮、不污染共享材质、不明显遮挡单位。
- [ ] 单位、装饰和桥面与逻辑表面高度一致，不悬空或陷入地面。
- [ ] BaseGround 只作为低位背景，不穿出 3D Tile。
- [ ] 指挥官头像、单位拾取、地面点击和编队命令行为不因 Mesh 改造而改变。

### 性能与构建

- [ ] 20x20 地图记录功能基线。
- [ ] 至少 80x80 地图记录构建耗时和 Profiler 指标，无持续每帧分配。
- [ ] 地图视觉不在 Update/LateUpdate 中反复实例化或名称查找。
- [ ] Development Build 成功；Build size 增量使用前后实测值记录。
- [ ] 若目标最大地图无法满足性能门槛，阶段 7 不宣称完成；另立 static batching、GPU instancing、对象池或 chunk mesh 优化任务。

---

## 9. 回退策略

1. 某 TerrainType 无有效 Catalog 条目：只回退该类型 Sprite。
2. Road 连接表不完整：Road/Bridge 保持原 Sprite 连接图。
3. Water 透明表现不稳定：Water/Ocean 暂时回退 Sprite，不阻塞其他类型。
4. HandAuthored 某 Tile 引用失效：记录坐标、PrefabPath、Category 后按逻辑 Terrain fallback。
5. 3D 总开关关闭：TestMap/Procedural 全部走旧 `TerrainVisualCatalog`；HandAuthored 是否仍显示原 Prefab 由独立开关控制。

建议开发期配置：

```csharp
bool EnableProcedural3DTerrain = true;
bool EnableHandAuthoredVisuals = true;
```

两个开关只用于迁移和故障隔离，不作为长期玩家设置。

---

## 10. 执行与工具约束

- 推荐顺序仍为：先阶段 6，后阶段 7；不能在阶段 6 的视觉数据契约未修订前实现阶段 7。
- 当前只要求复用现有 3D 资源，因此文档和 C# 工作不触发 Blender MCP。
- 一旦需要生成或实质修改 Road、Water、Tile、模型或材质资产，必须使用 Blender MCP，完成检查、截图验证、保存源文件和 Unity 导出。
- Codex/ChatGPT 禁止配置 Unity MCP。需要通过 MCP 修改或验证 Unity 场景、Prefab、材质和 PlayMode 时，应交给配置了 Unity MCP 的 Claude；否则由开发者在 Unity Editor 中人工执行并记录结果。
- 每个阶段必须保持可编译、可进入 BattleScene、旧 Sprite fallback 可用。

---

## 11. 开工前最终确认

本修订版默认采用以下组合，不再要求逐项重复选择：

- Catalog：独立 `TerrainPrefabCatalog` ScriptableObject。
- HandAuthored：原始 Prefab 忠实渲染，不随机替换。
- Procedural：确定性 Catalog 选型。
- Road：HandAuthored 手放；Procedural 完整连接表，未完成则 Sprite fallback。
- Water：3D Prefab 优先，Opaque Ground + Transparent Water；不稳定则局部 fallback。
- 相机：保留 Orthographic。
- BaseGround：保留为低位全图兜底。
- 执行顺序：阶段 6 契约修订并验证后，再按 P1-P5 推进阶段 7。

只有需要改变上述默认组合时才再次请求用户决策。实施记录应写回本文件，区分“代码完成”“自动化验证通过”“Unity 人工/MCP 验证通过”，未实际验证的项目不得标记完成。

---

## 12. 实施记录

### 2026-09-05：第一批 3D 地形替换已完成

代码完成：

- [x] 新增 `TerrainPrefabCatalog`，支持 3D Prefab、权重、固定缩放、XZ footprint 缩放、地面偏移和连接表。
- [x] Procedural/TestMap 按地图 Seed、坐标、TerrainType 和 CatalogVersion 确定性选择 Prefab，不使用 `UnityEngine.Random`。
- [x] `BattleCellView` 改为 3D Prefab 优先，只有 Catalog/引用无效时才使用旧 Sprite fallback。
- [x] 新增 `TerrainGeometryHelper`，统一设置 Ignore Raycast、关闭 Collider/Rigidbody、缩放和贴地。
- [x] `BattleGridController` 默认启用 3D 地形，并从 Resources 自动载入 Catalog。
- [x] `TerrainDecorationSpawner` 会跳过已经自带树木/岩石的组合地块，避免重复装饰。
- [x] Catalog 已覆盖 Plain、Forest、Hill、Mountain、SnowMountain、Desert、ShallowWater、Ocean、Road、Bridge 共 10 种 TerrainType。
- [x] Road 当前使用 `Tile_1_brick_A` 作为通用 3D 路面；Bridge 使用 `Bridge1/Bridge2`。专用 corner/T/cross 模型确认后可继续填写连接表。
- [x] 旧 2D Sprite 仅保留为引用故障和禁用 3D 开关时的回退路径。

验证完成：

- [x] 新增脚本已显式加入编译输入验证，`Assembly-CSharp` 编译成功：0 warning、0 error。
- [x] Catalog 包含 10 个 TerrainType 条目和 20 个 Prefab 引用。
- [x] 20 个 Prefab GUID 均能在项目 `.meta` 中解析，无悬空引用。
- [x] 临时用于编译覆盖的 `.csproj` 修改已撤回。

待 Unity 验证：

- [ ] 打开 BattleScene，确认 10 种地形均显示为 3D 且比例、原点和朝向正确。
- [ ] 检查 Water 透明排序和 Bridge 贴地高度。
- [ ] 检查 Road 视觉；当前为通用 3D 砖路，连接形态仍需在 Unity 中确认和精修。
- [ ] 使用 20x20 与至少 80x80 地图记录性能指标。
- [x] 已接入 HandAuthored 数据：运行时直接实例化原始 HandPlacedTile，并保留旋转、Z 层、HeightOffset 和缩放。
- [ ] 在 Unity 中对照 Builder 与 BattleScene，完成最终所见即所得视觉验收。
