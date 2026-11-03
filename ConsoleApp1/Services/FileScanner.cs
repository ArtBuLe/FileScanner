using System.Diagnostics;
using AsyncFileScanner.Models;

namespace AsyncFileScanner.Services;

/**
 * В этом примере есть несколько важных особенностей.
 * 1. перечисление файлов выполняется через `Directory.EnumerateFiles`. Это синхронный API. Он не становится асинхронным оттого, что находится внутри `async` метода.
 * 2. асинхронная операция здесь — чтение файла через `ReadHeaderAsync`.
 * 3. ошибки одного файла сохраняются в список. Они не приводят к остановке всего сканирования.
 * 4. отмена проверяется перед обработкой каждого следующего файла, а также передаётся непосредственно в `ReadAsync`.
 * Важно: отмена не может гарантированно мгновенно прервать системный вызов перечисления файлов. В данном варианте она проверяется между итерациями.
 */
public sealed class FileScanner
{
    private readonly FileProbe _probe;

    public FileScanner(FileProbe probe)
    {
        _probe = probe;
    }

    public async Task<ScanSummary> ScanAsync
        (
        ScanOptions options,
        CancellationToken cancellationToken
        )
    {
        ValidateOptions(options);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<ScanResult>();
        var errors = new List<string>();

        int scannedFiles = 0;
        long totalBytes = 0;

        // доабвить сюда код
        var normalizedExtensions = options.Extensions
            .Select(ext => ext.Trim().ToLowerInvariant())
            .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
            .ToHashSet();

        var files = Directory.EnumerateFiles(options.DirectoryPath, "*.*", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!normalizedExtensions.Contains(ext))
            {
                continue;
            }

            scannedFiles++;

            try
            {
                var fileInfo = new FileInfo(file);
                long size = fileInfo.Length;

                await _probe.ReadHeaderAsync(file, cancellationToken);

                results.Add(new ScanResult(Path.GetFileName(file), size));
                totalBytes += size;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        stopwatch.Stop();

        return new ScanSummary
            (
            scannedFiles,
            results.Count,
            totalBytes,
            stopwatch.Elapsed,
            results,
            errors
            );
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(options.DirectoryPath));
        }

        if (!Directory.Exists(options.DirectoryPath))
        {
            throw new DirectoryNotFoundException($"Директория не найдена: {options.DirectoryPath}");
        }

        if (options.Extensions == null || options.Extensions.Count == 0)
        {
            throw new ArgumentException("Список расширений не может быть пустым.", nameof(options.Extensions));
        }
    }
}