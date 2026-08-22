# AI 驱动肉鸽特性卡系统接入计划

## 1. 文档定位

本文是把“AI 驱动的肉鸽卡牌系统”接入当前《指挥大师》MVP 的实施计划。本文只规划系统接入和开发顺序，不直接替代以下既有规则：

- `规则/指挥官编队玩法完善规则.md`
- `规则/战斗结算商店与特性装配页面方案.md`
- `规则/战斗状态稳定与敌方AI开发方案.md`

核心原则：

```text
AI 负责生成候选内容，本地系统负责校验、落库、执行和结算。
特性卡作用于指挥官编队，不绕过 CommanderGroupRuntime 直接控制单兵。
```

## 2. 外部参考结论

### 2.1 RogueForge 的可借鉴点

参考项目：`https://github.com/kevinnie2003/rogueforge`

该项目的价值不在于具体战斗规则，而在于生成管线：

```text
构造上下文 Prompt
  -> LLM 生成结构化 JSON
  -> Schema 校验
  -> 机制白名单校验
  -> 数值预算校验
  -> 可修复则 Clamp/Repair
  -> 不可修复则 Retry
  -> 多次失败后使用策划兜底内容
  -> 记录日志用于回放、调试和平衡
```

适合本项目吸收的结论：

- AI 不生成 C# 脚本，不进入战斗 Update、伤害结算或寻路热路径。
- AI 只能组合预定义效果原语，例如伤害修正、冷却修正、减伤、低血触发。
- 卡牌描述是展示文本，真正执行的是结构化 `TraitEffect`。
- AI 生成结果必须可复现、可记录、可拒绝、可回退。
- Director 可以根据玩家战斗表现、当前卡池和地图难度调整商店候选，但最终仍受本地校验器约束。

### 2.2 Unity 卡牌框架的取舍

Unity 生态中存在完整卡牌框架，例如 Game Card System、TCG Engine Roguelike 等。它们适合从零开发传统卡牌构筑游戏，但不建议直接引入当前项目，原因是：

- 当前项目已有指挥官编队战斗、结算商店、特性实例和程序化地图系统。
- 完整卡牌框架通常围绕回合制手牌、抽弃牌堆、敌人意图构建，会与现有即时/半即时编队战斗发生架构冲突。
- 项目规则要求战斗中以指挥官编队作为玩家命令单位，不能退回单兵直接控制，也不应强行改成纯卡牌战斗。

因此，本项目采用“AI 肉鸽特性卡”路线，而不是“传统手牌战斗”路线。

## 3. 目标形态

### 3.1 玩家体验循环

```text
选择多名指挥官出征
  -> 程序化地图生成与敌方编队生成
  -> 战斗中指挥官编队执行移动/攻击/重整
  -> 胜利结算
  -> AI Director 依据本场表现生成或筛选特性卡候选
  -> 商店展示 3 张候选特性卡
  -> 玩家购买、出售、刷新、装备到出征指挥官
  -> 确认后写入玩家进度
  -> 下一场战斗加载装备特性并实际生效
```

### 3.2 系统边界

AI 可以生成：

- 新特性卡候选。
- 商店刷新权重。
- 敌方编队词缀。
- 地图难度修正建议。
- 事件奖励或惩罚候选。

AI 不可以生成：

- 可执行 C# 代码。
- 任意公式字符串。
- 直接修改单位坐标、血量或编队状态的命令。
- 绕过本地校验的卡牌定义。
- 战斗运行时每帧决策逻辑。

## 4. 数据模型扩展

当前已有：

```text
TraitCardDefinition
TraitCardInstance
CommanderLoadoutSnapshot
PlayerProgressionSnapshot
SettlementShopSession
```

建议新增：

```csharp
enum TraitEffectKind
{
    ModifyMaxHealth,
    ModifyAttackPower,
    ModifyAttackCooldown,
    ModifyMoveSpeed,
    ReduceIncomingDamage,
    ModifyHealingReceived,
    GrantOpeningShield,
    ModifyCommanderMorale
}

enum TraitTriggerKind
{
    Always,
    OnBattleStart,
    WhileGroupHealthBelowPercent,
    WhileGroupHealthAbovePercent,
    WhileGroupIdle,
    OnFirstHitTaken,
    OnFirstAttack,
    WhileFormationIntact,
    AfterRegroup
}

enum TraitTargetScope
{
    CommanderOnly,
    AllGroupMembers,
    FrontlineMembers,
    RangedMembers,
    LowestHealthMember
}

sealed class TraitEffect
{
    TraitEffectKind Kind;
    TraitTriggerKind Trigger;
    TraitTargetScope Scope;
    float Value;
    float DurationSeconds;
    int MaxStacks;
    string[] Tags;
}
```

