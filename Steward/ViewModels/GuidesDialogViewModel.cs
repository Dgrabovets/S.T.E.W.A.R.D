using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Окно «Методические указания»: две закладки, «Указания» и «Образцы», у каждого файла галочка «учитывать», значок открытия и значок удаления
/// </summary>
public sealed class GuidesDialogViewModel : Bindable
{
    /// <summary>
    /// Читает материалы предмета
    /// </summary>
    private readonly Func<string, IReadOnlyList<GuideEntry>> _lister;

    /// <summary>
    /// Папка материалов приложения
    /// </summary>
    private readonly string _folder;

    /// <summary>
    /// Открывает файл или папку
    /// </summary>
    private readonly Action<string> _open;

    /// <summary>
    /// Убирает материал
    /// </summary>
    private readonly Action<string> _remove;

    /// <summary>
    /// Сохраняет галочку «учитывать»
    /// </summary>
    private readonly Action<string, bool> _enabled;

    /// <summary>
    /// Копирует файлы в материалы предмета
    /// </summary>
    private readonly Func<string, GuideKindEnum, IReadOnlyList<string>, string> _addFiles;

    /// <summary>
    /// Запоминает папку
    /// </summary>
    private readonly Func<string, GuideKindEnum, string, string> _addFolder;

    /// <summary>
    /// Запоминает ссылку в интернете и скачивает файл
    /// </summary>
    private readonly Func<string, GuideKindEnum, string, Task<string>> _addLink;

    /// <summary>
    /// Открыта закладка «Образцы»
    /// </summary>
    private bool _examplesTab;

    /// <summary>
    /// Методические указания
    /// </summary>
    private IReadOnlyList<GuideViewModel> _guides = [];

    /// <summary>
    /// Образцы
    /// </summary>
    private IReadOnlyList<GuideViewModel> _examples = [];

    /// <summary>
    /// Что случилось при добавлении
    /// </summary>
    private string _message = string.Empty;

    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="subject">Название предмета. Материалы общие для всех предметов, поэтому здесь пусто</param>
    /// <param name="lister">Читает материалы предмета</param>
    /// <param name="folder">Папка материалов приложения</param>
    /// <param name="open">Открывает файл или папку</param>
    /// <param name="remove">Убирает материал</param>
    /// <param name="enabled">Сохраняет галочку «учитывать»</param>
    /// <param name="addFiles">Копирует файлы и возвращает текст ошибки</param>
    /// <param name="addFolder">Запоминает папку и возвращает текст ошибки</param>
    /// <param name="addLink">Запоминает ссылку в интернете, скачивает файл и возвращает пояснение</param>
    public GuidesDialogViewModel(
        string subject,
        Func<string, IReadOnlyList<GuideEntry>> lister,
        string folder,
        Action<string> open,
        Action<string> remove,
        Action<string, bool> enabled,
        Func<string, GuideKindEnum, IReadOnlyList<string>, string> addFiles,
        Func<string, GuideKindEnum, string, string> addFolder,
        Func<string, GuideKindEnum, string, Task<string>> addLink)
    {
        Subject = subject;
        _lister = lister;
        _folder = folder;
        _open = open;
        _remove = remove;
        _enabled = enabled;
        _addFiles = addFiles;
        _addFolder = addFolder;
        _addLink = addLink;
        ShowGuidesCommand = new RelayCommand(() => ExamplesTab = false);
        ShowExamplesCommand = new RelayCommand(() => ExamplesTab = true);
        Refresh();
    }

    /// <summary>
    /// Название предмета
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Открыта закладка «Образцы»
    /// </summary>
    public bool ExamplesTab
    {
        get => _examplesTab;
        private set
        {
            if (!Set(ref _examplesTab, value))
                return;

            Raise(nameof(IsGuidesTab));
            Raise(nameof(IsExamplesTab));
            Raise(nameof(CurrentKind));
        }
    }

    /// <summary>
    /// Открыта закладка «Указания»
    /// </summary>
    public bool IsGuidesTab => !_examplesTab;

