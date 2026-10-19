using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migrations of Milestone 15, parts 3 and 4 (ADR-023, ADR-024), run by <see cref="SaveMigrations"/>.</summary>
internal static class SocietySaveMigration
{
    /// <summary>
    /// Version 14 to 15: every settlement gains a society. Version 14 had none, so each starts as a
    /// newly founded settlement does: safe, fairly content and settled, poor and unlearned, knowing
    /// nothing yet, with no buildings beyond its houses and no priority.
    /// </summary>
    internal static void UpgradeFrom14(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Settlements"] is not JsonArray settlements)
        {
            return;
        }

        foreach (var node in settlements)
        {
            if (node is JsonObject settlement)
            {
                AddNode(settlement, "Society", Founded(), fromVersion: 14);
            }
        }
    }

    /// <summary>
    /// Version 15 to 16 (ADR-024): beliefs gain dependency and resentment, settlements a culture and
    /// gods their conduct and traits. Version 15 knew none of these, so nobody depends on or resents
    /// a god yet, no settlement holds a doctrine or has turned from the gods, and no god has done
    /// anything the world remembers.
    /// </summary>
    internal static void UpgradeFrom15(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        if (world["Settlements"] is JsonArray settlements)
        {
            foreach (var node in settlements)
            {
                if (node is not JsonObject settlement)
                {
                    continue;
                }

                AddNode(settlement, "Culture", Culture(), fromVersion: 15);
                if (settlement["Beliefs"] is JsonArray beliefs)
                {
                    foreach (var belief in beliefs)
                    {
                        if (belief is JsonObject feelings)
                        {
                            AddMember(feelings, "Dependency", 0f, fromVersion: 15);
                            AddMember(feelings, "Resentment", 0f, fromVersion: 15);
                        }
                    }
                }
            }
        }

        if (world["Migration"]?["Groups"] is JsonArray groups)
        {
            foreach (var group in groups)
            {
                if (group?["Beliefs"] is JsonArray carried)
                {
                    foreach (var belief in carried)
                    {
                        if (belief is JsonObject feelings)
                        {
                            AddMember(feelings, "Dependency", 0f, fromVersion: 15);
                            AddMember(feelings, "Resentment", 0f, fromVersion: 15);
                        }
                    }
                }
            }
        }

        if (world["Gods"] is JsonArray gods)
        {
            foreach (var node in gods)
            {
                if (node is JsonObject god)
                {
                    AddNode(god, "Conduct", Conduct(), fromVersion: 15);
                    AddNode(god, "Traits", new JsonArray(), fromVersion: 15);
                }
            }
        }
    }

    /// <summary>A culture holding no doctrine and devout, as JSON.</summary>
    private static JsonObject Culture() => new()
    {
        ["Pacifism"] = 0f, ["Militarism"] = 0f, ["Sacrifice"] = 0f, ["Charity"] = 0f, ["NatureWorship"] = 0f,
        ["KnowledgeSeeking"] = 0f, ["Expansionism"] = 0f, ["Isolationism"] = 0f, ["Secularism"] = 0f,
    };

    /// <summary>The conduct of a god that has done nothing yet, as JSON.</summary>
    private static JsonObject Conduct() => new()
    {
        ["Help"] = 0f, ["Gifts"] = 0f, ["Harm"] = 0f, ["Cruelty"] = 0f, ["Vengeance"] = 0f, ["Upheaval"] = 0f,
        ["Deceit"] = 0f, ["Faithfulness"] = 0f, ["Aggression"] = 0f,
    };

    /// <summary>A newly founded settlement's society, as JSON.</summary>
    private static JsonObject Founded() => new()
    {
        ["Safety"] = 1f,
        ["Happiness"] = 0.6f,
        ["Wealth"] = 0f,
        ["Education"] = 0.1f,
        ["Stability"] = 0.7f,
        ["Technology"] = new JsonObject
        {
            ["Agriculture"] = 0f, ["Architecture"] = 0f, ["Medicine"] = 0f, ["Warfare"] = 0f,
            ["Navigation"] = 0f, ["Engineering"] = 0f, ["Science"] = 0f, ["Religion"] = 0f,
        },
        ["Buildings"] = new JsonArray(),
        ["Priority"] = "None",
        ["PriorityGodId"] = null,
        ["PrioritySinceTick"] = 0,
        ["Project"] = null,
        ["ProjectProgress"] = 0f,
        ["LastFoundingTick"] = 0,
    };
}
