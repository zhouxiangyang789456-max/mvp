# Simple Military 人物单位使用说明

> 适用资源包：`Simple Military - Cartoon War v1.3.8.unitypackage`
> 内容范围：`Assets/SimpleMilitary/Old/Prefabs/Characters/` 下的人物单位
> 文档目标：说明人物单位能否正常使用、共有多少单位/动作、如何接入游戏

---

## 目录

1. [一、结论速览](#一结论速览)
2. [二、单位总览（21 个单位 / 61 个 Prefab）](#二单位总览21-个单位--61-个-prefab)
3. [三、动作总览（46 个可用动作）](#三动作总览46-个可用动作)
4. [四、统一骨架与 Avatar 机制](#四统一骨架与-avatar-机制)
5. [五、快速接入游戏](#五快速接入游戏)
6. [六、Animator 控制器说明](#六animator-控制器说明)
7. [七、12 个未接线动作的补接方法](#七12-个未接线动作的补接方法)
8. [八、使用注意事项（坑位清单）](#八使用注意事项坑位清单)
9. [九、两个 Prefab 打包错误修复说明](#九两个-prefab-打包错误修复说明)

---

## 一、结论速览

| 项目 | 结论 |
|------|------|
| **能否正常使用** | ✅ **可以**。除 2 个 prefab 有打包错误（已修复），其余 59 个结构完整 |
| **单位总数** | **21 个**（20 个完整角色 + 1 个 FPS 第一人称手臂） |
| **Prefab 总数** | **61 个**（20 个角色 × 3 配色 = 60，加 1 个 FPS） |
| **动作总数** | **46 个可用动作**（所有单位共用一套动作库） |
| **骨架兼容性** | ✅ 完全统一，动画可**跨单位任意复用** |
| **需手动补齐** | ❌ 无 Collider、无 Rigidbody；根运动需按需关闭 |

**一句话总结**：人物单位开箱即用，21 个单位共享同一套 21 骨骼骨架和 46 个动作，直接拖入场景即可播放动画；唯一要处理的是物理碰撞组件（需自行添加）和 2 个已修复的 prefab。

---

## 二、单位总览（21 个单位 / 61 个 Prefab）

所有单位位于 `Assets/SimpleMilitary/Old/Prefabs/Characters/`，命名规则：

```
SimpleMilitary_<单位名>_<配色>.prefab
配色取值：Black / Brown / White（黑 / 棕 / 白 三套军装配色）
```

### 2.1 完整单位名单

| # | 单位名 | 配色数 | 说明 |
|---|--------|--------|------|
| 1 | BombDisposal | 3 | 拆弹兵 |
| 2 | EasternSoldier | 3 | 东方士兵 |
| 3 | FemaleMedic | 3 | 女医疗兵 |
| 4 | FemaleSoldier | 3 | 女兵 |
| 5 | GasMaskSoldier | 3 | 防毒面具兵 |
| 6 | General | 3 | 将军 |
| 7 | GermanSoldier | 3 | 德军士兵 |
| 8 | JungleCommando | 3 | 丛林突击队 |
| 9 | Medic | 3 | 医疗兵 |
| 10 | Mercenary | 3 | 雇佣兵 |
| 11 | Pilot | 3 | 飞行员 |
| 12 | Soldier01 | 3 | 士兵 01 |
| 13 | SpecialForces01 | 3 | 特种部队 01 |
| 14 | SpecialForces02 | 3 | 特种部队 02 |
| 15 | SpecialForces03 | 3 | 特种部队 03 |
| 16 | SpecialForces04 | 3 | 特种部队 04 |
| 17 | Terrorist01 | 3 | 恐怖分子 01 |
| 18 | Terrorist02 | 3 | 恐怖分子 02 |
| 19 | Terrorist03 | 3 | 恐怖分子 03 |
| 20 | TrainingSoldier | 3 | 训练士兵 |
| 21 | **FPS** | **1** | 第一人称手臂（无配色，仅 1 个蒙皮） |

> 20 个完整角色 × 3 配色 = 60 个 prefab，加上 FPS 1 个 = **61 个 prefab**。

### 2.2 结构说明

- 每个角色 prefab 的结构完全一致：
  - 1 个根节点（`SimpleMilitary_<单位名>_<配色>`）
  - 1 个 `Animator`（指向共享控制器 `SimpleCharacter_5.0.controller`）
  - 20 个 `SkinnedMeshRenderer`（身体 + 各部位装备）
  - 21 根骨骼（统一骨架，见第四节）
- 3 个配色之间**仅材质不同**，模型、骨骼、动画完全相同，可任意混用。

---

## 三、动作总览（46 个可用动作）

所有动作来自 3 个动画 FBX，内嵌为子资产 clip：

| 动画文件 | Clip 数 | 内容 |
|----------|---------|------|
| `Animations.fbx` | 35 | 基础动作 + 叠加姿势 |
| `Animations_IK.fbx` | 20 | 武器射击 / 换弹 / 持枪待机 |
| `Animations_Static.fbx` | 2 | 原地走 / 跑（Static 版） |
| FPS 的 `MS_Hands` | 1 | 第一人称手臂动作 |

**合计 58 个 clip**，其中 **46 个被主控制器 `SimpleCharacter_5.0.controller` 引用**（即"可用动作"），其余 12 个需手动接线（见第七节）。

### 3.1 已接线动作清单（46 个）

**基础移动 / 待机（Animations.fbx）**

| 动作 | 中文 |
|------|------|
| Idle | 待机 |
| Walk | 走路 |
| Run | 跑步 |
| Running_Jump | 跑步跳跃 |
| Standing_Jump | 站立跳跃 |
| Crouch_Down / Crouch_Idle / Crouch_Up | 蹲下 / 蹲姿待机 / 起身 |
| Falling | 坠落 |
| Death_01 / Death_02 | 死亡 1 / 死亡 2 |
| Dead_01 / Dead_02 | 倒地 1 / 倒地 2 |

**待机姿势变体（Animations.fbx）**

| 动作 | 中文 |
|------|------|
| Idle_SittingOnGround | 坐地待机 |
| Idle_CheckWatch | 看表待机 |
| Idle_WipeMouth | 擦嘴待机 |
| Idle_LeaningAgaintWall | 靠墙待机 |
| Idle_Smoking | 抽烟待机 |
| Idle_SexyDance | 舞蹈待机 |
| Idle_HandOnHips | 叉腰待机 |
| Idle_CrossedArms | 抱臂待机 |
| Salute | 敬礼 |
| GrenadeThrow | 投掷手雷 |

**武器动作（Animations_IK.fbx，20 个）**

| 武器 | 射击 | 换弹 | 待机 |
|------|------|------|------|
| 手枪 Handgun | Shoot / Reload | — | Idle |
| 步枪 Auto（自动步枪） | SingleShot / FullAuto_Shoot | Reload | Idle |
| 冲锋枪 SubMachineGun | SingleShot / FullAuto_Shoot | Reload | Idle |
| 霰弹枪 Shotgun | Shoot | Reload | Idle |
| 步枪 Rifle | Shoot_Reload | — | Idle |
| RPG 火箭筒 | Shoot | — | Idle |
| 加特林 MiniGun | 射击（Character_MiniGun） | — | Idle |

> 精确动作名（可复制搜索）：`Character_Handgun_Shoot`、`Character_Handgun_Reload`、`Character_Auto_SingleShot`、`Character_Auto_FullAuto_Shoot`、`Character_Auto_Reload`、`Character_SubMachineGun_SingleShot`、`Character_SubMachineGun_FullAuto_Shoot`、`Character_SubMachineGun_Reload`、`Character_Shotgun_Shoot`、`Character_Shotgun_Reload`、`Character_Rifle_Shoot_Reload`、`Character_RPG_Shoot`，以及各类 `_Idle`。

### 3.2 原地移动（Static 版，2 个）

| 动作 | 中文 |
|------|------|
| Walk_Static | 原地走（不产生位移） |
| Run_Static | 原地跑（不产生位移） |

> 适合用代码控制位移（NavMeshAgent / Rigidbody）时播放，避免根运动叠加。

---

## 四、统一骨架与 Avatar 机制

这是本资源包**最大优势**，直接决定"人物单位能否顺利使用"。

### 4.1 统一骨架（21 根骨骼）

61 个 prefab 全部使用**完全相同的 21 根骨骼**，骨骼名、层级、数量完全一致：

```
Root_jnt
├─ Hips_jnt
│  ├─ Spine_jnt
│  │  ├─ LowerBody
│  │  └─ Head_jnt
│  │     ├─ Head
│  │     └─ Hat_jnt / Hat_jnt1
│  ├─ UpperLeg_Left / Right → LowerLeg_Left / Right → Foot_Left / Right
│  └─ ...
├─ UpperArm_Left / Right → LowerArm_Left / Right → Hand_Left / Right
├─ Weapon_Shield / Weapon_Sword / Prop_FireFighterPack
└─ ...
```

### 4.2 共享 Avatar

所有单位共用**同一个 Avatar**：`SimpleMilitary_Characters_NewRig.fbx`。

> 这意味着：任何角色的动画 clip 都可以**直接套用到任何其他角色**上，无需 Retargeting 配置。

### 4.3 5 个 Avatar Mask（用于叠加动画层）

| Mask 名 | 用途 |
|---------|------|
| `Mask_Head` | 只作用于头部（用于头部朝向叠加） |
| `Mask_Body` | 作用于身体 |
| `Mask_Upper` | 上半身 |
| `Mask_UpperBody` | 上身躯干 |
| `Mask_LowerBody` | 下半身（腿） |

> 用于在 Animator 里建"叠加层"，让角色边走边转头、边走边换弹等。

---

## 五、快速接入游戏

### 5.1 三步接入

1. **拖入场景**：把任意 `SimpleMilitary_<单位名>_<配色>.prefab` 拖到 Hierarchy。
2. **确认 Animator**：选中实例，Inspector 里 `Animator` 组件的 `Controller` 应为 `SimpleCharacter_5.0.controller`，`Avatar` 应为 `SimpleMilitary_Characters_NewRig`。
3. **触发动画**：用代码设置 Animator 参数，或用 `Animator.Play("动作名")` 直接播放。

### 5.2 代码示例（C#）

```csharp
using UnityEngine;

public class CharacterDemo : MonoBehaviour
{
    Animator anim;

    void Start() => anim = GetComponent<Animator>();

    public void PlayWalk()    => anim.Play("Walk");
    public void PlayRun()     => anim.Play("Run");
    public void PlayIdle()    => anim.Play("Idle");
    public void PlayShoot()   => anim.Play("Character_Handgun_Shoot");
    public void PlayDead()    => anim.Play("Death_01");
}
```

### 5.3 播放任意动作的通用方法

由于 46 个动作都在主控制器里，你可以直接用 clip 名播放：

```csharp
// 播放名为 clipName 的动作，layer=0
anim.Play(clipName);
```

---

## 六、Animator 控制器说明

### 6.1 主控制器

`SimpleCharacter_5.0.controller` 是唯一需要关注的主控制器：

- **87 个状态** / **76 个带动画** / **46 个唯一动画片段**
- 覆盖移动、待机、武器、死亡等完整状态机

### 6.2 演示控制器（21 个 Demo_*）

另外有 21 个 `Demo_*.controller`，它们都是主状态机的**副本**，只是默认状态不同（用于演示单个动作）。日常开发**无需使用**，直接挂主控制器即可。

---

## 七、12 个未接线动作的补接方法

主控制器里**没有**引用以下 12 个 clip（多为叠加姿势），需要时手动接线：

| 类型 | Clip 名 | 说明 |
|------|---------|------|
| 头部朝向 | Head_Up / Head_Down / Head_Left / Head_Right / Head_Normal | 头部 5 方向 |
| 身体朝向 | Body_Up / Body_Down / Body_Left / Body_Right / Body_Normal | 身体 5 方向 |
| 站姿 | CrossArms | 抱臂站姿 |
| 站姿 | HandsOnHips | 叉腰站姿 |

### 7.1 补接步骤（以"边走边转头"为例）

1. 打开 `SimpleCharacter_5.0.controller` 的 Animator 窗口。
2. 在左侧 Layers 面板点 `+` 新建一层，命名为 `HeadLook`。
3. 该层 `Weight` 设为 1，`Mask` 选择 `Mask_Head`。
4. 在该层创建 Blend Tree 或直接放置 `Head_Left / Head_Right / Head_Normal` 状态，用参数控制。
5. 运行即可：下半身播 Walk，头部叠加朝向动画，互不干扰。

> 这 12 个 clip 都已存在于 `Animations.fbx` 中，无需重新导入，只需在 Animator 里加层引用。

---

## 八、使用注意事项（坑位清单）

| 坑位 | 说明 | 应对 |
|------|------|------|
| **无 Collider / Rigidbody** | 角色只有 `Animator` + `SkinnedMeshRenderer` + `Transform`，**没有碰撞体** | 需手动添加 `CapsuleCollider` + `Rigidbody` 或 `CharacterController` |
| **根运动默认开启** | Animator 的 `applyRootMotion = 1`，代码控位移时角色会"自己跑" | 用 NavMeshAgent / 代码移动时，把 `Apply Root Motion` 关掉 |
| **Static 版动作** | `Walk_Static` / `Run_Static` 不产生位移 | 代码控位移时优先用这两个 |
| **渲染管线** | 材质用 Built-in Standard Shader（fileID=46） | URP 项目需先转换材质（`Window > Rendering > Render Pipeline Converter`） |
| **两个 prefab 已修复** | 见第九节 | 覆盖安装修复包 |

---

## 九、两个 Prefab 打包错误修复说明

### 9.1 问题描述

以下 2 个 prefab 存在打包错误：

- `SimpleMilitary_FemaleSoldier_Black.prefab`
- `SimpleMilitary_FemaleMedic_Black.prefab`

**错误内容**：这两个 prefab 内部**错误嵌套了一整个完整角色** `SimpleMilitary_GasMaskSoldier_Brown`（含其 20 个蒙皮和 1 个 Animator），并多挂载了 `MS_SpecialForces_03`、`MS_GasMask`、`MS_BombDisposal` 等节点。

**表现症状**：

| 指标 | 错误值 | 正常值 |
|------|--------|--------|
| Animator 数量 | 2 | 1 |
| SkinnedMeshRenderer 数量 | 40 | 20 |
| 根子节点数 | 22 | 21 |

> 后果：把这两个 prefab 拖入场景会**多出一个隐形的 GasMaskSoldier_Brown 角色**，同时叠加两个 Animator，导致渲染重复、动画混乱。

### 9.2 修复结果

已删除嵌套的错误角色及其多余节点，两个 prefab 现已与正常角色完全一致：

| 指标 | 修复前 | 修复后 | 参照（正常） |
|------|--------|--------|--------------|
| 条目总数 | 214 | 107 | 107 ✅ |
| Animator | 2 | 1 | 1 ✅ |
| SkinnedMeshRenderer | 40 | 20 | 20 ✅ |
| Transform | 86 | 43 | 43 ✅ |
| 骨骼数 | 21 | 21 | 21 ✅ |
| 根子节点数 | 22 | 21 | 21 ✅ |
| 悬空引用检查 | — | 通过 | — ✅ |

### 9.3 安装修复

1. 双击导入 `SimpleMilitary-修复2个Prefab-v1.0.unitypackage`。
2. 导入路径会自动落到 `Assets/SimpleMilitary/Old/Prefabs/Characters/`。
3. 提示覆盖时选择 **Import / Replace**，覆盖掉原来的 2 个错误 prefab 即可。

> 修复包只包含这 2 个 prefab，不影响其他任何资源。

---

## 附：资源路径速查

| 资源 | 路径 |
|------|------|
| 角色 Prefab | `Assets/SimpleMilitary/Old/Prefabs/Characters/` |
| 动画 FBX | `Assets/SimpleMilitary/Old/Models/Characters/Animations*.fbx` |
| 共享 Avatar | `Assets/SimpleMilitary/Old/Models/Characters/SimpleMilitary_Characters_NewRig.fbx` |
| 主控制器 | `Assets/SimpleMilitary/Old/.../SimpleCharacter_5.0.controller` |
| Avatar Mask | `Assets/SimpleMilitary/Old/Models/Characters/Mask_*.mask` |
| 场景参考 | `Assets/SimpleMilitary/Old/Scenes/`（2 个场景） |
