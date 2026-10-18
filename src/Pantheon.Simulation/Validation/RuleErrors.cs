using System;
using System.Collections.Generic;

namespace Pantheon.Simulation.Validation;

/// <summary>
/// Collects every problem found in a data-driven content record, so a validator reports them all
/// at once and the person editing the file can fix them in one pass.
/// </summary>
internal sealed class RuleErrors
{
    private readonly List<string> _errors = [];

    /// <summary>Records an error message.</summary>
    /// <param name="message">What is wrong.</param>
    public void Add(string message) => _errors.Add(message);

    /// <summary>Records an error unless <paramref name="value"/> is at least <paramref name="minimum"/>.</summary>
    public void RequireAtLeast(int value, int minimum, string name)
    {
        if (value < minimum)
        {
            Add($"{name} must be at least {minimum}.");
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> is a finite, non-negative number.</summary>
    public void RequireNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            Add($"{name} must be a finite, non-negative number.");
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> is a finite number above zero.</summary>
    public void RequirePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            Add($"{name} must be a finite number above 0.");
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> lies in 0..1.</summary>
    public void RequireShare(float value, string name)
    {
        if (!(value is >= 0f and <= 1f))
        {
            Add($"{name} must lie between 0 and 1.");
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> lies in -1..1.</summary>
    public void RequireSignedShare(float value, string name)
    {
        if (!(value is >= -1f and <= 1f))
        {
            Add($"{name} must lie between -1 and 1.");
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> lies in 0..1.</summary>
    public void RequireProbability(double value, string name)
    {
        if (!(value is >= 0.0 and <= 1.0))
        {
            Add($"{name} must lie between 0 and 1.");
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> is a defined member of its enum.</summary>
    public void RequireDefined<TEnum>(TEnum value, string name)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            Add($"{name} has an unknown value {value}.");
        }
    }

    /// <summary>Throws an <see cref="ArgumentException"/> listing every recorded error, if there are any.</summary>
    /// <param name="title">What was checked, for example "settlement rules".</param>
    /// <param name="parameterName">Name of the checked parameter.</param>
    public void ThrowIfAny(string title, string parameterName)
    {
        if (_errors.Count > 0)
        {
            throw new ArgumentException($"Invalid {title}:" + Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", _errors), parameterName);
        }
    }
}