    /// <summary>
    /// Открыта закладка «Образцы»
    /// </summary>
    public bool IsExamplesTab => _examplesTab;

    /// <summary>
    /// Что добавляется кнопками внизу: то, что открыто на закладке
    /// </summary>
    public GuideKindEnum CurrentKind => _examplesTab ? GuideKindEnum.Example : GuideKindEnum.Guide;

    /// <summary>
    /// Методические указания
    /// </summary>
    public IReadOnlyList<GuideViewModel> Guides
    {
        get => _guides;
        private set
        {
            if (Set(ref _guides, value))
            {
                Raise(nameof(GuidesTitle));
                Raise(nameof(GuidesEmpty));
            }
        }
    }

    /// <summary>
    /// Образцы
    /// </summary>
    public IReadOnlyList<GuideViewModel> Examples
    {
        get => _examples;
        private set
        {
            if (Set(ref _examples, value))
            {
                Raise(nameof(ExamplesTitle));
                Raise(nameof(ExamplesEmpty));
            }
        }
    }

    /// <summary>
    /// Подпись закладки «Указания» с числом файлов
    /// </summary>
    public string GuidesTitle => $"Указания · {Guides.Count}";

    /// <summary>
    /// Подпись закладки «Образцы» с числом файлов
    /// </summary>
    public string ExamplesTitle => $"Образцы · {Examples.Count}";

    /// <summary>
    /// На закладке «Указания» пока пусто
    /// </summary>
    public bool GuidesEmpty => Guides.Count == 0;

    /// <summary>
    /// На закладке «Образцы» пока пусто
    /// </summary>
    public bool ExamplesEmpty => Examples.Count == 0;

    /// <summary>
    /// Что случилось при добавлении
    /// </summary>
    public string Message
    {
        get => _message;
        private set
        {
            if (Set(ref _message, value))
                Raise(nameof(HasMessage));
        }
    }

    /// <summary>
    /// Есть сообщение
    /// </summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>
    /// Открывает закладку «Указания»
    /// </summary>
    public RelayCommand ShowGuidesCommand { get; }

    /// <summary>
    /// Открывает закладку «Образцы»
    /// </summary>
    public RelayCommand ShowExamplesCommand { get; }

    /// <summary>
    /// Добавляет выбранные файлы на открытую закладку
    /// </summary>
    /// <param name="paths">Файлы</param>
    public void AddFiles(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return;

        Message = _addFiles(Subject, CurrentKind, paths);
        Refresh();
    }

    /// <summary>
    /// Добавляет выбранную папку на открытую закладку
    /// </summary>
    /// <param name="path">Папка</param>
    public void AddFolder(string path)
    {
        if (path.Length == 0)
            return;

        Message = _addFolder(Subject, CurrentKind, path);
        Refresh();
    }

    /// <summary>
    /// Добавляет на открытую закладку ссылку на файл в интернете, которую взяли из буфера обмена
    /// </summary>
    /// <param name="text">Текст из буфера</param>
    /// <returns>Задача, которая завершается, когда файл скачан или ссылка отклонена</returns>
    public async Task AddLinkAsync(string text)
    {
        var link = text.Trim();
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            Message = "В буфере обмена нет ссылки. Скопируй адрес файла в браузере и нажми «Ссылка» ещё раз.";

            return;
        }

        Message = "Скачиваю…";
        Message = await _addLink(Subject, CurrentKind, link);
        Refresh();
    }

    /// <summary>
    /// Перечитывает материалы предмета
    /// </summary>
    public void Refresh()
    {
        var entries = _lister(Subject);
        GuideViewModel Row(GuideEntry entry) => new(entry, _folder, _open, row =>
        {
            _remove(row.Id);
            Refresh();
        }, _enabled);

        Guides = [.. entries.Where(entry => entry.Kind == GuideKindEnum.Guide).Select(Row)];
        Examples = [.. entries.Where(entry => entry.Kind == GuideKindEnum.Example).Select(Row)];
    }
}
