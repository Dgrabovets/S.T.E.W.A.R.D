using System.IO;
using Steward.Status.Common;
using Steward.ViewModels;
using Steward.Shell;

namespace Steward;

/// <summary>
/// Приложение иконки в трее. Окно не завершает процесс
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Имя мьютекса, чтобы второй запуск не открывал ещё одну иконку
    /// </summary>
    private const string SINGLE_INSTANCE_NAME = "Steward";

    /// <summary>
    /// Удерживает единственный экземпляр
    /// </summary>
    private Mutex? _instance;

    /// <summary>
    /// Иконка и опрос контейнера
    /// </summary>
    private TrayHost? _host;

    /// <summary>
    /// Ключ запуска: сразу открыть главное окно на странице, например <c>--open Homework</c>
    /// </summary>
    private const string OPEN_ARGUMENT = "--open";

    #region Lifetime

    /// <summary>
    /// Поднимает одну иконку
    /// </summary>
    /// <param name="e">Аргументы запуска</param>
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += (_, args) =>
        {
            LogUnhandled(args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogUnhandled(args.Exception);
            args.SetObserved();
        };

        _instance = new Mutex(true, SINGLE_INSTANCE_NAME, out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        _host = new TrayHost();

        var open = Array.IndexOf(e.Args, OPEN_ARGUMENT);
        if (open >= 0 && open + 1 < e.Args.Length && Enum.TryParse<AppPageEnum>(e.Args[open + 1], ignoreCase: true, out var page))
            _host.ShowMain(page);
    }

    /// <summary>
    /// Записывает необработанную ошибку в <c>data/steward-errors.log</c>: иконка в трее не должна исчезать из-за одного сбоя, а причину потом можно найти в файле
    /// </summary>
    /// <param name="exception">Ошибка</param>
    private static void LogUnhandled(Exception exception)
    {
        try
        {
            var file = Path.Combine(DataLocation.DataDirectory(), "steward-errors.log");

            // Журнал не растёт без предела: большой файл уходит в .old, а прежний .old стирается
            if (File.Exists(file) && new FileInfo(file).Length > 512 * 1024)
                File.Move(file, file + ".old", overwrite: true);

            File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception logging) when (logging is IOException or UnauthorizedAccessException)
        {
            // Журнал недоступен: ошибку уже погасили, дальше работаем
        }
    }

    /// <summary>
    /// Отпускает иконку и мьютекс
    /// </summary>
    /// <param name="e">Аргументы выхода</param>
    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _host?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    #endregion Lifetime
}
