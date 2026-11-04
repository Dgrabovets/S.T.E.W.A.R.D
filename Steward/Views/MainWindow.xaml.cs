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
    /// Какую долю рабочей области экрана занимает окно по умолчанию
    /// </summary>
    private const double WINDOW_SHARE = 0.9;

    /// <summary>
    /// Наименьший размер окна из разметки. На экране меньше него окно получает наименьший размер по рабочей области, иначе низ обрезался бы
    /// </summary>
    private readonly System.Windows.Size _designedMinimum;

    /// <summary>
    /// Скругление рамки обычного окна: у развёрнутого его нет
    /// </summary>
    private readonly CornerRadius _frameRadius;

    /// <summary>
    /// Толщина рамки обычного окна: у развёрнутого её нет
    /// </summary>
    private readonly Thickness _frameBorder;

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
        _designedMinimum = new System.Windows.Size(MinWidth, MinHeight);
        _frameRadius = Frame.CornerRadius;
        _frameBorder = Frame.BorderThickness;
        Steward.Shell.WindowMaximizer.Attach(this);
        SourceInitialized += (_, _) =>
        {
            Steward.Shell.WindowCorners.Round(this);
            FitMinimum();
        };
        Steward.Shell.WindowDragging.Attach(this, TitleBar);
        Model = new MainViewModel();
        DataContext = Model;
        StateChanged += (_, _) => ApplyWindowState();
        SizeChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
                ApplyFrame(true, Steward.Shell.WindowMaximizer.Insets(this));
        };
        LocationChanged += (_, _) => FitMinimum();
        DpiChanged += (_, _) => FitMinimum();
        Fit();
    }

    /// <summary>
    /// Рисует рамку окна для его состояния. У развёрнутого окна нет скругления и линии по краю, а содержимое отступает на столько, на сколько окно вылезло за рабочую область (обычно ноль). Снимки DevTools вызывают его, чтобы показать развёрнутое окно
    /// </summary>
    /// <param name="maximized">Окно развёрнуто</param>
    /// <param name="insets">На сколько окно вылезло за рабочую область с каждой стороны</param>
    internal void ApplyFrame(bool maximized, Thickness insets)
    {
        Frame.Margin = maximized ? insets : new Thickness(0);
        Frame.CornerRadius = maximized ? new CornerRadius(0) : _frameRadius;
        Frame.BorderThickness = maximized ? new Thickness(0) : _frameBorder;
    }

    /// <summary>
    /// Обновляет кнопку «Развернуть» и рамку после смены состояния окна
    /// </summary>
    private void ApplyWindowState()
    {
        UpdateMaximizeButton();
        ApplyFrame(WindowState == WindowState.Maximized, WindowState == WindowState.Maximized ? Steward.Shell.WindowMaximizer.Insets(this) : new Thickness(0));
    }

    /// <summary>
    /// Рабочая область, которую снимки DevTools подставляют вместо настоящего монитора, чтобы нарисовать окно на экране другого размера. <see langword="null"/>: берётся монитор, на котором стоит окно
    /// </summary>
    internal Rect? WorkAreaOverride { get; set; }

    /// <summary>
    /// Рабочая область монитора окна в единицах WPF
    /// </summary>
    /// <returns>Подставленная для снимка область или настоящая</returns>
    private Rect CurrentWorkArea() => WorkAreaOverride ?? Steward.Shell.WindowMaximizer.WorkArea(this);

    /// <summary>
    /// Ограничивает наименьший размер окна рабочей областью монитора, на котором оно стоит
    /// </summary>
    private void FitMinimum()
    {
        var minimum = Steward.Shell.WindowFit.MinimumSize(_designedMinimum, CurrentWorkArea().Size);
        MinWidth = minimum.Width;
        MinHeight = minimum.Height;
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
    /// Делает окно размером с 90% рабочей области монитора и ставит его по центру. Окно не бывает больше рабочей области, даже если наименьший размер из разметки больше экрана
    /// </summary>
    private void Fit()
    {
        FitMinimum();
        var bounds = Steward.Shell.WindowFit.Fitted(CurrentWorkArea(), _designedMinimum, WINDOW_SHARE);
        Width = bounds.Width;
        Height = bounds.Height;
        Left = bounds.Left;
        Top = bounds.Top;
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
