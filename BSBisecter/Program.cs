// See https://aka.ms/new-console-template for more information

using System.Diagnostics;

public static class Program
{
    private const string MONOMOD_SRC = "/home/sky/source/MonoMod/";
    private const string LIBS_DIR = "/home/sky/.local/share/BSManager/BSInstances/1.42.2 Debug/Libs/";


    static void RunProcess(string command, string[] args, string workingDirectory)
    {
        var psi = new ProcessStartInfo(command, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        psi.EnvironmentVariables["DOTNET_ROOT"] = "/home/sky/dotnet-legacy";
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
        process.OutputDataReceived += (_, args) =>
        {
            Console.WriteLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            Console.Error.WriteLine(args.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new Exception($"{command} exited with exit code: " + process.ExitCode);
        }
    }

    static void CheckoutAndCompileAndReplaceMonoMod(string commit)
    {
        Console.WriteLine($"Checkout {commit}");
        RunProcess("git", ["checkout", commit], MONOMOD_SRC);
        RunProcess("git", ["submodule", "update"], MONOMOD_SRC);
        Console.WriteLine("rm artifacts");
        var artifactsDir = Path.Combine(MONOMOD_SRC, "artifacts");
        if (Directory.Exists(artifactsDir))
        {
            Directory.Delete(artifactsDir, recursive: true);
        }
        // RunProcess("dotnet", ["clean", "./src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj"], MONOMOD_SRC);
        Console.WriteLine("dotnet build");
        RunProcess("/home/sky/dotnet-legacy/dotnet", ["build", "./src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj", "-c", "Release", "-f", "net452"], MONOMOD_SRC);
        
        var buildOutputDir = Path.Combine(artifactsDir, "bin/MonoMod.RuntimeDetour/Release/net452");
        if (!Directory.Exists(buildOutputDir))
        {
            buildOutputDir =  Path.Combine(artifactsDir, "bin/MonoMod.RuntimeDetour/release_net452");
        }

        if (!Directory.Exists(buildOutputDir))
        {
            throw new Exception("MonoMod build not found");
        }
        
        RunProcess("ls", ["-l", buildOutputDir], MONOMOD_SRC);
        Console.WriteLine("Replacing monomod");
        foreach (var file in Directory.GetFiles(buildOutputDir))
        {
            if (Path.GetFileName(file) != "System.ValueTuple.dll")
            {
                Console.WriteLine($"Copying {file}");
                File.Copy(file, Path.Combine(LIBS_DIR, Path.GetFileName(file)), overwrite: true);
            }
        }
    }
    
    static bool Run()
    {
        var psi = new ProcessStartInfo("BSTester")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
        var output = "";
        process.OutputDataReceived += (_, args) =>
        {
            Console.WriteLine(args.Data);
            output += args.Data + "\n";
        };
        process.ErrorDataReceived += (_, args) => Console.Error.WriteLine(args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return output.Contains("TRAMPOLINE ERROR DETECTED");
    }

    static void LoopRun()
    {
        for (var i = 0; i < 10; i++)
        {
            Console.WriteLine($"Run {i + 1}/10");
            if (Run())
            {
                Console.WriteLine("Trampoline error detected!");
                return;
            }
        }

        Console.WriteLine("Trampoline error not detected after 10 runs.");
    }
    
    public static void Main(string[] args)
    {
        var starting = "";
        var ending = "337bf786c"; // first 25 prerelease
        //git rev-list --reverse --first-parent ?????..337bf786c
        CheckoutAndCompileAndReplaceMonoMod("337bf786c");
        LoopRun();
    }
}