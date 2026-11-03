namespace AsyncFileScanner.Models;

public sealed record ScanSummary(
    int TotalViewedFiles,
    int MatchingFilesCount,
    long TotalSizeBytes,
    int ErrorCount,
    TimeSpan ElapsedTime,
    int MaxObservedConcurrency,
    IReadOnlyList<ScanResult> Results,
    IReadOnlyList<string> Errors
);