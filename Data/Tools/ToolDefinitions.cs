namespace SeraphLeveling.Data.Tools
{
    public static class ToolDefinitions
    {
        public static readonly ToolDefinition Pickaxe = new()
        {
            Name = "pickaxe",
            BaseIncrement = SeraphLevelingModSystem.BaseBlocksPerIncrement,
            IncrementStep = SeraphLevelingModSystem.IncrementStep,
            IncrementUnits = "blocks"
        };
    }
}
