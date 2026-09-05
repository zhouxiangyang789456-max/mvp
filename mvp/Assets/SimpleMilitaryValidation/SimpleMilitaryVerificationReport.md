# Simple Military 单位验证报告

生成时间：2026-09-02 23:40:52

## 人物单位（Black 配色代表）

| 人物 | Animator | Avatar | 动画片段 | 渲染器 | 材质 | 结论 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| BombDisposal | 1 | 1 | 55 | 20 | 完整 | 通过 |
| EasternSoldier | 1 | 1 | 55 | 20 | 完整 | 通过 |
| FemaleMedic | 1 | 1 | 55 | 20 | 完整 | 通过 |
| FemaleSoldier | 1 | 1 | 55 | 20 | 完整 | 通过 |
| GasMaskSoldier | 1 | 1 | 55 | 20 | 完整 | 通过 |
| General | 1 | 1 | 55 | 20 | 完整 | 通过 |
| GermanSoldier | 1 | 1 | 55 | 20 | 完整 | 通过 |
| JungleCommando | 1 | 1 | 55 | 20 | 完整 | 通过 |
| Medic | 1 | 1 | 55 | 20 | 完整 | 通过 |
| Mercenary | 1 | 1 | 55 | 20 | 完整 | 通过 |
| Pilot | 1 | 1 | 55 | 20 | 完整 | 通过 |
| Soldier01 | 1 | 1 | 55 | 20 | 完整 | 通过 |
| SpecialForces01 | 1 | 1 | 55 | 20 | 完整 | 通过 |
| SpecialForces02 | 1 | 1 | 55 | 20 | 完整 | 通过 |
| SpecialForces03 | 1 | 1 | 55 | 20 | 完整 | 通过 |
| SpecialForces04 | 1 | 1 | 55 | 20 | 完整 | 通过 |
| Terrorist01 | 1 | 1 | 55 | 20 | 完整 | 通过 |
| Terrorist02 | 1 | 1 | 55 | 20 | 完整 | 通过 |
| Terrorist03 | 1 | 1 | 55 | 20 | 完整 | 通过 |
| TrainingSoldier | 1 | 1 | 55 | 20 | 完整 | 通过 |

## 优化版载具

| 载具 | 网格渲染器 | 材质 | 自动配置 | 结论/限制 |
| --- | ---: | ---: | --- | --- |
| SK_Veh_Apc_01 | 12 | 完整 | 车轮×8、炮塔/炮管 | 通过 |
| SK_Veh_Apc_02 | 6 | 完整 | 炮塔/炮管 | 通过；履带滚动待分离网格 |
| SK_Veh_Armor_Car_01 | 8 | 完整 | 车轮×4、炮塔/炮管 | 通过 |
| SK_Veh_Attack_Heli_01 | 14 | 完整 | 炮塔/炮管、旋翼 | 通过 |
| SK_Veh_Drone_01 | 5 | 完整 | 车轮×2、旋翼 | 通过 |
| SK_Veh_FieldGun_01 | 5 | 完整 | 车轮×2、炮塔/炮管 | 通过 |
| SK_Veh_Radar_Unit_01 | 3 | 完整 | 炮塔/炮管 | 通过 |
| SK_Veh_ScudTruck_01 | 12 | 完整 | 车轮×8、导弹架 | 通过 |
| SK_Veh_Small_Heli_01 | 4 | 完整 | 旋翼 | 通过 |
| SK_Veh_Tank_01 | 7 | 完整 | 炮塔/炮管 | 通过；履带滚动待分离网格 |
| SK_Veh_tank_02 | 5 | 完整 | 炮塔/炮管 | 通过；履带滚动待分离网格 |
| SK_Veh_Troop_Car_01 | 7 | 完整 | 车轮×4、炮塔/炮管 | 通过 |
| SK_Veh_Truck_Fuel_01 | 8 | 完整 | 车轮×6 | 通过 |
| SK_Veh_Truck_Medic_01 | 8 | 完整 | 车轮×6 | 通过 |
| SK_Veh_Truck_Troop_01 | 8 | 完整 | 车轮×6 | 通过 |
| SK_Veh_Truck_Troop_02 | 8 | 完整 | 车轮×6 | 通过 |

## 汇总

- 人物：20 种（各用 Black 配色代表三套配色验证）
- 载具：16 个优化版 Prefab
- 场景缺失脚本：0
- 人物预览动作：Idle → Walk → Run → 自动步枪单发 → Death_01
- 已知限制：两种坦克和 APC_02 的履带与车身共用网格/材质，本阶段不修改模型，因此不启用履带 UV 滚动。
- 本场景未加入 Build Settings，也未连接现有战斗规则。
