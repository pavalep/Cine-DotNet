using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Cine.Packager;

/// <summary>
/// Creates an MSIX package from a published framework-dependent .NET app output.
/// Wraps MakeAppx.exe and SignTool.exe from the Windows SDK.
/// 
/// Usage: dotnet run -- --source publish/win-x64/ --output dist/ --version 1.2.0 --arch x64 [--cert cert.pfx]
/// </summary>
internal class Program
{
    private static int Main(string[] args)
    {
        var parsed = ParseArgs(args);
        if (parsed == null) return 1;

        var (sourceDir, outputDir, version, arch, certPath, certPass) = parsed.Value;

        Console.WriteLine($"Cine.Packager v{version}");
        Console.WriteLine($"  Source : {sourceDir}");
        Console.WriteLine($"  Output : {outputDir}");
        Console.WriteLine($"  Arch   : {arch}");
        Console.WriteLine();

        // 1. Validate
        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"Error: Source directory not found: {sourceDir}");
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        // 2. Find MakeAppx.exe
        var makeAppx = FindWindowsSdkTool("MakeAppx.exe");
        if (makeAppx == null)
        {
            Console.Error.WriteLine("Error: MakeAppx.exe not found. Install Windows SDK (10.0.20348+).");
            return 1;
        }
        Console.WriteLine($"  MakeAppx: {makeAppx}");

        // 3. Create mapping file
        var mappingFile = Path.Combine(outputDir, "mapping.txt");
        CreateMappingFile(sourceDir, mappingFile);
        Console.WriteLine($"  Mapping : {mappingFile}");

        // 4. Create MSIX with MakeAppx
        var manifestPath = FindPackageManifest();
        if (manifestPath == null)
        {
            Console.Error.WriteLine("Error: Package.appxmanifest not found. Run from repo root.");
            return 1;
        }

        var msixFile = Path.Combine(outputDir, $"Cine_{version}_{arch}.msix");
        Console.WriteLine($"  Packaging: {msixFile}");

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = makeAppx,
            Arguments = $"pack /m \"{manifestPath}\" /f \"{mappingFile}\" /p \"{msixFile}\" /o",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;

        process.WaitForExit();
        Console.WriteLine(process.StandardOutput.ReadToEnd());

        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine(process.StandardError.ReadToEnd());
            Console.Error.WriteLine($"MakeAppx failed with exit code {process.ExitCode}");
            return process.ExitCode;
        }

        Console.WriteLine($"  MSIX created: {msixFile} ({new FileInfo(msixFile).Length / 1024 / 1024} MB)");

        // 5. Sign if certificate provided
        if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
        {
            var signtool = FindWindowsSdkTool("signtool.exe");
            if (signtool != null)
            {
                Console.WriteLine($"  Signing: {signtool}");

                var signArgs = $"sign /fd SHA256 /f \"{certPath}\" /tr http://timestamp.digicert.com /td SHA256 \"{msixFile}\"";
                if (!string.IsNullOrEmpty(certPass))
                    signArgs = signArgs.Replace("/f", $"/p \"{certPass}\" /f");

                var sign = Process.Start(new ProcessStartInfo
                {
                    FileName = signtool,
                    Arguments = signArgs,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                })!;

                sign.WaitForExit();
                if (sign.ExitCode == 0)
                    Console.WriteLine("  Signed successfully.");
                else
                    Console.Error.WriteLine($"  SignTool exit code: {sign.ExitCode}");
            }
        }

        Console.WriteLine("\nDone.");
        return 0;
    }

    private static readonly HashSet<string> ExcludeFromMSIX = new(StringComparer.OrdinalIgnoreCase)
    {
        // These are downloaded on-demand by RuntimeDownloader on first launch.
        // Excluding them reduces MSIX from ~150 MB to ~15 MB.
        "libmpv-2.dll",
        "libEGL.dll",
        "libGLESv2.dll",
        "av_libglesv2.dll",

        // PDBs should never be in a release package
        ".pdb",  // filtered by extension below
    };

    private static void CreateMappingFile(string sourceDir, string outputPath)
    {
        var lines = new List<string> { "[Files]" };
        var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.TopDirectoryOnly);

        int excluded = 0;
        foreach (var file in files.OrderBy(f => f))
        {
            var name = Path.GetFileName(file);

            // Skip PDBs (debug symbols)
            if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                excluded++;
                Console.WriteLine($"  Excluded (PDB): {name}");
                continue;
            }

            // Skip on-demand runtime DLLs
            if (ExcludeFromMSIX.Contains(name))
            {
                excluded++;
                Console.WriteLine($"  Excluded (download on demand): {name}");
                continue;
            }

            var escapedSource = $"\"{file.Replace('\\', '/')}\"";
            lines.Add($"{escapedSource} \"{name}\"");
        }

        if (excluded > 0)
            Console.WriteLine($"  {excluded} files excluded (downloaded on first launch)");

        File.WriteAllLines(outputPath, lines);
    }

    private static string? FindPackageManifest()
    {
        var dirs = new[] { ".", "..", "../..", "../../.." };
        foreach (var dir in dirs)
        {
            var candidate = Path.GetFullPath(Path.Combine(dir, "src", "App", "Package.appxmanifest"));
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindWindowsSdkTool(string toolName)
    {
        var kitsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits", "10", "bin");

        if (!Directory.Exists(kitsRoot)) return null;

        var versions = Directory.GetDirectories(kitsRoot)
            .Select(d => Path.GetFileName(d))
            .Where(n => n!.StartsWith("10."))
            .OrderByDescending(n => n)
            .ToList();

        foreach (var ver in versions)
        {
            var tool = Path.Combine(kitsRoot, ver, "x64", toolName);
            if (File.Exists(tool)) return tool;
        }

        return null;
    }

    private static (string source, string output, string version, string arch, string? cert, string? certPass)? ParseArgs(string[] args)
    {
        string? source = null, output = null, version = null, arch = "x64", cert = null, certPass = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--source" when i + 1 < args.Length: source = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
                case "--version" when i + 1 < args.Length: version = args[++i]; break;
                case "--arch" when i + 1 < args.Length: arch = args[++i]; break;
                case "--cert" when i + 1 < args.Length: cert = args[++i]; break;
                case "--cert-pass" when i + 1 < args.Length: certPass = args[++i]; break;
            }
        }

        if (source == null || output == null || version == null)
        {
            Console.Error.WriteLine("Usage: dotnet run -- --source <dir> --output <dir> --version <ver> [--arch x64] [--cert cert.pfx]");
            return null;
        }

        return (source, output, version, arch, cert, certPass);
    }
}
