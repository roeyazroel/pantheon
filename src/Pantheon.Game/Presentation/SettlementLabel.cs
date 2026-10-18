using System;
using System.Globalization;
using Pantheon.Domain.Settlements;

namespace Pantheon.Game.Presentation;

/// <summary>Text of the floating label above a settlement.</summary>
public static class SettlementLabel
{
    /// <summary>Formats the settlement name with its population below it.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <returns>Two-line label text.</returns>
    public static string Format(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        var people = settlement.Population == 1 ? "person" : "people";
        return string.Create(CultureInfo.InvariantCulture, $"{settlement.Name}\n{settlement.Population} {people}");
    }
}
