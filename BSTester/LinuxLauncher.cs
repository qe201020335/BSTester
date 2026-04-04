using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class LinuxLauncher
{
    private const int PR_SET_CHILD_SUBREAPER = 36;

    [DllImport("libc", SetLastError = true)]
    private static extern int prctl(int option, ulong arg2, ulong arg3, ulong arg4, ulong arg5);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);

    public static void EnableSubreaper()
    {
        if (prctl(PR_SET_CHILD_SUBREAPER, 1, 0, 0, 0) != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "prctl(PR_SET_CHILD_SUBREAPER) failed");
    }

    public static Process StartGame(string protonScript, string gameExe, string[] args, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo("python3", [protonScript, "run", gameExe, ..args])
        {
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = Path.GetDirectoryName(gameExe) ?? throw new Exception("Failed to get game directory"),
        };
        
        foreach (var kvp in env)
        {
            psi.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        var p = Process.Start(psi) ?? throw new Exception("Failed to start Proton");

        return p;
    }

    public static int? FindGamePidByCmdline(string exeName)
    {
        string target = exeName.ToLowerInvariant();

        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            var name = Path.GetFileName(dir);
            if (!int.TryParse(name, out int pid))
                continue;

            try
            {
                string comm = File.ReadAllText($"/proc/{pid}/comm")
                    .Trim()
                    .ToLowerInvariant();

                if (comm.StartsWith(target) || target.StartsWith(comm))
                    return pid;
            }
            catch
            {
                // ignore dead/inaccessible processes
            }
        }

        return null;
    }

    public static void KillPid(int pid, bool force = false)
    {
        const int SIGTERM = 15;
        const int SIGKILL = 9;

        if (kill(pid, force ? SIGKILL : SIGTERM) != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"kill({pid}) failed");
    }
}