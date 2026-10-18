using System;

namespace Pantheon.Persistence;

/// <summary>A data-driven content file (for example <c>worldgen.json</c>) cannot be parsed.</summary>
public sealed class ContentFormatException : Exception
{
    /// <summary>Creates the exception without a message.</summary>
    public ContentFormatException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What is wrong with the content.</param>
    public ContentFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What is wrong with the content.</param>
    /// <param name="innerException">The underlying parse error.</param>
    public ContentFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
