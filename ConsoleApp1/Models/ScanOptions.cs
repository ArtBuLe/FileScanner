namespace AsyncFileScanner.Models;

public sealed record ScanOptions(
    string DirectoryPath,
    IReadOnlyList<string> Extensions,
    long MinSizeBytes,
    long MaxSizeBytes,
    int MaxConcurrency
);