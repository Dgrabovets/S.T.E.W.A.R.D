using System.Windows;
using System.Windows.Threading;
using Steward.Models;
using Steward.Monitoring.Models;
using Steward.Status.Rows;
using Steward.ViewModels;

namespace Steward.Views;

/// <summary>
/// Меню у иконки в трее: быстрый статус задач и кнопки из капель
/// </summary>
public partial class TrayMenuWindow : TrayPopupWindow
{
    /// <summary>
    /// Создаёт окно и подключает модель представления
    /// </summary>
    public TrayMenuWindow()
    {
        InitializeComponent();
        Model = new TrayMenuViewModel();
        DataContext = Model;
        Model.ExitRequested += (_, _) => Hide();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
                Cluster.IsOpen = false;
        };
    }

    /// <summary>
    /// Модель, к которой привязана разметка
    /// </summary>
    public TrayMenuViewModel Model { get; }

    /// <summary>
    /// Задаёт кнопки из капель. Окно прячется перед тем, как выполнить действие кнопки
    /// </summary>
    /// <param name="items">Кнопки от ближней к главной капле к дальней</param>
    public void SetItems(IReadOnlyList<GooeyItem> items) =>
        Cluster.Items = [.. items.Select(item => item with { Activate = () => Activate(item) })];

    /// <summary>
    /// Показывает свежие данные
    /// </summary>
    /// <param name="snapshot">Состояние контейнера расписания</param>
    /// <param name="jobs">Строки задач из реестра</param>
    public void Apply(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs) => Model.Apply(snapshot, jobs);

    /// <summary>
    /// Раскрывает капли и отдаёт им клавиатуру
    /// </summary>
    /// <param name="animate">
    /// <see langword="false"/>, чтобы кнопки встали на места сразу, без движения
    /// </param>
    public void Expand(bool animate = true)
    {
        Cluster.IsOpen = true;
        if (!animate)
            Cluster.SnapToState();

        Cluster.Focus();
    }

    /// <summary>
    /// Раскрывает капли сразу после появления окна
    /// </summary>
    protected override void OnOpened() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Expand());

    /// <summary>
    /// Прячет окно и выполняет действие кнопки
    /// </summary>
    /// <param name="item">Нажатая кнопка</param>
    private void Activate(GooeyItem item)
    {
        Hide();
        item.Activate();
    }
}