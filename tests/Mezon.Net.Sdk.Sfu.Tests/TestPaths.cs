namespace Mezon.Net.Sdk.Sfu.Tests;

internal static class TestPaths
{
    /// <summary>The Mezon.Net checkout (the directory holding Mezon.Net.sln).</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    private static string FindRepoRoot()
    {
        // Set by the e2e harness when tests run in a container with --artifacts-path (outputs outside the checkout).
        if (Environment.GetEnvironmentVariable("MEZON_NET_ROOT") is { Length: > 0 } fromEnv && File.Exists(Path.Combine(fromEnv, "Mezon.Net.sln")))
        {
            return fromEnv;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Mezon.Net.sln")))
            {
                return dir.FullName;
            }
        }

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Mezon.Net.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Mezon.Net.sln not found above the test assembly");
    }

    /// <summary>An optional sibling checkout (or the directory named by <paramref name="variable"/>), when it contains <paramref name="probe"/>.</summary>
    public static string? Optional(string variable, string sibling, string probe)
    {
        var dir = Environment.GetEnvironmentVariable(variable) is { Length: > 0 } fromEnv
            ? fromEnv
            : Path.GetFullPath(Path.Combine(RepoRoot, "..", sibling));
        return File.Exists(Path.Combine(dir, probe)) ? dir : null;
    }
}
