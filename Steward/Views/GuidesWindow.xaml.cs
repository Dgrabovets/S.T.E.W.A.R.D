using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Steward.ViewModels;

namespace Steward.Views;

/// <summary>
/// Окно «Методические указания»: файлы и папки с требованиями и образцами предмета. Открывается поверх окна приложения и по его центру
/// </summary>
public partial class GuidesWindow : Window
{
    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="model">Материалы предмета</param>
    public GuidesWindow(GuidesDialogViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    /// <summary>
    /// Открывает окно по центру окна приложения и ждёт, пока его закроют
    /// </summary>
    /// <param name="owner">Окно приложения</param>
    /// <param name="model">Материалы предмета</param>
    public static void Show(Window? owner, GuidesDialogViewModel model)
    {
        var window = new GuidesWindow(model);
        if (owner is { IsVisible: true })
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        window.ShowDialog();
    }

    /// <summary>
    /// Выбирает в проводнике файлы или папку одним окном: выбранные файлы добавляются как есть, а если открыть папку и нажать «Открыть», добавляется вся папка
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        const string PLACEHOLDER = "Выбор этой папки";
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = false,
            ValidateNames = false,
            FileName = PLACEHOLDER,
            Title = "Выбери файлы или открой папку и нажми «Открыть»",
            Filter = "Документы и текст|*.docx;*.pptx;*.pdf;*.md;*.txt;*.rtf|Все файлы|*.*",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var model = (GuidesDialogViewModel)DataContext;
        model.AddFiles([.. dialog.FileNames.Where(System.IO.File.Exists)]);
        foreach (var name in dialog.FileNames.Where(name => !System.IO.File.Exists(name) && System.IO.Path.GetFileName(name) == PLACEHOLDER))
        {
            var folder = System.IO.Path.GetDirectoryName(name);
            if (folder is not null && System.IO.Directory.Exists(folder))
                model.AddFolder(folder);
        }
    }

    /// <summary>
    /// Берёт ссылку на файл из буфера обмена
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private async void Link_OnClick(object sender, RoutedEventArgs e) => await PasteLinkAsync();

    /// <summary>
    /// Читает буфер обмена и передаёт текст модели
    /// </summary>
    /// <returns>Задача, которая завершается, когда ссылка обработана</returns>
    private async Task PasteLinkAsync()
    {
        var text = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty;
        await ((GuidesDialogViewModel)DataContext).AddLinkAsync(text);
    }

    /// <summary>
    /// Закрывает окно
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

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
    /// Закрывает окно по Esc. Ctrl+V вне поля ввода берёт ссылку из буфера обмена, кроме закладки «Замечания»: туда файлы не добавляются
    /// </summary>
    /// <param name="sender">Окно</param>
    /// <param name="e">Нажатие клавиши</param>
    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
        else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not System.Windows.Controls.TextBox && DataContext is GuidesDialogViewModel { CanAddFiles: true })
            _ = PasteLinkAsync();
    }
}
