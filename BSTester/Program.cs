using System;
using System.Collections.Generic;
using System.Diagnostics;

const string STEAM_DIR = "/home/sky/.steam/steam";
const string STEAM_COMPAT_DIR = "/home/sky/.local/share/BSManager/SharedContent/compatdata";
const string BS_DIR = "/home/sky/.local/share/BSManager/BSInstances/1.42.2 Debug";
const string BS_PATH = $"{BS_DIR}/Beat Saber.exe";
const string PROTON_PATH = "/home/sky/.steam/debian-installation/steamapps/common/Proton 9.0 (Beta)/proton";

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
    // { "CHROME_DESKTOP", "bs-manager.desktop" },
    // { "FC_FONTATIONS", "1" },
    // { "GDK_BACKEND", "wayland" },
    // { "MONOMOD_DMD_DEBUG", "1" },
    // { "MONOMOD_LogToFile", "./monomod.log" },
    // { "OXR_PARALLEL_VIEWS", "1" },
    // { "SBX_CHROME_API_RQ", "1" },
    // { "SHLVL", "0" },
    { "STEAM_COMPAT_APP_ID", "620980" },
    { "STEAM_COMPAT_CLIENT_INSTALL_PATH", STEAM_DIR },
    { "STEAM_COMPAT_DATA_PATH", STEAM_COMPAT_DIR },
    { "STEAM_COMPAT_INSTALL_PATH", BS_DIR },
    { "SteamAppId", "620980" },
    { "SteamEnv", "1" },
    { "SteamGameId", "620980" },
    { "SteamOverlayGameId", "620980" },
    { "WINEDLLOVERRIDES", "winhttp=n,b" },
    // { "_", PROTON_PATH },
};

foreach (var kvp in envVars)
{
    processInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
}

using var process = Process.Start(processInfo);
process?.WaitForExit();
