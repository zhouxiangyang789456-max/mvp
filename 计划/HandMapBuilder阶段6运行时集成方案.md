# HandMapBuilder 阶段 6 — 运行时集成与关卡配置

> 阶段 1-5 完成：HandMapBuilder 编辑器工具（IMGUI 手作 3D 等距地图）。
> 阶段 6 目标：让创作好的地图数据 **真正应用到 BattleScene**，支持按关卡配置不同的 HandMap。

---

## 1. 现状摘要（来自探索报告 + 实读代码）

| 项目 | 状态 | 说明 |
|------|------|------|
| `LevelMapGenerationProfile.HandMapOverride` | **未实现** | 注释里提到，但字段没声明 |
| `ProceduralBattleMapProvider` | 只走程序化 | `static` 类，纯 Perlin+规则生成，**完全不读 HandAuthoredMapData** |
| `BattleGridController._mapSource` | 只有 TestMap / Procedural | enum 缺 `HandAuthored` |
| `BattleGridController.ResolveMap()` | **唯一需要打补丁的入口** | 已经按优先级串联了 PendingRequest / Profile / InlineSettings |
| `HandPlacedTile.PrefabPath` | **Editor-only string** | 存 `Assets/Isometric Pack 3d/...`，运行时 `Resources.Load` 加载不到 |
| 跨场景手递手 | **已就绪** | `BattleStartContext.MapProfile` + `LevelIndex` |
| `BattleMapContext.LastGeneratedData` | 已就绪 | 被 `TerrainDecorationSpawner.Build(this, data)` 消费 |
| `TerrainType` | 10 种 | Plain/Forest/Hill/Mountain/SnowMountain/Desert/ShallowWater/Ocean/Road/Bridge |
| 现成桥接 Provider | **缺失** | `IBattleMapProvider` / `HandMapBattleMapProvider` 全无 |

**最小工作量估算**：~280 行新增 + 3 处文件改动 + 1 个回填工具 + 1 个新 enum 值。

---

## 2. 决策点（请你选）

### 决策 1 — Prefab 引用方式（最关键）

> 运行时拿不到 `Assets/Isometric Pack 3d/...` 这种编辑器路径的 Prefab，必须改成运行时可加载的形式。

| 选项 | 工作量 | 说明 |
|------|--------|------|
| **A. 直接持 `GameObject` 引用**（**推荐**） | 最小 | `string PrefabPath` → `[SerializeField] GameObject Prefab`，运行时 `Instantiate(tile.Prefab)`。**需写一个一次性回填脚本**：遍历所有 `HandAuthoredMapData` 用 `AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)` 把 Prefab 填好，再清空旧 PrefabPath 字段。 |
| B. 搬到 `Resources/HandMap/...` | 中等 | 美术资源重组织、HandMapPalette 改路径；运行时走 `Resources.Load<GameObject>(prefabPath)`，但要保持两份 Palette（Editor / Runtime）。 |
| C. 混合（保留 string + Editor helper） | 中等 | 运行时持有 Asset 引用，编辑器再加一个"按名称查 Prefab"的回退。代码路径较多。 |

**推荐 A**：Unity 惯例、运行时最稳、回填一次性完成。**遗留代价**：现存 .asset 里 `PrefabPath` 字段保留（兼容旧值），新字段 `Prefab` 加在后面；`HandMapBuilderWindow.BuildModel` 优先用 `Prefab`，缺失时退回 `AssetDatabase.LoadAssetAtPath<PrefabPath>` 兜底。

---

### 决策 2 — 是否抽 `IBattleMapProvider` 接口

| 选项 | 工作量 | 说明 |
|------|--------|------|
| **A. 抽接口**（**推荐**，长期更可扩展） | +50 行 | `IBattleMapProvider.BuildMap(BattleMapRequest, out data, out identity)`，让 `Procedural` 和 `HandMap` 都实现，将来 Hybrid / 自定义 Provider 直接接入。 |
| B. 不抽接口 | 最小 | `BattleGridController.ResolveMap()` 里 `if (profile.HandMapOverride != null) { return HandAuthoredStore(profile.HandMapOverride); }` 一个分支搞定。 |