`TraitCardDefinition` 增加：

```csharp
List<TraitEffect> Effects;
int PowerBudget;
string AiSourceId;
bool IsAiGenerated;
```

说明：

- `Description` 只用于显示。
- `Effects` 才是战斗执行依据。
- `PowerBudget` 用于平衡校验。
- `AiSourceId` 用于追踪一次 AI 生成批次。

## 5. 本地执行层

### 5.1 TraitRuntimeResolver

职责：

- 战斗开始时读取 `PlayerProgressionStore.Current`。
- 根据 `BattleStartContext.ExpeditionRoster` 找到每名出征指挥官的装备卡。
- 把 `TraitCardInstanceId` 解析成 `TraitCardDefinition` 与 `TraitEffect`。
- 绑定到对应 `CommanderGroupRuntime.GroupId`。
- 对无效卡、重复卡、缺失定义输出警告，并跳过该效果。

禁止：

- 修改静态 `CommanderDefinition.Traits`。
- 在运行时把卡牌归属写回商店草稿。
- 将效果直接挂在普通士兵上而丢失 `CommanderGroupId`。

### 5.2 TraitEffectService

职责：

- 提供统一数值查询 API。
- 让移动、伤害、冷却、生命、减伤等系统通过服务取得修正后数值。
- 管理有持续时间或一次性触发的特性状态。
- 在编队阵亡、战斗结束、场景退出时清理运行时状态。

建议 API：

```csharp
float GetMoveSpeedMultiplier(UnitView unit);
float GetAttackPowerMultiplier(UnitView attacker, UnitView target);
float GetAttackCooldownMultiplier(UnitView attacker);
float GetIncomingDamageMultiplier(UnitView defender, UnitView attacker);
int GetMaxHealthBonus(UnitView unit);
void NotifyBattleStarted(CommanderGroupRuntime group);
void NotifyGroupCommandChanged(CommanderGroupRuntime group);
void NotifyDamageTaken(UnitView defender, UnitView attacker, int rawDamage);
```

第一版优先支持纯被动效果，少做复杂事件链。

### 5.3 战斗接入点

优先修改以下位置：

- `UnitSpawner`：生成单位时应用最大生命修正。
- `UnitMovementController`：移动速度读取 `TraitEffectService`。
- `UnitCombatController`：伤害和攻击冷却读取 `TraitEffectService`。
- `CommanderGroupCommandController`：通知编队命令变化，用于 `WhileGroupIdle`、`AfterRegroup` 等触发条件。
- `BattleUiController`：显示当前激活指挥官装备特性和实际生效状态。

## 6. AI 生成层

### 6.1 生成输入

`AiTraitGenerationContext`：

```csharp
string RunId;
int LevelIndex;
int ShopRefreshCount;
BattleResultSnapshot LastBattleResult;
List<string> ActiveCommanderIds;
List<string> EquippedTraitDefinitionIds;
List<string> OwnedTraitDefinitionIds;
MapDifficultySummary MapSummary;
EnemyDifficultySummary EnemySummary;
TraitGenerationBias Bias;
```

其中 `Bias` 可由本地 Director 先算出：

- 玩家损失惨重：提高防御、生命、恢复类权重。
- 战斗耗时过长：提高攻击、冷却、破阵类权重。
- 玩家阵型频繁被打散：提高重整、阵型完整类权重。
- 某类卡过多：降低同类生成权重，避免商店同质化。

### 6.2 AI 输出格式

AI 只能输出 JSON，示例：

```json
{
  "cards": [
    {
      "id_hint": "steady_fireline",
      "display_name": "稳固火线",
      "description": "编队停止移动时，受到的远程伤害降低。",
      "rarity": "Rare",
      "buy_price": 7,
      "sell_price": 3,
      "effects": [
        {
          "kind": "ReduceIncomingDamage",
          "trigger": "WhileGroupIdle",
          "scope": "AllGroupMembers",
          "value": 0.18,
          "duration_seconds": 0,
          "max_stacks": 1,
          "tags": ["defense", "idle"]
        }
      ]
    }
  ]
}
```

