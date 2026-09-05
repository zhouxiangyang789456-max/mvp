# Simple Military - Cartoon War 载具动画添加方法

> 适用资源包：`Simple Military - Cartoon War v1.3.8.unitypackage`
> 目标：让坦克、导弹车、装甲车、直升机等静态载具，在**游戏中移动与作战时自动播放对应动画**
> 配套脚本：`Scripts/` 目录（6 个运行时组件 + 1 个编辑器工具）

---

## 0. 先搞清楚一件事：为什么载具不会动

这个资源包里的载具**全部是静态模型，没有任何内嵌动画**。

我把包里所有 `.fbx` 的导入配置都检查了一遍：只有 `Old/Models/Characters/` 下的角色文件带 `clipAnimations`（行走、奔跑、各类持枪射击等），所有载具文件的动画片段列表都是空的。

**但有个好消息**：载具的部件是**按节点拆分好**的，不是一整块死网格。

```
SK_Veh_Tank_01                        ← 根节点（车身）
  └── SK_Veh_Tank_01_Turret_01        ← 炮塔，独立节点
        ├── SK_Veh_Tank_01_MgGun_01   ← 机枪
        ├── SK_Veh_Tank_01_Lid_02     ← 舱盖
        ├── SK_Veh_Tank_01_Lid_01     ← 舱盖
        └── SK_Veh_Tank_01_Barrel_01  ← 炮管
```

这意味着**完全不需要骨骼绑定**——直接旋转这些节点就能做出动画。这是本文所有方案的基础。

### 有一个例外要特别注意

**坦克的履带和车身是同一个网格、同一个材质槽。** 这让"履带滚动"不能直接做（滚动会让整辆车贴图一起动），需要变通。详见第 4.2 节。

---

## 1. 五分钟快速上手

先让炮塔转起来，建立直觉。

1. 把 `SK_Veh_Tank_01` 拖进场景
2. 选中它，菜单执行 **GameObject → Simple Military → 一键配置载具动画**
3. 看 Console 输出，会列出自动挂载的组件
4. 点 Play，在 Inspector 里把 `VehicleTurretAim` 的 `World Point` 拖来拖去，炮塔会跟着转，炮管自动俯仰

轮子、导弹架、旋翼也是同样流程——工具会自动识别节点并填好引用，不用手动拖。

> 如果只想手动配置，跳过第 2 步，直接 AddComponent 挂 `VehicleTurretAim`，把 `Turret_01` 拖进 `Turret` 槽、`Barrel_01` 拖进 `Barrel` 槽即可。

---

## 2. 部件节点速查表

### 2.1 命名规则

包里有两套目录，命名不同，**推荐用 `Optimized/`**：

| 目录 | 命名风格 | 示例 | 说明 |
|---|---|---|---|
| `Optimized/Models/Vehicles/` | `SK_` 前缀，部件后缀 | `SK_Veh_Tank_01_Turret_01` | 规范，部件名带编号 |
| `Old/Models/Vehicles/` | 全小写 | `turret`、`barrel` | 简写，但含义一致 |

部件后缀含义：

| 后缀 | 含义 | 可做动画 |
|---|---|---|
| `_Turret_01` | 炮塔 | 绕 Y 轴旋转 |
| `_Barrel_01` | 炮管 | 绕 X 轴俯仰 |
| `_MgGun_01` | 机枪 | 绕 Y/X 轴 |
| `_Lid_01` / `_Lid_02` | 舱盖 | 绕 X 轴开合 |
| `Wheel_fl/fr/rl/rr` | 车轮（前左/前右/后左/后右） | 绕 X 轴滚动 |
| `Wheel_rl2/rl3/rr2/rr3` | 后桥双轮的附加轮 | 同上 |
| `_Rotor_Main_01` | 主旋翼 | 绕 Y 轴 |
| `_Rotor_Tail_01` | 尾桨 | 绕 X 轴 |
| `_Radar_Turret_01` | 雷达天线 | 绕 Y 轴旋转（雷达扫描很好用） |
| `_CannonBase_01` / `_Cannon_01` | 机炮基座 / 机炮 | 基座 Y 轴、炮管 X 轴 |

### 2.2 全载具清单

