namespace Chubb.Claims.Api.Diagnostics;

public static class LoggingConfiguration
{
    // Resolve from the application's content root so startup working directories do not move logs.
    // This configures each host independently and does not replace the global static logger.
    public static void ResolveFilePaths(IConfiguration configuration, string contentRootPath)
    {
        foreach (var sink in configuration.GetSection("Serilog:WriteTo").GetChildren())
        {
            if (!string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase))
                continue;

            var configuredPath = sink["Args:path"];
            if (string.IsNullOrWhiteSpace(configuredPath))
                throw new InvalidOperationException("A Serilog file sink requires a configured path.");

            var expandedPath = Environment.ExpandEnvironmentVariables(configuredPath);
            var absolutePath = Path.GetFullPath(expandedPath, contentRootPath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (directory is not null) Directory.CreateDirectory(directory);
            configuration[$"{sink.Path}:Args:path"] = absolutePath;
        }
    }
}