### 6.3 校验器

新增 `TraitCardValidator`，分层校验：

1. Schema 校验：字段完整、枚举合法、数值类型正确。
2. 机制白名单校验：`Kind`、`Trigger`、`Scope` 必须在允许列表内。
3. 数值范围校验：
   - Common 单项修正通常不超过 10%。
   - Rare 单项修正通常不超过 20%。
   - Epic 单项修正通常不超过 35%。
   - Legendary 需要额外负面条件或稀有触发。
4. 触发条件校验：低血、待命、阵型完整等触发必须能由本地状态判断。
5. 编队兼容校验：效果必须能映射到 `CommanderGroupRuntime` 或其成员。
6. UI 文本校验：名称长度、描述长度、敏感词、空文本。
7. 价格校验：价格与稀有度和预算匹配。

处理结果：

```text
Accepted  直接使用
Repaired  数值被 Clamp 或文本被修正后使用
Rejected  进入重试
Fallback  使用策划预设卡
```

### 6.4 日志与回放

新增 `AiGenerationLogEntry`：

```csharp
string GenerationId;
string Provider;
string Model;
string PromptHash;
string RawResponsePath;
string ValidationResult;
string RepairSummary;
int AcceptedCount;
int RejectedCount;
DateTime CreatedAtUtc;
```

第一版可以只写 JSONL 到本地开发目录；正式版再考虑存档或调试面板。

## 7. Director 规则

第一版 Director 必须是本地确定性的，即使没有 AI 也能运行。

### 7.1 商店 Director

输入：

- 上一场胜负。
- 我方损失单位数。
- 存活编队数。
- 当前已有卡牌标签。
- 当前关卡编号。
- 地图和敌人难度摘要。

输出：

- 本次商店的标签权重。
- 稀有度权重。
- 是否允许 AI 生成新卡。
- 兜底卡池筛选条件。

示例：

```text
损失单位 >= 初始单位 40% -> defense、max_health 权重提高
敌方剩余编队很少但战斗耗时长 -> attack、cooldown 权重提高
玩家已有 3 张以上 defense -> defense 权重降低
连续两次刷新未出现 Rare -> Rare 保底权重提高
```

### 7.2 难度 Director

后续阶段再接：

- 地图参数修正：河流、山地、建筑密度、桥梁概率。
- 敌方编队词缀：进攻型、防守型、机动型、压制型。
- 房间事件：增益、代价、临时限制。

第一版不直接调整战斗 AI 行为，避免同时改动过多系统。

## 8. 分阶段实施

### 第一阶段：现有特性真正生效

目标：

- 不接在线 AI。
- 不改商店大结构。
- 让当前 `TraitCatalog` 中的 9 张卡通过结构化效果影响战斗。

任务：

1. 给 `TraitCardDefinition` 增加 `Effects`。
2. 为现有 9 张卡补齐结构化效果。
3. 新增 `TraitRuntimeResolver`。
4. 新增 `TraitEffectService`。
5. 接入最大生命、攻击力、攻击冷却、移动速度、减伤。
6. 战斗 UI 显示装备特性名称与基础状态。
7. 增加编辑器或 PlayMode 验证场景。

验收：

- 装备“坚韧”的指挥官，其编队成员进入战斗后最大生命提高。
- 装备“勇敢”的编队在低生命条件下伤害提高。
- 装备“鼓舞”的编队满足条件时移动速度提高。
- 未装备卡时战斗表现与当前版本一致。

### 第二阶段：本地 Director + 策划卡池

目标：

- 不依赖网络。
- 商店候选不再纯随机，而是被战斗表现和当前卡池轻度驱动。

任务：

1. 新增卡牌标签：`attack`、`defense`、`mobility`、`formation`、`low_health`、`economy`。
2. 新增 `TraitShopDirector`。
3. `SettlementShopSession.RollOffers()` 改为使用 Director 权重。
4. 记录最近战斗摘要和刷新次数。
5. 增加保底逻辑，避免连续刷新同质卡。

验收：

- 高损失战斗后防御/生命类卡出现概率提高。
- 已拥有多张同标签卡时，该标签候选概率下降。
- 同一 `SessionId + RandomSeed + RefreshCount` 生成结果可复现。

### 第三阶段：AI 生成候选卡

目标：

- AI 只参与商店候选生成。
- 生成失败不阻塞商店。

任务：

