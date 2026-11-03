using System.IO;

namespace AsyncFileScanner.Services;

/**
 * Что здесь происходит?
 * 1. Создаётся буфер размером 1024 байта.
 * 2. Открывается файл для чтения.
 * 3. Создаётся асинхронный `FileStream`.
 * 4. Выполняется чтение первых 1024 байт или меньшего количества, если файл меньше.
 * 5. Возвращается количество прочитанных байтов. 
 *
 * Наша задача — проверить сам факт асинхронной обработки и получить небольшую часть содержимого. Это позволяет не загружать большие файлы целиком.
 * Обратите внимание: `FileStream` открывается с `FileShare.Read`. Если другой процесс открыл файл с несовместимым режимом доступа, открытие может завершиться ошибкой.
 */
public sealed class FileProbe
{
    public async Task<int> ReadHeaderAsync
        (
        string path,
        CancellationToken cancellationToken
        )
    {
        byte[] buffer = new byte[1024];
        // доабвить сюда код
        await using var stream = new FileStream
            (
            path, 
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize:1024,
            FileOptions.Asynchronous
            );

        return await stream.ReadAsync
            (
            buffer.AsMemory(),
            cancellationToken
            );
    }
}