| 载具 | 根节点 | 可动部件 |
|---|---|---|
| 坦克 01 | `SK_Veh_Tank_01` | 炮塔、炮管、2 个舱盖、机枪 |
| 坦克 02 | `SK_Veh_tank_02` | 炮塔、炮管、1 个舱盖 |
| 飞毛腿导弹车 | `SK_Veh_ScudTruck_01` | 导弹架（**可起竖**）、导弹、8 个车轮 |
| 装甲车 APC 01 | `SK_Veh_Apc_01` | 炮塔、炮管、8 个车轮 |
| 装甲车 APC 02 | `SK_Veh_Apc_02` | 炮塔、炮管、机枪、舱盖 |
| 轮式装甲车 | `SK_Veh_Armor_Car_01` | 炮塔、炮管、4 个车轮 |
| 攻击直升机 | `SK_Veh_Attack_Heli_01` | 主旋翼、尾桨、机炮基座 + 机炮、8 枚导弹 |
| 小型直升机 | `SK_Veh_Small_Heli_01` | 主旋翼、尾桨 |
| 无人机 | `SK_Veh_Drone_01` | 螺旋桨、前轮、后轮 |
| 雷达车 | `SK_Veh_Radar_Unit_01` | 雷达天线 |
| 野战炮 | `SK_Veh_FieldGun_01` | 炮管、2 个车轮 |
| 运兵车 | `SK_Veh_Troop_Car_01` | 机枪、4 个车轮 |
| 运兵卡车 01/02 | `SK_Veh_Truck_Troop_01/02` | 6 个车轮 |
| 医疗车 | `SK_Veh_Truck_Medic_01` | 6 个车轮 |
| 油罐车 | `SK_Veh_Truck_Fuel_01` | 6 个车轮 |

### 2.3 坐标约定（重要）

实测所有载具：**车头朝 +Z**，车轮沿 **X** 轴左右分布，高度在 **Y**。

由此推出各部件的旋转轴（脚本默认值已按此设置）：

| 部件 | 旋转轴 | 说明 |
|---|---|---|
| 炮塔 | **Y** | 水平旋转 |
| 炮管 | **X** | 抬高时角度为负（脚本已处理） |
| 车轮 | **X** | 滚动 |
| 前轮转向 | **Y** | 左右偏转 |
| 导弹架 | **X** | 起竖（车头朝 Z，绕 X 抬升） |
| 主旋翼 | **Y** | 水平旋转 |
| 尾桨 | **X** | 垂直旋转 |
| 舱盖 | **X** | 向后掀开 |

> 如果你的模型朝向被改过，脚本 Inspector 里每个组件都有轴向参数可以调。

---

## 3. 核心设计：动画怎么"跟着行动"自动播

你要的是载具一跑起来轮子就转、一停下就停。为此我做了 `VehicleMotion` 这个统一速度源。

### 3.1 为什么不直接读速度

载具的移动方式千差万别——可能是 `Rigidbody` 物理，可能是 `NavMeshAgent` 寻路，也可能是你自己写的位移。脚本如果写死某一种，换个项目就废了。

### 3.2 VehicleMotion 的做法

挂在载具根节点上，**自动检测你的移动方式**：

```
Auto 模式依次尝试：
  Rigidbody         → NavMeshAgent       → Transform 帧间位移
  （物理驱动）        （AI 寻路）            （自写位移 / 路径动画）
```

然后对外提供：

| 属性 | 含义 |
|---|---|
| `Speed` | 速度大小（米/秒） |
| `ForwardSpeed` | 沿车头方向的分量，**正=前进，负=倒车** |
| `Velocity` | 世界空间速度向量 |

轮子、履带组件都从它取速度，所以**你只要让载具动起来，动画自动就播了**。

> `ForwardSpeed` 用 `Vector3.Dot(velocity, transform.forward)` 算，所以倒车时是负值——轮子会自动反向转。

### 3.3 如果你的移动逻辑读不到速度

比如移动是自己写的、又不改 Transform（少见），或者你想手动控制：

```csharp
// 方式一：切换为 Manual 模式，手动喂速度
GetComponent<VehicleMotion>().SetSpeed(12f);   // 12 米/秒前进

// 方式二：直接给世界空间速度向量
GetComponent<VehicleMotion>().SetVelocity(rb.velocity);
```

也可以在 Inspector 把 `Source` 从 `Auto` 改成具体某一项，排除自动检测出错的可能。

---

## 4. 六项动画实战

### 4.1 车轮滚动

**组件**：`VehicleWheels`

自动收集名称含 "wheel" 的子节点，按速度滚动。

| 参数 | 建议值 | 说明 |
|---|---|---|
| `Wheel Radius` | 看模型，一般 0.4~0.6 | 半径越小，同样速度转得越快 |
| `Spin Axis` | `(1,0,0)` | X 轴 |
| `Reverse With Velocity` | 勾选 | 倒车时轮子反转 |
| `Enable Steering` | 需要转向时勾选 | 前轮视觉偏转 |