1. 定义 `AiTraitCardSpec` DTO。
2. 新增 `TraitCardValidator`。
3. 新增 `AiTraitCardGenerator` 接口。
4. 第一版实现 `MockAiTraitCardGenerator`，用本地样例模拟 LLM 输出。
5. 第二版再实现真实 Provider 适配层。
6. 生成结果通过校验后转为临时 `TraitCardDefinition`。
7. 商店展示 AI 生成卡，并在购买后写入玩家进度。

验收：

- Mock 输出合法卡时商店可显示并购买。
- Mock 输出超标数值时被修复或拒绝。
- Mock 输出非法效果时回退到策划卡池。
- 不接网络时游戏仍可完整运行。

### 第四阶段：AI Director 扩展到敌方与地图

目标：

- AI 不只生成卡，还能生成“下一场挑战配置建议”。

任务：

1. 新增 `AiRunDirectorSuggestion`。
2. 允许 AI 建议敌方词缀，但词缀仍来自白名单。
3. 允许 AI 建议地图参数区间，但最终由 `MapGenerationSettings` 校验。
4. 战斗开始前将建议固化为 `BattleMapRequest` 和敌方配置。
5. 日志记录 AI 建议与最终采用结果。

验收：

- AI 建议不会生成不可达地图。
- AI 建议不会生成超预算敌方编队。
- 同一次 Run 的建议可追踪、可复现、可回退。

## 9. 风险与控制

| 风险 | 控制方式 |
| --- | --- |
| AI 生成过强卡导致失衡 | 本地预算评分、稀有度上限、Clamp、拒绝 |
| AI 输出不可解析 | Schema 校验失败后重试，最终回退策划卡 |
| 网络不可用 | 默认使用本地卡池和本地 Director |
| 生成卡描述与效果不一致 | 描述由本地模板重写或追加数值摘要 |
| 战斗逻辑变复杂 | 第一阶段只接被动效果，事件触发延后 |
| 性能风险 | 战斗热路径只查本地缓存，不调用 AI |
| 存档兼容 | AI 生成卡必须持久化完整定义或保存可恢复 ID |
| 编队规则被破坏 | 所有效果以 `CommanderGroupId` 为边界，不直接接管单兵命令 |

## 10. 推荐优先级

必须先做：

1. `TraitEffect` 数据结构。
2. `TraitEffectService`。
3. 现有 9 张卡战斗生效。
4. 本地商店 Director。

之后再做：

1. Mock AI 生成。
2. 校验器和日志。
3. 真实 AI Provider。
4. 敌方与地图 Director。

暂不建议做：

- 抽牌、弃牌、能量费用、每回合手牌系统。
- AI 在战斗中实时生成技能。
- AI 直接写入脚本或热更新战斗逻辑。
- 大规模引入完整第三方卡牌框架。

## 11. 第一版文件落点建议

```text
mvp/Assets/Scripts/Progression/
  TraitEffectModels.cs
  TraitEffectCatalogExtensions.cs

mvp/Assets/Scripts/Battle/Traits/
  TraitRuntimeResolver.cs
  TraitEffectService.cs
  TraitRuntimeState.cs

mvp/Assets/Scripts/SettlementShop/
  TraitShopDirector.cs
  TraitOfferRollContext.cs

mvp/Assets/Scripts/AiDirector/
  AiTraitCardSpec.cs
  TraitCardValidator.cs
  AiTraitCardGenerator.cs
  MockAiTraitCardGenerator.cs
  AiGenerationLog.cs
```

如果第一阶段暂不接 AI，可以先不创建 `AiDirector` 目录。

## 12. 最小闭环验收清单

- 战斗胜利后进入商店。
- 商店可以买到特性卡。
- 特性卡可以装备到本次出征指挥官。
- 确认商店后保存到玩家进度。
- 下一场战斗读取装备卡。
- 装备卡影响对应指挥官编队。
- 效果不影响其他指挥官编队。
- 编队阵亡、战斗结束、返回选择页后运行时效果清理。
- 没有 AI 或 AI 失败时仍使用本地卡池。

## 13. 进一步优化与完善点

### 13.1 先区分“静态卡”和“运行时生成卡”

当前 `TraitCatalog` 是静态代码卡池。接入 AI 后，必须避免只保存 `DefinitionId` 却找不到 AI 生成定义的问题。

建议把卡牌定义来源分成三类：

