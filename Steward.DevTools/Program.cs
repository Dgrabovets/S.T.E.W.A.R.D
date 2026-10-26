using System.Windows;

namespace Steward.DevTools;

/// <summary>
/// Точка входа инструментов разработчика. Подключает ресурсы оформления приложения и выполняет команду
/// </summary>
public static class Program
{
    /// <summary>
    /// Текст подсказки по командам
    /// </summary>
    private const string USAGE = "Команды:\n  snapshot <файл.png> [режим]  нарисовать окно в PNG (режимы: main[:status|ideas|homework|journal[:open]], tray, settings[:formats], design, orbs, icons, slider, appicon, logo, social)\n";

    /// <summary>
    /// Адрес ресурсов оформления приложения
    /// </summary>
    private const string THEME = "pack://application:,,,/Steward;component/Themes/Controls.xaml";

    /// <summary>
    /// Выполняет команду и возвращает код выхода
    /// </summary>
    /// <param name="args">Команда и её параметры</param>
    /// <returns>0 при успехе, 1 если команда не распознана</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        var check = args.Length == 1 && args[0] is "check-tray" or "measure-tray";
        if (!check && (args.Length < 2 || !string.Equals(args[0], "snapshot", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine(USAGE);

            return 1;
        }

        // Своё приложение WPF с ресурсами оформления из сборки Steward: иконка в трее и опрос контейнера не запускаются
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(THEME) });
        app.Startup += (_, _) =>
        {
            if (check)
                Console.WriteLine(args[0] == "measure-tray" ? TrayCheck.Measure() : TrayCheck.Run());
            else
                SnapshotRenderer.Render(args[1], args.Length > 2 ? args[2] : "main");

            app.Shutdown();
        };

        return app.Run();
    }
}
