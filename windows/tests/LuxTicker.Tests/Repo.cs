namespace LuxTicker.Tests;

public static class Repo
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LuxTicker.sln"))
                || File.Exists(Path.Combine(dir.FullName, "src", "LuxTicker.Core", "LuxTicker.Core.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

}