**推荐 A**：阶段 6 后还会加 Hybrid / 玩家自定义地图之类的需求，接口一次抽好，永久受益。

---

### 决策 3 — 关卡配置粒度

> 用户原话："**可以配置对应关卡最好**"。即：不同关卡能配不同 HandMap。

| 选项 | 工作量 | 说明 |
|------|--------|------|
| **A. `LevelMapGenerationProfile` 加 1 个 `HandMapOverride` 字段**（**推荐**） | 最小 | 每张 HandMap 是独立 SO，Profile 跨多关卡可共享一份 HandMap。不同关卡 → 不同 Profile → 不同 HandMap。**目前项目无 Level ScriptableObject 系统，按"按 Profile 分"已经满足需求。** |
| B. 新增 `HandMapLevelConfig` ScriptableObject（每关一份） | 大 | 需先有 Level ScriptableObject 系统支撑，目前没有；过度设计。 |
| C. 混合：在 `LevelMapGenerationRule` 上加 `HandMapOverride` | 中等 | 同 Profile 可为不同关卡配不同 HandMap（最细粒度）。 |

**推荐 A**：当前 `CommanderSelectController` 能且只能选一个 `LevelMapGenerationProfile`，**多 Profile 多 HandMap** 已经天然支持"配置对应关卡"。如果以后需要"同 Profile 不同关卡不同 HandMap"，升级到 C 是无痛的（profile + rule 都查同一个字段即可）。

---

### 决策 4 — `HandTileCategory` → `TerrainType` 语义映射

> 编辑器有 12 类，运行时 `TerrainType` 只有 10 种。Building 这种在 Z=0 是 Plain（可走）还是阻挡？这套规则必须先定。

**默认映射规则**（用户可覆盖）：

| HandTileCategory (Z=0) | 目标 TerrainType | walkable | 备注 |
|------------------------|------------------|----------|------|
| Base | Plain | ✅ | 默认底 |
| Path | Road | ✅ | 兼容 `BuildRoadMask` 自动连路 |
| Forest | Forest | ✅ | |
| Plant | Plain | ✅ | 视觉装饰，不影响走位 |
| Water | ShallowWater | ✅ | 默认浅水；可配 Ocean |
| Ramp | Plain | ✅ | 过渡格，必须可走 |
| Bridge | Bridge | ✅ | 跨水连通 |
| Mountain | Mountain | ✅ | 可走的山 |
| Building | Plain | ❌ | **视觉站位 + `SetBlocked` 单格阻挡** |
| Decoration | Plain | ✅ | 纯视觉 |
| Effect | Plain | ✅ | 纯视觉特效 |
| Erase | Plain | ✅ | 用户清过的格子 |

**优先级规则**（同格多 tile）：
1. **Building** 永远占优（不可走的最高优先级）
2. **Ocean/雪山级** 阻挡其次
3. **Path / Bridge** > Forest / Mountain > Plain（决定 BuildRoadMask 输入）
4. 其它按 z 升序覆盖

**推荐**：先用这套默认规则上线，编辑器里显示一个"运行时映射"Tooltip 让用户心里有数。

---

### 决策 5 — Z 层运行时渲染策略

> HandMapBuilder 支持 Z=0..9 堆叠，建筑、桥、塔楼有不同高度。

| 选项 | 视觉 | 阻挡 |
|------|------|------|
| A. 只渲染 Z=0 | 仅地面格 | 仅 Z=0 走位 |
| **B. 所有 Z 都按 Y 偏移渲染**（**推荐**） | 完整保留美术 | 仅 Z=0 走位（不影响 pathfinding） |
| C. B + Z>=1 阻挡 | 完整保留 | Z>=1 tile 占地阻挡（更复杂） |

**推荐 B**：用户已经花时间精调视角和高度，运行时别浪费这层工作；但走位不引入 Z 维度是因为现有 `IsWalkable(Vector2Int)` 是 2D 静态查询，不动它保持兼容。

实现：渲染时 `position.y = Z * LayerHeightScale + HeightOffset`；用 `BattleGridController.transform` 下的一个 `HandMapVisuals` 子节点管理所有 instantiate 的实例。

---

