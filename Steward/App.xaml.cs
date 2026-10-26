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
    /// Ключ запуска, который рисует окно в PNG и завершается
    /// </summary>
    private const string SNAPSHOT_ARGUMENT = "--snapshot";

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

        // Рисование в PNG идёт до проверки мьютекса: рядом уже работает настоящая иконка, и она не должна мешать
        var snapshot = Array.IndexOf(e.Args, SNAPSHOT_ARGUMENT);
        if (snapshot >= 0 && snapshot + 1 < e.Args.Length)
        {
            var which = snapshot + 2 < e.Args.Length ? e.Args[snapshot + 2] : "main";
            SnapshotRenderer.Render(e.Args[snapshot + 1], which);
            Shutdown();

            return;
        }

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
