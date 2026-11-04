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
    }

    /// <summary>
    /// Фоновый процесс сам готовит работы по заданиям предмета
    /// </summary>
    public bool AutoPrepare
    {
        get => _current.AutoPrepare;
        set => Change(_current with { AutoPrepare = value });
    }

    /// <summary>
    /// Ключи общих указаний и образцов, которые к предмету не относятся
    /// </summary>
    public IReadOnlyList<string> ExcludedGuides => _current.Excluded;

    /// <summary>
    /// Запоминает, какие общие указания и образцы к предмету не относятся
    /// </summary>
    /// <param name="excluded">Ключи исключённых материалов; пусто: действуют все</param>
    public void SetExcludedGuides(IEnumerable<string> excluded) => Change(_current with { ExcludedGuides = [.. excluded.Distinct(StringComparer.Ordinal)] });

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
    }
}
