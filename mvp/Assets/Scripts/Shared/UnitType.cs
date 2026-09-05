namespace Mvp.Shared
{
    /// <summary>
    /// Unit archetypes used in the battle (see 建筑与兵工厂系统设计文档 §7.2).
    /// MVP uses this enum as the stable unit identity for catalog, production and spawning.
    /// </summary>
    public enum UnitType
    {
        Infantry,
        MachineGunner,
        Scout,
        ScoutCar,
        Tank,
        HeavyTank,
        SelfPropelledArtillery,
        RocketArtillery
    }
}