```text
BuiltIn      代码或 ScriptableObject 内置卡
Generated    AI 生成并通过校验的卡
Fallback     AI 失败后由本地兜底池提供的卡
```

`TraitCardInstance` 仍然只保存实例归属，但玩家进度需要额外保存已购买的 AI 生成卡定义快照：

```csharp
sealed class GeneratedTraitDefinitionRecord
{
    string DefinitionId;
    string SourceGenerationId;
    int DefinitionVersion;
    TraitCardDefinition DefinitionSnapshot;
    string ValidationSummary;
}
```

这样即使以后 AI 模型、Prompt 或校验规则变化，旧存档中的已购卡也能稳定还原。

### 13.2 效果叠加规则要提前固定

如果多张卡同时修改攻击、移速、减伤，必须有统一叠加规则，否则后期平衡会变得不可控。

建议第一版采用：

```text
同类百分比加成先相加，再整体 Clamp。
减伤类总乘区设置上限，例如最终受到伤害最低不低于 55%。
攻击速度/冷却缩减设置下限，例如最终冷却不低于基础值 60%。
移速倍率设置上限，例如最终移速不高于基础值 140%。
最大生命加成只在单位生成或战斗初始化时结算，不在战斗中动态反复改最大生命。
```

示例：

```text
攻击力倍率 = Clamp(1 + Sum(ModifyAttackPower), 0.5, 1.8)
冷却倍率 = Clamp(1 - Sum(AttackCooldownReduction), 0.6, 1.5)
受伤倍率 = Clamp(1 - Sum(DamageReduction), 0.55, 1.5)
```

### 13.3 第一阶段避免复杂触发器

第一阶段目标是让现有卡生效，不建议马上实现所有触发器。

第一阶段允许：

- `Always`
- `OnBattleStart`
- `WhileGroupHealthBelowPercent`
- `WhileGroupHealthAbovePercent`

第一阶段暂缓：

- `OnFirstHitTaken`
- `OnFirstAttack`
- `AfterRegroup`
- `WhileFormationIntact`
- 需要持续 Buff 实例、倒计时或事件反订阅的效果

原因：

- 现有战斗代码的主要接入点是移动速度、伤害、冷却和生命值。
- 一次性触发和持续时间效果需要额外状态机，容易和编队命令序号、战斗冻结、单位死亡清理互相影响。

### 13.4 TraitEffectService 应该只读查询优先

为了降低对战斗系统的侵入，`TraitEffectService` 第一版应优先设计成“查询式服务”，而不是主动到处修改单位。

推荐模式：

```text
UnitCombatController 计算伤害时询问 TraitEffectService。
UnitMovementController 计算移动步长时询问 TraitEffectService。
UnitSpawner 生成单位时询问 TraitEffectService。
```

暂不推荐：

```text
TraitEffectService 主动遍历全部单位并实时改 Definition。
TraitEffectService 直接发起移动、攻击或停止命令。
TraitEffectService 每帧扫描全场单位判断触发。
```

这能保持战斗主逻辑仍在原控制器里，特性系统只提供修正值。

### 13.5 不要直接修改共享 UnitDefinition

`UnitRuntimeData.Definition` 指向单位定义。如果多个单位共享同一个 `UnitDefinition`，直接改 `Definition.MaxHealth`、`MoveSpeed`、`AttackPower` 会污染同类型所有单位。

第一版建议：

- 最大生命：在生成 `UnitRuntimeData.CurrentHealth` 时按修正后的最大生命赋值，并在运行时状态里记录 `RuntimeMaxHealth`。
- 伤害：不改 `Definition.AttackPower`，开火时计算修正后的伤害。
- 移速：不改 `Definition.MoveSpeed`，每次移动步长计算时乘倍率。
- 冷却：不改 `Definition.AttackCooldown`，设置冷却时乘倍率。

如果需要显示正确血条，建议给 `UnitRuntimeData` 增加：

```csharp
public int RuntimeMaxHealth;
```

并让血条优先读取 `RuntimeMaxHealth`，没有设置时回退 `Definition.MaxHealth`。

### 13.6 商店候选应支持“临时定义”

第三阶段 AI 生成卡时，商店会先展示一张尚未被玩家拥有的临时卡。它不能只靠 `TraitCatalog.Get(definitionId)` 查找。

建议扩展商店会话：

```text
OfferIndex -> TraitCardDefinitionSnapshot
Purchased -> 将 DefinitionSnapshot 写入 GeneratedTraitDefinitionRecord
Instance -> DefinitionId 引用该快照
```