### 决策 6 — HandMap vs Procedural 切换优先级

`BattleGridController.ResolveMap()` 当前优先级（从高到低）：
1. `_mapSource == TestMap` → 硬编码测试地图
2. `BattleMapContext.PendingRequest`（关卡队列的 one-shot）
3. `_useAppliedToolSettings`（编辑器工具写入的）
4. `profile.BuildRequest(level)`（profile + level 路径）

**新规则（推荐）**：在第 4 步之前插一个：
```
if (profile != null && profile.HandMapOverride != null) {
    if (_mapSource == BattleMapSource.Procedural || _mapSource == BattleMapSource.HandAuthored || has_hand_override)
        return HandAuthoredStore(profile.HandMapOverride);
}
```

更简化的方案：扩展 enum
```csharp
public enum BattleMapSource {
    TestMap,
    Procedural,
    HandAuthored    // 新增
}
```
- `HandAuthored` + 有 HandMapOverride → 走 HandMap
- `HandAuthored` + 无 HandMapOverride → Debug.LogError 回退到 Procedural
- `Procedural` + 有 HandMapOverride → 警告"Profile 有 HandMapOverride 但 _mapSource 是 Procedural，按 Profile 字段静默忽略"

**推荐**：用 enum 新增值（更显式，更可控），并扩展 `BattleStartContext` 加 `BattleMapSource? MapSourceOverride` 让 CommanderSelectScene 也能选。

---

### 决策 7 — 回填已有 .asset

> 现存 `HandMap_New.asset` 已在使用，需要回填 GameObject 引用。

| 选项 | 说明 |
|------|------|
| **A. Editor 工具**（**推荐**） | `Tools/HandMapBuilder/Refill Prefab References`，遍历所有 `HandAuthoredMapData`，对每条 tile 用 `AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)` 填 `Prefab` 字段，`SetDirty + SaveAssets`。一次性操作。 |
| B. 改 `HandMapBuilderWindow.BuildModel` 自动回填 | 用户每次 BuildModel 时顺手回填一条。简单但不全（可能漏掉编辑器外被直接打开的 .asset）。 |

**推荐 A**：一次性 Editor 工具，跨所有 asset，安全。

---

## 3. 实施方案（基于上述推荐组合）

### 3.1 新增文件

| 路径 | 角色 | 行数 |
|------|------|------|
| `Assets/Scripts/Battle/Map/Generation/IBattleMapProvider.cs` | 接口 | ~30 |
| `Assets/Scripts/Battle/Map/Generation/HandMapBattleMapProvider.cs` | HandMap Provider 实现 | ~150 |
| `Assets/Scripts/Battle/Map/HandMapVisualRenderer.cs` | 运行时实例化 Prefab + Y 偏移 | ~120 |
| `Assets/Editor/HandMapBuilder/HandMapPrefabRefillTool.cs` | 回填 Editor 工具 | ~80 |
| `Assets/Editor/HandMapBuilder/HandMapRuntimeMappingWindow.cs` | 可选：可视化"Hand→Terrain"映射验证器 | ~100 |

### 3.2 改动文件

| 路径 | 改动 | 行数 |
|------|------|------|
| `Assets/Scripts/Battle/Map/Generation/LevelMapGenerationProfile.cs` | 加 1 字段 `HandMapOverride` | +5 |
| `Assets/Scripts/Battle/Map/Generation/HandAuthoredMapData.cs` | `HandPlacedTile.PrefabPath` 旁加 `Prefab` 字段（直接 `[SerializeField] GameObject`，兼容旧 `PrefabPath`） | +3 |
| `Assets/Editor/HandMapBuilder/HandMapBuilderWindow.cs` | `BuildModel` 写入 Prefab 字段；新增"运行时映射"小节显示当前 tile 的 TerrainType 推断；palette tooltips 说明运行时映射 | +40 |
| `Assets/Scripts/Battle/Map/BattleGridController.cs` | `BattleMapSource` 加 `HandAuthored`；`ResolveMap()` 插分支；`BuildVisual()` 加调用 `HandMapVisualRenderer.Render(...)` | +30 |
| `Assets/Scripts/Shared/BattleStartContext.cs` | 加 `BattleMapSource? MapSourceOverride` | +2 |
| `Assets/Scripts/Battle/Map/Generation/BattleMapContext.cs` | 加 `HandAuthoredMapData LastHandMapData` | +1 |
| `Assets/Scripts/Battle/Map/TerrainCatalog.cs` | 加 helper `TerrainType ResolveFromHandCategory(HandTileCategory, bool isBuildingOrOcean)` | +25 |

