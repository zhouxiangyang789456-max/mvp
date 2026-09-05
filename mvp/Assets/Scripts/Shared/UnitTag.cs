namespace Mvp.Shared
{
    /// <summary>
    /// Composite unit classification tags (see 建筑与兵工厂系统设计文档 §7.1).
    /// A unit can carry multiple tags, e.g. 侦察兵 = Infantry | Scout | CanCaptureBuilding.
    /// This replaces the removed bool UnitDefinition.CanCaptureCity.
    /// </summary>
    [System.Flags]
    public enum UnitTag
    {
        None = 0,
        Infantry = 1 << 0,            // 步系单位
        CanCaptureBuilding = 1 << 1,  // 可占领建筑
        Scout = 1 << 2,               // 视野单位
        Vehicle = 1 << 3,             // 车辆
        CloseMechanical = 1 << 4,     // 近程机械
        LongRangeMechanical = 1 << 5, // 远程机械
        Transport = 1 << 6,           // 载具或运输单位
        Ranged = 1 << 7,              // 远程攻击单位（远攻技能资格，不依赖 AttackRangeMax 猜测）
        Tank = 1 << 8                 // 坦克系单位（冲刺技能资格，不依赖模型/名称判断）
    }
}
