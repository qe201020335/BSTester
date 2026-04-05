using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using FileSystemWatcher = System.IO.FileSystemWatcher;

const string STEAM_DIR = "/home/sky/.steam/steam";
const string STEAM_COMPAT_DIR = "/home/sky/.local/share/BSManager/SharedContent/compatdata";
const string BS_DIR = "/home/sky/.local/share/BSManager/BSInstances/1.42.2 Debug";
const string BS_EXE = "Beat Saber.exe";
const string BS_PATH = $"{BS_DIR}/{BS_EXE}";
const string PROTON_PATH = "/home/sky/.steam/debian-installation/steamapps/common/Proton 9.0 (Beta)/proton";

LinuxLauncher.EnableSubreaper();

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


var logsDir = Path.Combine(BS_DIR, "Logs");

using var gameProcess = LinuxLauncher.StartGame(PROTON_PATH, BS_PATH, 
    ["--debug", "--trace", "--no-yeet", "fpfc", "--verbose"], envVars);
gameProcess.EnableRaisingEvents = true;

var gameExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
gameProcess.Exited += (_, _) => gameExited.TrySetResult(true);
var trampolineTimeout = Task.Delay(TimeSpan.FromSeconds(60));

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
        {
            Console.WriteLine($"Log file recreated: {e.Name}");
            fileRecreated.TrySetResult(e.Name);
        }
    };

    watcher.Created += onCreated;

    // Wait for either recreation, game exit, or timeout.
    var timeout = Task.Delay(TimeSpan.FromSeconds(10));
    var ready = await Task.WhenAny(fileRecreated.Task, gameExited.Task, timeout);

    watcher.Created -= onCreated;

    var gamePid = LinuxLauncher.FindGamePidByCmdline(BS_EXE);
    if (gamePid == null)
    {
        Console.Error.WriteLine("Failed to find game process");
        gameProcess.Kill(true);
        return;
    }

    Console.WriteLine($"game pid = {gamePid}");

    if (ready == timeout)
    {
        Console.WriteLine("Log file creation timeout");
        LinuxLauncher.KillPid(gamePid.Value);
        return;
    } 

    if (ready == gameExited.Task)
    {
        Console.WriteLine("Game exited before log file was created");
        return;
    }

    Console.WriteLine("Reading log file to detect trampoline error");
    var logFileName = await fileRecreated.Task;
    if (string.IsNullOrWhiteSpace(logFileName))
    {
        Console.WriteLine("Log file name is null or whitespace");
        LinuxLauncher.KillPid(gamePid.Value);
        return;
    }
    var logPath = Path.Combine(logsDir, logFileName);
    
    var tailStartInfo = new ProcessStartInfo("tail")
    {
        ArgumentList = { "-F", logPath },
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    using var tailProcess = Process.Start(tailStartInfo);

    tailProcess.EnableRaisingEvents = true;
    var tailExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    var trampolineRe = new Regex(@"DynamicMethodDefinition\.Trampoline<.+\(.*\)>\(.*\)$", RegexOptions.Compiled);
    
    DataReceivedEventHandler onOutput = (_, e) =>
    {
        if (e.Data != null)
        {
            // Console.WriteLine(e.Data);
            if (trampolineRe.Match(e.Data).Success)
            {
                Console.WriteLine("TRAMPOLINE ERROR DETECTED, EXITING");
                LinuxLauncher.KillPid(gamePid.Value);
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

    var finished = await Task.WhenAny(gameExited.Task, tailExited.Task, trampolineTimeout);

    if (finished == trampolineTimeout)
    {
        // game running without trampoline error
        Console.WriteLine("Game running without trampoline error, exiting");
        LinuxLauncher.KillPid(gamePid.Value);
    }
    else if (finished == gameExited.Task && !tailProcess.HasExited)
    {
        try { tailProcess.Kill(); } catch { }
        await tailExited.Task;
    }

    tailProcess.OutputDataReceived -= onOutput;
    tailProcess.ErrorDataReceived -= onError;
    tailProcess.Exited -= onTailExited;
});

await Task.WhenAll(gameExited.Task, monitorTask);
