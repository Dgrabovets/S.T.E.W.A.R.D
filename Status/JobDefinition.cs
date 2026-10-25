namespace SingularityTools.Status;

/// <summary>
/// Условие запуска задачи
/// </summary>
public sealed record JobTrigger
{
    /// <summary>
    /// При запуске приложения
    /// </summary>
    public const string START = "start";

    /// <summary>
    /// Каждый день в заданное местное время
    /// </summary>
    public const string DAILY = "daily";

    /// <summary>
    /// Через заданное число минут после прошлого запуска
    /// </summary>
    public const string INTERVAL = "interval";

    /// <summary>
    /// Вид условия: <c>start</c>, <c>daily</c> или <c>interval</c>
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Время <c>HH:mm</c> для <c>daily</c>
    /// </summary>
    public string? At { get; init; }

    /// <summary>
    /// Число минут для <c>interval</c>
    /// </summary>
    public int? EveryMinutes { get; init; }
}

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
}
