using System.Diagnostics;

namespace BSTester;

public class ProcessHelper
{
    public static Process StartProcess(ProcessStartInfo startInfo, CancellationToken token)
    {
        var exit = false;
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start process.");
        process.Exited += (_, _) => exit = true;
        process.EnableRaisingEvents = true;
        
        token.Register(() => 
        {
            if (!exit)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to kill process: {ex}");
                }
            }
        });
        return process;
    }
}