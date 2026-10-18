using System;

namespace Pantheon.Persistence;

/// <summary>A save file is malformed, inconsistent, or uses an unsupported <see cref="SaveGame.SaveVersion"/>.</summary>
public sealed class SaveFormatException : Exception
{
    /// <summary>Creates the exception without a message.</summary>
    public SaveFormatException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What is wrong with the save.</param>
    public SaveFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What is wrong with the save.</param>
    /// <param name="innerException">The underlying parse or I/O error.</param>
    public SaveFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
