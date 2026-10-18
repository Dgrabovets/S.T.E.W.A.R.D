using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using UltimateParser.Tray.Monitoring.Models;
using UltimateParser.Tray.ViewModels;

namespace UltimateParser.Tray.Views;

/// <summary>
/// Окно панели. Рисует то, что отдаёт модель представления, и само только открывается и прячется
/// </summary>
public partial class StatusWindow : Window
{
    /// <summary>
    /// Отступ панели от края рабочей области, в аппаратно-независимых пикселях
    /// </summary>
    private const double SCREEN_MARGIN = 8;

    /// <summary>
    /// Сдвиг панели влево от курсора, чтобы она открывалась над иконкой, а не поверх неё
    /// </summary>
    private const double CURSOR_OFFSET_X = 28;

    /// <summary>
    /// Сдвиг панели вверх от курсора
    /// </summary>
    private const double CURSOR_OFFSET_Y = 8;

    /// <summary>
    /// Щелчок по иконке ещё не закончился, повторно открывать и закрывать панель нельзя
    /// </summary>
    private bool _ignoreToggle;

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
        Deactivated += OnDeactivated;
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
    /// Панель спряталась
    /// </summary>
    public event EventHandler? Dismissed;

    /// <summary>
    /// Просит пересоздать контейнер
    /// </summary>
    public event EventHandler? RestartRequested;

    /// <summary>
    /// Показывает свежий снимок
    /// </summary>
    /// <param name="snapshot">
    /// Текущее состояние синхронизации
    /// </param>
    /// <param name="scrollToProblem">
    /// <see langword="true"/>, если журнал нужно открыть сверху
    /// </param>
    public void Apply(SyncSnapshot snapshot, bool scrollToProblem)
    {
        Model.Apply(snapshot);
        if (scrollToProblem)
            JournalScroll.ScrollToTop();
    }

    /// <summary>
    /// Пишет итог перезапуска
    /// </summary>
    /// <param name="text">
    /// Короткая фраза
    /// </param>
    public void SetSettingsStatus(string text) => Model.SettingsMessage = text;

    #region Opening

    /// <summary>
    /// Открывает панель по щелчку на иконку или прячет её, если она уже видна
    /// </summary>
    public void ToggleFromIcon()
    {
        if (_ignoreToggle)
            return;

        if (IsVisible)
        {
            Hide();
            return;
        }

        Open();
    }

    /// <summary>
    /// Прячет панель по Escape
    /// </summary>
    /// <param name="e">
    /// Аргументы клавиши
    /// </param>
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            Dismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Показывает панель сразу в нужной точке, без кадра в чужом месте и без ухода в прозрачность
    /// </summary>
    private void Open()
    {
        _ignoreToggle = true;
        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursor);
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        Left = screen.WorkingArea.Left;
        Top = screen.WorkingArea.Top;
        Show();
        UpdateLayout();
        PlaceNearCursor(cursor, screen);
        Opacity = 1;
        Activate();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, ReleaseToggle);
    }

    /// <summary>
    /// Прячет панель, когда фокус ушёл. Щелчок, который её открыл, сюда не попадает
    /// </summary>
    /// <param name="sender">
    /// Окно
    /// </param>
    /// <param name="e">
    /// Аргументы события
    /// </param>
    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_ignoreToggle || !IsVisible)
            return;

        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
        _ignoreToggle = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, ReleaseToggle);
    }

    /// <summary>
    /// Снова разрешает открытие и закрытие, когда щелчок по иконке уже обработан
    /// </summary>
    private void ReleaseToggle() => _ignoreToggle = false;

    /// <summary>
    /// Даёт перетащить панель за шапку
    /// </summary>
    /// <param name="sender">
    /// Шапка
    /// </param>
    /// <param name="e">
    /// Аргументы нажатия
    /// </param>
    private void Header_OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    #endregion Opening

    #region Glow

    /// <summary>
    /// Включает или гасит свечение, когда контейнер начинает или перестаёт работать
    /// </summary>
    /// <param name="sender">
    /// Модель представления
    /// </param>
    /// <param name="e">
    /// Имя изменившегося свойства
    /// </param>
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

    #region Placement

    /// <summary>
    /// Ставит окно над курсором и не выпускает его за край экрана
    /// </summary>
    /// <param name="cursor">
    /// Точка курсора в физических пикселях
    /// </param>
    /// <param name="screen">
    /// Экран, на котором лежит курсор
    /// </param>
    private void PlaceNearCursor(System.Drawing.Point cursor, System.Windows.Forms.Screen screen)
    {
        var area = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var toDip = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var origin = toDip.Transform(new System.Windows.Point(cursor.X, cursor.Y));
        var topLeft = toDip.Transform(new System.Windows.Point(area.Left, area.Top));
        var bottomRight = toDip.Transform(new System.Windows.Point(area.Right, area.Bottom));
        var x = Math.Clamp(origin.X - Width + CURSOR_OFFSET_X, topLeft.X + SCREEN_MARGIN, Math.Max(topLeft.X + SCREEN_MARGIN, bottomRight.X - Width - SCREEN_MARGIN));
        var y = origin.Y - Height - CURSOR_OFFSET_Y;
        if (y < topLeft.Y + SCREEN_MARGIN)
            y = Math.Min(origin.Y + CURSOR_OFFSET_Y, Math.Max(topLeft.Y + SCREEN_MARGIN, bottomRight.Y - Height - SCREEN_MARGIN));

        Left = x;
        Top = y;
    }

    #endregion Placement
}
