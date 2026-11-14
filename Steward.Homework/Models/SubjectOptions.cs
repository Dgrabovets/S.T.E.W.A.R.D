namespace Steward.Homework.Models;

/// <summary>
/// Настройки предмета для домашних заданий: готовить ли работы сами и откуда брать задания
/// </summary>
/// <param name="AutoPrepare">Фоновый процесс сам готовит работы по заданиям этого предмета. По умолчанию выключено: кнопка «Подготовить задание» работает всегда</param>
/// <param name="ExcludedGuides">Ключи общих указаний и образцов, которые к этому предмету не относятся и при подготовке не учитываются. Необязательное поле: в старых записях его нет, тогда действуют все</param>
/// <param name="ProjectFolder">Папка единого проекта предмета, например проекта Unity, который растёт с каждым домашним заданием. Агент читает её только на чтение, а на плитке предмета видно состояние проекта. Пусто: проекта нет</param>
/// <param name="SingularityProjectId">Проект SingularityApp, в который попадают задачи и заметки по этому предмету. Пусто: проект из настроек Steward.Lms, а без него задача идёт во входящие</param>
/// <param name="Language">Язык, на котором пишется работа по этому предмету, словами: «английском». Пусто: по-русски</param>
public sealed record SubjectOptions(bool AutoPrepare = false, IReadOnlyList<string>? ExcludedGuides = null, string? ProjectFolder = null, string? Language = null, string? SingularityProjectId = null)
{
    /// <summary>
    /// Ключи исключённых указаний и образцов; пусто, если исключений нет
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> Excluded => ExcludedGuides ?? [];

    /// <summary>
    /// Сравнивает настройки по значению, в том числе списки исключений: пустой список и его отсутствие считаются одним и тем же
    /// </summary>
    /// <param name="other">Другие настройки</param>
    /// <returns>
    /// <see langword="true"/>, если все настройки совпадают
    /// </returns>
    public bool Equals(SubjectOptions? other) => other is not null && AutoPrepare == other.AutoPrepare && string.Equals(ProjectFolder ?? string.Empty, other.ProjectFolder ?? string.Empty, StringComparison.Ordinal) && string.Equals(Language ?? string.Empty, other.Language ?? string.Empty, StringComparison.Ordinal) && string.Equals(SingularityProjectId ?? string.Empty, other.SingularityProjectId ?? string.Empty, StringComparison.Ordinal) && Excluded.Order(StringComparer.Ordinal).SequenceEqual(other.Excluded.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>
    /// Хеш, согласованный со сравнением по значению
    /// </summary>
    /// <returns>Хеш настроек</returns>
    public override int GetHashCode() => HashCode.Combine(AutoPrepare, Excluded.Count, ProjectFolder ?? string.Empty, Language ?? string.Empty, SingularityProjectId ?? string.Empty);
}
