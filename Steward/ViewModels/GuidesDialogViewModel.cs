using System.IO;
using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Окно «Методические указания»: закладки «Указания», «Образцы» и «Замечания». У каждого файла галочка «учитывать», значок открытия и значок удаления; у замечания правка текста, галочки «учитывать» и «для всех предметов» и удаление
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
    /// Как читать и менять замечания владельца. Пусто: закладки «Замечания» нет
    /// </summary>
    private readonly RemarkHooks? _remarkHooks;

    /// <summary>
    /// Открытая закладка
    /// </summary>
    private GuidesTabEnum _tab;

    /// <summary>
    /// Замечания владельца
    /// </summary>
    private IReadOnlyList<RemarkViewModel> _remarks = [];

    /// <summary>
    /// Замечания уже прочитаны хотя бы раз: до этого пустой список не называется пустым
    /// </summary>
    private bool _remarksLoaded;

    /// <summary>
    /// Номер последнего чтения замечаний: устаревший ответ не затирает свежий
    /// </summary>
    private int _remarksVersion;

    /// <summary>
    /// Идущие чтения и записи замечаний
    /// </summary>
    private readonly List<Task> _remarkTasks = [];

    /// <summary>
    /// Защищает список идущих задач: в тестах без потока окна задачи завершаются в пуле потоков
    /// </summary>
    private readonly object _remarkGate = new();

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
    /// <param name="remarks">Как читать и менять замечания владельца. Пусто: закладки «Замечания» нет</param>
    public GuidesDialogViewModel(
        string subject,
        Func<string, IReadOnlyList<GuideEntry>> lister,
        string folder,
        Action<string> open,
        Action<string> remove,
        Action<string, bool> enabled,
        Func<string, GuideKindEnum, IReadOnlyList<string>, string> addFiles,
        Func<string, GuideKindEnum, string, string> addFolder,
        Func<string, GuideKindEnum, string, Task<string>> addLink,
        RemarkHooks? remarks = null)
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
        _remarkHooks = remarks;
        ShowGuidesCommand = new RelayCommand(() => Tab = GuidesTabEnum.Guides);
        ShowExamplesCommand = new RelayCommand(() => Tab = GuidesTabEnum.Examples);
        ShowRemarksCommand = new RelayCommand(() => Tab = GuidesTabEnum.Remarks, () => HasRemarksTab);
        Refresh();
        if (remarks is not null)
            Track(LoadRemarksAsync());
    }

    /// <summary>
    /// Закладка окна
    /// </summary>
    private enum GuidesTabEnum
    {
        /// <summary>
        /// Методические указания
        /// </summary>
        Guides,

        /// <summary>
        /// Образцы
        /// </summary>
        Examples,

        /// <summary>
        /// Замечания владельца
        /// </summary>
        Remarks,
    }

    /// <summary>
    /// Название предмета
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Открытая закладка
    /// </summary>
    private GuidesTabEnum Tab
    {
        get => _tab;
        set
        {
            if (!Set(ref _tab, value))
                return;

            Raise(nameof(IsGuidesTab));
            Raise(nameof(IsExamplesTab));
            Raise(nameof(IsRemarksTab));
            Raise(nameof(CanAddFiles));
            Raise(nameof(CurrentKind));
        }
    }

    /// <summary>
    /// Открыта закладка «Указания»
    /// </summary>
    public bool IsGuidesTab => _tab == GuidesTabEnum.Guides;

    /// <summary>
    /// Открыта закладка «Образцы»
    /// </summary>
    public bool IsExamplesTab => _tab == GuidesTabEnum.Examples;

    /// <summary>
    /// Открыта закладка «Замечания»
    /// </summary>
    public bool IsRemarksTab => _tab == GuidesTabEnum.Remarks;

    /// <summary>
    /// Закладка «Замечания» подключена
    /// </summary>
    public bool HasRemarksTab => _remarkHooks is not null;

    /// <summary>
    /// Кнопки «Обзор…» и «Ссылка» работают: на закладке «Замечания» добавлять файлы некуда
    /// </summary>
    public bool CanAddFiles => !IsRemarksTab;

    /// <summary>
    /// Что добавляется кнопками внизу: то, что открыто на закладке
    /// </summary>
    public GuideKindEnum CurrentKind => _tab == GuidesTabEnum.Examples ? GuideKindEnum.Example : GuideKindEnum.Guide;

    /// <summary>
    /// Замечания владельца: новые выше
    /// </summary>
    public IReadOnlyList<RemarkViewModel> Remarks
    {
        get => _remarks;
        private set
        {
            if (Set(ref _remarks, value))
            {
                Raise(nameof(RemarksTitle));
                Raise(nameof(RemarksEmpty));
            }
        }
    }

    /// <summary>
    /// Подпись закладки «Замечания» с числом замечаний; пока замечания не прочитаны, без числа, чтобы не показывать ложный ноль
    /// </summary>
    public string RemarksTitle => _remarksLoaded ? $"Замечания · {Remarks.Count}" : "Замечания";

    /// <summary>
    /// Замечания прочитаны, и их нет
    /// </summary>
    public bool RemarksEmpty => _remarksLoaded && Remarks.Count == 0;

    /// <summary>
    /// Открывает закладку «Замечания»
    /// </summary>
    public RelayCommand ShowRemarksCommand { get; }

    /// <summary>
    /// Ждёт, пока закончатся начатые к этому моменту чтения и записи замечаний, в том числе начатые ими новые чтения
    /// </summary>
    /// <returns>Задача, которая завершается, когда всё применено; ошибок не выбрасывает</returns>
    public async Task WhenRemarksIdle()
    {
        while (true)
        {
            Task[] pending;
            lock (_remarkGate)
                pending = [.. _remarkTasks];

            if (pending.Length == 0)
                return;

            await Task.WhenAll(pending);
        }
    }

    /// <summary>
    /// Запоминает идущую задачу замечаний и убирает её из списка, когда она закончится
    /// </summary>
    /// <param name="task">Задача, которая сама ловит свои ошибки</param>
    private void Track(Task task)
    {
        lock (_remarkGate)
            _remarkTasks.Add(task);

        _ = task.ContinueWith(done =>
        {
            lock (_remarkGate)
                _remarkTasks.Remove(done);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>
    /// Перечитывает замечания в фоновой задаче и применяет список. Поправленный, но не сохранённый текст строк остаётся в полях
    /// </summary>
    /// <returns>Задача, которая завершается, когда список применён; ошибок не выбрасывает</returns>
    private async Task LoadRemarksAsync()
    {
        if (_remarkHooks is not { } hooks)
            return;

        var version = Interlocked.Increment(ref _remarksVersion);
        try
        {
            var loaded = await Task.Run(hooks.Load);
            if (version != Volatile.Read(ref _remarksVersion))
                return;

            var known = _remarks.ToDictionary(row => row.Id, StringComparer.Ordinal);
            var rows = new List<RemarkViewModel>();
            foreach (var remark in loaded.Reverse().OrderByDescending(remark => remark.Date))
            {
                if (known.TryGetValue(remark.Id, out var row))
                    row.Accept(remark);
                else
                    row = new RemarkViewModel(remark, (target, updated) => Track(ChangeRemarkAsync(target, updated)), target => Track(RemoveRemarkAsync(target)));

                rows.Add(row);
            }

            _remarksLoaded = true;
            Remarks = rows;
            Raise(nameof(RemarksEmpty));
            Raise(nameof(RemarksTitle));
        }
        catch (Exception exception)
        {
            Report("Не удалось прочитать замечания: ", exception);
        }
    }

    /// <summary>
    /// Записывает новое содержимое замечания в фоновой задаче. Удача: список перечитывается (при смене области повтор в другом файле сливается). Неудача: галочки возвращаются, набранный текст остаётся, владелец видит сообщение
    /// </summary>
    /// <param name="row">Строка</param>
    /// <param name="updated">Новое содержимое</param>
    /// <returns>Задача, которая завершается, когда итог применён; ошибок не выбрасывает</returns>
    private async Task ChangeRemarkAsync(RemarkViewModel row, Remark updated)
    {
        if (_remarkHooks is not { } hooks || row.IsBusy)
            return;

        var text = RemarkStore.Flatten(updated.Text);
        if (text.Length == 0)
        {
            Message = "Пустое замечание не сохраняю. Чтобы убрать его, нажми крестик.";

            return;
        }

        row.IsBusy = true;
        try
        {
            await Task.Run(() => hooks.Update(updated with { Text = text }));
            Message = string.Empty;
            row.IsBusy = false;
            Track(LoadRemarksAsync());
        }
        catch (Exception exception)
        {
            row.IsBusy = false;
            row.Revert();
            Report("Не удалось сохранить замечание: ", exception);
        }
    }

    /// <summary>
    /// Удаляет замечание в фоновой задаче. Неудача: строка остаётся в списке, владелец видит сообщение
    /// </summary>
    /// <param name="row">Строка</param>
    /// <returns>Задача, которая завершается, когда итог применён; ошибок не выбрасывает</returns>
    private async Task RemoveRemarkAsync(RemarkViewModel row)
    {
        if (_remarkHooks is not { } hooks || row.IsBusy)
            return;

        row.IsBusy = true;
        try
        {
            await Task.Run(() => hooks.Remove(row.Id));
            Message = string.Empty;
            row.IsBusy = false;
            Track(LoadRemarksAsync());
        }
        catch (Exception exception)
        {
            row.IsBusy = false;
            Report("Не удалось удалить замечание: ", exception);
        }
    }

    /// <summary>
    /// Показывает ошибку замечаний; неожиданную (не файлов и не данных) ещё и пишет в журнал
    /// </summary>
    /// <param name="prefix">Начало сообщения</param>
    /// <param name="exception">Исключение</param>
    private void Report(string prefix, Exception exception)
    {
        if (exception is not (IOException or UnauthorizedAccessException or System.Text.Json.JsonException))
            System.Diagnostics.Trace.TraceError($"Замечания: {exception}");

        Message = prefix + exception.Message;
    }

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
