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
        Console.WriteLine("git reset");
        RunProcess("git", ["reset", "--hard"], MONOMOD_SRC);

        Console.WriteLine($"Checkout {commit}");
        RunProcess("git", ["checkout", commit], MONOMOD_SRC);
        RunProcess("git", ["submodule", "update"], MONOMOD_SRC);

        Console.WriteLine("Apply ConditionalWeakTable patch");
        RunProcess("git", ["apply", "ConditionalWeakTable2.patch"], MONOMOD_SRC);

        Console.WriteLine("rm artifacts");
        var artifactsDir = Path.Combine(MONOMOD_SRC, "artifacts");
        if (Directory.Exists(artifactsDir))
        {
            Directory.Delete(artifactsDir, recursive: true);
        }
        // RunProcess("dotnet", ["clean", "./src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj"], MONOMOD_SRC);
        Console.WriteLine("dotnet build");
        RunProcess("/home/sky/dotnet-legacy/dotnet", ["build", "--property:WarningLevel=0", "./src/MonoMod.RuntimeDetour.New/MonoMod.RuntimeDetour.New.csproj", "-c", "Release", "-f", "net452"], MONOMOD_SRC);
        
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
        for (var i = 0; i < 25; i++)
        {
            Console.WriteLine($"Run {i + 1}/25");
            if (Run())
            {
                Console.WriteLine("Trampoline error detected!");
                return true;
            }
        }

        Console.WriteLine("Trampoline error not detected after 25 runs.");
        return false;
    }
    
    // doesn't compile
    private static readonly HashSet<string> BrokenCommits =
        ["f23591bb96b44e5bbd91af97e5925d99e6266a36", "7a9b5dc39a09feed4da0750bce8a6ebbc21a8cec", "c50fabe291b78608dd1e5bdba935cf99cae45040", "44f69293e3cc99cc39aa28e3615840dd7ba5aa0f", "e1f6879b9ab8558658b9369621bce7d2903d94a4"];
    
    public static void Main(string[] args)
    {
        const string starting = "v22.03.23.04";  // last know good
        const string ending = "337bf786c"; // first 25 prerelease

        // const string progress = "fadcd980a69b7aa6066810ae67c2e3b4d2732405"; // hard broke, no mod works, needs ConditionalWeakTable patch
        // const string progress = "8f66cfdfe73bfcc07414d777fe779d3dd9df34d3";  // need ConditionalWeakTable2 patch
        const string progress = "8003c89964b3fde56fdfe1facf10f04b89922d42";  // MonoMod.RuntimeDetour.New
        // const string progress = "";
        
        //git rev-list --reverse --first-parent 8fea484..337bf786c

        var psi = new ProcessStartInfo("git")
        {
            Arguments = $"rev-list --reverse --first-parent {starting}..{ending}",
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
        
        var commits = output.Split(new []{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse().ToList();
        var total = commits.Count;
        Console.WriteLine($"Total commits to test: {total}");

        int take;
        if (progress != "" && (take = commits.IndexOf(progress)) != -1)
        {
            Console.WriteLine($"Resuming from progress commit: {progress}");
        }

        const int rate = 10;
        Console.WriteLine($"Testing every {rate} commits after skipping {take} commits");
        var sample = 0;
        foreach (var commit in commits.Skip(take).Where(_ => sample++ % rate == 0))
        {
            var i = take + sample;
            if (BrokenCommits.Contains(commit))
            {
                Console.WriteLine($"\n\n----- ({i}/{total}) Broken commit {commit}, skipping  -----\n\n");
                continue;
            }
            Console.WriteLine($"\n\n----- ({i}/{total}) Begin Testing {commit} -----\n\n");
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
            Console.WriteLine($"\n\n----- ({i}/{total}) Finish Testing {commit} -----\n\n");
        }

    }
}