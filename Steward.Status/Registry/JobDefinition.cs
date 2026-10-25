namespace Steward.Status.Registry;

/// <summary>
/// Задача, которую приложение запускает само и статус которой показывает
/// </summary>
public sealed record JobDefinition
{
    /// <summary>
    /// Идентификатор из латиницы, цифр, дефиса и подчёркивания. По нему называется файл статуса
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Название для людей
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Запускать ли задачу по условиям. Выключенную можно запустить вручную
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Программа, которую нужно запустить. Вместо <c>{repo}</c> подставляется корень репозитория
    /// </summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// Аргументы командной строки
    /// </summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>
    /// Рабочий каталог. По умолчанию каталог программы
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Когда запускать
    /// </summary>
    public IReadOnlyList<JobTrigger> Triggers { get; init; } = [];

    /// <summary>
    /// Через сколько минут зависшая задача останавливается
    /// </summary>
    public double TimeoutMinutes { get; init; } = 30;

    /// <summary>
    /// Через сколько минут без успешного запуска задача считается проблемной. Пусто, если проверка не нужна
    /// </summary>
    public int? StaleAfterMinutes { get; init; }

    /// <summary>
    /// Предельное время работы
    /// </summary>
    public TimeSpan Timeout => TimeSpan.FromMinutes(TimeoutMinutes);

    /// <summary>
    /// Название для показа: если оно не задано, берётся идентификатор
    /// </summary>
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Id : Title;
}
