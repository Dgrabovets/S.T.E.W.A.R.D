using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Настройки предмета на вкладке «Домашние задания»: тумблер «Готовить автоматически» и выбор, откуда берутся задания. Изменение сразу записывается в фоне, а если запись не удалась, значение возвращается и показывается причина
/// </summary>
public sealed class SubjectOptionsViewModel : Bindable
{
    /// <summary>
    /// Что выбрано и записано сейчас
    /// </summary>
    private SubjectOptions _current;

    /// <summary>
    /// Запись выполняется: пока она идёт, повторные изменения не записываются
    /// </summary>
    private bool _saving;

    /// <summary>
    /// Причина последней неудачной записи или пустая строка
    /// </summary>
    private string _error = string.Empty;

    /// <summary>
    /// Записывает настройки предмета: название предмета и новые настройки. Работает в фоне
    /// </summary>
    private readonly Action<string, SubjectOptions> _save;

    /// <summary>
    /// Название предмета
    /// </summary>
    private readonly string _subject;

    /// <summary>
    /// Создаёт настройки предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="current">Настройки, записанные сейчас</param>
    /// <param name="save">Записывает настройки: название предмета и новые настройки. Может выбросить исключение, тогда значение возвращается</param>
    public SubjectOptionsViewModel(string subject, SubjectOptions current, Action<string, SubjectOptions> save)
    {
        _subject = subject;
        _current = current;
        _save = save;
        Sources =
        [
            new TaskSourceOptionViewModel(TaskSourceEnum.Auto, "Определять самому", "Задания в LMS, если там у предмета написано условие, иначе их даёт преподаватель на занятии"),
            new TaskSourceOptionViewModel(TaskSourceEnum.Lms, "В LMS", "Карточки строятся по работам LMS, из расшифровок задания не берутся"),
            new TaskSourceOptionViewModel(TaskSourceEnum.Transcript, "На занятии", "Преподаватель даёт задания на занятии: их ищет в расшифровке агент"),
        ];
    }

    /// <summary>
    /// Варианты источника заданий
    /// </summary>
    public IReadOnlyList<TaskSourceOptionViewModel> Sources { get; }

    /// <summary>
    /// Фоновый процесс сам готовит работы по заданиям предмета
    /// </summary>
    public bool AutoPrepare
    {
        get => _current.AutoPrepare;
        set => Change(_current with { AutoPrepare = value });
    }

    /// <summary>
    /// Выбранный источник заданий
    /// </summary>
    public TaskSourceOptionViewModel SelectedSource
    {
        get => Sources.First(option => option.Source == _current.TaskSource);
        set
        {
            if (value is not null)
                Change(_current with { TaskSource = value.Source });
        }
    }

    /// <summary>
    /// Подсказка под выбором: что значит выбранный источник
    /// </summary>
    public string SourceHint => SelectedSource.Hint;

    /// <summary>
    /// Причина, по которой настройки не записались
    /// </summary>
    public string Error
    {
        get => _error;
        private set
        {
            if (Set(ref _error, value))
                Raise(nameof(HasError));
        }
    }

    /// <summary>
    /// Есть сообщение о неудачной записи
    /// </summary>
    public bool HasError => _error.Length > 0;

    /// <summary>
    /// Принимает новое значение и записывает его в фоне
    /// </summary>
    /// <param name="next">Новые настройки</param>
    private void Change(SubjectOptions next)
    {
        if (next == _current || _saving)
        {
            Raise(nameof(AutoPrepare));
            Raise(nameof(SelectedSource));

            return;
        }

        var previous = _current;
        _current = next;
        Error = string.Empty;
        RaiseAll();
        _ = SaveAsync(previous, next);
    }

    /// <summary>
    /// Записывает настройки, а при неудаче возвращает прежние и показывает причину. Не выбрасывает исключений
    /// </summary>
    /// <param name="previous">Настройки до изменения</param>
    /// <param name="next">Новые настройки</param>
    /// <returns>Задача записи</returns>
    private async Task SaveAsync(SubjectOptions previous, SubjectOptions next)
    {
        _saving = true;
        try
        {
            await Task.Run(() => _save(_subject, next));
        }
        catch (Exception exception)
        {
            _current = previous;
            Error = "Не удалось сохранить настройки предмета: " + exception.Message;
            RaiseAll();
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>
    /// Сообщает окну, что значения изменились
    /// </summary>
    private void RaiseAll()
    {
        Raise(nameof(AutoPrepare));
        Raise(nameof(SelectedSource));
        Raise(nameof(SourceHint));
    }
}
