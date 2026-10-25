using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows;
using Steward.Monitoring.Models;
using Steward.Status.Rows;
using Steward.ViewModels;

namespace Steward.Views;

/// <summary>
/// Окно панели. Рисует то, что отдаёт модель представления, и само только открывается и прячется
/// </summary>
public partial class StatusWindow : TrayPopupWindow
{
    /// <summary>
    /// Создаёт окно и подключает модель представления
    /// </summary>
    public StatusWindow()
    {
        InitializeComponent();
        Model = new StatusViewModel();
        DataContext = Model;
        Model.PropertyChanged += OnModelChanged;
        Model.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        Model.RestartRequested += (_, _) => RestartRequested?.Invoke(this, EventArgs.Empty);
        Model.OpenAppRequested += (_, _) => OpenAppRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Модель, к которой привязана разметка
    /// </summary>
    public StatusViewModel Model { get; }

    /// <summary>
    /// Просит завершить процесс
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Просит пересоздать контейнер
    /// </summary>
    public event EventHandler? RestartRequested;

    /// <summary>
    /// Просит открыть главное окно приложения
    /// </summary>
    public event EventHandler? OpenAppRequested;

    /// <summary>
    /// Показывает свежий снимок
    /// </summary>
    /// <param name="snapshot">Текущее состояние синхронизации</param>
    /// <param name="jobs">Строки задач из реестра</param>
    /// <param name="scrollToProblem">
    /// <see langword="true"/>, если журнал нужно открыть сверху
    /// </param>
    public void Apply(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs, bool scrollToProblem)
    {
        Model.Apply(snapshot, jobs);
        if (scrollToProblem)
            JournalScroll.ScrollToTop();
    }

    /// <summary>
    /// Пишет итог перезапуска
    /// </summary>
    /// <param name="text">Короткая фраза</param>
    public void SetSettingsStatus(string text) => Model.SettingsMessage = text;

    #region Dragging

    /// <summary>
    /// Даёт перетащить панель за шапку
    /// </summary>
    /// <param name="sender">Шапка</param>
    /// <param name="e">Аргументы нажатия</param>
    private void Header_OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    #endregion Dragging

    #region Glow

    /// <summary>
    /// Включает или гасит свечение, когда контейнер начинает или перестаёт работать
    /// </summary>
    /// <param name="sender">Модель представления</param>
    /// <param name="e">Имя изменившегося свойства</param>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (null or nameof(StatusViewModel.IsRunning)))
            return;

        var pulse = (Storyboard)FindResource("GlowPulse");
        if (Model.IsRunning)
        {
            pulse.Begin(this, true);
            return;
        }

        pulse.Stop(this);
        Glow.Opacity = 0.35;
    }

    #endregion Glow


}