这样候选卡刷新、购买、恢复悬挂会话时都不会丢定义。

### 13.7 AI 成本与等待体验

真实 AI 生成可能需要数秒。商店不能因为 AI 慢而空白。

建议策略：

```text
打开商店时立即用本地 Director 生成 3 张候选。
后台请求 AI 生成下一批候选。
如果 AI 在刷新前完成，则下次刷新可混入 AI 卡。
如果 AI 失败或超时，继续使用本地候选。
```

第一版超时建议：

```text
编辑器/开发环境：10 秒
正式构建：3 秒或仅使用预生成缓存
```

### 13.8 Prompt 注入与内容安全

如果后续允许玩家输入“想要什么卡”，必须把玩家文本当作不可信输入。

基本规则：

- 玩家输入只能作为主题、风格或意图，不可成为系统指令。
- Prompt 中明确要求输出 JSON，不接受解释文本。
- 校验器忽略 AI 给出的任何“规则说明”。
- 敏感词、超长文本、控制字符、路径字符必须过滤。
- 生成内容不得引用本地文件路径、真实 API Key、系统 Prompt 或模型配置。

### 13.9 平衡评分要从简单公式开始

不需要第一版就做复杂机器学习评分。建议用可读公式：

```text
效果分 = 基础权重 * 数值倍率 * 触发可用率 * 作用范围倍率 * 持续时间倍率
卡牌分 = Sum(效果分)
```

参考倍率：

```text
Always                 1.00
OnBattleStart          0.85
WhileHealthBelow35%    0.55
WhileHealthAbove70%    0.65
WhileGroupIdle         0.70

CommanderOnly          0.45
LowestHealthMember     0.55
FrontlineMembers       0.75
RangedMembers          0.75
AllGroupMembers        1.00
```

第一版只要能防止明显离谱的卡，就已经足够。

### 13.10 测试矩阵补充

除最小闭环外，建议增加以下自动化或手动验收：

| 类别 | 用例 |
| --- | --- |
| 存档 | 购买 AI 生成卡后重开商店，定义仍可解析 |
| 归属 | 同一实例不能装备给两名指挥官 |
| 隔离 | A 指挥官装备攻击卡，不影响 B 指挥官编队 |
| 共享定义 | 装备生命卡不会提高敌方同类型单位血量 |
| 叠加 | 两张攻击卡叠加后不超过上限 |
| 死亡 | 编队全灭后特性运行时状态清理 |
| 冻结 | 战斗结算冻结后特性不再触发伤害或移动修正 |
| 兜底 | AI 返回非法 JSON 时商店仍能打开 |
| 恢复 | 商店会话 Suspend/Resume 后候选卡和已购买状态不变 |
| 复现 | 同一随机种子、同一刷新次数得到同一候选结果 |

### 13.11 UI 表达要显示“真实生效数值”

AI 生成卡如果只显示自然语言，玩家很难判断强度。

建议卡面统一追加本地生成的效果摘要：

```text
攻击力 +15%
生命低于 35% 时生效
作用范围：本指挥官编队全体单位
```

展示文本可由 AI 润色，但数值摘要必须由本地 `TraitEffect` 生成。

### 13.12 推荐更新后的最短开发路径

更稳的执行顺序：

```text
1. UnitRuntimeData 增加 RuntimeMaxHealth。
2. TraitCardDefinition 增加 Effects 和 Tags。
3. 给现有 9 张卡补 Effects。
4. TraitRuntimeResolver 绑定出征指挥官装备。
5. TraitEffectService 只实现 Always / 血量阈值被动查询。
6. 接入 UnitSpawner / UnitCombatController / UnitMovementController。
7. 修正血条和 UI 展示。
8. 做 TraitShopDirector 本地权重。
9. 做 AI Spec + Validator + Mock Generator。
10. 最后接真实 AI Provider。
```

## 14. 性能与可执行性优化

### 14.1 性能目标

本系统必须遵守 `计划/性能风险与优化策略.md` 中的中央 Tick、对象池、UI 脏标记和高频逻辑零分配原则。

特性卡系统的第一版性能目标：

```text
战斗中不调用 AI。
战斗中不解析 JSON。
战斗中不扫描全部卡牌定义。
战斗热路径不分配 List/Dictionary。
移动、攻击、受伤查询为 O(当前单位所属编队的已缓存效果数)。
商店生成和 AI 校验允许较慢，但不得阻塞战斗。
```

