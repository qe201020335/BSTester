using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FileSystemWatcher = System.IO.FileSystemWatcher;
using BSTester;

const string STEAM_DIR = "/home/sky/.steam/steam";
const string STEAM_COMPAT_DIR = "/home/sky/.local/share/BSManager/SharedContent/compatdata";
const string BS_DIR = "/home/sky/.local/share/BSManager/BSInstances/1.42.2 Debug";
const string BS_PATH = $"{BS_DIR}/Beat Saber.exe";
const string PROTON_PATH = "/home/sky/.steam/debian-installation/steamapps/common/Proton 9.0 (Beta)/proton";

var cts = new CancellationTokenSource();

AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

var processInfo = new ProcessStartInfo("python3")
{
    ArgumentList = { PROTON_PATH, "run", BS_PATH, "--debug", "--trace", "--no-yeet", "fpfc", "--verbose" },
    UseShellExecute = false,
    RedirectStandardOutput = false,
    RedirectStandardError = false,
    WorkingDirectory = BS_DIR,
};

// Add all the environment variables
var envVars = new Dictionary<string, string>
{
    { "STEAM_COMPAT_APP_ID", "620980" },
    { "STEAM_COMPAT_CLIENT_INSTALL_PATH", STEAM_DIR },
    { "STEAM_COMPAT_DATA_PATH", STEAM_COMPAT_DIR },
    { "STEAM_COMPAT_INSTALL_PATH", BS_DIR },
    { "SteamAppId", "620980" },
    { "SteamEnv", "1" },
    { "SteamGameId", "620980" },
    { "SteamOverlayGameId", "620980" },
    { "WINEDLLOVERRIDES", "winhttp=n,b" },
};

foreach (var kvp in envVars)
{
    processInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
}

var logsDir = Path.Combine(BS_DIR, "Logs");

var gameProcess = ProcessHelper.StartProcess(processInfo, cts.Token);
gameProcess.EnableRaisingEvents = true;

var gameExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
gameProcess.Exited += (_, _) => gameExited.TrySetResult(true);

// Monitor for log file recreation and stream it to stdout.
var monitorTask = Task.Run(async () =>
{
    using var watcher = new FileSystemWatcher(logsDir, "*.log")
    {
        NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
        EnableRaisingEvents = true,
    };

    var fileRecreated = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

    FileSystemEventHandler onCreated = (_, e) =>
    {
        if (e.Name != "_latest.log")
            fileRecreated.TrySetResult(e.Name);
    };

    watcher.Created += onCreated;

    // Wait for either recreation, game exit, or timeout.
    var timeout = Task.Delay(TimeSpan.FromSeconds(30));
    var ready = await Task.WhenAny(fileRecreated.Task, gameExited.Task, timeout);

    watcher.Created -= onCreated;

    if (ready != fileRecreated.Task) return;

    var logFileName = await fileRecreated.Task;
    if (string.IsNullOrWhiteSpace(logFileName)) return;
    var logPath = Path.Combine(logsDir, logFileName);
    
    var tailStartInfo = new ProcessStartInfo("tail")
    {
        ArgumentList = { "-F", logPath },
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    using var tailProcess = ProcessHelper.StartProcess(tailStartInfo, cts.Token);

    tailProcess.EnableRaisingEvents = true;
    var tailExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    DataReceivedEventHandler onOutput = (_, e) =>
    {
        if (e.Data != null)
        {
            Console.WriteLine(e.Data);
            if (e.Data.Contains("(wrapper dynamic-method) MonoMod.Utils.DynamicMethodDefinition.Trampoline<"))
            {
                Console.WriteLine("TRAMPOLINE ERROR DETECTED, EXITING");
                gameProcess.Kill(entireProcessTree: true);
            }
        }
    };

    DataReceivedEventHandler onError = (_, e) =>
    {
        if (e.Data != null)
            Console.Error.WriteLine(e.Data);
    };

    EventHandler onTailExited = (_, _) => tailExited.TrySetResult(true);

    tailProcess.OutputDataReceived += onOutput;
    tailProcess.ErrorDataReceived += onError;
    tailProcess.Exited += onTailExited;

    tailProcess.BeginOutputReadLine();
    tailProcess.BeginErrorReadLine();

    var finished = await Task.WhenAny(gameExited.Task, tailExited.Task);

    if (finished == gameExited.Task && !tailProcess.HasExited)
    {
        try { tailProcess.Kill(); } catch { }
        await tailExited.Task;
    }

    tailProcess.OutputDataReceived -= onOutput;
    tailProcess.ErrorDataReceived -= onError;
    tailProcess.Exited -= onTailExited;
});

await Task.WhenAll(gameExited.Task, monitorTask);
gameProcess.Dispose();