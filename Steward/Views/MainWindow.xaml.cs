using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Steward.Status;
using Steward.Monitoring.Models;
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
    private bool _closing;

    /// <summary>
    /// Создаёт окно и подключает модель представления
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Model = new MainViewModel();
        DataContext = Model;
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
    /// <param name="registryErrors">Ошибки чтения реестра</param>
    public void Apply(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs, IReadOnlyList<RunRecord> records, IReadOnlyList<string> registryErrors) =>
        Model.Apply(snapshot, jobs, records, registryErrors);

    /// <summary>
    /// Показывает окно и выводит его на передний план
    /// </summary>
    public void ShowAndActivate()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Show();
        Activate();
    }

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
    /// Даёт перетащить окно за заголовок
    /// </summary>
    /// <param name="sender">Заголовок</param>
    /// <param name="e">Аргументы нажатия</param>
    private void TitleBar_OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
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
