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
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Console.WriteLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Console.Error.WriteLine(args.Data);
            }
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
        RunProcess("/home/sky/dotnet-legacy/dotnet", ["build", "--property:WarningLevel=0", "./src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj", "-c", "Release", "-f", "net452"], MONOMOD_SRC);
        
        var buildOutputDir = Path.Combine(artifactsDir, "bin/MonoMod.RuntimeDetour/Release/net452");
        if (!Directory.Exists(buildOutputDir))
        {
            buildOutputDir =  Path.Combine(artifactsDir, "bin/MonoMod.RuntimeDetour/release_net452");
        }

        if (!Directory.Exists(buildOutputDir))
        {
            throw new Exception("MonoMod build not found");
        }
        
        // RunProcess("ls", ["-l", buildOutputDir], MONOMOD_SRC);
        Console.WriteLine("Replacing monomod"); 
        foreach (var file in Directory.GetFiles(buildOutputDir))
        {
            if (Path.GetFileName(file) != "System.ValueTuple.dll")
            {
                // Console.WriteLine($"Copying {file}");
                File.Copy(file, Path.Combine(LIBS_DIR, Path.GetFileName(file)), overwrite: true);
            }
        }
        Console.WriteLine($"Monomod copied from {buildOutputDir} to {LIBS_DIR}");
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
        var trampoline = false;
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Console.WriteLine(args.Data);
                trampoline = trampoline || args.Data.Contains("TRAMPOLINE ERROR DETECTED");
            }
        };
        process.ErrorDataReceived += (_, args) => Console.Error.WriteLine(args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new Exception("BSTester exited with exit code: " + process.ExitCode);
        return trampoline;
    }

    static bool LoopRun()
    {
        for (var i = 0; i < 10; i++)
        {
            Console.WriteLine($"Run {i + 1}/10");
            if (Run())
            {
                Console.WriteLine("Trampoline error detected!");
                return true;
            }
        }

        Console.WriteLine("Trampoline error not detected after 10 runs.");
        return false;
    }
    
    public static void Main(string[] args)
    {
        var starting = "8fea484";  // last know good
        var ending = "337bf786c"; // first 25 prerelease

        var progress = "";
        
        //git rev-list --reverse --first-parent 8fea484..337bf786c

        var psi = new ProcessStartInfo("git")
        {
            Arguments = "rev-list --reverse --first-parent 8fea484..337bf786c",
            RedirectStandardOutput = true,
            WorkingDirectory = MONOMOD_SRC,
        };
        var proc = Process.Start(psi) ??  throw new InvalidOperationException("Failed to start process");
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            throw new Exception("git rev-list exited with code: " + proc.ExitCode);
        }
        
        var commits = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse().ToList();

        var take = 0;
        
        if (progress != "" && (take = commits.IndexOf(progress)) != -1)
        {
            Console.WriteLine($"Resuming from progress commit: {progress}");
        }

        foreach (var commit in commits.Skip(take))
        {
            Console.WriteLine($"\n\n----- Begin Testing {commit} -----\n\n");
            try
            {
                CheckoutAndCompileAndReplaceMonoMod(commit);
                if (!LoopRun())
                {
                    Console.WriteLine($"Trampoline error not detected on commit: {commit}");
                    return;
                }
                Console.WriteLine($"Trampoline error detected on commit: {commit}");
            }
            catch (Exception)
            {
                
                Console.WriteLine($"Error on commit {commit}");
                throw;
            }
            Console.WriteLine($"\n\n----- Finish Testing {commit} -----\n\n");
        }

    }
}