**总改动 ≈ +280 行新 + ~110 处增改**。

---

## 4. 详细执行计划（按依赖序）

### P0 — 通用基础设施（不改任何业务）
1. `BattleMapSource` enum 加 `HandAuthored` 值
2. `BattleStartContext` 加 `MapSourceOverride` 字段
3. `BattleMapContext` 加 `LastHandMapData`
4. `IBattleMapProvider` 接口
5. `ProceduralBattleMapProvider` 适配 `IBattleMapProvider`（或保留 static class，方法层加一个轻量 wrapper）

### P1 — 数据层
6. `HandPlacedTile` 加 `[SerializeField] GameObject Prefab`（保留 `string PrefabPath` 旧字段，仅作回填用）
7. `LevelMapGenerationProfile` 加 `public HandAuthoredMapData HandMapOverride;`
8. `HandMapPrefabRefillTool` Editor 脚本（遍历所有 HandMap，Prefill Prefab via `AssetDatabase.LoadAssetAtPath`）
9. `HandMapBuilderWindow.BuildModel` 优先用 tile.Prefab，没有再用 PrefabPath 兜底

### P2 — 运行时接入
10. `HandMapBattleMapProvider` 实现 `IBattleMapProvider.BuildMap(...)`：遍历 tile 推 TerrainType → 检查多 tile 同格优先级 → 填 TerrainType[,] → 调 `TerrainCatalog.ResolveFromHandCategory`
11. `TerrainCatalog.ResolveFromHandCategory(...)` helper（决策 4 的映射表）
12. `BattleGridController.ResolveMap()` 新优先级链（图示见 §5）
13. `HandMapVisualRenderer.Render(HandAuthoredMapData, grid)`：实例化所有 tile.Prefab 到子节点 `HandMapVisuals`，Y 偏移 = `Z * LayerHeightScale + HeightOffset`，rotation 套用 tile.RotationY

### P3 — 验证
14. `HandMapRuntimeMappingWindow`（可选）：可视化编辑器内的"Hand 类别 → 运行时 TerrainType"映射
15. 跑场景：建一份 `HandMap_Demo.asset`（20×20 全 Plain + 一些 Forest/Path/Building）→ 拖到 `LevelMapGenerationProfile.HandMapOverride` → 切 `BattleMapSource.HandAuthored` → 进入 BattleScene 验证
16. 测 5 场景：① 全 Base ② 含 Path → 检 BuildRoadMask 输出 ③ 含 Building → 检 SetBlocked ④ 含 Water/Ocean ⑤ 含 Mountain → 检 walkability

---

## 5. 新的 `BattleGridController.ResolveMap()` 优先级图

```
MapSource == TestMap ?
├── yes → TestBattleMapData.Create()        [现有]
└── no ↓
   BattleMapContext.PendingRequest ?
   ├── yes → 一律走 PendingRequest         [现有：可加 HandMapRequest 类型]
   └── no ↓
      _useAppliedToolSettings ?
      ├── yes → BuildDefaultRequest()      [现有]
      └── no ↓
         BattleStartContext.MapSourceOverride ?? _mapSource ?? BattleMapSource.Procedural
         │
         ├─ HandAuthored + profile.HandMapOverride != null
         │   → HandMapBattleMapProvider.BuildMap(HandMap, ...)
         │   → BattleMapContext.LastHandMapData = HandMap
         │
         ├─ HandAuthored + profile.HandMapOverride == null
         │   → Debug.LogError, fallback to Procedural
         │   → ProceduralBattleMapProvider.BuildMap(BuildRequest(level), ...)
         │
         └─ Procedural + profile != null
             → ProceduralBattleMapProvider.BuildMap(profile.BuildRequest(level), ...)
             → 静默忽略 profile.HandMapOverride（Console.warning）
```

