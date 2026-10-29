using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Steward.Homework.Storage;

/// <summary>
/// Блокировка файлов хранилища: общая для потоков одного процесса и именованный мьютекс для процессов (приложение Steward и Steward.Lms)
/// </summary>
internal static class FileLock
{
    /// <summary>
    /// Сколько секунд ждать освобождения блокировки
    /// </summary>
    private const int WAIT_SECONDS = 10;

    /// <summary>
    /// Блокировки по папкам для потоков одного процесса
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> Locks = new();

    /// <summary>
    /// Выполняет действие под блокировкой папки
    /// </summary>
    /// <param name="directory">Папка хранилища: по ней различаются блокировки</param>
    /// <param name="prefix">Начало имени мьютекса, свой для каждого вида хранилища</param>
    /// <param name="action">Действие над файлами хранилища</param>
    /// <exception cref="IOException">Блокировка занята другим процессом дольше ожидания</exception>
    public static void Run(string directory, string prefix, Action action)
    {
        var key = Path.GetFullPath(directory).ToLowerInvariant();
        lock (Locks.GetOrAdd(prefix + key, _ => new object()))
        {
            using var mutex = new Mutex(false, @"Local\" + prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32]);
            var owned = false;
            try
            {
                try
                {
                    owned = mutex.WaitOne(TimeSpan.FromSeconds(WAIT_SECONDS));
                }
                catch (AbandonedMutexException)
                {
                    // Другой процесс завершился, не отпустив мьютекс: файлы целы благодаря атомарной записи
                    owned = true;
                }

                if (!owned)
                    throw new IOException("Файлы хранилища заняты другим процессом");

                action();
            }
            finally
            {
                if (owned)
                    mutex.ReleaseMutex();
            }
        }
    }
}
