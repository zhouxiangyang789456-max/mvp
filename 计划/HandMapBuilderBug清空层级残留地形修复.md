# Bug 修复 — "清空层级还会有地形"

**文件**：`mvp/Assets/Editor/HandMapBuilder/HandMapBuilderWindow.cs`

## 症状

user 截图：3 个彩色 cube 残留在 Scene 视图；按钮显示 "清空所有层级 · 0个"、"清空当前层 (⇧⇧F) Z=0 · 0个"。

也就是说：`_mapData.Tiles.Count == 0`，但场景里还有 3 个 `HandMapTile_*` GameObject 残留 → user 报"老是出现"。

## 真因（多因叠加）

1. **Undo 通道不一致**
   - 创建：`EnsurePreviewFor` 用 `Undo.RegisterCreatedObjectUndo(instance, ...)`
   - 销毁：`ClearLevel / ClearAllLevels / DeleteSelection / EraseAt / FillLevel / ApplyGridSize / RemoveHighestZ` 全部用裸 `DestroyImmediate`
   - Undo 系统记录的"创建"无法对账到"销毁"，在 Undo 跨 group 操作或 redo 链上有概率让 instance 复活

2. **`_spawnedByCell` 字典 vs 场景状态可能错位**
   - 域重载：字典（`readonly` 字段）丢，scene 中 instance 留
   - 外部注入：用户手动拖 prefab 或外部脚本调 `InstantiatePrefab` 时字典不知道
   - `_spawnedByCell.TryGetValue` 拿不到这些 orphan → 无法清理

3. **没有兜底扫描机制**
   - 之前所有清理都基于字典，字典漏掉的 instance 永久残留

## 修复方案

### Part 1：所有销毁路径改走 Undo 通道（修第 1 个真因）

| 位置 | 改动 |
|------|------|
| `ApplyWidthAndHeight` line 330 | `DestroyImmediate` → `Undo.DestroyObjectImmediate` |
| `ClearSpawnedPreview`（按 clear call 间接调用）line 979 | 同上 |
| `DeleteSelection` line 1711 | 同上 |
| `EraseAt` line 1933、1965 | 同上 |
| `FillLevel` (overwrite 分支) line 2053 | 同上 |
| `RemoveHighestZ` line 1990 | 同上 |
| `ClearLevel` line 2118 | 同上 + 注释 |
| `ClearAllLevels` line 2166 | 同上 + 注释 |
| 顶层"清空所有层级"按钮 line 979 | 同上 |

### Part 2：兜底扫描清理（修第 2、3 个真因）

新增两个方法：

```csharp
void CleanOrphanedPreviews(int zFilter);   // -1 = 全层; >=0 = 该层
int  CountOrphanedPreviews(int zFilter);    // 按钮文字显示数量
```

**逻辑**：
1. 用 `Resources.FindObjectsOfTypeAll<GameObject>()` 拿所有 GameObject
2. 过滤 `EditorUtility.IsPersistent(go)` 跳过 prefab asset
3. 过滤 `!go.name.StartsWith("HandMapTile_")`
4. 从 name `HandMapTile_{x}_{y}_Z{z}` 解析坐标
5. 跟 `_mapData.Tiles` 比对：cell 在数据里 → 合法；不在 → 孤儿 → `Undo.DestroyObjectImmediate`

每个清理入口（ClearLevel / ClearAllLevels / ApplyGridSize / RemoveHighestZ / DeleteSelection / EraseAt / 顶层按钮）都加一道 `CleanOrphanedPreviews(...)` 调用。

### Part 3：UI 兜底按钮

主面板 "擦除模式" / "清空所有层级" 旁边新增 **"🧹 强制清理残留"** 按钮：

- `CountOrphanedPreviews(-1)` 实时显示孤儿数量
- 数量 = 0 时按钮 disable（灰掉，不误导点）
- 按下时只清 instance，不动数据

## 修改清单

- `HandMapBuilderWindow.cs`：~50 行新增 / 改动
  - 7 处 `DestroyImmediate` → `Undo.DestroyObjectImmediate`
  - 新增 `CleanOrphanedPreviews` 方法（~50 行）
  - 新增 `CountOrphanedPreviews` 方法（~45 行）
  - 新增"强制清理残留"按钮（~15 行）
  - 7 处加 `CleanOrphanedPreviews(...)` 兜底调用

## 验证步骤

1. Unity 重新编译（应该自动）
2. 进 HandMapBuilder（Tools/Map Generation/Hand Map Builder）
3. 测试场景：
   - 在地图上放置几个 tile
   - 点 "清空当前层（⌃⇧F）" → Scene 应该立即清干净
   - 点 "清空所有层级"（红色按钮） → Scene 应该立即清干净
   - 如果还有残留（极端情况），点 "🧹 强制清理残留 (N)" → 应该清掉

## 教训

- **Undo 通道成对原则**：注册用 `Undo.RegisterCreatedObjectUndo` → 销毁必须用 `Undo.DestroyObjectImmediate`，不能混用裸 API
- **关键状态字典 vs 真实场景状态** 应加"扫描场景对比"兜底机制，不能只信内存字典
- **`Resources.FindObjectsOfTypeAll`** 配合 `EditorUtility.IsPersistent` 是 Editor 下扫描场景 GameObject 的标准做法
