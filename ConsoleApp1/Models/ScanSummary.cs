namespace AsyncFileScanner.Models;

public sealed record ScanSummary
    (
    int ScannedFiles,
    int FoundFiles,
    long TotalBytes,
    TimeSpan ElapsedTime,
    IReadOnlyList<ScanResult> Results,
    IReadOnlyList<string> Errors
    );