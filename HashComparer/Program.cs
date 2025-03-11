using System.Security.Cryptography;
namespace HashComparer;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("File Hash Comparator");
        Console.WriteLine("===================");
        
        Console.WriteLine("\nEnter path to original game files (file or directory):");
        var originalPath = Console.ReadLine()?.Trim() ?? string.Empty;
        
        if (string.IsNullOrEmpty(originalPath) || !Path.Exists(originalPath))
        {
            Console.WriteLine("Invalid path. File or directory does not exist.");
            await WaitForExit();
            return;
        }
        
        Console.WriteLine("\nEnter path to processed/patched files (file or directory):");
        var processedPath = Console.ReadLine()?.Trim() ?? string.Empty;
        
        if (string.IsNullOrEmpty(processedPath) || !Path.Exists(processedPath))
        {
            Console.WriteLine("Invalid path. File or directory does not exist.");
            await WaitForExit();
            return;
        }
        
        var isDirectory = Directory.Exists(originalPath);
        
        if (isDirectory)
        {
            if (!Directory.Exists(processedPath))
            {
                Console.WriteLine("Error: First path is a directory but second path is a file.");
                await WaitForExit();
                return;
            }
            
            await CompareDirectories(originalPath, processedPath);
        }
        else
        {
            if (Directory.Exists(processedPath))
            {
                Console.WriteLine("Error: First path is a file but second path is a directory.");
                await WaitForExit();
                return;
            }
            
            await CompareFiles(originalPath, processedPath);
        }
        
        await WaitForExit();
    }

    private static async Task CompareDirectories(string originalDir, string processedDir)
    {
        Console.WriteLine("\nComparing directories...");
        Console.WriteLine($"Original directory: {originalDir}");
        Console.WriteLine($"Processed directory: {processedDir}");
        
        Console.WriteLine("\nEnter file pattern to match (e.g., *.dll, *.exe, leave blank for all files):");
        var pattern = Console.ReadLine()?.Trim() ?? "*.*";
        if (string.IsNullOrEmpty(pattern))
            pattern = "*.*";
        
        var originalFiles = Directory.GetFiles(originalDir, pattern, SearchOption.AllDirectories)
            .Select(f => f[originalDir.Length..].TrimStart('\\', '/'))
            .ToList();
        
        var processedFiles = Directory.GetFiles(processedDir, pattern, SearchOption.AllDirectories)
            .Select(f => f[processedDir.Length..].TrimStart('\\', '/'))
            .ToList();
        
        var commonFiles = originalFiles.Intersect(processedFiles).ToList();
        
        Console.WriteLine($"\nFound {originalFiles.Count} files in original directory");
        Console.WriteLine($"Found {processedFiles.Count} files in processed directory");
        Console.WriteLine($"Found {commonFiles.Count} common files to compare");
        
        if (commonFiles.Count == 0)
        {
            Console.WriteLine("No common files found for comparison.");
            return;
        }
        
        Console.WriteLine("\nDo you want to show all files or only mismatches? (A/M):");
        var showOnlyMismatches = Console.ReadLine()?.Trim().ToUpper() == "M";
        
        var results = new List<ComparisonResult>();
        
        var tasks = commonFiles.Select(async relativeFilePath => {
            var fullOriginalPath = Path.Combine(originalDir, relativeFilePath);
            var fullProcessedPath = Path.Combine(processedDir, relativeFilePath);
            
            return await CompareFilesInternalAsync(fullOriginalPath, fullProcessedPath);
        });
        
        results.AddRange(await Task.WhenAll(tasks));
        
        foreach (var result in results.Where(r => !showOnlyMismatches || !r.HashesMatch))
            DisplayResult(result);
        
        var matchedFiles = results.Count(r => r.HashesMatch);
        var mismatchedFiles = results.Count - matchedFiles;
        
        Console.WriteLine("\n===== SUMMARY =====");
        Console.WriteLine($"Total files compared: {results.Count}");
        Console.WriteLine($"Files with matching hashes: {matchedFiles}");
        Console.WriteLine($"Files with different hashes: {mismatchedFiles}");
        
        if (mismatchedFiles > 0)
        {
            Console.WriteLine("\nMismatched files:");
            foreach (var result in results.Where(r => !r.HashesMatch))
                Console.WriteLine($"- {result.FileName}");
        }
    }

    private static async Task CompareFiles(string originalFile, string processedFile)
    {
        Console.WriteLine("\nComparing individual files...");
        var result = await CompareFilesInternalAsync(originalFile, processedFile);
        DisplayResult(result);
    }

    private static async Task<ComparisonResult> CompareFilesInternalAsync(string originalFile, string processedFile)
    {
        try
        {
            var originalMD5 = await CalculateMD5Async(originalFile);
            var processedMD5 = await CalculateMD5Async(processedFile);
            
            var originalInfo = new FileInfo(originalFile);
            var processedInfo = new FileInfo(processedFile);
            
            var originalSize = originalInfo.Length;
            var processedSize = processedInfo.Length;
            
            var hashesMatch = originalMD5 == processedMD5;
            var isBinary = await IsBinaryFileAsync(originalFile);
            var lineEndingDifference = false;
            
            if (!hashesMatch && !isBinary)
            {
                var originalContent = NormalizeLineEndings(await File.ReadAllTextAsync(originalFile));
                var processedContent = NormalizeLineEndings(await File.ReadAllTextAsync(processedFile));
                
                lineEndingDifference = originalContent == processedContent;
            }
            
            return new ComparisonResult(
                FileName: Path.GetFileName(originalFile),
                OriginalPath: originalFile,
                ProcessedPath: processedFile,
                OriginalMD5: originalMD5,
                ProcessedMD5: processedMD5,
                OriginalSize: originalSize,
                ProcessedSize: processedSize,
                HashesMatch: hashesMatch,
                IsBinary: isBinary,
                LineEndingDifference: lineEndingDifference
            );
        }
        catch (Exception ex)
        {
            return new ComparisonResult(
                FileName: Path.GetFileName(originalFile),
                OriginalPath: originalFile,
                ProcessedPath: processedFile,
                OriginalMD5: string.Empty,
                ProcessedMD5: string.Empty,
                OriginalSize: 0,
                ProcessedSize: 0,
                HashesMatch: false,
                IsBinary: false,
                LineEndingDifference: false,
                Error: ex.Message
            );
        }
    }

    private static void DisplayResult(ComparisonResult result)
    {
        Console.WriteLine("\n----------------------------------------");
        Console.WriteLine($"File: {result.FileName}");
        Console.WriteLine("----------------------------------------");
        
        if (!string.IsNullOrEmpty(result.Error))
        {
            Console.WriteLine($"ERROR: {result.Error}");
            return;
        }
        
        Console.WriteLine("MD5 Hashes:");
        Console.WriteLine($"  Original:  {result.OriginalMD5}");
        Console.WriteLine($"  Processed: {result.ProcessedMD5}");
        Console.WriteLine($"  Match:     {result.HashesMatch}");
        
        Console.WriteLine("\nFile Sizes:");
        Console.WriteLine($"  Original:  {result.OriginalSize:N0} bytes");
        Console.WriteLine($"  Processed: {result.ProcessedSize:N0} bytes");
        Console.WriteLine($"  Match:     {result.OriginalSize == result.ProcessedSize}");
        
        Console.WriteLine($"\nFile Type: {(result.IsBinary ? "Binary" : "Text")}");
        
        if (!result.HashesMatch)
        {
            if (result.LineEndingDifference)
            {
                Console.WriteLine("\nOnly line ending differences detected (CRLF vs LF)");
            }
            else if (!result.IsBinary)
            {
                Console.WriteLine("\nText files differ in content beyond line endings");
            }
            
            if (result.OriginalSize != result.ProcessedSize)
            {
                Console.WriteLine($"\nSize difference: {Math.Abs(result.OriginalSize - result.ProcessedSize):N0} bytes");
                
                if (result.OriginalSize < 10 * 1024 * 1024 && result.ProcessedSize < 10 * 1024 * 1024)
                {
                    Console.WriteLine("\nAnalyzing byte-level differences:");
                    AnalyzeBytePatterns(result.OriginalPath, result.ProcessedPath);
                }
            }
        }
    }

    private static void AnalyzeBytePatterns(string file1, string file2)
    {
        try
        {
            var bytes1 = File.ReadAllBytes(file1);
            var bytes2 = File.ReadAllBytes(file2);
            
            var minLength = Math.Min(bytes1.Length, bytes2.Length);
            var maxLength = Math.Max(bytes1.Length, bytes2.Length);
            
            var diffCount = 0;
            var firstDiffPos = -1;
            
            for (var i = 0; i < minLength; i++)
            {
                if (bytes1[i] != bytes2[i])
                {
                    diffCount++;
                    firstDiffPos = firstDiffPos == -1 ? i : firstDiffPos;
                    
                    if (diffCount <= 5)
                        Console.WriteLine($"  Difference at byte {i}: {bytes1[i]:X2} vs {bytes2[i]:X2}");
                }
            }
            
            if (bytes1.Length != bytes2.Length)
            {
                var longerArray = bytes1.Length > bytes2.Length ? bytes1 : bytes2;
                var extraBytes = maxLength - minLength;
                
                var allZeroes = Enumerable.Range(minLength, maxLength - minLength)
                    .All(i => longerArray[i] == 0);
                
                Console.WriteLine(allZeroes
                    ? $"  Extra {extraBytes} bytes are all zeroes"
                    : $"  Extra {extraBytes} bytes contain data");
            }
            
            Console.WriteLine($"  Total differences: {diffCount} bytes");
            if (diffCount > 0 && firstDiffPos >= 0)
                Console.WriteLine($"  First difference at byte position: {firstDiffPos}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error analyzing byte patterns: {ex.Message}");
        }
    }

    private static async Task<string> CalculateMD5Async(string filePath)
    {
        using var md5 = MD5.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await md5.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static async Task<bool> IsBinaryFileAsync(string filePath)
    {
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[Math.Min(4096, stream.Length)];
            var bytesRead = await stream.ReadAsync(buffer);
            
            return buffer.Take(bytesRead).Any(b => b == 0 || b > 127);
        }
        catch
        {
            return true;
        }
    }

    private static string NormalizeLineEndings(string text) => 
        text.Replace("\r\n", "\n").Replace("\r", "\n");

    private static async Task WaitForExit()
    {
        Console.WriteLine("\nPress any key to exit...");
        await Task.Run(Console.ReadKey);
    }
}