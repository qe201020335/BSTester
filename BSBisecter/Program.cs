// See https://aka.ms/new-console-template for more information

using System.Diagnostics;

public static class Program
{
    private const string MONOMOD_SRC = "/home/sky/source/MonoMod/";
    private const string LIBS_DIR = "/home/sky/.local/share/BSManager/BSInstances/1.42.2 Debug/Libs/";
    private const string DOTNET_ROOT = "/home/sky/dotnet-legacy";


    static void RunProcess(string command, string[] args, string workingDirectory)
    {
        var psi = new ProcessStartInfo(command, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        psi.EnvironmentVariables["DOTNET_ROOT"] = DOTNET_ROOT;
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

    static void Dotnet(string[] args, string cwd)
    {
        var dotnet = Path.Combine(DOTNET_ROOT, "dotnet");
        RunProcess(dotnet, args, cwd);
    }
    
    static void Git(string[] args, string cwd)
    {
        RunProcess("git", args, cwd);
    }

    static void CheckoutAndCompileAndReplaceMonoMod(string commit, string? patch, bool isNew)
    {
        Console.WriteLine("git reset");
        Git(["reset", "--hard"], MONOMOD_SRC);

        Console.WriteLine("git clean");
        Git(["clean", "-df"], MONOMOD_SRC);

        Console.WriteLine($"Checkout {commit}");
        Git(["checkout", commit], MONOMOD_SRC);
        Git(["submodule", "update"], MONOMOD_SRC);

        if (!string.IsNullOrWhiteSpace(patch))
        {
            var absPath = Path.GetFullPath(patch);
            if (!File.Exists(absPath)) throw new FileNotFoundException($"Patch file not found: {absPath}");
            Console.WriteLine($"Applying patch: {Path.GetFileName(absPath)}");
            Git(["apply", absPath], MONOMOD_SRC);
            Git(["add", "*.cs"], MONOMOD_SRC);  // stage the changes so reset can remove created files
        }

        var projectName = isNew ? "MonoMod.RuntimeDetour.New" : "MonoMod.RuntimeDetour";
        
        // Dotnet(["clean", "./src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj"], MONOMOD_SRC);
        Console.WriteLine("dotnet build");
        Dotnet(["build", "--property:WarningLevel=0", $"./src/{projectName}/{projectName}.csproj", "-c", "Release", "-f", "net452"], MONOMOD_SRC);
        
        var artifactsDir = Path.Combine(MONOMOD_SRC, "artifacts");
        var buildOutputDir = Path.Combine(artifactsDir, $"bin/{projectName}/Release/net452");
        if (!Directory.Exists(buildOutputDir))
        {
            buildOutputDir =  Path.Combine(artifactsDir, $"bin/{projectName}/release_net452");
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

    private static readonly Dictionary<string, string> PatchIndex = new Dictionary<string, string>
    {
        ["fadcd980a69b7aa6066810ae67c2e3b4d2732405"] = "ConditionalWeakTable.patch",
        ["8f66cfdfe73bfcc07414d777fe779d3dd9df34d3"] = "ConditionalWeakTable2.patch",
        ["d6d33aa3647e8b1d5e7a9ba383c8fcfb405d0f88"] = "ConditionalWeakTable3.patch",
        ["fc403799c306945f37a502c3973ea96a0bffb32f"] = "ConditionalWeakTable4.patch",
    };

    private const string COMMIT_NEW = "8003c89964b3fde56fdfe1facf10f04b89922d42"; // MonoMod.RuntimeDetour.New

    public static void Main(string[] args)
    {
        const string starting = "v22.03.23.04";  // last know good
        const string ending = "337bf786c"; // first 25 prerelease

        // must be full commit hash
        const string progress = "fc403799c306945f37a502c3973ea96a0bffb32f";  
        // const string progress = "";
        
        var patchDir = Path.Combine(Directory.GetCurrentDirectory(), "patches");
        if (!Directory.Exists(patchDir)) throw new DirectoryNotFoundException($"Patches directory not found: {patchDir}");

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

        var skip = 0;
        if (progress != "")
        {
            skip = commits.IndexOf(progress);
            if (skip != -1)
            {
                Console.WriteLine($"Resuming from progress commit: {progress}");
            }
            else
            {
                skip = 0;
                Console.WriteLine($"Resuming from progress commit: {progress}");
            }
        }

        var patch = "";
        var isNew = false;

        const int rate = 4;
        Console.WriteLine($"Testing every {rate} commits after skipping {skip} commits");
        var i = 0;
        var sample = 0;
        foreach (var commit in commits)
        {
            i++;
            if (PatchIndex.TryGetValue(commit, out var p))
            {
                Console.WriteLine($"Patch found for commit {commit}: {p}");
                patch = Path.Combine(patchDir, p);
            }
            
            if (!isNew && commit == COMMIT_NEW)
            {
                isNew = true;
                Console.WriteLine($"Switching to MonoMod.RuntimeDetour.New at commit {commit}");
            }

            if (skip > 0)
            {
                skip--;
                continue;
            }
            
            if (sample++ % rate != 0)
            {
                // Console.WriteLine($"\n\n----- ({i}/{total}) Skipping {commit} -----\n\n");
                continue;
            }
            
            if (BrokenCommits.Contains(commit))
            {
                Console.WriteLine($"\n\n----- ({i}/{total}) Broken commit {commit}, skipping  -----\n\n");
                continue;
            }
            Console.WriteLine($"\n\n----- ({i}/{total}) Begin Testing {commit} -----\n\n");
            try
            {
                CheckoutAndCompileAndReplaceMonoMod(commit, patch, isNew);
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