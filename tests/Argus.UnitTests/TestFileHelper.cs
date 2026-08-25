namespace Argus.UnitTests;

internal static class TestFileHelper
{
    public static string GetSampleEmailPath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "samples",
            "emails",
            fileName));
    }
}