`BuildVisual()` 末尾追加：
```csharp
if (BattleMapContext.LastHandMapData != null)
    HandMapVisualRenderer.Render(BattleMapContext.LastHandMapData, _width, _height, transform);
```

---

## 6. HandTileCategory → TerrainType 默认映射表（决策 4 落地）

| Hand | Z=0 Terrain | Blocked | 备注 |
|------|-------------|---------|------|
| Base | Plain | no | |
| Path | Road | no | 走 BuildRoadMask |
| Forest | Forest | no | |
| Plant | Plain | no | 视觉 |
| Water (default) | ShallowWater | no | 可配 Ocean |
| Water (with override) | Ocean | yes | |
| Ramp | Plain | no | 必走 |
| Bridge | Bridge | no | 跨水 |
| Mountain | Mountain | no | |
| Building | Plain | **yes** | 自动 SetBlocked |
| Decoration | Plain | no | 视觉 |
| Effect | Plain | no | 视觉特效 |
| Erase | Plain | no | |

多 tile 同格冲突（按下列顺序后者吃前者）：
```
Building > Ocean/SnowMountain > Mountain > Forest > Road/Bridge > Plain
```

---

## 7. 验证 Checklist（实施完毕后逐条过）

### 编辑器
- [ ] `Tools/HandMapBuilder/Refill Prefab References` 一键执行，现有 `HandMap_New.asset` 所有 tile 的 Prefab 都非 null
- [ ] `[CreateAssetMenu]` 给 `HandMap_New` 拖到 `LevelMapGenerationProfile.HandMapOverride` 不报警
- [ ] HandMapBuilderWindow 显示"运行时映射"辅助信息（X = Path → Road 等）

### 运行时（PlayMode）
- [ ] `BattleMapSource = HandAuthored` + 有 HandMapOverride → 进 BattleScene 看到手作的 prefab 实例化，Y 偏移正确
- [ ] 同上但 HandMapOverride 为空 → Console 报错，回退到 Procedural 不崩
- [ ] `BattleMapSource = Procedural` 但 profile.HandMapOverride 非空 → Console 警告，静默忽略
- [ ] `Procedural`（默认）行为不变（回归）
- [ ] `TestMap` 行为不变（回归）

### 走位与视觉
- [ ] Building 占据的格子 `IsWalkable == false`
- [ ] Path 接 Path 处显示正确的路连接（BuildRoadMask 输出非 0）
- [ ] Ocean 格子 `IsWalkable == false`
- [ ] Mountain / Forest / Plain 全部可走
- [ ] Z>=1 的视觉 Y 偏移不与 BattleGridController 其它 visual 子节点重叠（错层堆叠）

### 关卡切换
- [ ] CommanderSelectController 切到不同 `_mapProfile`（各自有不同 HandMapOverride）→ 进入 BattleScene 用不同地图

---

## 8. 风险与边界

| 风险 | 缓解 |
|------|------|
| Prefab 实例化数量爆炸（200×200 = 40000 GameObject） | BattleGridController 已用 `perf rule: no colliders`；新加的 HandMapVisualRenderer 实例化时 `static batching`，无 collider。给一个"DOTS/合批"备注留待后续 profile。 |
| `PrefabPath` → `Prefab` 回填遗漏 | 回填工具扫所有 `HandAuthoredMapData` 资产，扫不到路径时 Console LogError 但不 throw。 |
| `ProceduralBattleMapProvider` 是 static class 转接口需要改签名 | 不改它，新增 `ProceduralBattleMapProviderAdapter : IBattleMapProvider` 包装。或者只在 `ResolveMap()` 里直接调 static 方法，接口只用在新加的 `HandMapBattleMapProvider` 上。 |
| HandMap 全空（Tiles.Count == 0） | Provider 自动 fallback 到全 Plain TerrainType[,] + 渲染 0 个 prefab，不崩。 |
| `LayerHeightScale` / `HeightOffset` 字段在哪？ | HandMapBuilder 阶段4 的全局 `LayerHeightScale` 在 `HandMapBuilderWindow` 私有；需要把这两个字段提到 `HandAuthoredMapData` 上（或单独的 `HandMapRenderingSettings`），运行时持久化。 |

