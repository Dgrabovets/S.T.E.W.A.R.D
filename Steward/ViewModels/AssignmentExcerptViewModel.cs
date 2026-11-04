using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Одно задание на вкладке «Задания» читалки: название, слова преподавателя с репликами вокруг них и кнопка «Добавить в задание»
/// </summary>
public sealed class AssignmentExcerptViewModel : Bindable
{
    /// <summary>
    /// Что показано под кнопкой после добавления или отказа
    /// </summary>
    private string _note = string.Empty;

    /// <summary>
    /// Задание уже лежит в списке заданий занятия
    /// </summary>
    private bool _added;

    /// <summary>
    /// По заданию уже заведена задача в Singularity
    /// </summary>
    private bool _inSingularity;

    /// <summary>
    /// Создаёт строку задания
    /// </summary>
    /// <param name="number">Номер задания на занятии, с единицы</param>
    /// <param name="task">Задача из разбора занятия</param>
    /// <param name="added">Задание уже есть в списке заданий занятия</param>
    /// <param name="add">Запускает добавление задания в список; итог она сообщает через <see cref="Report"/>. <see langword="null"/>, если добавлять нечем</param>
    /// <param name="toSingularity">Запускает заведение задачи в Singularity без добавления в список; итог она сообщает через <see cref="ReportSingularity"/>. <see langword="null"/>, если Singularity не подключён</param>
    /// <param name="inSingularity">Задача по заданию уже заведена в Singularity</param>
    /// <param name="dismiss">Убирает задание из разбора: кнопка «Убрать». <see langword="null"/>, если убирать нечем</param>
    public AssignmentExcerptViewModel(int number, AnalysisTask task, bool added, Action<AssignmentExcerptViewModel>? add, Action<AssignmentExcerptViewModel>? toSingularity = null, bool inSingularity = false, Action<AssignmentExcerptViewModel>? dismiss = null)
    {
        _inSingularity = inSingularity;
        DismissCommand = new RelayCommand(() => dismiss?.Invoke(this), () => dismiss is not null);
        Number = number;
        Heading = $"Задание {number}";
        Title = task.Title;
        Due = task.Due;
        Task = task;
        _added = added;
        Lines = ExcerptLines(task);
        Warning = WarningFor(task);
        AddCommand = new RelayCommand(() => add?.Invoke(this), () => !_added && add is not null);
        SingularityCommand = new RelayCommand(() => toSingularity?.Invoke(this), () => !_inSingularity && toSingularity is not null);
    }

    /// <summary>
    /// Собирает строки отрывка: реплики до слов преподавателя, сами слова (выделены) и реплики после. Если отрывка нет, показывается одна цитата из разбора
    /// </summary>
    /// <param name="task">Задача из разбора</param>
    /// <returns>Строки по порядку; пусто, если нет и цитаты</returns>
    private static IReadOnlyList<ExcerptLineViewModel> ExcerptLines(AnalysisTask task)
    {
        if (!task.HasExcerpt)
            return task.Quote.Length > 0 ? [new ExcerptLineViewModel(task.Quote, true)] : [];

        return [.. (task.Before ?? []).Select(line => new ExcerptLineViewModel(line, false)), .. task.Said!.Select(line => new ExcerptLineViewModel(line, true)), .. (task.After ?? []).Select(line => new ExcerptLineViewModel(line, false))];
    }

    /// <summary>
    /// Объясняет, почему показан не весь отрывок
    /// </summary>
    /// <param name="task">Задача из разбора</param>
    /// <returns>Пояснение или пустая строка, если отрывок найден</returns>
    private static string WarningFor(AnalysisTask task)
    {
        if (task.HasExcerpt)
            return string.Empty;

        return task.Quote.Length > 0
            ? "Это место не удалось найти в тексте расшифровки: показаны слова из разбора."
            : "Слова преподавателя для этого задания не сохранились: разбор сделан до того, как их стали записывать.";
    }

    /// <summary>
    /// Номер задания в разборе занятия, с единицы
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Задача из разбора занятия
    /// </summary>
    public AnalysisTask Task { get; }

    /// <summary>
    /// Заголовок блока: «Задание 1», «Задание 2»
    /// </summary>
    public string Heading { get; }

    /// <summary>
    /// Что нужно сделать
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Срок, если его назвали
    /// </summary>
    public string Due { get; }

    /// <summary>
    /// Срок назван
    /// </summary>
    public bool HasDue => Due.Length > 0;

    /// <summary>
    /// Реплики до слов преподавателя, сами слова (выделены) и реплики после
    /// </summary>
    public IReadOnlyList<ExcerptLineViewModel> Lines { get; }

    /// <summary>
    /// Есть реплики для показа
    /// </summary>
    public bool HasLines => Lines.Count > 0;

    /// <summary>
    /// Почему показан не весь отрывок. Пусто, если отрывок найден
    /// </summary>
    public string Warning { get; }

    /// <summary>
    /// Есть предупреждение об отрывке
    /// </summary>
    public bool HasWarning => Warning.Length > 0;

    /// <summary>
    /// Подпись кнопки: меняется, когда задание уже в списке
    /// </summary>
    public string ButtonText => _added ? "Уже в заданиях" : "Добавить в задание";

    /// <summary>
    /// Добавляет задание в список заданий занятия
    /// </summary>
    public RelayCommand AddCommand { get; }

    /// <summary>
    /// Заводит задачу в Singularity, не добавляя задание в список заданий занятия
    /// </summary>
    public RelayCommand SingularityCommand { get; }

    /// <summary>
    /// Убирает задание из разбора занятия: оно считается разобранным, а убрать можно ошибочно найденное
    /// </summary>
    public RelayCommand DismissCommand { get; }

    /// <summary>
    /// Владелец уже отреагировал на задание: добавил в список или завёл задачу в Singularity
    /// </summary>
    public bool IsHandled => _added || _inSingularity;

    /// <summary>
    /// Подпись кнопки Singularity: меняется, когда задача заведена
    /// </summary>
    public string SingularityText => _inSingularity ? "Заведено в Singularity" : "Только в Singularity";

    /// <summary>
    /// Пояснение под кнопкой после добавления
    /// </summary>
    public string Note
    {
        get => _note;
        private set
        {
            if (Set(ref _note, value))
                Raise(nameof(HasNote));
        }
    }

    /// <summary>
    /// Есть пояснение под кнопкой
    /// </summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>
    /// Сообщает итог добавления: пояснение под кнопкой, а если задание добавлено, то кнопка гаснет и меняет подпись
    /// </summary>
    /// <param name="note">Что показать под кнопкой</param>
    /// <param name="added">Задание теперь лежит в списке заданий</param>
    public void Report(string note, bool added)
    {
        Note = note;
        if (!added)
            return;

        _added = true;
        Raise(nameof(ButtonText));
        Raise(nameof(IsHandled));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Сообщает итог заведения задачи в Singularity: пояснение под кнопками, а если задача заведена, то кнопка гаснет и меняет подпись
    /// </summary>
    /// <param name="note">Что показать под кнопками</param>
    /// <param name="created">Задача теперь есть в Singularity</param>
    public void ReportSingularity(string note, bool created)
    {
        Note = note;
        if (!created)
            return;

        _inSingularity = true;
        Raise(nameof(SingularityText));
        Raise(nameof(IsHandled));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
