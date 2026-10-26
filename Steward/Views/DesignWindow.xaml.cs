using System.Windows;
using Steward.Models;

namespace Steward.Views;

/// <summary>
/// Страница-образец дизайн-системы: цвета, шкала текста, кнопки, поля и плашки в одном окне. Открывается только ключом <c>--snapshot</c>
/// </summary>
public partial class DesignWindow : Window
{
    /// <summary>
    /// Создаёт окно и заполняет образцы
    /// </summary>
    public DesignWindow()
    {
        InitializeComponent();
        DataContext = this;
        Cluster.Items =
        [
            new GooeyItem("\uE80F", "Статусы", () => { }),
            new GooeyItem("\uEA80", "Идеи", () => { }),
            new GooeyItem("\uE7BE", "Домашние задания", () => { }),
            new GooeyItem("\uE81C", "Журнал", () => { }),
            new GooeyItem("\uE713", "Настройки", () => { }, DividerBefore: true),
            new GooeyItem("\uE7E8", "Выход", () => { }),
        ];
        Cluster.IsOpen = true;
        Loaded += (_, _) => Cluster.SnapToState();
    }

    /// <summary>
    /// Цвета по группам: поверхности, линии, текст, акцент, состояния
    /// </summary>
    public IReadOnlyList<ColorSwatch> Surfaces { get; } = Swatches("Window", "Surface", "Raised", "Line", "LineStrong");

    /// <summary>
    /// Текст и акцент
    /// </summary>
    public IReadOnlyList<ColorSwatch> TextAndAccent { get; } = Swatches("Text", "Muted", "Accent", "AccentHover", "AccentPressed", "AccentText");

    /// <summary>
    /// Состояния задач
    /// </summary>
    public IReadOnlyList<ColorSwatch> States { get; } = Swatches("Ok", "OkSurface", "Attention", "AttentionSurface", "Danger", "DangerSurface", "Idle");

    /// <summary>
    /// Плашки всех состояний
    /// </summary>
    public IReadOnlyList<ToneSample> Tones { get; } =
    [
        new(PanelToneEnum.Idle, "Не запущено"),
        new(PanelToneEnum.Running, "Работает"),
        new(PanelToneEnum.Attention, "Ждёт тебя"),
        new(PanelToneEnum.Problem, "Сбой"),
    ];

    /// <summary>
    /// Собирает образцы по именам
    /// </summary>
    /// <param name="names">Имена цветов без суффиксов</param>
    /// <returns>Образцы в том же порядке</returns>
    private static ColorSwatch[] Swatches(params string[] names) => [.. names.Select(name => new ColorSwatch(name))];
}