---

## 9. 用户确认项

请告诉我下面三件事再开始写代码：

1. **决策 1（Prefab 引用方式）** 选哪个？
   - A. 直接持 `GameObject` 引用（推荐）
   - B. 搬到 Resources/
   - C. 混合

2. **决策 5（Z 层渲染）** 选哪个？
   - A. 仅 Z=0
   - B. 所有 Z 按 Y 偏移（推荐）
   - C. B + Z>=1 阻挡

3. **决策 6（切换优先级）** 选哪个？
   - 走 `BattleMapSource.HandAuthored` enum 新增值（推荐）
   - 走 `profile.HandMapOverride` 存在就强制用
   - 两者结合

确认后我开始 P0 → P3 顺序写代码，并按你的习惯先在 plan 文件里更新实施进度。

---

## 10. 实施记录（2026-09-05）

本阶段已按“编辑器保存原 Prefab，运行时忠实实例化”的方向完成首轮开发。

### 已完成

- [x] `HandPlacedTile` 增加运行时 `GameObject Prefab` 引用，继续保留 `PrefabPath` 兼容旧地图。
- [x] `HandAuthoredMapData` 持久化 `LayerHeightScale` 与 `DefaultPrefabScale`。
- [x] HandMapBuilder 单格绘制、填充和复制粘贴都会写入 Prefab 引用。
- [x] 保存地图时自动回填旧 Tile 的 Prefab 引用。
- [x] 增加 `Tools/HandMapBuilder/Refill Runtime Prefab References`，可批量迁移已有 HandMap 资产。
- [x] 地图工具增加“关卡配置”字段和“应用到关卡”按钮。
- [x] `LevelMapGenerationProfile` 增加 `HandMapOverride`。
- [x] `BattleMapSource` 增加 `HandAuthored`，并支持场景内直接指定 HandMap。
- [x] Procedural 来源遇到当前 Profile 已配置 HandMap 时自动加载 HandMap，满足从指挥官选择页进入战斗的正常流程。
- [x] `HandMapBattleMapProvider` 将 Base/Path/Forest/Water/Bridge/Mountain 等类别转换为逻辑 `TerrainType[,]`。
- [x] Building Tile 写入 BattleGrid 的 blocked/occupied 集合，单位不能进入建筑格。
- [x] `HandMapVisualRenderer` 实例化所有层的原始 Prefab，并应用 RotationY、Z 层高、HeightOffset 和 Builder 缩放。
- [x] 有手作 Ground 的格子不再生成 Catalog Ground，避免双重渲染。
- [x] 运行时关闭 HandMap Prefab 的 Collider/Rigidbody，并放到 Ignore Raycast 层，避免破坏地面点击和寻路。
- [x] HandAuthored 模式不再运行随机 DecorationSpawner，保持手作地图内容一致。
- [x] 运行时与编辑器程序集完整编译通过：0 warning、0 error。

### 实际使用流程

1. 在 `Tools > Map Generation > Hand Map Builder` 中创建或选择地图数据。
2. 完成绘制后点击“保存”；保存会自动补齐运行时 Prefab 引用。
3. 在新增的“关卡配置”字段中拖入该关卡使用的 `LevelMapGenerationProfile`。
4. 点击“应用到关卡”。
5. 从指挥官选择页进入该关卡，BattleScene 会读取 Profile 的 `HandMapOverride` 并加载手作地图。
6. 直接打开 BattleScene 调试时，也可以把 `_mapSource` 设为 `HandAuthored`，再把地图拖入 `_handMapOverride`。

### 待 Unity 视觉验证

- [ ] Builder 与 BattleScene 中 Prefab 位置、缩放、RotationY、Z 和 HeightOffset 一致。
- [ ] Building 阻挡、Path/Bridge 通行以及 Water/Mountain 地形语义符合地图设计。
- [ ] 大地图首次实例化性能满足目标；若 80x80 以上出现明显卡顿，再按 Chunk/合批方案优化。
- [ ] 场景切换后使用的 Profile 和 HandMap 与目标关卡一致。
