using System;
using System.IO;

namespace Pantheon.Tests.Support;

/// <summary>Locates the repository checkout that the tests were built from.</summary>
internal static class RepositoryRoot
{
    private const string SolutionFileName = "Pantheon.sln";

    private static readonly Lazy<string> Root = new(Find);

    /// <summary>Returns an absolute path inside the repository.</summary>
    /// <param name="relativeParts">Path segments below the repository root.</param>
    /// <returns>The combined path.</returns>
    public static string Combine(params string[] relativeParts) => Path.Combine([Root.Value, .. relativeParts]);

    /// <summary>Walks up from the test assembly's directory to the first directory holding the solution file.</summary>
    /// <returns>The repository root.</returns>
    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"No {SolutionFileName} found above {AppContext.BaseDirectory}; run the tests from a repository checkout.");
    }
}
