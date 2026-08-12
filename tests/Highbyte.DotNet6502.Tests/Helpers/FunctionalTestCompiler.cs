using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Tests.Helpers;

/// <summary>
/// Helper class to compile a 6502 functional test program.
/// 
/// It downloads the test source code and the "AS65" assembler that it's written in from
/// https://github.com/Klaus2m5/6502_65C02_functional_tests
/// 
/// 
/// An option in the method defines if 6502 decimal mode will be tested for instructions.
/// If not, the downloaded source code is automatically modified to set row
///   disable_decimal = 0
/// to 
///   disable_decimal = 1
/// 
/// After source code has been downloaded (and optionally updated for decimal mode or not), 
/// the "AS65" assembler is downloaded from the same repo (a .zip file that is extracted).
/// 
/// Then the source code is assembled with these options (the .a65 filename depends on whether decimal mode was disabled or not)
///   as65.exe -l -m -w -h0 6502_functional_test.a65
/// 
/// This will generate two files
///   6502_functional_test.bin
///   6502_functional_test.lst
/// 
/// The .bin file can be loaded into the emulator for execution.
/// It should be loaded at memory location 0x000A
/// And started at 0x0400
/// 
/// If successfull, it the program will end in a forever-loop at a certain memory location (as of 2021-02-06 it was 0x336d but can change in the future)
/// The emulator should be configured to stop processing when the Program Counter (PC) reaches this position.
/// 
/// If unsuccessfull (i.e. a 6502 instruction did not behave correctly), the program will also end with a forever-loop, at the location where the error occurred.
/// The emulator should also be configured to stop processing after X number of instructions, so it can exit when the test is unsuccessful (as of 2021-06-02 a successfull test took 26765880 instructions, so the emulator should be configure to execute AT LEAST that many instructions)
/// 
/// The .lst file is a symbol/map-file that can be reference if the code does not execute correctly to identify what the error is based on
/// where the program may enter a forever-loop.
/// </summary>
public class FunctionalTestCompiler
{
    private readonly ILogger<FunctionalTestCompiler> _logger;

    private static readonly HttpClient s_httpClient = new HttpClient();

    /// <summary>
    /// Pinned revision of the Klaus2m5/6502_65C02_functional_tests repository used for
    /// parameterized test artifacts (<see cref="GetKlausTestBuild"/>), so that assembled
    /// binaries, label addresses, and test outcomes are reproducible.
    /// </summary>
    public const string PinnedSourceRevision = "7954e2dbb49c469ea286070bf46cdd71aeb29e4b";

    /// <summary>Result of assembling a Klaus test source: the binary and its listing file.</summary>
    public sealed record KlausTestBuild(string BinaryFilePath, string ListFilePath);

