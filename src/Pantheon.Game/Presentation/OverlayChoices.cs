using System;
using System.Collections.Generic;
using System.Linq;

namespace Pantheon.Game.Presentation;

/// <summary>The map's overlays as the god's bar offers them (ADR-068): by name, the debug overlays only with the debug tools on.</summary>
public static class OverlayChoices
{
    /// <summary>The overlays offered, in the overlay key's order.</summary>
    /// <param name="debugTools">Whether the debug tools are on.</param>
    /// <returns>The overlays.</returns>
    public static IReadOnlyList<OverlayKind> Offered(bool debugTools) => Enum.GetValues<OverlayKind>().Where(kind => debugTools || !IsDebug(kind)).ToList();

    /// <summary>Whether an overlay is a debug tool: the navigation and authority overlays of ADR-044.</summary>
    /// <param name="kind">The overlay.</param>
    /// <returns><c>true</c> for a debug overlay.</returns>
    public static bool IsDebug(OverlayKind kind) => kind is OverlayKind.Navigation or OverlayKind.Authority;

    /// <summary>An overlay's name, for the player.</summary>
    /// <param name="kind">The overlay.</param>
    /// <returns>Its name.</returns>
    public static string Title(OverlayKind kind) => kind switch
    {
        OverlayKind.Influence => "Influence: whose power reaches where",
        OverlayKind.Religion => "Religion: the god each village follows most",
        OverlayKind.Faith => "Faith in you",
        OverlayKind.Food => "Food: how well each village is fed",
        OverlayKind.Weather => "Weather: the water in the soil",
        OverlayKind.Danger => "Danger: fires, scorched land and shields",
        OverlayKind.Migration => "Migration: pressure to leave, and people on the move",
        OverlayKind.Culture => "Culture: doctrines and turning from the gods",
        OverlayKind.Nature => "Nature: the land's life and its wounds",
        OverlayKind.Population => "Population",
        OverlayKind.Risk => "Disaster risk",
        OverlayKind.Politics => "Political control",
        OverlayKind.Navigation => "Navigation (debug)",
        OverlayKind.Authority => "Authority (debug)",
    };
}
