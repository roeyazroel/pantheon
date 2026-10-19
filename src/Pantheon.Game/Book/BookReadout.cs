using System;

namespace Pantheon.Game.Book;

/// <summary>The Book of the World (spec §57, ADR-031): its pages, built from the session. Pure.</summary>
public static class BookReadout
{
    /// <summary>Builds a page.</summary>
    /// <param name="session">The session.</param>
    /// <param name="tab">Which page.</param>
    /// <returns>The page.</returns>
    public static BookPage Build(GameSession session, BookTab tab)
    {
        ArgumentNullException.ThrowIfNull(session);
        return tab switch
        {
            BookTab.World => WorldPage.Build(session),
            BookTab.Religion => ReligionPage.Build(session),
            BookTab.Miracles => MiraclesPage.Build(session),
            BookTab.Creature => CreaturePage.Build(session),
            BookTab.Villager => VillagerPage.Build(session),
        };
    }

    /// <summary>A page's name on its tab.</summary>
    /// <param name="tab">The page.</param>
    /// <returns>For example "Religion".</returns>
    public static string Name(BookTab tab) => tab switch
    {
        BookTab.World => "World",
        BookTab.Religion => "Religion",
        BookTab.Miracles => "Miracles",
        BookTab.Creature => "Creature",
        BookTab.Villager => "Villager",
    };
}
