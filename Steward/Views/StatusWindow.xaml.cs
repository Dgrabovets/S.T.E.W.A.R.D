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
/// Окно истории сверок расписания: что менялось в таблице и когда. Рисует то, что отдаёт модель представления
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
    }

    /// <summary>
    /// Модель, к которой привязана разметка
    /// </summary>
    public StatusViewModel Model { get; }

    /// <summary>
    /// Показывает свежий снимок
    /// </summary>
    /// <param name="snapshot">Текущее состояние синхронизации</param>
    /// <param name="scrollToProblem">
    /// <see langword="true"/>, если журнал нужно открыть сверху
    /// </param>
    public void Apply(SyncSnapshot snapshot, bool scrollToProblem)
    {
        Model.Apply(snapshot);
        if (scrollToProblem)
            JournalScroll.ScrollToTop();
    }

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
