using Pantheon.Domain.Disasters;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Disasters;

/// <summary>Checks <see cref="HazardRules"/> (ADR-029) into a disaster check's errors.</summary>
internal static class HazardRulesValidator
{
    /// <summary>Adds every problem with the hazards and the new preparations.</summary>
    /// <param name="errors">The disaster check's errors.</param>
    /// <param name="rules">The disaster rules.</param>
    public static void Check(RuleErrors errors, DisasterRules rules)
    {
        var prepared = rules.Preparedness;
        errors.RequireShare(prepared.SeaWallDamage, "Preparedness.SeaWallDamage");
        errors.RequireShare(prepared.DamDamage, "Preparedness.DamDamage");
        errors.RequireShare(prepared.EvacuationDeaths, "Preparedness.EvacuationDeaths");

        var hazards = rules.Hazards;
        var tsunami = hazards.Tsunami;
        errors.RequireNonNegative(tsunami.CoastDistance, "Hazards.Tsunami.CoastDistance");
        errors.RequireNonNegative(tsunami.RadiusPerMagnitude, "Hazards.Tsunami.RadiusPerMagnitude");
        errors.RequireNonNegative(tsunami.HeightPerMagnitude, "Hazards.Tsunami.HeightPerMagnitude");
        errors.RequireShare(tsunami.HousingLoss, "Hazards.Tsunami.HousingLoss");
        errors.RequireShare(tsunami.Deaths, "Hazards.Tsunami.Deaths");

        var slide = hazards.Landslide;
        errors.RequireShare(slide.SlidePerSecond, "Hazards.Landslide.SlidePerSecond");
        errors.RequirePositive(slide.MinDrop, "Hazards.Landslide.MinDrop");
        errors.RequireShare(slide.WetSoil, "Hazards.Landslide.WetSoil");
        errors.RequireShare(slide.ShakeChance, "Hazards.Landslide.ShakeChance");
        errors.RequireShare(slide.HousingLoss, "Hazards.Landslide.HousingLoss");
        errors.RequireShare(slide.Deaths, "Hazards.Landslide.Deaths");
        errors.RequireShare(slide.Damage, "Hazards.Landslide.Damage");
        errors.RequireShare(slide.RubbleClearPerSecond, "Hazards.Landslide.RubbleClearPerSecond");

        var volcano = hazards.Volcano;
        errors.RequireNonNegative(volcano.EruptionsPerHour, "Hazards.Volcano.EruptionsPerHour");
        errors.RequirePositive(volcano.RumbleSeconds, "Hazards.Volcano.RumbleSeconds");
        errors.RequirePositive(volcano.EruptSeconds, "Hazards.Volcano.EruptSeconds");
        errors.RequirePositive(volcano.TremorSeconds, "Hazards.Volcano.TremorSeconds");
        errors.RequireAtLeast(volcano.LavaRegions, 1, "Hazards.Volcano.LavaRegions");
        errors.RequireNonNegative(volcano.AshRadius, "Hazards.Volcano.AshRadius");
        errors.RequireShare(volcano.AshDamagePerSecond, "Hazards.Volcano.AshDamagePerSecond");
        errors.RequireShare(volcano.AshHealthLossPerSecond, "Hazards.Volcano.AshHealthLossPerSecond");
        errors.RequireShare(volcano.AshFoodLossPerSecond, "Hazards.Volcano.AshFoodLossPerSecond");
        errors.RequireNonNegative(volcano.CooldownSeconds, "Hazards.Volcano.CooldownSeconds");

        var tornado = hazards.Tornado;
        errors.RequireShare(tornado.PerStormPerSecond, "Hazards.Tornado.PerStormPerSecond");
        errors.RequirePositive(tornado.Radius, "Hazards.Tornado.Radius");
        errors.RequirePositive(tornado.MinSeconds, "Hazards.Tornado.MinSeconds");
        Range(errors, tornado.MinSeconds, tornado.MaxSeconds, "Hazards.Tornado");
        errors.RequireShare(tornado.HousingLossPerSecond, "Hazards.Tornado.HousingLossPerSecond");
        errors.RequireShare(tornado.DeathsPerSecond, "Hazards.Tornado.DeathsPerSecond");
        errors.RequireShare(tornado.DamagePerSecond, "Hazards.Tornado.DamagePerSecond");

        var meteor = hazards.Meteor;
        errors.RequireNonNegative(meteor.PerHour, "Hazards.Meteor.PerHour");
        errors.RequirePositive(meteor.Radius, "Hazards.Meteor.Radius");
        errors.RequireShare(meteor.HousingLoss, "Hazards.Meteor.HousingLoss");
        errors.RequireShare(meteor.Deaths, "Hazards.Meteor.Deaths");
        errors.RequireShare(meteor.Scar, "Hazards.Meteor.Scar");

        var blight = hazards.Blight;
        errors.RequireShare(blight.EmergencePerSecond, "Hazards.Blight.EmergencePerSecond");
        errors.RequireShare(blight.WetSoil, "Hazards.Blight.WetSoil");
        errors.RequireShare(blight.GrowthPerSecond, "Hazards.Blight.GrowthPerSecond");
        errors.RequireShare(blight.SpreadPerSecond, "Hazards.Blight.SpreadPerSecond");
        errors.RequireShare(blight.DeclinePerSecond, "Hazards.Blight.DeclinePerSecond");
        errors.RequireShare(blight.BlessingCurePerSecond, "Hazards.Blight.BlessingCurePerSecond");
        errors.RequireShare(blight.Notice, "Hazards.Blight.Notice");

        var locusts = hazards.Locusts;
        errors.RequireNonNegative(locusts.SwarmsPerHour, "Hazards.Locusts.SwarmsPerHour");
        errors.RequireShare(locusts.MaxSoil, "Hazards.Locusts.MaxSoil");
        errors.RequirePositive(locusts.Radius, "Hazards.Locusts.Radius");
        errors.RequireNonNegative(locusts.Speed, "Hazards.Locusts.Speed");
        errors.RequirePositive(locusts.MinSeconds, "Hazards.Locusts.MinSeconds");
        Range(errors, locusts.MinSeconds, locusts.MaxSeconds, "Hazards.Locusts");
        errors.RequireShare(locusts.FoodEatenPerSecond, "Hazards.Locusts.FoodEatenPerSecond");
        errors.RequireShare(locusts.RainLossPerSecond, "Hazards.Locusts.RainLossPerSecond");
        errors.RequireShare(locusts.DamagePerSecond, "Hazards.Locusts.DamagePerSecond");
        errors.RequirePositive(hazards.MarkSeconds, "Hazards.MarkSeconds");
    }

    /// <summary>A longest life no shorter than the shortest.</summary>
    private static void Range(RuleErrors errors, float min, float max, string name)
    {
        if (max < min)
        {
            errors.Add($"{name}.MaxSeconds must be at least {name}.MinSeconds.");
        }
    }
}
