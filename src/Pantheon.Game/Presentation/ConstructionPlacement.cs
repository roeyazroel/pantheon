namespace Pantheon.Game.Presentation;

/// <summary>The house under construction.</summary>
/// <param name="House">Where the house will stand once finished: the slot the next house takes.</param>
/// <param name="Progress">Share of the work done, 0 (just started) to 1 (finished).</param>
public readonly record struct ConstructionPlacement(HutPlacement House, float Progress);
