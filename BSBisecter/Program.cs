// See https://aka.ms/new-console-template for more information

using System.Diagnostics;


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


bool Run()
{
    var psi = new ProcessStartInfo("BSTester")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true
    };
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
    var output = "";
    process.OutputDataReceived += (_, args) =>
    {
        Console.WriteLine(args.Data);
        output += args.Data + "\n";
    };
    process.BeginOutputReadLine();
    process.WaitForExit();
    return output.Contains("TRAMPOLINE ERROR DETECTED");
}