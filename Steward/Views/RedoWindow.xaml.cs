using System.Windows;
using System.Windows.Input;
using Steward.Homework.Models;
using Steward.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Steward.Views;

/// <summary>
/// Окно «Переделать задание»: короткое поле, куда пишут, что не понравилось, вид замечания, переключатель «Для всех предметов» и кнопка, которой прикладывают файлы с замечаниями преподавателя. Открывается поверх окна приложения и по его центру
/// </summary>
public partial class RedoWindow : Window
{
    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="heading">Занятие, работа по которому переделывается</param>
    public RedoWindow(string heading)
    {
        InitializeComponent();
        Heading.Text = heading;
        Kind.ItemsSource = KindOptions;
        Kind.SelectedIndex = 0;
        Loaded += (_, _) => Remarks.Focus();
    }

    /// <summary>
    /// Вид замечания и его название в списке
    /// </summary>
    /// <param name="Value">Вид</param>
    /// <param name="Title">Название</param>
    public sealed record KindOption(RemarkKindEnum Value, string Title);

    /// <summary>
    /// Что ответили в окне
    /// </summary>
    /// <param name="Text">Что не понравилось</param>
    /// <param name="Files">Файлы с замечаниями преподавателя</param>
    /// <param name="Kind">Вид замечания</param>
    /// <param name="Global">Замечание для всех предметов</param>
    public sealed record Answer(string Text, IReadOnlyList<string> Files, RemarkKindEnum Kind, bool Global);

    /// <summary>
    /// Виды замечаний в списке: первым «общее», оно выбрано по умолчанию
    /// </summary>
    private static readonly IReadOnlyList<KindOption> KindOptions = [.. new[] { RemarkKindEnum.General, RemarkKindEnum.Condition, RemarkKindEnum.Format, RemarkKindEnum.Content, RemarkKindEnum.Style }.Select(kind => new KindOption(kind, RemarkViewModel.KindName(kind)))];

    /// <summary>
    /// Прикреплённые файлы с замечаниями
    /// </summary>
    private readonly List<string> _files = [];

    /// <summary>
    /// Что написал пользователь
    /// </summary>
    public string Text => Remarks.Text.Trim();

    /// <summary>
    /// Открывает окно по центру окна приложения и ждёт ответа
    /// </summary>
    /// <param name="owner">Окно приложения</param>
    /// <param name="heading">Занятие, работа по которому переделывается</param>
    /// <returns>Замечания, файлы, вид и область или <see langword="null"/>, если окно закрыли без переделки</returns>
    public static Answer? Ask(Window? owner, string heading)
    {
        var window = new RedoWindow(heading);
        if (owner is { IsVisible: true })
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true
            ? new Answer(window.Text, window._files, (window.Kind.SelectedItem as KindOption)?.Value ?? RemarkKindEnum.General, window.ForAll.IsChecked == true)
            : null;
    }

    /// <summary>
    /// Выбирает файлы с замечаниями преподавателя
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Attach_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Файлы с замечаниями преподавателя",
            Multiselect = true,
            Filter = "Замечания|*.docx;*.pptx;*.pdf;*.txt;*.md;*.png;*.jpg;*.jpeg|Все файлы|*.*",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        _files.Clear();
        _files.AddRange(dialog.FileNames);
        Attached.Text = string.Join(", ", _files.Select(System.IO.Path.GetFileName));
    }

    /// <summary>
    /// Подтверждает: без замечаний переделывать нечего
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Confirm_OnClick(object sender, RoutedEventArgs e)
    {
        if (Text.Length == 0 && _files.Count == 0)
        {
            Remarks.Focus();

            return;
        }

        DialogResult = true;
    }

    /// <summary>
    /// Закрывает окно
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>
    /// Переносит окно за шапку
    /// </summary>
    /// <param name="sender">Шапка</param>
    /// <param name="e">Нажатие мыши</param>
    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button)
            DragMove();
    }

    /// <summary>
    /// Закрывает окно по Esc
    /// </summary>
    /// <param name="sender">Окно</param>
    /// <param name="e">Нажатие клавиши</param>
    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            DialogResult = false;
    }
}
