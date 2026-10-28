using System.ComponentModel;
using System.Windows.Input;
using System.Windows;
using Steward.Homework.Models;
using Steward.Ideas.Models;
using Steward.Monitoring.Models;
using Steward.Status.Rows;
using Steward.Status.Running;
using Steward.ViewModels;

namespace Steward.Views;

/// <summary>
/// Главное окно приложения: статус задач, идеи, домашние задания и журнал. Закрытие прячет его в трей
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Приложение завершается, окно можно закрыть по-настоящему
    /// </summary>
    /// <summary>
    /// Какую долю рабочей области экрана занимает окно по умолчанию
    /// </summary>
    private const double WINDOW_SHARE = 0.9;

    private bool _closing;

    /// <summary>
    /// Создаёт окно и подключает модель представления
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Steward.Shell.WindowCorners.Round(this);
        Steward.Shell.WindowDragging.Attach(this, TitleBar);
        Model = new MainViewModel();
        DataContext = Model;
        StateChanged += (_, _) => UpdateMaximizeButton();
        Fit();
    }

    /// <summary>
    /// Модель, к которой привязана разметка
    /// </summary>
    public MainViewModel Model { get; }

    /// <summary>
    /// Показывает свежие данные
    /// </summary>
    /// <param name="snapshot">Состояние контейнера расписания</param>
    /// <param name="jobs">Строки задач из реестра</param>
    /// <param name="records">Последние запуски</param>
    /// <param name="ideas">Идеи, которые ждут решения</param>
    /// <param name="registryErrors">Ошибки чтения реестра</param>
    public void Apply(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs, IReadOnlyList<RunRecord> records, IReadOnlyList<string> registryErrors, IReadOnlyList<Idea> ideas) =>
        Model.Apply(snapshot, jobs, records, registryErrors, ideas);

    /// <summary>
    /// Показывает найденные домашние задания
    /// </summary>
    /// <param name="rows">Задания с занятиями от свежих к старым</param>
    public void ApplyHomework(IReadOnlyList<HomeworkRow> rows) => Model.ApplyHomework(rows);

    /// <summary>
    /// Показывает окно и выводит его на передний план
    /// </summary>
    public void ShowAndActivate()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        if (!IsVisible && WindowState == WindowState.Normal)
            Fit();

        Show();
        Activate();
    }

    /// <summary>
    /// Делает окно размером с 90% рабочей области экрана и ставит его по центру. Для развёрнутого окна ничего не меняет
    /// </summary>
    private void Fit()
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, area.Width * WINDOW_SHARE);
        Height = Math.Max(MinHeight, area.Height * WINDOW_SHARE);
        Left = area.Left + ((area.Width - Width) / 2);
        Top = area.Top + ((area.Height - Height) / 2);
    }

    /// <summary>
    /// Меняет значок кнопки: квадрат у обычного окна и два квадрата у развёрнутого
    /// </summary>
    private void UpdateMaximizeButton()
    {
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Вернуть размер" : "Развернуть";
    }

    /// <summary>
    /// Разворачивает окно на всю рабочую область или возвращает прежний размер
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Данные события</param>
    private void Maximize_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>
    /// Разрешает окну закрыться по-настоящему
    /// </summary>
    public void CloseForExit()
    {
        _closing = true;
        Close();
    }

    /// <summary>
    /// Вместо закрытия прячет окно, пока приложение не завершается
    /// </summary>
    /// <param name="e">Аргументы закрытия</param>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Сворачивает окно
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Аргументы нажатия</param>
    private void Minimize_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    /// <summary>
    /// Прячет окно в трей
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Аргументы нажатия</param>
    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
