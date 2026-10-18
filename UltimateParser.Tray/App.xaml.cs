using UltimateParser.Tray.Tray.Implementations;

namespace UltimateParser.Tray;

/// <summary>
/// Приложение иконки в трее. Окно не завершает процесс
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Имя мьютекса, чтобы второй запуск не открывал ещё одну иконку
    /// </summary>
    private const string SINGLE_INSTANCE_NAME = "UltimateParser.Tray";

    /// <summary>
    /// Удерживает единственный экземпляр
    /// </summary>
    private Mutex? _instance;

    /// <summary>
    /// Иконка и опрос контейнера
    /// </summary>
    private TrayHost? _host;

    #region Lifetime

    /// <summary>
    /// Поднимает одну иконку
    /// </summary>
    /// <param name="e">
    /// Аргументы запуска
    /// </param>
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new Mutex(true, SINGLE_INSTANCE_NAME, out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        _host = new TrayHost();
    }

    /// <summary>
    /// Отпускает иконку и мьютекс
    /// </summary>
    /// <param name="e">
    /// Аргументы выхода
    /// </param>
    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _host?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    #endregion Lifetime
}
