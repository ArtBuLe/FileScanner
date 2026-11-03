using System;
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

        Console.WriteLine("\nВведите расширения через запятую:");
        string extInput = Console.ReadLine()?.Trim() ?? string.Empty;

        var extensions = extInput
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var options = new ScanOptions(path, extensions);
        var probe = new FileProbe();
        var scanner = new FileScanner(probe);

        using var cts = new CancellationTokenSource();

        var cancelTask = Task.Run(() =>
        {
            Console.WriteLine("\nСканирование...");
            Console.WriteLine("Нажмите Enter для отмены.");
            Console.ReadLine();
            cts.Cancel();
        });

        try
        {
            var summary = await scanner.ScanAsync(options, cts.Token);

            Console.WriteLine("\nНайденные файлы:\n");

            if (summary.Results.Count == 0)
            {
                Console.WriteLine("Файлы не найдены.");
            }
            else
            {
                for (int i = 0; i < summary.Results.Count; i++)
                {
                    Console.WriteLine($"[{i + 1}] {summary.Results[i].FileName,-20} {summary.Results[i].SizeInBytes} байт");
                }
            }

            Console.WriteLine();
            if (cts.Token.IsCancellationRequested)
            {
                Console.WriteLine("Сканирование отменено пользователем.");
            }
            else
            {
                Console.WriteLine("Сканирование завершено.");
            }

            Console.WriteLine($"Обработано файлов: {summary.ScannedFiles}");

            if (summary.FoundFiles > 0)
            {
                Console.WriteLine($"Всего найдено файлов: {summary.FoundFiles}");
                Console.WriteLine($"Общий размер: {summary.TotalBytes} байт");
            }

            if (summary.Errors.Count > 0)
            {
                Console.WriteLine($"\n[!] Ошибок при обработке: {summary.Errors.Count}");
                foreach (var err in summary.Errors.Take(3))
                {
                    Console.WriteLine($"    - {err}");
                }
                if (summary.Errors.Count > 3)
                {
                    Console.WriteLine($"    ... и ещё {summary.Errors.Count - 3}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nСканирование прервано критической ошибкой отмены.");
        }
    }
}