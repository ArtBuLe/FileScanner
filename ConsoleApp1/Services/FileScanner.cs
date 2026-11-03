using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncFileScanner.Models;

namespace AsyncFileScanner.Services;

public sealed class FileScanner
{
    private readonly FileProbe _probe;

    public FileScanner(FileProbe probe)
    {
        _probe = probe;
    }

    public async Task<ScanSummary> ScanAsync(
        ScanOptions options,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var stopwatch = Stopwatch.StartNew();
        var context = new ScanContext();
        using var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);

        var files = Directory.EnumerateFiles(options.DirectoryPath, "*.*", SearchOption.TopDirectoryOnly);
        var normalizedExtensions = options.Extensions
            .Select(ext => ext.Trim().ToLowerInvariant())
            .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
            .ToHashSet();

        var tasks = files
            .Where(f => normalizedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(path => ProcessFileLimitedAsync(path, options, semaphore, context, cancellationToken));
        var resultsArray = await Task.WhenAll(tasks);

        foreach (var res in resultsArray)
        {
            if (res.IsCancelled)
            {
                context.IsCancelled = true;
            }
            else if (res.IsError)
            {
                context.AddError(res.ErrorMessage!);
            }
            else if (res.Result != null)
            {
                context.AddResult(res.Result);
            }
        }

        stopwatch.Stop();

        return new ScanSummary(
            TotalViewedFiles: context.TotalViewedFiles,
            MatchingFilesCount: context.Results.Count,
            TotalSizeBytes: context.TotalSizeBytes,
            ErrorCount: context.Errors.Count,
            ElapsedTime: stopwatch.Elapsed,
            MaxObservedConcurrency: context.MaxObservedOperations,
            Results: context.Results,
            Errors: context.Errors
        );
    }

    private async Task<ProcessResult> ProcessFileLimitedAsync(
        string path,
        ScanOptions options,
        SemaphoreSlim semaphore,
        ScanContext context,
        CancellationToken token)
    {
        try
        {
            await semaphore.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            return new ProcessResult(IsCancelled: true);
        }

        try
        {
            int currentActive = Interlocked.Increment(ref context.ActiveOperations);
            UpdateMaxObserved(ref context.MaxObservedOperations, currentActive);

            if (token.IsCancellationRequested)
            {
                return new ProcessResult(IsCancelled: true);
            }

            Interlocked.Increment(ref context.TotalViewedFiles);

            var fileInfo = new FileInfo(path);
            long size = fileInfo.Length;

            if (size < options.MinSizeBytes || size > options.MaxSizeBytes)
            {
                return new ProcessResult(IsSuccess: true);
            }

            await _probe.ReadHeaderAsync(path, token);

            long addedSize = Interlocked.Add(ref context.TotalSizeBytes, size);
            context.AddResult(new ScanResult(Path.GetFileName(path), size));

            return new ProcessResult(IsSuccess: true);
        }
        catch (OperationCanceledException)
        {
            return new ProcessResult(IsCancelled: true);
        }
        catch (Exception ex)
        {
            context.AddError($"{Path.GetFileName(path)}: {ex.Message}");
            return new ProcessResult(IsError: true, ErrorMessage: ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref context.ActiveOperations);
            semaphore.Release();
        }
    }
    private static void UpdateMaxObserved(ref int maxObserved, int current)
    {
        int currentMax;
        do
        {
            currentMax = maxObserved;
            if (current <= currentMax) return;
        }
        while (Interlocked.CompareExchange(ref maxObserved, current, currentMax) != currentMax);
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(options.DirectoryPath));

        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException($"Директория не найдена: {options.DirectoryPath}");

        if (options.Extensions == null || options.Extensions.Count == 0)
            throw new ArgumentException("Список расширений не может быть пустым.", nameof(options.Extensions));

        if (options.MaxConcurrency < 1)
            throw new ArgumentException("Максимальное количество одновременных операций должно быть >= 1.", nameof(options.MaxConcurrency));
    }

    private sealed class ScanContext
    {
        public int TotalViewedFiles;
        public long TotalSizeBytes;
        public int ActiveOperations;
        public int MaxObservedOperations;
        public bool IsCancelled;

        private readonly object _lock = new();
        public readonly List<ScanResult> Results = new();
        public readonly List<string> Errors = new();

        public void AddResult(ScanResult result)
        {
            lock (_lock) Results.Add(result);
        }

        public void AddError(string error)
        {
            lock (_lock) Errors.Add(error);
        }
    }

    private sealed record ProcessResult(
        bool IsSuccess = false,
        bool IsError = false,
        bool IsCancelled = false,
        ScanResult? Result = null,
        string? ErrorMessage = null
    );
}