转速公式：`角速度 = 速度 / 半径`，脚本已实现。

**接入转向**：在你的控制脚本里调用

```csharp
GetComponent<VehicleWheels>().SetSteerInput(Input.GetAxis("Horizontal"));
```

工具会自动挑前轮（优先匹配 `front/fl/fr`，否则取本地 Z 坐标最大的两个）。

---

### 4.2 坦克履带滚动（**本节最重要**）

#### 问题

`SK_Veh_Tank_01` 根节点只有 **1 个材质槽**，车身和履带共用网格与材质：

```
SK_Veh_Tank_01    Transform, MeshFilter, MeshRenderer   ← 材质槽数 = 1
```

直接滚动它的 UV，**整辆车的贴图都会跟着动**，看起来像车在原地融化。

`TankTrackScroll` 组件内置了共用检测，发现材质被多个 Renderer 共用时会在 Console 报警告提示你。

#### 三种解决方案

**方案 A：建模软件分离履带（效果最好）**

在 Blender 中：
1. 选中坦克车身，进 Edit Mode
2. 框选履带的面片，`P` → Separate → Selection
3. 给分离出的对象单独一个材质（如 `Tank_Track`），履带贴图设为 Repeat
4. 导出 FBX，作为子节点拖到坦克下，命名 `Track_L` / `Track_R`
5. 挂 `TankTrackScroll`，把这两个 Renderer 拖进去

优点：干净、可控、能做左右差速（转向时两条履带速度不同）。

**方案 B：叠加独立履带贴片（零改模型）**

在履带位置放两个细长 Plane/Cube，贴上履带纹理，挂 `TankTrackScroll` 滚动其 UV。原车身履带不动也看不出来（卡通风格下更不明显）。

优点：不动原模型，10 分钟搞定。缺点：静止时可能看到 z-fighting，需要微调位置。

**方案 C：顶点色遮罩 + 自定义 Shader（无需改拓扑）**

给履带面片刷顶点色作为遮罩，Shader 只在遮罩区域滚动 UV。适合不想拆分网格又要精确控制的场景。

核心思路（Shader 片段）：

```hlsl
// 顶点着色器传出顶点色
// 片元着色器中：
float mask = step(0.5, i.color.r);          // 顶点色 R 通道作为遮罩
float2 uv = i.uv;
uv.y += _ScrollOffset * mask;               // 只在遮罩区滚动
fixed4 col = tex2D(_MainTex, uv);
```

需要把刷好顶点色的履带部分作为独立子网格，并替换材质 Shader。

#### 组件参数

| 参数 | 说明 |
|---|---|
| `Track Renderers` | 履带的 Renderer（左右两条） |
| `Channel` | 沿 U 还是 V 滚动，多数履带贴图沿 **V** |
| `Scroll Per Meter` | 每米滚动多少 UV，贴图重复越多值越大，一般 0.5~2 |
| `Texture Property Name` | Built-in 用 `_MainTex`，**URP/Lit 要改成 `_BaseMap`** |
| `Apply Mode` | `InstanceMaterial`（默认，安全）或 `PropertyBlock`（高性能） |

> `InstanceMaterial` 会复制一份材质再改，**不会污染原始资源文件**。脚本销毁时会自动清理副本。

---

### 4.3 炮塔旋转 + 炮管俯仰

**组件**：`VehicleTurretAim`

三种瞄准模式：

| 模式 | 用途 | 用法 |
|---|---|---|
| `TargetTransform` | 锁定敌人 | 把目标 Transform 拖进 `Target` |
| `WorldPoint` | 鼠标点选地面 | 代码调用 `SetAimPoint(hit.point)` |
| `ManualAngles` | 键盘控制 | 代码调用 `SetManualAngles(yaw, pitch)` |

代码示例：

```csharp
var aim = GetComponent<VehicleTurretAim>();

// 锁定目标
aim.SetAimTarget(enemyTransform);

// 或瞄向鼠标点击处
if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit))
    aim.SetAimPoint(hit.point);

// 对准后再开火（避免甩头时开炮）
if (aim.AimReady) Fire();

// 取炮口位置生成炮口火焰
Vector3 muzzle = aim.GetMuzzlePosition();
```

**角度限位**：`Max Pitch`（最大仰角）、`Min Pitch`（最大俯角）、`Yaw Limit`（炮塔转角，需勾选 `Limit Yaw`）。这些能有效避免炮管插进车体。

---

### 4.4 导弹架起竖与发射

**组件**：`MissileRackLauncher`

