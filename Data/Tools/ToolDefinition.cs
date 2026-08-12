namespace SeraphLeveling.Data.Tools
{
    public record class ToolDefinition
    {
        public required string Name { get; init; }
        public required int BaseIncrement { get; init; }
        public required int IncrementStep { get; init; }
        public required string IncrementUnits { get; init; }
    }
}
