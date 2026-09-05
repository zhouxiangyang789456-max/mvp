# Simple Military 载具使用说明

> 适用资源包：`Simple Military - Cartoon War v1.3.8.unitypackage`
> 配套脚本：`SimpleMilitary-VehicleAnimation-v1.0.unitypackage`（载具动画组件包）
> 命名空间：`SimpleMilitary.VehicleAnimation`
> 文档目标：说明载具资源如何接入游戏、动画组件如何使用、有哪些坑

---

## 目录

1. [一、结论速览](#一结论速览)
2. [二、载具资源总览](#二载具资源总览)
3. [三、载具部件命名规则（关键）](#三载具部件命名规则关键)
4. [四、动画组件总览（7 个脚本）](#四动画组件总览7-个脚本)
5. [五、一键配置（最快接入）](#五一键配置最快接入)
6. [六、组件逐一使用说明](#六组件逐一使用说明)
7. [七、典型接入代码示例](#七典型接入代码示例)
8. [八、使用注意事项（坑位清单）](#八使用注意事项坑位清单)
9. [九、坦克履带滚动专项说明](#九坦克履带滚动专项说明)

---

## 一、结论速览

| 项目 | 结论 |
|------|------|
| **载具是否有动画** | ❌ 原包内**载具全部静态，无任何内嵌动画** |
| **能否做动画** | ✅ 可以。部件已按节点拆分，脚本直接旋转/平移 Transform 即可 |
| **载具总数** | **55 个 prefab**（Optimized 16 + Old 39） |
| **载具种类** | **16 种**（坦克、装甲车、卡车、直升机、无人机、雷达车、导弹车、火炮等） |
| **动画组件** | 7 个 C# 脚本（轮子/履带/炮塔/导弹架/旋翼/速度源/一键配置） |
| **需手动补齐** | ❌ 无 Collider、无 Rigidbody（需自行添加） |

**一句话总结**：载具本身没有任何动画，但模型已把轮子、炮塔、炮管、旋翼等部件拆成独立节点，配合动画组件包即可实现"载具移动时自动播放对应动画"。接入游戏前先给载具加碰撞体和刚体。

---

## 二、载具资源总览

载具分布在两套目录：

| 目录 | 前缀 | 数量 | 说明 |
|------|------|------|------|
| `Optimized/Prefabs/Vehicles/` | `SK_Veh_` | 16 | **推荐**，优化版，部件命名规范（大写） |
| `Old/Prefabs/Vehicles/` | 小写 | 39 | 旧版，13 种 × 3 配色（_a/_b/_c） |

### 2.1 Optimized 载具清单（推荐使用）

| # | 载具 | 类型 | 可动画部件 |
|---|------|------|-----------|
| 1 | SK_Veh_Tank_01 | 坦克 | 炮塔、炮管、履带 |
| 2 | SK_Veh_tank_02 | 坦克 2 | 炮塔、炮管、履带 |
| 3 | SK_Veh_Apc_01 | 装甲运兵车 | 车轮 |
| 4 | SK_Veh_Apc_02 | 装甲运兵车 2 | 车轮 |
| 5 | SK_Veh_Armor_Car_01 | 装甲车 | 车轮 |
| 6 | SK_Veh_Troop_Car_01 | 运兵车 | 车轮 |
| 7 | SK_Veh_Truck_Troop_01 | 运兵卡车 | 车轮 |
| 8 | SK_Veh_Truck_Troop_02 | 运兵卡车 2 | 车轮 |
| 9 | SK_Veh_Truck_Fuel_01 | 油罐卡车 | 车轮 |
| 10 | SK_Veh_Truck_Medic_01 | 医疗卡车 | 车轮 |
| 11 | SK_Veh_Attack_Heli_01 | 武装直升机 | 主旋翼、尾旋翼 |
| 12 | SK_Veh_Small_Heli_01 | 小型直升机 | 主旋翼、尾旋翼 |
| 13 | SK_Veh_Drone_01 | 无人机 | 旋翼 |
| 14 | SK_Veh_Radar_Unit_01 | 雷达车 | 雷达转台 |
| 15 | SK_Veh_ScudTruck_01 | 导弹车（飞毛腿） | 导弹发射架 |
| 16 | SK_Veh_FieldGun_01 | 野战火炮 | 炮管 |

### 2.2 Old 载具（13 种 × 3 配色）

`apc_01` / `apc_02` / `armor_car` / `attack_heli` / `radar_unit` / `small_heli` / `tank_01` / `tank_02` / `troop_car` / `truck_Medic` / `truck_fuel` / `truck_troop` / `truck_troop_02`，每种配 `_a` / `_b` / `_c` 三套涂装。

> 旧版部件命名是小写（`turret` / `barrel`），新版是大写（`_Turret_01`）。动画组件同时兼容两种，但**推荐用 Optimized 版**。

---

## 三、载具部件命名规则（关键）

载具的各个可动部件都是独立子节点，动画组件**靠节点名关键词自动识别**。这是整套方案的基础。

### 3.1 部件命名对照表

| 部件 | 命名关键词 | 动画方式 |
|------|-----------|---------|
| 车轮 | `Wheel`（如 `Wheel_fl` / `Wheel_fr` / `Wheel_rl` / `Wheel_rr`） | 绕 X 轴滚动 + 前轮绕 Y 轴转向 |
| 炮塔 | `_Turret_` | 绕 Y 轴（水平旋转） |
| 炮管 | `_Barrel_` | 绕 X 轴（俯仰） |
| 机枪 | `_MgGun_` | 绕 X 轴俯仰 |
| 舱盖 | `_Lid_` | 绕任意轴开合 |
| 主旋翼 | `_Rotor_Main_` | 绕 Y 轴高速旋转 |
| 尾旋翼 | `_Rotor_Tail_` | 绕 X 轴高速旋转 |
| 雷达转台 | `_Radar_Turret_` | 绕 Y 轴旋转 |
| 导弹架 | `Missle` / `Missile`（资源包拼写为 `Missle`，少了 i） | 绕 X 轴起竖 |

### 3.2 坐标约定（务必记住）

实测得到的坐标约定：

| 方向 | 轴 |
|------|-----|
| 车头朝向 | **+Z** |
| 车轮左右分布 | 沿 **X** 轴 |
| 车轮滚动 | 绕 **X** 轴 |
| 炮塔 / 雷达 / 主旋翼旋转 | 绕 **Y** 轴 |
| 炮管 / 导弹架俯仰 | 绕 **X** 轴 |
| 尾旋翼旋转 | 绕 **X** 轴 |

---

## 四、动画组件总览（7 个脚本）

导入 `SimpleMilitary-VehicleAnimation-v1.0.unitypackage` 后，可在 Inspector 的 `Add Component` 菜单 `Simple Military/载具动画/` 下找到：

| 脚本 | 用途 | 挂载对象 |
|------|------|---------|
| `VehicleMotion` | **速度源统一**：从 Rigidbody/NavMeshAgent/Transform 自动读取速度 | 载具根节点 |
| `VehicleWheels` | 车轮滚动 + 前轮转向 | 载具根节点 |
| `TankTrackScroll` | 履带 UV 滚动（坦克专用） | 载具根节点 |
| `VehicleTurretAim` | 炮塔指向目标（水平 + 俯仰） | 载具根节点 |
| `MissileRackLauncher` | 导弹架起竖 + 发射 | 载具根节点 |
| `HeliRotor` | 直升机旋翼旋转 + 启动/停机渐变 | 载具根节点 |
| `Editor/VehicleAutoSetup` | 一键自动配置（菜单项） | 无需手动挂载 |

**核心设计**：`VehicleMotion` 是唯一的速度来源，其余组件通过它拿到速度，实现"载具一动，动画自动跟着动"。

### 4.1 速度自动检测链（Auto 模式）

`VehicleMotion.SpeedSource.Auto` 会依次尝试：

```
Rigidbody（物理速度）
   → NavMeshAgent（寻路速度）
      → Transform 帧间位移（手动移动）
         → Manual（手动 SetSpeed）
```

---

## 五、一键配置（最快接入）

最快的方式是用自动配置菜单：

1. 场景中选中载具根节点（例如 `SK_Veh_Tank_01`）。
2. 顶部菜单 `GameObject > Simple Military > 一键配置载具动画`。
3. 工具会按节点名自动：
   - 识别车轮 / 炮塔 / 炮管 / 导弹架 / 旋翼等部件；
   - 挂载对应组件并自动接线引用；
   - 挂载 `VehicleMotion` 作为速度源。

**注意**：工具会自动识别出以下情况并给出警告：

- 若是**坦克**（履带无法自动配置，见第九节）；
- 若载具**没有 Collider**（提示需手动添加）。

---

## 六、组件逐一使用说明

### 6.1 VehicleMotion（速度源）

挂载在载具根节点，是整套动画的"心脏"。

**关键属性 / 方法**：

| 成员 | 说明 |
|------|------|
| `SpeedSource source` | 速度来源（Auto / Rigidbody / NavMeshAgent / TransformDelta / Manual） |
| `float Speed` | 当前速度（标量） |
| `Vector3 Velocity` | 当前速度向量 |
| `float ForwardSpeed` | 前进方向速度（`Vector3.Dot(velocity, forward)`，倒车为负） |
| `SetSpeed(float)` | 手动设速度 |
| `SetVelocity(Vector3)` | 手动设速度向量 |
| `bool debugLog` | 调试日志开关 |

> `ForwardSpeed` 用点积计算，所以**倒车会得到负值**，车轮组件据此自动反向旋转。

### 6.2 VehicleWheels（车轮）

自动收集名字含 `wheel` 的子节点，按 `ω = v / r` 计算角速度。

**关键属性 / 方法**：

| 成员 | 说明 |
|------|------|
| `bool autoFindWheels` | 自动收集车轮（默认开） |
| `float wheelRadius` | 车轮半径（默认 0.5） |
| `Vector3 spinAxis` | 滚动轴（默认 X） |
| `Vector3 steerAxis` | 转向轴（默认 Y） |
| `bool enableSteering` | 是否启用前轮转向 |
| `float maxSteerAngle` | 最大转向角（默认 30°） |
| `SetSteerInput(float)` | 设置转向输入（-1~1） |
| `CollectWheels()` | 手动重新收集车轮（右键菜单） |
| `AutoPickSteeringWheels()` | 自动挑选前轮（匹配 front/fl/fr，否则按 Z 取前 2） |

> 转向轮旋转合成：`AngleAxis(steer, Y) * AngleAxis(spin, X)`，即先转向再滚动。

### 6.3 VehicleTurretAim（炮塔指向）

让炮塔和炮管指向目标（敌方单位或世界坐标）。

**关键属性 / 方法**：

| 成员 | 说明 |
|------|------|
| `Transform turret` / `barrel` | 炮塔 / 炮管节点 |
| `AimMode mode` | 目标来源（TargetTransform / WorldPoint / ManualAngles） |
| `float yawLimit` / `maxPitch` / `minPitch` | 水平 / 俯仰角度限制 |
| `float yawSpeed` / `pitchSpeed` | 转向平滑速度 |
| `float CurrentYaw` / `CurrentPitch` | 当前角度（只读） |
| `bool AimReady` | 是否已指向目标（只读） |
| `SetAimTarget(Transform)` / `SetAimPoint(Vector3)` / `SetManualAngles(yaw, pitch)` | 设定目标 |
| `GetMuzzlePosition()` | 获取炮口世界坐标（用于发射） |

### 6.4 MissileRackLauncher（导弹架发射）

处理起竖 / 放下 / 发射 / 装填整套流程。

**关键属性 / 方法**：

| 成员 | 说明 |
|------|------|
| `Transform rack` | 发射架节点 |
| `float raiseAngle` / `raiseDuration` | 起竖角度（默认 90°）/ 时长 |
| `bool autoFireAfterRaise` | 起竖后自动发射 |
| `float fireInterval` / `missileSpeed` / `missileGravity` / `missileLifetime` | 发射间隔 / 速度 / 重力 / 寿命 |
| `int MissilesLeft` | 剩余导弹数（只读） |
| `Raise()` / `Lower()` / `Fire()` / `Reload()` | 起竖 / 放下 / 发射 / 装填 |
| `CollectMissiles()` | 收集导弹节点（兼容 `Missle` / `Missile` 两种拼写） |

> 发射时会把导弹解除父子关系，并挂上 `MissileProjectile` 组件（自带 `Initialize` / `Explode`），实现抛物线飞行和爆炸。

### 6.5 HeliRotor（直升机旋翼）

处理主旋翼 + 尾旋翼的旋转，带启动/停机的转速渐变。

**关键属性 / 方法**：

| 成员 | 说明 |
|------|------|
| `Transform mainRotor` / `tailRotor` | 主旋翼 / 尾旋翼节点 |
| `float mainRotorSpeed` / `tailRotorSpeed` | 转速（默认 2200 / 3600） |
| `Vector3 mainAxis` / `tailAxis` | 旋转轴（默认 Y / X） |
| `bool autoStart` | 启动即转（默认开） |
| `float spinUpTime` / `spinDownTime` | 启动 / 停机渐变时长 |
| `bool IsRunning` / `float NormalizedSpeed` | 运行状态 / 归一化转速（只读） |
| `StartRotors()` / `StopRotors()` | 启动 / 停止旋翼 |

### 6.6 TankTrackScroll（履带滚动）

坦克专用，用 UV 滚动模拟履带转动（详见第九节）。

**关键属性 / 方法**：

| 成员 | 说明 |
|------|------|
| `ScrollChannel channel` | 滚动方向（U / V） |
| `ApplyMode mode` | 应用方式（InstanceMaterial / PropertyBlock） |
| `float scrollPerMeter` | 每米滚动量 |
| `PrepareMaterials()` | 准备材质（自动实例化避免污染源资产） |
| `bool HasSharedMaterialWarning` | 是否检测到共享材质（只读） |

---

## 七、典型接入代码示例

### 7.1 坦克：自动行驶 + 炮塔指向敌人

```csharp
using UnityEngine;
using UnityEngine.AI;
using SimpleMilitary.VehicleAnimation;

public class TankController : MonoBehaviour
{
    public VehicleMotion motion;
    public VehicleWheels wheels;
    public VehicleTurretAim turret;
    public Transform enemy;

    NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        // 速度源设为 NavMeshAgent 后，轮子/履带会自动跟随
        motion.SetVelocity(agent.velocity);
    }

    void Update()
    {
        if (enemy != null)
            turret.SetAimTarget(enemy);

        if (turret.AimReady)
        {
            // 炮口对准后可以开火
        }
    }
}
```

### 7.2 直升机：旋翼随油门渐变

```csharp
using SimpleMilitary.VehicleAnimation;

public class HeliController : MonoBehaviour
{
    public HeliRotor rotor;

    void Takeoff() => rotor.StartRotors();
    void Land()     => rotor.StopRotors();
}
```

### 7.3 导弹车：起竖后齐射

```csharp
using SimpleMilitary.VehicleAnimation;

public class ScudController : MonoBehaviour
{
    public MissileRackLauncher launcher;

    public void Launch()
    {
        launcher.Raise();          // 起竖（约 3 秒）
        // 起竖完成后自动发射（若 autoFireAfterRaise = true）
    }
}
```

### 7.4 手动速度源（无物理系统时）

```csharp
using SimpleMilitary.VehicleAnimation;

public class SimpleMover : MonoBehaviour
{
    public VehicleMotion motion;
    public float speed = 10f;

    void Update()
    {
        motion.SetSpeed(speed);   // 手动喂速度，轮子/旋翼自动响应
    }
}
```

---

## 八、使用注意事项（坑位清单）

| 坑位 | 说明 | 应对 |
|------|------|------|
| **无 Collider / Rigidbody** | 所有载具只有 `Transform` + `MeshFilter` + `MeshRenderer`，**没有碰撞体和刚体** | 手动添加 `BoxCollider` + `Rigidbody`，或用 NavMeshAgent |
| **坦克履带材质共享** | `SK_Veh_Tank_01` 根节点只有 1 个材质槽，车身和履带共用同一材质 | UV 滚动会带动整个车身，需专项处理（见第九节） |
| **拼写错误 `Missle`** | 资源包里导弹节点名是 `MissleRack` / `Missle_01`（少了 i） | 脚本已兼容两种拼写，无需改资源 |
| **渲染管线** | 136 个材质用 Built-in Standard Shader（fileID=46） | URP 项目需先转材质（`Window > Rendering > Render Pipeline Converter`） |
| **两套目录** | Optimized（SK_ 大写）与 Old（小写）并存 | 推荐用 Optimized 版；脚本兼容两者 |
| **`_MainTex` vs `_BaseMap`** | Built-in 用 `_MainTex`，URP 用 `_BaseMap` | `TankTrackScroll` 的 UV 滚动需对应切换纹理属性名 |

---

## 九、坦克履带滚动专项说明

### 9.1 问题

`SK_Veh_Tank_01`（及 Old 的 tank_01）根节点**只有一个材质槽**，车身和履带共用同一张纹理和网格。直接用 `Material.SetTextureOffset` 滚动 UV，会导致**整个坦克的贴图一起滚动**。

### 9.2 三种解决方案

| 方案 | 做法 | 优缺点 |
|------|------|--------|
| **A. 分离履带网格** | 在 Blender 里把履带从车身拆成独立网格 + 独立材质 | ✅ 最彻底；❌ 需改模型 |
| **B. 叠加独立履带片** | 额外做几片独立履带贴片盖在原履带上，只滚动这些片 | ✅ 不改原模型；❌ 需额外建模 |
| **C. 顶点色遮罩 + 自定义 Shader** | 用顶点色标记履带区域，写 Shader 只滚动标记区域 | ✅ 灵活；❌ 需写 Shader（附 HLSL 片段） |

方案 C 的 Shader 核心思路（Built-in）：

```hlsl
// 顶点色 R 通道作为遮罩，只在遮罩区域滚动 UV
float scroll = _Time.y * _ScrollSpeed;
float mask = IN.color.r;                     // 顶点色 R = 履带区域
float2 uv = IN.uv_MainTex;
uv.y += scroll * mask;                        // 只滚动遮罩区域
fixed4 c = tex2D(_MainTex, uv);
```

### 9.3 建议

- **快速验证**：先用方案 B 或 C；
- **正式上线**：优先方案 A，在 Blender 里把履带分离成独立材质，最干净。

---

## 附：资源路径速查

| 资源 | 路径 |
|------|------|
| Optimized 载具 | `Assets/SimpleMilitary/Optimized/Prefabs/Vehicles/` |
| Old 载具 | `Assets/SimpleMilitary/Old/Prefabs/Vehicles/` |
| 动画组件脚本 | `Assets/SimpleMilitary/VehicleAnimation/Scripts/` |
| 一键配置工具 | `Assets/SimpleMilitary/VehicleAnimation/Scripts/Editor/VehicleAutoSetup.cs` |

> 配套方案文档见《Simple Military 载具动画添加方法.md》（详细设计 + 6 种动画实现 + FAQ）。
