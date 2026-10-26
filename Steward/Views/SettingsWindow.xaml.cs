using System.Windows;
using System.Windows.Input;
using Steward.ViewModels;

namespace Steward.Views;

/// <summary>
/// Окно настроек: параметры синхронизации расписания, перезапуск сервиса и форматы домашних заданий. Статусов задач здесь нет
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>
    /// Создаёт окно и подключает модель представления
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();
        Model = new SettingsViewModel();
        DataContext = Model;
    }

    /// <summary>
    /// Модель, к которой привязана разметка
    /// </summary>
    public SettingsViewModel Model { get; }

    /// <summary>
    /// Показывает итог перезапуска над полями настроек
    /// </summary>
    /// <param name="text">Короткий итог</param>
    public void SetStatus(string text) => Model.SettingsMessage = text;

    /// <summary>
    /// Даёт перетащить окно за шапку
    /// </summary>
    /// <param name="sender">Шапка</param>
    /// <param name="e">Аргументы нажатия</param>
    private void Header_OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.Button)
            DragMove();
    }

    /// <summary>
    /// Закрывает окно
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Аргументы нажатия</param>
    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Закрывает окно по Escape
    /// </summary>
    /// <param name="e">Аргументы клавиши</param>
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }
}