using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncFileScanner.Models;
using AsyncFileScanner.Services;

namespace AsyncFileScanner;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== FILE SCANNER ===\n");

        Console.WriteLine("Введите путь к директории:");
        string path = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.WriteLine("\nВведите расширения через запятую (например: .cs,.txt):");
        string extInput = Console.ReadLine()?.Trim() ?? string.Empty;
        var extensions = extInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        Console.WriteLine("\nМинимальный размер (КБ) [0, если не нужно ограничивать]:");
        long minKb = ParseLong(Console.ReadLine(), 0);

        Console.WriteLine("Максимальный размер (КБ) [0 = без ограничений]:");
        long maxKbInput = ParseLong(Console.ReadLine(), 0);
        long maxKb = maxKbInput > 0 ? maxKbInput : long.MaxValue;

        Console.WriteLine("\nМаксимальное количество одновременных операций:");
        int maxConcurrency = (int)ParseLong(Console.ReadLine(), 4);

        var options = new ScanOptions(
            DirectoryPath: path,
            Extensions: extensions,
            MinSizeBytes: minKb * 1024,
            MaxSizeBytes: maxKb * 1024,
            MaxConcurrency: maxConcurrency
        );

        var probe = new FileProbe();
        var scanner = new FileScanner(probe);
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[!] Получен сигнал отмены (Ctrl+C). Завершаем обработку...");
        };

        Console.WriteLine("\nСканирование... (нажмите Ctrl+C для отмены)");

        try
        {
            var summary = await scanner.ScanAsync(options, cts.Token);
            PrintSummary(summary, options.MaxConcurrency);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[!] Критическая ошибка: {ex.Message}");
        }
    }

    private static void PrintSummary(ScanSummary summary, int configuredLimit)
    {
        Console.WriteLine("\n=== РЕЗУЛЬТАТ СКАНИРОВАНИЯ ===\n");

        if (summary.Results.Count > 0)
        {
            Console.WriteLine("Найденные файлы:");
            for (int i = 0; i < summary.Results.Count; i++)
            {
                Console.WriteLine($"[{i + 1}] {summary.Results[i].FileName,-25} {FormatSize(summary.Results[i].SizeInBytes)}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Просмотрено файлов: {summary.TotalViewedFiles}");
        Console.WriteLine($"Подходящих файлов: {summary.MatchingFilesCount}");
        Console.WriteLine($"Общий размер: {FormatSize(summary.TotalSizeBytes)}");
        Console.WriteLine($"Ошибок: {summary.ErrorCount}");
        Console.WriteLine($"Время выполнения: {summary.ElapsedTime.TotalSeconds:F2} сек.");

        Console.WriteLine($"\nОграничение: {configuredLimit}");
        Console.WriteLine($"Максимум одновременно обрабатываемых файлов: {summary.MaxObservedConcurrency}");

        if (summary.Errors.Count > 0)
        {
            Console.WriteLine("\n[!] Детали ошибок:");
            foreach (var err in summary.Errors.Take(5))
            {
                Console.WriteLine($"    - {err}");
            }
            if (summary.Errors.Count > 5)
            {
                Console.WriteLine($"    ... и ещё {summary.Errors.Count - 5}");
            }
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} МБ";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} КБ";
        return $"{bytes} байт";
    }

    private static long ParseLong(string? input, long defaultValue)
    {
        if (long.TryParse(input, out long result) && result >= 0)
            return result;
        return defaultValue;
    }
}