    public FunctionalTestCompiler(ILogger<FunctionalTestCompiler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Downloads a test source file from the pinned revision of the Klaus test
    /// repository, applies the given assembler-setting overrides (lines of the form
    /// "name = value"), and assembles it. Windows only — the AS65 assembler is a
    /// Windows executable; callers should gate on <see cref="WindowsOnlyFactAttribute"/>.
    /// </summary>
    public KlausTestBuild GetKlausTestBuild(string sourceFileName, IReadOnlyDictionary<string, string> settingOverrides, string? downloadDir = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Assembling Klaus test sources requires Windows (AS65 assembler is Windows-only).");

        if (string.IsNullOrEmpty(downloadDir))
            downloadDir = Directory.GetCurrentDirectory();

        var sourceUrl = $"https://raw.githubusercontent.com/Klaus2m5/6502_65C02_functional_tests/{PinnedSourceRevision}/{sourceFileName}";
        var sourceFilePath = Path.Join(downloadDir, sourceFileName);
        DownloadFile(sourceUrl, sourceFilePath);

        // Apply setting overrides to a copy, so different configurations don't collide.
        var modifiedFileName = $"{Path.GetFileNameWithoutExtension(sourceFileName)}_modified{Path.GetExtension(sourceFileName)}";
        var modifiedFilePath = Path.Join(downloadDir, modifiedFileName);
        ApplyAsmSettingOverrides(sourceFilePath, modifiedFilePath, settingOverrides);

        var as65exeFilePath = GetAS65AssemblerFilePath(downloadDir);
        var binaryFilePath = Compile6502FunctionalTestBinary(as65exeFilePath, modifiedFilePath);
        var listFilePath = Path.Join(Path.GetDirectoryName(binaryFilePath), Path.GetFileNameWithoutExtension(binaryFilePath)) + ".lst";
        return new KlausTestBuild(binaryFilePath, listFilePath);
    }

    private static void ApplyAsmSettingOverrides(string originalFile, string newFile, IReadOnlyDictionary<string, string> settingOverrides)
    {
        var appliedSettings = new HashSet<string>();
        var lines = File.ReadAllLines(originalFile);
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var (name, value) in settingOverrides)
            {
                if (lines[i].StartsWith(name, StringComparison.Ordinal) && lines[i].Contains('='))
                {
                    lines[i] = $"{name} = {value}";
                    appliedSettings.Add(name);
                }
            }
        }
        var missingSettings = settingOverrides.Keys.Where(k => !appliedSettings.Contains(k)).ToList();
        if (missingSettings.Count != 0)
            throw new DotNet6502Exception($"Assembler setting(s) not found in {originalFile}: {string.Join(", ", missingSettings)}. The pinned source revision may not match the expected settings.");
        File.WriteAllLines(newFile, lines);
    }

    /// <summary>
    /// Finds the address of a label DEFINITION in an AS65 listing file. Handles both
    /// code lines ("&lt;line#&gt; &lt;addr&gt; : &lt;bytes...&gt; label mnemonic ...", where the label is
    /// the first token after the machine-code bytes — this avoids matching reference
    /// sites like "beq success") and symbol-table style lines ("label &lt;addr&gt;" /
    /// "&lt;addr&gt; label"). On failure the exception carries every line mentioning the
    /// label plus a sample of the file, so a mismatched format is diagnosable from a
    /// CI log alone.
    /// </summary>
    public static ushort FindLabelAddressInListFile(string listFilePath, string label)
    {
        var allLines = File.ReadAllLines(listFilePath);
        foreach (var line in allLines)
        {
            var tokens = line.Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
            var colonIndex = Array.IndexOf(tokens, ":");
            if (colonIndex >= 1)
            {
                // Code line: the token just before ':' is the address.
                var addressToken = tokens[colonIndex - 1];
                if (!IsHexAddress(addressToken))
                    continue;

                // Skip the machine-code byte tokens (1-2 hex chars) after ':'; the next
                // token is the label column (or the mnemonic when the line has no label).
                var i = colonIndex + 1;
                while (i < tokens.Length && tokens[i].Length <= 2 && tokens[i].All(Uri.IsHexDigit))
                    i++;
                if (i < tokens.Length && TokenIsLabel(tokens[i], label))
                    return Convert.ToUInt16(addressToken, 16);
            }
            else if (tokens.Length >= 2)
            {
                // Symbol-table style: "label addr" or "addr label".
                if (TokenIsLabel(tokens[0], label) && IsHexAddress(tokens[1]))
                    return Convert.ToUInt16(tokens[1], 16);
                if (IsHexAddress(tokens[0]) && TokenIsLabel(tokens[1], label))
                    return Convert.ToUInt16(tokens[0], 16);
            }
        }

        var linesMentioningLabel = allLines
            .Where(l => l.Contains(label, StringComparison.OrdinalIgnoreCase))
            .Take(10);
        var sampleLines = allLines.Take(5).Concat(allLines.TakeLast(15));
        throw new DotNet6502Exception(
            $"Label definition '{label}' not found in listing file {listFilePath}.\n" +
            $"Lines mentioning the label:\n{string.Join('\n', linesMentioningLabel)}\n" +
            $"File format sample (first 5 + last 15 lines):\n{string.Join('\n', sampleLines)}");

        static bool IsHexAddress(string token) => token.Length == 4 && token.All(Uri.IsHexDigit);
        static bool TokenIsLabel(string token, string label) => token == label || token == label + ":";
    }
    public string Get6502FunctionalTestBinary(bool disableDecimalTests = false, string? downloadDir = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // The AS65 assembler is a Windows-only executable.
            // On non-Windows platforms, use the pre-assembled binary from the repo.
            // Note: the pre-built binary has decimal mode enabled (disableDecimalTests = false).
            if (disableDecimalTests)
                throw new PlatformNotSupportedException("Assembling with decimal tests disabled requires Windows (AS65 assembler is Windows-only). The pre-built binary always has decimal mode enabled.");

            return GetPrebuilt6502FunctionalTestBinary(downloadDir);
        }

        // Get source code file path (with modified contents to suit our test purpose)
        var sourceCodeFilePath = Get6502FunctionalTestSourceCode(disableDecimalTests, downloadDir);
        // Download AS65 assembler .zip file, extract it, and return full file path to as65.exe
        var as65exeFilePath = GetAS65AssemblerFilePath(downloadDir);
        // Compile source code to .bin & .lst file
        var functionalTestBinary = Compile6502FunctionalTestBinary(as65exeFilePath, sourceCodeFilePath);
        // Return full path to the compiled 6502 functional test binary
        return functionalTestBinary;
    }

    private string GetPrebuilt6502FunctionalTestBinary(string? downloadDir = null)
    {
        if (string.IsNullOrEmpty(downloadDir))
            downloadDir = Directory.GetCurrentDirectory();

        var url = "https://github.com/Klaus2m5/6502_65C02_functional_tests/blob/master/bin_files/6502_functional_test.bin?raw=true";
        var filePath = Path.Join(downloadDir, "6502_functional_test.bin");

        _logger.LogInformation("Non-Windows platform detected. Downloading pre-built binary from {Url}", url);
        DownloadFile(url, filePath);

        // The 6502 functional test source code uses the assembler directive "*= $000A" to set
        // the code origin, meaning the first 10 bytes ($0000-$0009) are never written by the
        // program and exist only as zero-padding in the full 64KB pre-built image.
        //
        // When AS65 assembles the source on Windows, it outputs only the bytes starting from
        // the origin ($000A), producing a 65526-byte file. The test then loads that file at
        // address $000A via BinaryLoader (forceLoadAddress: 0x000A in Functional_test.cs).
        //
        // To make the pre-built 64KB image compatible with that same load address, strip the
        // leading 10 zero-bytes so the resulting file has the same layout as the AS65 output.
        const ushort originAddress = 0x000A;
        var fullImage = File.ReadAllBytes(filePath);
        if (fullImage.Length == 65536)
        {
            File.WriteAllBytes(filePath, fullImage[originAddress..]);
        }

        return filePath;
    }
    private string Compile6502FunctionalTestBinary(string as65exeFilePath, string sourceCodeFilePath)
    {
        // Assume output files of the compilation (.bin and .lst file) are placed 
        // in same directory as the source code that was compiled.
        string compiledBinFile = Path.Join(Path.GetDirectoryName(sourceCodeFilePath), Path.GetFileNameWithoutExtension(sourceCodeFilePath)) + ".bin";
        if (File.Exists(compiledBinFile))
            File.Delete(compiledBinFile);
        string compiledLstFile = Path.Join(Path.GetDirectoryName(sourceCodeFilePath), Path.GetFileNameWithoutExtension(sourceCodeFilePath)) + ".lst";
        if (File.Exists(compiledLstFile))
            File.Delete(compiledLstFile);

        string arguments = $"-l -m -w -h0 {sourceCodeFilePath}";
        // Captured so an assembly failure can report WHAT the assembler said — the
        // logger is typically a NullLogger in tests, which would swallow the errors.
        var assemblerOutput = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using (var process = new Process())
        {
            process.StartInfo.FileName = as65exeFilePath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            //Seems every row written to stderr by the as65.exe does not mean it's an error.
            //Hack: Don't send logs to _logger.LogError(data.Data), instead as trace
            process.OutputDataReceived += (sender, data) => { if (data.Data != null) { assemblerOutput.Enqueue(data.Data); _logger.LogTrace("{Data}", data.Data); } };
            process.ErrorDataReceived += (sender, data) => { if (data.Data != null) { assemblerOutput.Enqueue(data.Data); _logger.LogTrace("{Data}", data.Data); } };
            _logger.LogInformation("Executing {As65ExeFilePath}", as65exeFilePath);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var exited = process.WaitForExit(1000 * 120);
            _logger.LogInformation("Exited: {Exited}", exited);
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                throw new DotNet6502Exception($"Assembler {as65exeFilePath} did not finish within 120s for {sourceCodeFilePath}. Output so far:\n{string.Join('\n', assemblerOutput)}");
            }
        }

        if (!File.Exists(compiledBinFile))
            throw new DotNet6502Exception($"Executing {as65exeFilePath} with arguments {arguments} did not generate expected binary file at {compiledBinFile}. Assembler output:\n{string.Join('\n', assemblerOutput)}");
        return compiledBinFile;
    }

    private string Get6502FunctionalTestSourceCode(bool disableDecimalTests, string? downloadDir = null)
    {
        if (string.IsNullOrEmpty(downloadDir))
            downloadDir = Directory.GetCurrentDirectory();

        // Download 6502 functional test source code (.as64 assembler)
        var functionalTestSourceCodeUrl = "https://raw.githubusercontent.com/Klaus2m5/6502_65C02_functional_tests/master/6502_functional_test.a65";
        var functionalTestSourceCodeFileName = "6502_functional_test.a65";
        var functionalTestSourceCodeFileFilePath = Path.Join(downloadDir, functionalTestSourceCodeFileName);

        DownloadFile(functionalTestSourceCodeUrl, functionalTestSourceCodeFileFilePath);

        if (!disableDecimalTests)
            return functionalTestSourceCodeFileFilePath;

        // Modify test source code to disable decimal tests
        var modifiedFileName = "6502_functional_test_decimal_disabled.a65";
        var modifiedFunctionalTestSourceCodeFileFilePath = Path.Join(downloadDir, modifiedFileName);
        ModifyAsmSourceCodeSettings(functionalTestSourceCodeFileFilePath, modifiedFunctionalTestSourceCodeFileFilePath, disableDecimal: true);

        return modifiedFunctionalTestSourceCodeFileFilePath;
    }

    private static void DownloadFile(string uri, string outputPath)
    {
        byte[] fileBytes = s_httpClient.GetByteArrayAsync(uri).Result;
        File.WriteAllBytes(outputPath, fileBytes);
    }

    private static void ModifyAsmSourceCodeSettings(string originalFile, string newFile, bool disableDecimal)
    {
        // Change settings by modifying assembler source code
        var fileContentsLineArray = File.ReadAllLines(originalFile);
        var modifiedFileContentsLineArray = new List<string>();
        for (int i = 0; i < fileContentsLineArray.Length; i++)
        {
            var line = fileContentsLineArray[i];
            if(disableDecimal)
            {
                if (line.StartsWith("disable_decimal") && line.Contains('='))
                    line = "disable_decimal = 1";
            }
            modifiedFileContentsLineArray.Add(line);
        }
        // Write modified 6502 assembler code to new file
        File.WriteAllLines(newFile, modifiedFileContentsLineArray);
    }

    private string GetAS65AssemblerFilePath(string? downloadDir = null)
    {
        // Download 6502 functional test program assembler source code
        var url = "https://github.com/Klaus2m5/6502_65C02_functional_tests/blob/master/as65_142.zip?raw=true";

        if (string.IsNullOrEmpty(downloadDir))
            downloadDir = Directory.GetCurrentDirectory();

        var downloadFileName = "as65_142.zip";
        var downloadFullFilePath = Path.Join(downloadDir, downloadFileName);
        DownloadFile(url, downloadFullFilePath);

        // Unzip as65.exe from .zip and get full file path to it
        var as65ExeFilePath = GetAS65AssemblerExeFilePath(downloadFullFilePath);

        // Return full file path to as65.exe
        return as65ExeFilePath;
    }

    private static string GetAS65AssemblerExeFilePath(string as65ZipFilePath)
    {
        // Unzip to folder in same directory as .zip file
        string zipExtractPath = Path.Join(Path.GetDirectoryName(as65ZipFilePath), Path.GetFileNameWithoutExtension(as65ZipFilePath));
        if (Directory.Exists(zipExtractPath))
            Directory.Delete(zipExtractPath, recursive: true);
        Directory.CreateDirectory(zipExtractPath);

        // Which files from .zip file we'll extract
        string as65ExeFileName = "as65.exe";
        List<string> extractFileNames = new()
        {
            as65ExeFileName
        };

        // Ensures that the last character on the extraction path
        // is the directory separator char.
        // Without this, a malicious zip file could try to traverse outside of the expected
        // extraction path.
        if (!zipExtractPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            zipExtractPath += Path.DirectorySeparatorChar;

        using (ZipArchive archive = ZipFile.OpenRead(as65ZipFilePath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {

                if (extractFileNames.Contains(entry.FullName))
                //if (entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Gets the full path to ensure that relative segments are removed.
                    string destinationPath = Path.GetFullPath(Path.Combine(zipExtractPath, entry.FullName));

                    // Ordinal match is safest, case-sensitive volumes can be mounted within volumes that
                    // are case-insensitive.
                    if (destinationPath.StartsWith(zipExtractPath, StringComparison.Ordinal))
                        entry.ExtractToFile(destinationPath);
                }
            }
        }

        // Return the full file path to the unzipped as65.exe
        return Path.Join(zipExtractPath, as65ExeFileName);
    }
}
