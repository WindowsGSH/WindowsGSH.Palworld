using System.Diagnostics;
using System.Globalization;
using System.Text;
using WindowsGSH.Core.Modules;
using WindowsGSH.Core.Readiness;

namespace WindowsGSH.Modules.Palworld;

public sealed class PalworldModule :
    ManifestBackedGameServerModule,
    IModuleExistingServerImportCapability,
    IModuleReadinessCapability
{
    private const string PrimaryExecutable = "PalServer.exe";
    private const string LegacyExecutable = "Pal/Binaries/Win64/PalServer-Win64-Shipping-Cmd.exe";
    private const string ConfigRelativePath = "Pal/Saved/Config/WindowsServer/PalWorldSettings.ini";
    private const string DefaultConfigRelativePath = "DefaultPalWorldSettings.ini";
    private const string Win64LogPath = "Pal/Binaries/Win64/logs";
    private const string SavedLogPath = "Pal/Saved/Logs";

    public override ModuleCapabilities Capabilities => Manifest.ToCapabilities(
        supportsQuery: false,
        supportsRcon: false);

    public override ServerDisplayInfo GetDisplayInfo(ServerInstance instance)
    {
        return new ServerDisplayInfo(
            IpAddress: GetSetting(instance, "network.publicIp", GetSetting(instance, "network.ip", "0.0.0.0")),
            Port: GetSetting(instance, "network.port", "8211"),
            MaxPlayers: GetSetting(instance, "server.maxPlayers", "32"));
    }

    public override Task<InstallPlan> CreateInstallPlanAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        return Task.FromResult(new InstallPlan(
            "steamcmd",
            $"+force_install_dir \"{instance.InstallPath}\" +login anonymous +app_update 2394010 validate +quit",
            instance.InstallPath,
            [
                "Palworld dedicated server Steam app: 2394010.",
                "The official Windows entry point is PalServer.exe.",
                "WindowsGSH writes Pal/Saved/Config/WindowsServer/PalWorldSettings.ini before start."
            ]));
    }

    public override bool IsInstallValid(ServerInstance instance)
    {
        return ResolveExecutablePath(instance.InstallPath) != null;
    }

    public override Task<ProcessStartInfo> CreateStartInfoAsync(
        ServerInstance instance,
        CancellationToken cancellationToken)
    {
        var executable = ResolveExecutablePath(instance.InstallPath)
            ?? throw new FileNotFoundException("Palworld server executable was not found.", Path.Combine(instance.InstallPath, PrimaryExecutable));

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = instance.InstallPath,
            Arguments = BuildLaunchArguments(instance),
            UseShellExecute = !ConsoleInputStrategyPolicy.UsesRedirectedStreams(Runtime),
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = ConsoleInputStrategyPolicy.UsesRedirectedStreams(Runtime),
            RedirectStandardOutput = ConsoleInputStrategyPolicy.UsesRedirectedStreams(Runtime),
            RedirectStandardError = ConsoleInputStrategyPolicy.UsesRedirectedStreams(Runtime)
        };

        return Task.FromResult(startInfo);
    }

    public override string? GetConsoleLogPath(ServerInstance instance)
    {
        var win64Logs = Path.Combine(instance.InstallPath, NormalizePath(Win64LogPath));
        if (Directory.Exists(win64Logs))
        {
            return win64Logs;
        }

        return Path.Combine(instance.InstallPath, NormalizePath(SavedLogPath));
    }

    protected override string BuildLaunchArguments(ServerInstance instance)
    {
        var args = new List<string>();

        if (GetBool(instance.Settings, "launch.performancePreset", true))
        {
            args.Add("-useperfthreads");
            args.Add("-NoAsyncLoadingThread");
            args.Add("-UseMultithreadForDS");
        }

        args.Add($"-port={GetInt(instance.Settings, "network.port", 8211)}");
        var queryPort = GetInt(instance.Settings, "network.queryPort", 27015);
        if (queryPort > 0)
        {
            args.Add($"-QueryPort={queryPort}");
        }

        var workerThreads = GetInt(instance.Settings, "launch.workerThreads", 0);
        if (workerThreads > 0)
        {
            args.Add($"-NumberOfWorkerThreadsServer={workerThreads}");
        }

        if (GetBool(instance.Settings, "server.publicLobby", false))
        {
            args.Add("-publiclobby");

            if (GetBool(instance.Settings, "network.overridePublicEndpoint", false))
            {
                var publicIp = GetSetting(instance.Settings, "network.publicIp", string.Empty);
                if (!string.IsNullOrWhiteSpace(publicIp))
                {
                    args.Add($"-publicip={QuoteArgument(publicIp)}");
                }

                var publicPort = GetInt(instance.Settings, "network.publicPort", 0);
                if (publicPort > 0)
                {
                    args.Add($"-publicport={publicPort}");
                }
            }
        }

        if (!GetBool(instance.Settings, "mods.enabled", true))
        {
            args.Add("-NoMods");
        }
        else
        {
            var workshopRoot = GetSetting(instance.Settings, "mods.workshopRootDir", string.Empty);
            if (!string.IsNullOrWhiteSpace(workshopRoot))
            {
                args.Add($"-workshopdir={QuoteArgument(workshopRoot)}");
            }
        }

        var extraArguments = SanitizeAdditionalArguments(GetSetting(instance.Settings, "server.additionalArguments", string.Empty));
        if (!string.IsNullOrWhiteSpace(extraArguments))
        {
            args.Add(extraArguments);
        }

        return string.Join(' ', args);
    }

    public override Task<IReadOnlyDictionary<string, object?>> ReadConfigFileSettingsAsync(
        ServerInstance instance,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(instance.InstallPath, NormalizePath(ConfigRelativePath));
        if (!File.Exists(path))
        {
            return Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>());
        }

        var options = OptionSettings.Parse(File.ReadAllText(path));
        var settings = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        CopyString(options, settings, "ServerName", "server.name");
        CopyString(options, settings, "ServerDescription", "server.description");
        CopyString(options, settings, "AdminPassword", "server.adminPassword");
        CopyString(options, settings, "ServerPassword", "server.password");
        CopyString(options, settings, "PublicIP", "network.publicIp");
        CopyString(options, settings, "LogFormatType", "server.logFormat");
        CopyNumber(options, settings, "PublicPort", "network.publicPort");
        CopyNumber(options, settings, "QueryPort", "network.queryPort");
        CopyNumber(options, settings, "ServerPlayerMaxNum", "server.maxPlayers");
        CopyNumber(options, settings, "RESTAPIPort", "rest.port");
        CopyBool(options, settings, "RESTAPIEnabled", "rest.enabled");
        CopyString(options, settings, "Difficulty", "game.difficulty");
        CopyString(options, settings, "DeathPenalty", "game.deathPenalty");
        CopyNumber(options, settings, "AutoSaveSpan", "game.autoSaveSpan");
        CopyNumber(options, settings, "DayTimeSpeedRate", "game.dayTimeSpeedRate");
        CopyNumber(options, settings, "NightTimeSpeedRate", "game.nightTimeSpeedRate");
        CopyNumber(options, settings, "ExpRate", "game.expRate");
        CopyNumber(options, settings, "PalCaptureRate", "game.palCaptureRate");
        CopyNumber(options, settings, "PalSpawnNumRate", "game.palSpawnNumRate");
        CopyNumber(options, settings, "CollectionDropRate", "game.collectionDropRate");
        CopyNumber(options, settings, "EnemyDropItemRate", "game.enemyDropItemRate");
        CopyNumber(options, settings, "WorkSpeedRate", "game.workSpeedRate");
        CopyNumber(options, settings, "PalEggDefaultHatchingTime", "game.palEggDefaultHatchingTime");
        CopyBool(options, settings, "bAllowClientMod", "game.allowClientMod");
        CopyBool(options, settings, "bIsPvP", "game.pvp");
        CopyBool(options, settings, "bEnablePlayerToPlayerDamage", "game.playerToPlayerDamage");
        CopyBool(options, settings, "bEnableDefenseOtherGuildPlayer", "game.guildDefense");
        CopyBool(options, settings, "bEnableFriendlyFire", "game.friendlyFire");
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(settings);
    }

    public override Task WriteConfigFileSettingsAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(instance.InstallPath, NormalizePath(ConfigRelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        var options = LoadOptionSettings(instance.InstallPath, configPath);
        options.SetRaw("Difficulty", GetSetting(instance.Settings, "game.difficulty", "None"));
        options.SetRaw("DeathPenalty", GetSetting(instance.Settings, "game.deathPenalty", "All"));
        options.SetNumber("DayTimeSpeedRate", GetDouble(instance.Settings, "game.dayTimeSpeedRate", 1));
        options.SetNumber("NightTimeSpeedRate", GetDouble(instance.Settings, "game.nightTimeSpeedRate", 1));
        options.SetNumber("ExpRate", GetDouble(instance.Settings, "game.expRate", 1));
        options.SetNumber("PalCaptureRate", GetDouble(instance.Settings, "game.palCaptureRate", 1));
        options.SetNumber("PalSpawnNumRate", GetDouble(instance.Settings, "game.palSpawnNumRate", 1));
        options.SetNumber("CollectionDropRate", GetDouble(instance.Settings, "game.collectionDropRate", 1));
        options.SetNumber("EnemyDropItemRate", GetDouble(instance.Settings, "game.enemyDropItemRate", 1));
        options.SetNumber("WorkSpeedRate", GetDouble(instance.Settings, "game.workSpeedRate", 1));
        options.SetNumber("PalEggDefaultHatchingTime", GetDouble(instance.Settings, "game.palEggDefaultHatchingTime", 72));
        options.SetNumber("AutoSaveSpan", GetDouble(instance.Settings, "game.autoSaveSpan", 30));
        options.SetString("ServerName", GetSetting(instance.Settings, "server.name", "Default Palworld Server"));
        options.SetString("ServerDescription", GetSetting(instance.Settings, "server.description", string.Empty));
        options.SetString("AdminPassword", GetSetting(instance.Settings, "server.adminPassword", string.Empty));
        options.SetString("ServerPassword", GetSetting(instance.Settings, "server.password", string.Empty));
        options.SetNumber("ServerPlayerMaxNum", GetInt(instance.Settings, "server.maxPlayers", 32));
        options.SetString("PublicIP", GetSetting(instance.Settings, "network.publicIp", string.Empty));
        options.SetNumber("PublicPort", GetInt(instance.Settings, "network.publicPort", GetInt(instance.Settings, "network.port", 8211)));
        var queryPort = GetInt(instance.Settings, "network.queryPort", 27015);
        if (queryPort > 0)
        {
            options.SetNumber("QueryPort", queryPort);
        }
        else
        {
            options.Remove("QueryPort");
        }

        // RCON is deprecated and is not managed by this module. Preserve an operator's
        // existing values instead of silently disabling an independently configured client.
        options.SetBool("RESTAPIEnabled", GetBool(instance.Settings, "rest.enabled", false));
        options.SetNumber("RESTAPIPort", GetInt(instance.Settings, "rest.port", 8212));
        options.SetString("LogFormatType", GetSetting(instance.Settings, "server.logFormat", "text"));
        options.SetBool("bAllowClientMod", GetBool(instance.Settings, "game.allowClientMod", false));
        options.SetBool("bIsPvP", GetBool(instance.Settings, "game.pvp", false));
        options.SetBool("bEnablePlayerToPlayerDamage", GetBool(instance.Settings, "game.playerToPlayerDamage", false));
        options.SetBool("bEnableDefenseOtherGuildPlayer", GetBool(instance.Settings, "game.guildDefense", false));
        options.SetBool("bEnableFriendlyFire", GetBool(instance.Settings, "game.friendlyFire", false));

        File.WriteAllText(configPath, options.ToFileText());
        WriteModSettings(instance);
        return Task.CompletedTask;
    }

    public bool CanImport(string path)
    {
        return ResolveImportInstallPath(path) != null;
    }

    public async Task<ModuleExistingServerImportProbe> PreviewImportAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var installPath = ResolveImportInstallPath(path) ?? path;
        var settings = GetConfigFields().ToDictionary(
            field => field.Key,
            field => field.DefaultValue,
            StringComparer.OrdinalIgnoreCase);

        var tempInstance = new ServerInstance(
            "palworld-import",
            Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Id,
            installPath,
            installPath,
            Path.Combine(installPath, "ServerConfig.json"),
            settings);

        foreach (var pair in await ReadConfigFileSettingsAsync(tempInstance, cancellationToken).ConfigureAwait(false))
        {
            settings[pair.Key] = pair.Value;
        }

        var warnings = new List<string>();
        if (!File.Exists(Path.Combine(installPath, NormalizePath(ConfigRelativePath))))
        {
            warnings.Add("PalWorldSettings.ini was not found. WindowsGSH will create one from defaults on first start.");
        }

        if (File.Exists(Path.Combine(installPath, NormalizePath(LegacyExecutable))) &&
            !File.Exists(Path.Combine(installPath, PrimaryExecutable)))
        {
            warnings.Add("Detected the older Win64 shipping executable layout. The module can start it, but a SteamCMD validate should restore the current PalServer.exe entry point.");
        }

        return new ModuleExistingServerImportProbe(
            Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            installPath,
            settings,
            warnings);
    }

    public Task<IReadOnlyList<ReadinessCheckResult>> CheckReadinessAsync(
        ServerInstance instance,
        CancellationToken cancellationToken)
    {
        var checks = new List<ReadinessCheckResult>();
        var executable = ResolveExecutablePath(instance.InstallPath);
        checks.Add(executable == null
            ? ReadinessCheckResult.Fail("Palworld executable", $"Missing {PrimaryExecutable}. Run install/update with SteamCMD app 2394010.")
            : ReadinessCheckResult.Pass("Palworld executable", $"Found: {executable}"));

        var configPath = Path.Combine(instance.InstallPath, NormalizePath(ConfigRelativePath));
        checks.Add(File.Exists(configPath)
            ? ReadinessCheckResult.Pass("Palworld settings", $"Found: {configPath}")
            : ReadinessCheckResult.Info("Palworld settings", "PalWorldSettings.ini will be created from defaults before the server starts."));

        if (GetBool(instance.Settings, "rest.enabled", false) &&
            string.IsNullOrWhiteSpace(GetSetting(instance.Settings, "server.adminPassword", string.Empty)))
        {
            checks.Add(ReadinessCheckResult.Warning(
                "Palworld REST API",
                "REST API is enabled but Admin Password is empty. Palworld uses the admin password for REST authentication."));
        }

        if (GetBool(instance.Settings, "game.pvp", false) &&
            (!GetBool(instance.Settings, "game.playerToPlayerDamage", false) ||
             !GetBool(instance.Settings, "game.guildDefense", false)))
        {
            checks.Add(ReadinessCheckResult.Info(
                "Palworld PvP",
                "Official PvP guidance enables PvP, player-to-player damage, and guild defense together."));
        }

        return Task.FromResult<IReadOnlyList<ReadinessCheckResult>>(checks);
    }

    private static OptionSettings LoadOptionSettings(string installPath, string configPath)
    {
        if (File.Exists(configPath))
        {
            return OptionSettings.Parse(File.ReadAllText(configPath));
        }

        var defaultPath = Path.Combine(installPath, DefaultConfigRelativePath);
        if (File.Exists(defaultPath))
        {
            return OptionSettings.Parse(File.ReadAllText(defaultPath));
        }

        return OptionSettings.CreateDefault();
    }

    private static void WriteModSettings(ServerInstance instance)
    {
        var activeMods = GetSetting(instance.Settings, "mods.activeModList", string.Empty);
        if (string.IsNullOrWhiteSpace(activeMods) && GetBool(instance.Settings, "mods.enabled", true))
        {
            return;
        }

        var modsDirectory = Path.Combine(instance.InstallPath, "Mods");
        Directory.CreateDirectory(modsDirectory);

        var builder = new StringBuilder();
        builder.AppendLine("[ModSettings]");
        builder.AppendLine($"bGlobalEnableMod={FormatBool(GetBool(instance.Settings, "mods.enabled", true))}");
        if (!string.IsNullOrWhiteSpace(activeMods))
        {
            builder.AppendLine($"ActiveMods={activeMods.Trim()}");
        }

        File.WriteAllText(Path.Combine(modsDirectory, "PalModSettings.ini"), builder.ToString());
    }

    private static string? ResolveExecutablePath(string installPath)
    {
        var primary = Path.Combine(installPath, PrimaryExecutable);
        if (File.Exists(primary))
        {
            return primary;
        }

        var legacy = Path.Combine(installPath, NormalizePath(LegacyExecutable));
        return File.Exists(legacy) ? legacy : null;
    }

    private static string? ResolveImportInstallPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return null;
        }

        var candidates = new[]
        {
            path,
            Path.Combine(path, "serverfiles")
        };

        return candidates.FirstOrDefault(candidate =>
            File.Exists(Path.Combine(candidate, PrimaryExecutable)) ||
            File.Exists(Path.Combine(candidate, NormalizePath(LegacyExecutable))));
    }

    private static string SanitizeAdditionalArguments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var managedPrefixes = new[]
        {
            "-port",
            "-publiclobby",
            "-publicip",
            "-publicport",
            "-useperfthreads",
            "-NoAsyncLoadingThread",
            "-UseMultithreadForDS",
            "-NumberOfWorkerThreadsServer",
            "-NoMods",
            "-workshopdir"
        };

        var remaining = SplitCommandLine(value)
            .Where(token => !managedPrefixes.Any(prefix =>
                token.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith(prefix + "=", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return string.Join(' ', remaining);
    }

    private static IReadOnlyList<string> SplitCommandLine(string value)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                AddToken(tokens, current);
                continue;
            }

            current.Append(c);
        }

        AddToken(tokens, current);
        return tokens;
    }

    private static void AddToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }

    private static void CopyString(
        OptionSettings options,
        Dictionary<string, object?> settings,
        string optionKey,
        string settingKey)
    {
        if (options.TryGet(optionKey, out var value))
        {
            settings[settingKey] = Unquote(value);
        }
    }

    private static void CopyBool(
        OptionSettings options,
        Dictionary<string, object?> settings,
        string optionKey,
        string settingKey)
    {
        if (options.TryGet(optionKey, out var value) && bool.TryParse(Unquote(value), out var parsed))
        {
            settings[settingKey] = parsed;
        }
    }

    private static void CopyNumber(
        OptionSettings options,
        Dictionary<string, object?> settings,
        string optionKey,
        string settingKey)
    {
        if (!options.TryGet(optionKey, out var value))
        {
            return;
        }

        value = Unquote(value);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            settings[settingKey] = integer;
            return;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            settings[settingKey] = number;
        }
    }

    private static bool GetBool(IReadOnlyDictionary<string, object?> settings, string key, bool fallback)
    {
        if (!settings.TryGetValue(key, out var value) || value == null)
        {
            return fallback;
        }

        if (value is bool boolean)
        {
            return boolean;
        }

        return bool.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }

    private static int GetInt(IReadOnlyDictionary<string, object?> settings, string key, int fallback)
    {
        if (!settings.TryGetValue(key, out var value) || value == null)
        {
            return fallback;
        }

        if (value is int integer)
        {
            return integer;
        }

        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static double GetDouble(IReadOnlyDictionary<string, object?> settings, string key, double fallback)
    {
        if (!settings.TryGetValue(key, out var value) || value == null)
        {
            return fallback;
        }

        if (value is double number)
        {
            return number;
        }

        return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string QuoteArgument(string value)
    {
        return value.Any(char.IsWhiteSpace)
            ? "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : value;
    }

    private static string QuoteIniString(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal)
            : value;
    }

    private static string FormatBool(bool value)
    {
        return value ? "True" : "False";
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    private sealed class OptionSettings
    {
        private const string Header = "[/Script/Pal.PalGameWorldSettings]";
        private readonly List<KeyValuePair<string, string>> _values;

        private OptionSettings(IEnumerable<KeyValuePair<string, string>> values)
        {
            _values = values.ToList();
        }

        public static OptionSettings CreateDefault()
        {
            return new OptionSettings(
            [
                new("Difficulty", "None"),
                new("DayTimeSpeedRate", "1.000000"),
                new("NightTimeSpeedRate", "1.000000"),
                new("ExpRate", "1.000000"),
                new("PalCaptureRate", "1.000000"),
                new("PalSpawnNumRate", "1.000000"),
                new("CollectionDropRate", "1.000000"),
                new("EnemyDropItemRate", "1.000000"),
                new("DeathPenalty", "All"),
                new("bEnablePlayerToPlayerDamage", "False"),
                new("bEnableFriendlyFire", "False"),
                new("bEnableDefenseOtherGuildPlayer", "False"),
                new("bIsPvP", "False"),
                new("PalEggDefaultHatchingTime", "72.000000"),
                new("WorkSpeedRate", "1.000000"),
                new("AutoSaveSpan", "30.000000"),
                new("ServerPlayerMaxNum", "32"),
                new("ServerName", QuoteIniString("Default Palworld Server")),
                new("ServerDescription", QuoteIniString(string.Empty)),
                new("AdminPassword", QuoteIniString(string.Empty)),
                new("ServerPassword", QuoteIniString(string.Empty)),
                new("PublicPort", "8211"),
                new("PublicIP", QuoteIniString(string.Empty)),
                new("RCONEnabled", "False"),
                new("RCONPort", "25575"),
                new("RESTAPIEnabled", "False"),
                new("RESTAPIPort", "8212"),
                new("bAllowClientMod", "False"),
                new("LogFormatType", QuoteIniString("text"))
            ]);
        }

        public static OptionSettings Parse(string text)
        {
            var start = text.IndexOf("OptionSettings=(", StringComparison.Ordinal);
            if (start < 0)
            {
                return CreateDefault();
            }

            start += "OptionSettings=(".Length;
            var end = FindOptionSettingsEnd(text, start);
            if (end <= start)
            {
                return CreateDefault();
            }

            var body = text[start..end];
            var values = new List<KeyValuePair<string, string>>();
            foreach (var item in SplitTopLevel(body))
            {
                var separator = item.IndexOf('=', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    continue;
                }

                var key = item[..separator].Trim();
                var value = item[(separator + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    values.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            return values.Count == 0 ? CreateDefault() : new OptionSettings(values);
        }

        public bool TryGet(string key, out string value)
        {
            var match = _values.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            value = match.Value;
            return match.Key != null;
        }

        public void SetString(string key, string value)
        {
            SetRaw(key, QuoteIniString(value));
        }

        public void SetRaw(string key, string value)
        {
            Set(key, value);
        }

        public void SetBool(string key, bool value)
        {
            Set(key, FormatBool(value));
        }

        public void SetNumber(string key, double value)
        {
            Set(key, value.ToString("0.######", CultureInfo.InvariantCulture));
        }

        public void Remove(string key)
        {
            _values.RemoveAll(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public string ToFileText()
        {
            return Header + Environment.NewLine +
                   "OptionSettings=(" + string.Join(",", _values.Select(item => item.Key + "=" + item.Value)) + ")" +
                   Environment.NewLine;
        }

        private void Set(string key, string value)
        {
            for (var i = 0; i < _values.Count; i++)
            {
                if (string.Equals(_values[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _values[i] = new KeyValuePair<string, string>(_values[i].Key, value);
                    return;
                }
            }

            _values.Add(new KeyValuePair<string, string>(key, value));
        }

        private static int FindOptionSettingsEnd(string text, int start)
        {
            var depth = 0;
            var inQuotes = false;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (inQuotes)
                {
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    if (depth == 0)
                    {
                        return i;
                    }

                    depth--;
                }
            }

            return -1;
        }

        private static IReadOnlyList<string> SplitTopLevel(string value)
        {
            var items = new List<string>();
            var current = new StringBuilder();
            var depth = 0;
            var inQuotes = false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '"' && (i == 0 || value[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                    continue;
                }

                if (!inQuotes)
                {
                    if (c is '(' or '[' or '{')
                    {
                        depth++;
                    }
                    else if (c is ')' or ']' or '}')
                    {
                        depth = Math.Max(0, depth - 1);
                    }
                    else if (c == ',' && depth == 0)
                    {
                        items.Add(current.ToString().Trim());
                        current.Clear();
                        continue;
                    }
                }

                current.Append(c);
            }

            if (current.Length > 0)
            {
                items.Add(current.ToString().Trim());
            }

            return items;
        }
    }
}
