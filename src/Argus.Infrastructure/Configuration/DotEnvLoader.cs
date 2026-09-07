namespace Argus.Infrastructure.Configuration;

public static class DotEnvLoader
{
    public static string? LoadRepositoryEnvironment(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var environmentPath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(environmentPath))
            {
                DotNetEnv.Env.NoClobber().Load(environmentPath);
                return environmentPath;
            }

            directory = directory.Parent;
        }

        return null;
    }
}