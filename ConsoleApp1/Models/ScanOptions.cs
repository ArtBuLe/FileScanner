namespace AsyncFileScanner.Models;

public sealed record ScanOptions
    (
    string DirectoryPath,
    IReadOnlyList<string> Extensions
    );