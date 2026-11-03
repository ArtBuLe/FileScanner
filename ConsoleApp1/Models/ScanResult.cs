namespace AsyncFileScanner.Models;

public sealed record ScanResult
    (
    string FileName,
    long SizeInBytes
    );