namespace Mvp.Shared
{
    /// <summary>
    /// Hand-off data carried from CommanderSelectScene to BattleScene.
    /// Stored statically so the battle page knows which commander was picked.
    /// </summary>
    public static class BattleStartContext
    {
        public static CommanderDefinition SelectedCommander;
    }
}
