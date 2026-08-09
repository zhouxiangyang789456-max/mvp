# UI 还原与验证流程

## 目标

本项目 UI 不能只做到“功能可用”，而要尽量贴近现有样例图。为了避免 UI 在开发过程中逐渐偏离参考，需要建立一套固定流程：先用样例图校准，再组件化制作，最后用截图和检查清单持续验证。

适用页面：

- 指挥官选择页面。
- 战斗页面。
- 后续商店、部队、卡牌等页面。

## 核心原则

### 样例图是尺子

每个页面先选择一张主参考图：

- 指挥官选择页：`D:/prounity/mvp/指挥官选择页面/ui演示.png`
- 战斗页：`D:/prounity/mvp/战斗页面/ui展示.png`
- 战斗 UI 组件：`D:/prounity/mvp/战斗页面/战斗ui.png`
- 指挥官详情组件：`D:/prounity/mvp/战斗页面/指挥官详情.png`

制作时先把参考图放入 Unity 作为半透明对齐层，UI 完成后隐藏，但不删除。

### 先还原布局，再接功能

开发顺序：

1. 背景。
2. 大面板位置。
3. 按钮和卡槽。
4. 图标、头像、血条。
5. 文本。
6. 交互状态。
7. 数据绑定。

不要一开始就先写复杂功能，否则 UI 很容易变成“能用但不像”。

### 组件必须复用

深蓝面板、金色边框、卡槽、按钮、血条、Tooltip 都要做成 prefab。不能每个地方临时拼一个，否则后期会出现风格不统一、边距不一致、缩放不同步的问题。

## Unity UI 技术方案

### UI 框架

第一版使用：

- UGUI。
- TextMeshPro。
- Canvas Scaler。
- Sprite 9-slice。
- Prefab Variant。

推荐 Canvas 设置：

```text
Canvas Render Mode: Screen Space - Overlay
Canvas Scaler:
  UI Scale Mode: Scale With Screen Size
  Reference Resolution: 1600 x 900
  Screen Match Mode: Match Width Or Height
  Match: 0.5
```

第一版固定使用 `Screen Space - Overlay`。世界空间单位血条和状态条单独使用跟随单位的 World Space Canvas 或 Billboard，不和主战斗 UI 混在同一个 Canvas 中。

### 分辨率策略

第一阶段优先保证 16:9：

- `1600x900`：主制作分辨率。
- `1920x1080`：高清验证。
- `1366x768`：低分辨率验证。

暂不优先适配手机竖屏或超宽屏。后续如果需要，再单独做响应式布局。

## 参考图叠底流程

每个页面创建一个隐藏参考层：

```text
Canvas
├── ReferenceOverlay
│   └── ReferenceImage
├── RealUI
└── DebugLayer
```

`ReferenceOverlay` 设置：

```text
Alpha: 0.35 - 0.55
Raycast Target: false
Visible In Build: false
Editor Only: true
```

制作步骤：

1. 将样例图导入 Unity。
2. 设置为 Sprite。
3. 放入 `ReferenceImage`。
4. 拉伸到参考分辨率全屏。
5. 将真实 UI 组件覆盖到对应位置。
6. 反复切换参考层显示/隐藏。
7. 完成后默认隐藏参考层。

## 组件规范

### 金色边框面板

Prefab：

```text
Assets/Prefabs/UI/Common/FramedPanel.prefab
```

要求：

- 深蓝底。
- 金色边框。
- 四角角花可单独控制。
- 中间区域可拉伸。
- 边框使用 9-slice，避免变形。

### 按钮

Prefab：

```text
Assets/Prefabs/UI/Common/GoldButton.prefab
```

状态：

- Normal。
- Hover。
- Pressed。
- Selected。
- Disabled。

要求：

- 尺寸接近样例图。
- 文本为金色或浅黄色。
- 选中态有更亮边框或内发光。
- Disabled 态降低透明度，不改变布局尺寸。

### 卡牌槽

Prefab：

```text
Assets/Prefabs/UI/Battle/CardSlot.prefab
```

要求：

- 统一宽高。
- 统一边框。
- 左上角有小徽章位置。
- 底部有数量文本位置。
- 空槽也必须保留完整边框。

### 指挥官面板

Prefab：

```text
Assets/Prefabs/UI/Battle/CommanderPanel.prefab
```

要求：

- 头像区域、名称、生命、血条、特性图标位置固定。
- 点击头像进入阵型部署模式。
- 生命条长度和参考图接近。
- 四个特性图标等距排列。

### 小地图面板

Prefab：

```text
Assets/Prefabs/UI/Battle/MiniMapPanel.prefab
```

要求：

- 右下角固定。
- 小地图边框还原金色装饰。
- 放大按钮贴近样例位置。
- 第一版使用静态图。

### Tooltip

Prefab：

```text
Assets/Prefabs/UI/Common/TooltipPanel.prefab
```

要求：

- 半透明深色背景。
- 信息分行显示。
- 不遮挡鼠标目标。
- 屏幕边缘自动翻转位置。

## 页面验收清单

### 指挥官选择页面

视觉检查：

- 背景与 `ui演示.png` 一致或接近。
- 左上返回按钮位置正确。
- 标题居中。
- 左侧三块摘要面板垂直排列。
- 中央详情面板大小和位置接近样例。
- 底部 6 个卡槽等距排列。
- 右下“出征”按钮位置正确。

交互检查：

- 未选择时出征不可用。
- 点击伊莲娜卡牌后卡牌高亮。
- 摘要信息更新。
- 详情信息更新。
- 点击出征进入战斗页。

### 战斗页面

