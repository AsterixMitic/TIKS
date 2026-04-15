using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;

namespace Npp.PlaywrightTests;

internal static class TestSettings
{
    private sealed record SettingsFileConfig(string? Path, Dictionary<string, string> Values);

    private static readonly Lazy<SettingsFileConfig> SettingsFile = new(LoadSettingsFileConfig);

    public static string FrontendUrl => GetRequiredSetting("NPP_FRONTEND_URL");

    public static string BackendUrl => GetRequiredSetting("NPP_BACKEND_URL");

    public static string PlaywrightArtifactsDir
    {
        get
        {
            var raw = GetRequiredSetting("NPP_PW_ARTIFACTS_DIR");
            if (Path.IsPathRooted(raw))
            {
                return raw;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, raw));
        }
    }

    public static bool PlaywrightHeadless => GetRequiredBool("NPP_PW_HEADLESS");

    public static float PlaywrightSlowMoMs => GetRequiredFloat("NPP_PW_SLOWMO_MS");

    public static int PlaywrightViewportWidth => GetRequiredInt("NPP_PW_VIEWPORT_WIDTH");

    public static int PlaywrightViewportHeight => GetRequiredInt("NPP_PW_VIEWPORT_HEIGHT");

    public static ColorScheme PlaywrightColorScheme => ParseColorScheme(GetRequiredSetting("NPP_PW_COLOR_SCHEME"));

    private static string GetRequiredSetting(string key)
    {
        var envValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue.Trim();
        }

        if (SettingsFile.Value.Values.TryGetValue(key, out var fileValue) && !string.IsNullOrWhiteSpace(fileValue))
        {
            return fileValue.Trim();
        }

        var settingsPath = SettingsFile.Value.Path ?? "(not found)";
        throw new InvalidOperationException(
            $"Missing required Playwright setting '{key}'. " +
            $"Set environment variable '{key}' or add it to settings file ({settingsPath}).");
    }

    private static bool GetRequiredBool(string key)
    {
        var raw = GetRequiredSetting(key);
        if (bool.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Invalid boolean value for '{key}': '{raw}'. Allowed values: true|false.");
    }

    private static int GetRequiredInt(string key)
    {
        var raw = GetRequiredSetting(key);
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Invalid integer value for '{key}': '{raw}'.");
    }

    private static float GetRequiredFloat(string key)
    {
        var raw = GetRequiredSetting(key);
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Invalid float value for '{key}': '{raw}'. Use '.' as decimal separator.");
    }

    private static ColorScheme ParseColorScheme(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "light" => ColorScheme.Light,
            "dark" => ColorScheme.Dark,
            "no-preference" => ColorScheme.NoPreference,
            "nopreference" => ColorScheme.NoPreference,
            _ => throw new InvalidOperationException(
                $"Invalid value for 'NPP_PW_COLOR_SCHEME': '{value}'. Allowed: dark|light|no-preference.")
        };
    }

    private static SettingsFileConfig LoadSettingsFileConfig()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = ResolveSettingsFilePath();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new SettingsFileConfig(path, values);
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Playwright settings file must be a JSON object. File: {path}");
        }

        foreach (var prop in document.RootElement.EnumerateObject())
        {
            var kind = prop.Value.ValueKind;
            var value = kind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => throw new InvalidOperationException(
                    $"Unsupported JSON value type for '{prop.Name}' in settings file: {kind}")
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                values[prop.Name] = value.Trim();
            }
        }

        return new SettingsFileConfig(path, values);
    }

    private static string? ResolveSettingsFilePath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("NPP_TEST_SETTINGS_FILE");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "playwright.settings.json"),
            Path.Combine(AppContext.BaseDirectory, "playwright.settings.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "playwright.settings.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "playwright.settings.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "playwright.settings.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "NppTests", "Npp.PlaywrightTests", "playwright.settings.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "NppTests", "Npp.PlaywrightTests", "playwright.settings.json"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
