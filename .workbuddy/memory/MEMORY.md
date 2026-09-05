# Workspace Memory — Mvp Project

## 项目结构
- Unity 2022.3 LTS，Built-in 渲染管线
- 工作区根目录：`D:\prounity\mvp\mvp\`
- Isometric Pack 3d v1.2 资源：`Assets\Isometric Pack 3d\`（MK4Toon shaders 兼容 Built-in）

## 长期工具集

### HandMapBuilder（`Assets/Editor/HandMapBuilder/HandMapBuilderWindow.cs`）
- 菜单：`Tools/Map Generation/Hand Map Builder`
- 数据：`HandAuthoredMapData` (ScriptableObject) 位于 `Assets/Scripts/Battle/Map/Generation/`
- 11 类别调色板（`HandMapPalette.cs`）：Base/Path/Forest/Plant/Water/Ramp/Bridge/Mountain/Building/Decoration/Effect
- 手动层级（最多 10 层，用户驱动）；用户保留层级用 `_userReservedMaxZ` 跟踪，避免 OnGUI 同步逻辑覆盖
- 旋转（4 方向吸附 + 任意角）+ HeightOffset（单 tile 微调）+ LayerHeightScale（全局层高缩放）
- 添加层级自动 refocus 相机 + 闪烁 2 秒 + Z 轴引导线
- 笔刷：1×1 / 3×3 圆 / 5×5 圆，B 循环
- 快捷键：E=擦除 B=笔刷 F=填充 ⇧F=覆盖 ⌃⇧F=清空当前层 R=+90° ⇧R=-90° H=+0.1 ⇧H=-0.1 [/]=翻页 +/=层级 - 降层
- Shift+左键 = 选中 tile → Inspector 调整 HeightOffset
- **阶段 5**：尺寸 pending+应用按钮 / 框选（拖 ≥2 格自动）/ 复制按钮→待粘贴态→Ghost preview→Space/Enter/单击实贴
- 5 状态机：`Idle / BoxSelecting / BoxSelected / ReadyToCopy`（enum BuilderState）
- 快捷键 ⏎（Enter）=进入复制态 / Space=实贴 / Esc=退出 / Delete=删选区
- 复制组结构 `CopyGroupEntry {x,y,z,prefabPath,rotY,hOff}` 相对原组最小 X/Y

### 重要教训
- **OnGUI 末尾的"数据 → 状态"同步逻辑** 只增不减；用户主动设置的状态不能被"扫描"覆盖
- **SceneView 内的动画**必须用 `SceneView.lastActiveSceneView.Repaint()` 才能持续刷新，裸 `Repaint()` 只重绘当前 EditorWindow
- **磁盘 vs 内存**：工具的 `_mapData` 在内存里改了但没保存 → 下次打开磁盘数据回来；大改动后要点保存按钮
- **SceneView 相机控制**：`HandleUtility.AddDefaultControl()` 会强制接管所有未消费事件 → SceneView orbit/pan 完全失灵。**正确做法**：不调用它，只在 `OnSceneGUI` 用 `e.Use()` 精确消费关心的左键/右键 MouseDown。Unity 默认相机操作：**Alt+左键拖动 = orbit** / 中键拖动 = pan / 滚轮 = zoom
- **焦点态保留**：`FocusSceneViewOnLevel(z)` 只改 `pivot.y`，**不要覆盖 size/rotation/orthographic** —— 用户原本视角必须保留

### BattleMapProvider 集成
- `LevelMapGenerationProfile.HandMapOverride` 字段（**待阶段 5 实施**）
- `ProceduralBattleMapProvider` 已支持 HandMapOverride 读取（待验证）

## 已修复的 Meta 问题
- 不能手写 `.cs.meta` — Unity YAML 解析失败。让 Unity 自动生成新 GUID
- 必要时删 meta 文件 → Unity 启动时重建（代价：.asset 引用断开需重新指定脚本）

## 用户沟通偏好
- 中文回复
- "先计划 → 再执行 → 后文档" 工作流
- 必给 Markdown 格式文档
- 截图/截图 + Console log 比纯文字更有效