建议预算：

```text
每名指挥官装备槽：4
每张卡第一版效果数：1-2
每个编队缓存效果数：建议 <= 8
战斗中单次数值查询：目标 < 0.05 ms
商店本地 RollOffers：目标 < 1 ms
AI 校验单批候选：目标 < 5 ms，不含网络
```

### 14.2 运行时缓存结构

不要在攻击、移动、血条刷新时从 `PlayerProgressionStore`、`TraitCatalog` 或商店会话现查。

战斗初始化时一次性构建：

```csharp
sealed class CommanderTraitRuntime
{
    public string GroupId;
    public string CommanderId;
    public TraitRuntimeBucket Always;
    public TraitRuntimeBucket LowHealth;
    public TraitRuntimeBucket HighHealth;
}

sealed class TraitRuntimeBucket
{
    public float AttackPowerAdd;
    public float AttackCooldownReduction;
    public float MoveSpeedAdd;
    public float IncomingDamageReduction;
    public int MaxHealthAdd;
}
```

推荐查询链：

```text
UnitView
  -> UnitRuntimeData.CommanderGroupId
  -> Dictionary<string, CommanderTraitRuntime>
  -> 当前条件对应 Bucket
  -> 返回修正倍率
```

其中 `Dictionary<string, CommanderTraitRuntime>` 只在战斗初始化和编队清理时写入。战斗中查询不创建临时集合。

### 14.3 条件判断缓存

`WhileGroupHealthBelowPercent`、`WhileGroupHealthAbovePercent` 不能每次开火时重新遍历整支编队计算血量比例。

建议在 `TraitEffectService` 中维护编队生命摘要：

```csharp
sealed class GroupTraitConditionState
{
    public int AliveCount;
    public int CurrentHealthTotal;
    public int MaxHealthTotal;
    public bool IsLowHealth;
    public bool IsHighHealth;
    public bool IsIdle;
    public int Version;
}
```

更新时机：

- 单位生成后初始化一次。
- 单位受伤、治疗、死亡时增量更新。
- 编队状态变化时更新 `IsIdle`。
- 不在每帧遍历全编队。

如果第一版增量维护成本过高，可以退一步：在 `BattleTickService.MediumTick` 上每 0.1 秒按编队重算一次摘要，但禁止在每次伤害查询里重算。

### 14.4 避免事件订阅泄漏

特性系统会监听伤害、死亡、编队状态变化。必须有明确生命周期：

```text
BattleStart -> TraitEffectService.BuildRuntime()
BattleResolved / SceneExit -> TraitEffectService.ClearRuntime()
CommanderGroupDefeated -> Remove group runtime cache
```

规则：

- 订阅 `BattleTickService`、`CommanderGroupRegistry` 或单位事件时，必须在 `OnDisable`/`OnDestroy` 反订阅。
- 不把 `UnitView` 长期作为字典 Key，除非能在死亡和场景退出时清理。
- 优先使用 `UnitRuntimeData.Id` 或 `CommanderGroupId` 作为稳定 Key。

### 14.5 商店 UI 的可执行优化

商店页面已经会重建候选、库存和指挥官槽。AI 卡接入后要避免进一步放大 Canvas rebuild。

建议：

- `ShopOffer` 增加 `DefinitionSnapshot` 后，UI 仍只在 `ChangedOfferIndices` 指定的槽位重建。
- 卡牌效果摘要提前生成成字符串，UI 不在每帧拼接。
- AI 生成卡图标第一版复用品质/标签图标，不实时生成图片。
- 商店恢复会话时只重建当前可见列表，不为了校验去遍历所有历史生成卡。
- 购买、装备、出售事务继续沿用 `ShopChangeSet`，不要新增全量刷新事件。

### 14.6 AI 调用不进入 Unity 主线程热路径

真实 AI Provider 接入时，必须当作慢速外部服务。

建议接口：

```csharp
interface IAiTraitCardGenerator
{
    bool IsAvailable { get; }
    void RequestAsync(AiTraitGenerationContext context, Action<AiTraitGenerationResult> onDone);
    void Cancel(string requestId);
}
```

规则：

- AI 请求只在商店打开、刷新后预取、关卡间整备时发起。
- 回调回到主线程后，只提交已通过校验的 DTO。
- 商店关闭或会话提交后取消未完成请求。
- 同一商店会话最多保留一个进行中的 AI 请求。
- 网络异常、超时、取消都视为普通失败，不弹阻断式错误。

