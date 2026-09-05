namespace Mvp.Shared
{
    /// <summary>
    /// Type of a building on the battle map (see 建筑与兵工厂系统设计文档 §4.1).
    /// </summary>
    public enum BuildingType
    {
        /// <summary>楼房：被占领后周期性产出金币。</summary>
        House,
        /// <summary>兵工厂：被占领后可消耗金币生产陆地军事单位。</summary>
        Armory
    }
}
