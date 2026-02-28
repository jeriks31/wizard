namespace Wizard.Api.Services;

public sealed class GameFlowOptions
{
    public const string SectionName = "GameFlow";

    public int TrickRevealDelayMs { get; set; } = 1200;
}
