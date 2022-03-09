using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Tests.Helpers
{
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

        public FunctionalTestCompiler(ILogger<FunctionalTestCompiler> logger)        
        {
            _logger = logger;
        }
        public string Get6502FunctionalTestBinary(bool disableDecimalTests = true, string downloadDir = null)
        {
            // Get source code file path (with modified contents to suit our test purpose)
            var sourceCodeFilePath = Get6502FunctionalTestSourceCode(disableDecimalTests, downloadDir);
            // Download AS65 assembler .zip file, extract it, and return full file path to as65.exe
            var as65exeFilePath = GetAS65AssemblerFilePath(downloadDir);
            // Compile source code to .bin & .lst file
            var functionalTestBinary = Compile6502FunctionalTestBinary(as65exeFilePath, sourceCodeFilePath);
            // Return full path to the compiled 6502 functional test binary
            return functionalTestBinary;
        }
        private string Compile6502FunctionalTestBinary(string as65exeFilePath, string sourceCodeFilePath)
        {
            // Assume output files of the compilation (.bin and .lst file) are placed 
            // in same directory as the source code that was compiled.
            string compiledBinFile = Path.Join(Path.GetDirectoryName(sourceCodeFilePath), Path.GetFileNameWithoutExtension(sourceCodeFilePath)) + ".bin";
            if(File.Exists(compiledBinFile))
                File.Delete(compiledBinFile);
            string compiledLstFile = Path.Join(Path.GetDirectoryName(sourceCodeFilePath), Path.GetFileNameWithoutExtension(sourceCodeFilePath)) + ".lst";
            if(File.Exists(compiledLstFile))
                File.Delete(compiledLstFile);

            string arguments = $"-l -m -w -h0 {sourceCodeFilePath}";
            using (var process = new Process())
            {
                process.StartInfo.FileName = as65exeFilePath;
                process.StartInfo.Arguments = arguments;
                //process.StartInfo.FileName = @"cmd.exe";
                //process.StartInfo.Arguments = @"/c dir";      // print the current working directory information
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.OutputDataReceived += (sender, data) => _logger.LogTrace(data.Data);
                //Seems every row written to stderr by the as65.exe does not mean it's an error.
                //Hack: Don't send logs to _logger.LogError(data.Data), insted as trace
                process.ErrorDataReceived += (sender, data) => _logger.LogTrace(data.Data); 
                _logger.LogInformation($"Executing {as65exeFilePath}");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var exited = process.WaitForExit(1000 * 10);     // (optional) wait up to 10 seconds
                _logger.LogInformation($"Exited: {exited}");
            }

            if(!File.Exists(compiledBinFile))
                throw new DotNet6502Exception($"Executing {as65exeFilePath} with arguments {arguments} did not generate expected binary file at {compiledBinFile}");
            return compiledBinFile;
        }

        private string Get6502FunctionalTestSourceCode(bool disableDecimalTests, string downloadDir)
        {
            if(string.IsNullOrEmpty(downloadDir))
                Directory.GetCurrentDirectory();
            
            // Download 6502 functional test source code (.as64 assembler)
            var functionalTestSourceCodeUrl = "https://raw.githubusercontent.com/Klaus2m5/6502_65C02_functional_tests/master/6502_functional_test.a65";
            var functionalTestSourceCodeFileName = "6502_functional_test.a65";
            var functionalTestSourceCodeFileFilePath = Path.Join(downloadDir, functionalTestSourceCodeFileName);

            DownloadFile(functionalTestSourceCodeUrl, functionalTestSourceCodeFileFilePath);
            //var wc = new System.Net.WebClient();
            //wc.DownloadFile(functionalTestSourceCodeUrl, functionalTestSourceCodeFileFilePath);

            if(!disableDecimalTests)
                return functionalTestSourceCodeFileFilePath;

            // Modify test source code to disable decimal tests
            var modifiedFileName = "6502_functional_test_decimal_disabled.a65";
            var modifiedFunctionalTestSourceCodeFileFilePath = Path.Join(downloadDir, modifiedFileName);
            ModifyAsmSourceCodeSettings(functionalTestSourceCodeFileFilePath, modifiedFunctionalTestSourceCodeFileFilePath);

            return modifiedFunctionalTestSourceCodeFileFilePath;
        }

        private void DownloadFile(string uri, string outputPath)
        {
            byte[] fileBytes = s_httpClient.GetByteArrayAsync(uri).Result;
            File.WriteAllBytes(outputPath, fileBytes);
        }

        private void ModifyAsmSourceCodeSettings(string originalFile, string newFile)
        {
            // Change settings by modifying assembler source code
            var fileContentsLineArray = File.ReadAllLines(originalFile);
            var modifiedFileContentsLineArray = new List<string>();
            for (int i = 0; i < fileContentsLineArray.Length; i++)
            {
                var line = fileContentsLineArray[i];
                if(line.StartsWith("disable_decimal") && line.Contains("="))
                    line = "disable_decimal = 1";
                modifiedFileContentsLineArray.Add(line);
            }
            // Write modified 6502 assembler code to new file
            File.WriteAllLines(newFile, modifiedFileContentsLineArray);
        }        

        private string GetAS65AssemblerFilePath(string downloadDir = null)
        {
            // Download 6502 functional test program assembler source code
            var wc = new System.Net.WebClient();
            var url = "https://github.com/Klaus2m5/6502_65C02_functional_tests/blob/master/as65_142.zip?raw=true";

            if(string.IsNullOrEmpty(downloadDir))
                downloadDir = Directory.GetCurrentDirectory();
                
            var downloadFileName = "as65_142.zip";
            var downloadFullFilePath = Path.Join(downloadDir, downloadFileName);
            wc.DownloadFile(url, downloadFullFilePath);

            // Unzip as65.exe from .zip and get full file path to it
            var as65ExeFilePath = GetAS65AssemblerExeFilePath(downloadFullFilePath);

            // Return full file path to as65.exe
            return as65ExeFilePath;
        }

        private string GetAS65AssemblerExeFilePath(string as65ZipFilePath)
        {
            // Unzip to folder in same directory as .zip file
            string zipExtractPath = Path.Join(Path.GetDirectoryName(as65ZipFilePath), Path.GetFileNameWithoutExtension(as65ZipFilePath));
            if(Directory.Exists(zipExtractPath))
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
}
