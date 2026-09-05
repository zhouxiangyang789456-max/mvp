namespace Mvp.Shared
{
    /// <summary>
    /// Ownership of a building. Independent from <see cref="TeamId"/> so unit
    /// AI / vision / combat are untouched (see 建筑与兵工厂系统设计文档 §18.1).
    /// </summary>
    public enum BuildingOwner
    {
        Neutral,
        Player,
        Enemy
    }
}
