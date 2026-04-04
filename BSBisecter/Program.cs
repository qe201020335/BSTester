// See https://aka.ms/new-console-template for more information

using System.Diagnostics;


Console.WriteLine(Run());


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