视觉检查：

- 主战场占据画面中心，不被 UI 过度遮挡。
- 左上设置、金币区域位置正确。
- 右上部队、卡牌按钮位置正确。
- 左下指挥官面板贴近样例。
- 底部卡槽与指挥官面板不重叠。
- 阵型按钮位于卡槽右侧。
- 右下小地图贴近样例。
- 所有金边、角花、按钮风格统一。

交互检查：

- 点击单位后有选中反馈。
- 点击地面后单位缓慢移动。
- 点击敌方单位后进入攻击命令。
- 点击指挥官头像后显示 `5x5` 阵型部署范围。
- 阵型按钮有选中状态。
- 小地图放大按钮至少有占位响应。

## 截图回归流程

每完成一个 UI 阶段，保存截图：

```text
计划/UI截图记录/
├── commander_select_1600x900.png
├── commander_select_1920x1080.png
├── commander_select_1366x768.png
├── battle_1600x900.png
├── battle_1920x1080.png
└── battle_1366x768.png
```

每次改 UI 后重新截图，对比旧截图和样例图。

重点看：

- 位置是否偏移。
- 面板是否变形。
- 文字是否溢出。
- 图标是否压住文字。
- 按钮状态是否统一。
- UI 是否遮挡关键战场信息。

## 量化对齐标准

为了避免“看起来差不多”过于主观，第一阶段使用以下容许误差：

### 1600x900 主分辨率

- 主面板位置误差：不超过 `12px`。
- 主面板宽高误差：不超过 `16px`。
- 按钮位置误差：不超过 `10px`。
- 卡槽等距误差：不超过 `6px`。
- 文本基线误差：不超过 `6px`。
- 小地图位置误差：不超过 `12px`。

### 其他 16:9 分辨率

- UI 不允许重叠。
- 文本不允许溢出容器。
- 关键按钮不允许超出屏幕。
- 战斗主视野至少保留画面面积的 `55%`。
- 左下指挥官面板和底部卡槽不能互相覆盖。

### 可接受差异

- 字体完全一致不作为第一阶段硬性要求，但字号、字重、颜色需要接近。
- 金色边框纹样可以先用近似素材，但厚度和颜色必须接近。
- 角花细节可以后续优化，但四角装饰位置必须一致。
- 小地图内容第一版可以是静态图，但边框、位置和尺寸要接近样例。

## UI 资产切图与 9-Slice 规范

如果原型图是整图，不能直接整张拉伸作为最终 UI。需要按组件拆分：

### 必须切出的组件

- 深蓝面板底。
- 金色面板边框。
- 面板四角角花。
- 普通按钮底。
- 选中按钮底。
- 禁用按钮底。
- 卡牌槽边框。
- 血条底和血条填充。
- 小地图边框。
- Tooltip 背板。

### 9-Slice 要求

- 面板和按钮必须设置 Sprite Border。
- 角花不能被拉伸，作为独立 Image 覆盖在四角。
- 细线边框优先保持原始像素厚度。
- 同一个组件只能有一个标准切图来源，避免多个版本混用。

### 命名规范

```text
ui_panel_blue_base
ui_panel_gold_border
ui_panel_corner_gold
ui_button_gold_normal
ui_button_gold_selected
ui_button_gold_disabled
ui_card_slot_empty
ui_bar_health_bg
ui_bar_health_fill
ui_minimap_frame
ui_tooltip_bg
```

## 交互状态验收

每个可点击 UI 至少需要以下状态：

- 默认。
- 鼠标悬停。
- 按下。
- 选中。
- 禁用。

需要覆盖的组件：

- 指挥官卡。
- 出征按钮。
- 顶部设置/部队/卡牌按钮。
- 阵型按钮。
- 小地图放大按钮。
- 卡牌槽。

状态不能改变组件尺寸，只改变颜色、亮度、描边、内发光或透明度。

## 自动检查工具规划

后续可以写 Unity Editor 工具：

```text
Assets/Editor/UI/UiValidationTool.cs
```

功能：

- 一键切换分辨率截图。
- 检查关键 UI 节点是否越界。
- 检查 TextMeshPro 文本是否溢出。
- 检查按钮 RectTransform 是否小于最小尺寸。
- 检查 prefab 是否丢失引用。
- 检查 `ReferenceOverlay` 是否在 Build 中关闭。
- 导出 UI 节点 RectTransform 报告。
- 记录当前截图与上次截图的文件名和时间。

示例检查项：

```text
CommanderPanel must be inside bottom-left safe area.
MiniMapPanel must be inside bottom-right safe area.
TopBar buttons must not overlap each other.
Card slots must keep equal width and spacing.
Text preferred width must be <= container width.
```

自动工具不能替代人工审美，但可以防止常见错误反复出现。

## 开发纪律

- 不直接在场景里复制临时按钮作为最终 UI。
- 不使用 Unity 默认按钮外观作为正式 UI。
- 不随意拉伸整张样例图当最终 UI。
- 不删除参考图叠底层。
- 不在多个页面重复手工制作同一种边框。
- 每次大改 UI 后必须截图。
- UI prefab 改动后要检查所有引用页面。

## 第一阶段 UI 完成定义

UI 第一阶段完成，需要同时满足：

- 视觉布局接近样例图。
- 主要组件已经 prefab 化。
- 指挥官选择页能完成选择和出征。
- 战斗页能显示指挥官、卡槽、阵型按钮、小地图。
- 16:9 三个分辨率下无明显重叠和溢出。
- 截图已保存到 `计划/UI截图记录/`。
- Unity Console 无 UI 相关报错。