用于 `SK_Veh_ScudTruck_01`。注意资源里节点拼作 **`MissleRack`**（少了 i），脚本两种拼写都能识别。

```csharp
var launcher = GetComponent<MissileRackLauncher>();

launcher.Raise();      // 起竖（约 3 秒，带缓动）
launcher.Fire();       // 发射（未起竖会先自动起竖）
launcher.Lower();      // 放平
launcher.Reload();     // 重新装填（导弹归位、架放平）
```

| 参数 | 建议值 | 说明 |
|---|---|---|
| `Raise Angle` | 90 | 垂直发射；倾斜发射可设 45~75 |
| `Raise Duration` | 3 | 起竖耗时，真实感建议 3~5 秒 |
| `Fire Interval` | 0.6 | 连发间隔 |
| `Missile Speed` | 40 | 初速度 |
| `Missile Gravity` | 9.81 | 设 0 则直线飞行（适合火箭弹） |
| `Auto Fire After Raise` | 按需 | 起竖完自动开火 |

发射时导弹会**脱离发射架父节点**并挂上 `MissileProjectile` 飞行组件，超时自动销毁。`Reload()` 可以把它们全部复位。

---

### 4.5 旋翼旋转

**组件**：`HeliRotor`

| 参数 | 建议值 | 说明 |
|---|---|---|
| `Main Rotor Speed` | 2200 | 度/秒，直升机 2000~3000 |
| `Tail Rotor Speed` | 3600 | 尾桨通常更快 |
| `Spin Up Time` | 2.5 | 启动加速时间，做出"转速爬升"感 |
| `Spin Down Time` | 4 | 停机减速时间 |
| `Use Fixed Rate` | 高转速时勾选 | 按固定频率更新，省性能 |

```csharp
var rotor = GetComponent<HeliRotor>();
rotor.StartRotors();   // 启动（缓动到全速）
rotor.StopRotors();    // 停转（缓慢减速，不是瞬间停）
```

无人机 `SK_Veh_Drone_01` 的螺旋桨节点叫 `SK_Veh_Drone_Prop`，同样适用，转速可以调更高。

---

### 4.6 舱盖开合与雷达扫描

这两个简单，给个思路。

**舱盖**：`SK_Veh_Tank_01_Lid_01/02`，绕 X 轴旋转即可。可以用 `VehicleTurretAim` 的思路，或直接：

```csharp
lid.localRotation = Quaternion.AngleAxis(openAngle, Vector3.right);
```

**雷达扫描**：`SK_Veh_Radar_Unit_Radar_Turret_01`，让它匀速绕 Y 轴转：

```csharp
radarTurret.Rotate(Vector3.up, scanSpeed * Time.deltaTime);
```

这两个没有单独做组件，因为需求差异大，直接写两行更灵活。

---

## 5. 接入你现有项目的检查清单

把载具放进游戏前，逐项过一遍：

- [ ] **渲染管线**：本包材质用的是 **Built-in 的 Standard Shader**（136 个材质全如此）。如果你的项目是 URP，导入后材质会变紫，需要执行 `Edit → Rendering → Materials → Convert Selected Built-in Materials to URP`。同时 `TankTrackScroll` 的 `Texture Property Name` 要改成 `_BaseMap`。

- [ ] **碰撞体**：**本包所有载具都没有 Collider 和 Rigidbody**，每个节点只有 `Transform + MeshFilter + MeshRenderer`。进游戏前必须自己加（车体用 Box Collider，地形行走用 Mesh Collider）。一键配置工具会检测并提醒。

- [ ] **移动逻辑对接**：确认载具是怎么动的，对照 3.2 节确认 `VehicleMotion` 能读到速度。Console 里勾上 `Debug Log` 可以实时看速度来源和数值。

- [ ] **Prefab 变体**：不建议直接改原始 Prefab。做法是创建 Prefab Variant，或拖进场景后 Override，方便后续资源包升级。

- [ ] **性能**：旋翼高转速时开 `Use Fixed Rate`；履带用 `PropertyBlock` 模式可避免生成材质实例。

---

## 6. 避坑指南