### 14.7 可执行开发切片

为了避免一次改太大，建议把第一阶段拆成 5 个可独立验收的 PR/任务。

任务 A：数据结构最小改动

```text
新增 TraitEffectModels.cs。
TraitCardDefinition 增加 Effects 和 Tags。
UnitRuntimeData 增加 RuntimeMaxHealth。
TraitCatalog 给 9 张卡补 Effects。
不接战斗逻辑。
```

验收：

```text
项目编译通过。
现有商店仍能打开。
TraitCatalog.Get() 返回的卡都包含合法 Effects。
```

任务 B：运行时解析

```text
新增 TraitRuntimeResolver。
从 PlayerProgressionStore 和 ExpeditionRoster 解析出 GroupId -> TraitEffect。
只打印调试摘要，不修改战斗数值。
```

验收：

```text
出征 2 名指挥官时，各自只解析自己的装备卡。
缺失定义或空槽不会报错中断。
```

任务 C：生命和血条

```text
UnitSpawner 应用 RuntimeMaxHealth。
UnitView/血条读取 RuntimeMaxHealth。
先只实现 ModifyMaxHealth。
```

验收：

```text
装备“坚韧”的编队血量上限提高。
敌方同类型单位不受影响。
```

任务 D：攻击与冷却

```text
UnitCombatController 查询攻击倍率和冷却倍率。
实现 ModifyAttackPower、ModifyAttackCooldown。
```

验收：

```text
低血攻击卡在阈值内生效。
冷却缩减不低于配置下限。
```

任务 E：移动与减伤

```text
UnitMovementController 查询移速倍率。
UnitCombatController 结算伤害前查询受伤倍率。
实现 ModifyMoveSpeed、ReduceIncomingDamage。
```

验收：

```text
移动类卡只影响所属编队。
减伤类卡不会把伤害降到 0。
```

### 14.8 第一版实现范围再收紧

为了更可执行，第一版只建议落地 5 种效果：

```text
ModifyMaxHealth
ModifyAttackPower
ModifyAttackCooldown
ModifyMoveSpeed
ReduceIncomingDamage
```

第一版只建议落地 3 种触发：

```text
Always
WhileGroupHealthBelowPercent
WhileGroupHealthAbovePercent
```

暂不实现：

```text
GrantOpeningShield
ModifyCommanderMorale
OnFirstHitTaken
OnFirstAttack
AfterRegroup
WhileFormationIntact
持续时间 Buff
多层 Stack
```

这样可以让 9 张初始卡先跑通，后续再扩展事件型和主动型特性。

### 14.9 当前 9 张卡的第一版映射建议

| 卡牌 | 第一版效果 | 暂缓内容 |
| --- | --- | --- |
| 勇敢 | 低血时攻击力提高 | 无 |
| 谨慎 | 生命高于阈值时小幅减伤，先替代“首次受击” | 首次受击短时间减伤 |
| 鼓舞 | 高血时移动速度提高 | 无 |
| 坚韧 | 最大生命提高 | 无 |
| 纪律 | 高血或默认状态下攻击冷却降低 | 阵型完整判断 |
| 沉着 | 高血时小幅减伤或暂不生效 | 命中稳定性系统 |
| 守备 | 待命触发暂缓，第一版可改为固定小幅减伤 | 停止移动判断 |
| 坚守 | 固定小幅减伤或暂不生效 | 阵型未被打乱判断 |
| 回光反照 | 极低血时攻击力大幅提高，并设置上限 | 每场触发次数 |

注意：为了减少状态系统复杂度，第一版可以允许“文本先保留设计意图，效果采用简化映射”，但 UI 必须显示真实生效摘要，避免玩家误解。

### 14.10 性能验收清单

- 战斗 6 个玩家编队、2 个敌方编队时，特性查询不产生 GC Alloc。
- 一次普通攻击结算不创建临时集合。
- 一次移动 Tick 不创建临时集合。
- 商店刷新本地候选不阻塞画面。
- AI 请求超时不会卡住 UI。
- 战斗冻结后 `BattleTickService` 停止触发，特性系统没有独立 Update 继续运行。
- 场景退出后，特性服务缓存清空，未完成 AI 请求取消。
- Deep Profile 下，`TraitEffectService` 不出现在主要耗时热点前列。