| 坑 | 现象 | 解决 |
|---|---|---|
| 坦克履带动不了 / 整车贴图乱动 | 车身与履带共用材质槽 | 见 4.2，必须先分离履带 |
| 材质被改坏，其他车也跟着变 | 直接改了 `sharedMaterial` | `TankTrackScroll` 默认 `InstanceMaterial` 模式已规避 |
| 轮子不转 | 速度读不到 | 检查 `VehicleMotion` 的 `Source`，勾 `Debug Log` 看输出 |
| 轮子转但方向反了 | 车头判定反了 | 改 `VehicleMotion.ForwardSpeed` 符号，或翻转 `Spin Axis` |
| 炮管插进车体 | 俯仰无限位 | 设置 `Min Pitch`，一般 5~10 度 |
| 炮塔疯狂抖动 | 目标点每帧跳变 | 提高 `Yaw Speed` 或给目标点加平滑 |
| 导弹发射后跟着车飞 | 未解除父子关系 | `MissileRackLauncher` 已处理，检查是否自己发射 |
| 找不到 `MissleRack` 节点 | 资源包拼写错误 | 脚本兼容 Missile/Missle 两种拼写 |
| Play 模式下改的参数丢失 | 正常，Unity 机制 | 退出 Play 前复制组件值，或在 Prefab 模式下改 |
| 一键配置后 Console 无输出 | 未选中根节点 | 必须在 Hierarchy 选中载具根对象再执行 |

---

## 7. 脚本文件清单

```
Scripts/
├── VehicleMotion.cs          速度源统一（核心，建议所有载具都挂）
├── VehicleWheels.cs          车轮滚动 + 前轮转向
├── TankTrackScroll.cs        履带 UV 滚动（含材质共用检测）
├── VehicleTurretAim.cs       炮塔旋转 + 炮管俯仰
├── MissileRackLauncher.cs    导弹架起竖 + 发射 + 装填
├── HeliRotor.cs              主旋翼 + 尾桨（含启停缓动）
└── Editor/
    └── VehicleAutoSetup.cs   一键配置工具（必须放 Editor 目录下）
```

**安装**：把 `Scripts` 整个文件夹拖进 Unity 项目的 `Assets` 下即可。命名空间都是 `SimpleMilitary.VehicleAnimation`，不会和你现有代码冲突。

**最小依赖**：只用 Unity 标准 API（`UnityEngine`、`UnityEngine.AI`），无需任何第三方插件。

---

## 9. 一键导入资源包

不想手动拷贝文件夹的话，直接导入现成的资源包：

**`SimpleMilitary-VehicleAnimation-v1.0.unitypackage`**（24 KB）

双击它，或在 Unity 中 `Assets → Import Package → Custom Package`，全选导入即可。

导入后位于：

```
Assets/SimpleMilitary/VehicleAnimation/
├── Scripts/          6 个运行时组件
│   └── Editor/       1 个一键配置工具
└── Docs/
    └── 使用文档.md    本文（可在 Unity 中直接查看）
```

导入后菜单项出现在 **GameObject → Simple Military → 一键配置载具动画**。

### 自己重新打包

如果你修改了脚本，用随附的工具重新打包：

```bash
python 打包工具.py    # 生成 unitypackage
python 验证包.py      # 校验包结构是否正确
```

打包工具会自动为每个资产生成 GUID 和 Unity 元数据（`.cs` → MonoImporter、`.md` → TextScriptImporter、文件夹 → DefaultImporter）。改了文件清单就编辑 `打包工具.py` 顶部的 `FILES` 列表。

---

## 8. 常见问题

**Q：能不能直接用 Unity 的 Animation 窗口录关键帧，不写代码？**

能。选中炮塔节点 → Window → Animation → Create，录制旋转关键帧即可。但**轮子滚动和履带滚动做不到**——它们需要跟随实时速度变化，关键帧无法动态响应。建议：固定动作（舱盖开合、起竖演示）用 Animation Clip，动态响应（轮子、履带、瞄准）用脚本。

**Q：需要 Wheel Collider 吗？**

看你需求。本文方案是**纯视觉动画**——轮子按速度转，车身位移由你自己控制（物理、寻路或代码位移都行）。如果你要真实的悬挂、摩擦、打滑，才需要 Wheel Collider，但那要重做载具的物理结构，本包模型没有为物理做过拆分，成本很高。卡通风格的载具用视觉方案性价比最高。

**Q：能用 AI 生成这些动画吗？**

目前 AI 在载具动画上基本帮不上忙。原因：这类动画本质是**部件级的精确旋转约束**（炮塔绕 Y 转、炮管限位多少度），是规则明确的程序化行为，不是 AI 擅长的"生成"任务。反倒是脚本是最优解——写一次，所有同类载具通用。AI 更适合用在：生成履带贴图、生成载具迷彩变体、生成 UI 图标。

**Q：动画能复用吗？比如我做了 20 辆车。**

能，这正是脚本方案的优势。配置好一辆车后，把它存成 Prefab Variant，或者用一键配置工具批量处理。节点命名规范，工具能自动识别，20 辆车几分钟就配完。
