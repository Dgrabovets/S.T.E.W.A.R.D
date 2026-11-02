namespace Steward.Homework.Models;

/// <summary>
/// Настройки предмета для домашних заданий: готовить ли работы сами и откуда брать задания
/// </summary>
/// <param name="AutoPrepare">Фоновый процесс сам готовит работы по заданиям этого предмета. По умолчанию выключено: кнопка «Подготовить задание» работает всегда</param>
/// <param name="ExcludedGuides">Ключи общих указаний и образцов, которые к этому предмету не относятся и при подготовке не учитываются. Необязательное поле: в старых записях его нет, тогда действуют все</param>
public sealed record SubjectOptions(bool AutoPrepare = false, IReadOnlyList<string>? ExcludedGuides = null)
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
    public bool Equals(SubjectOptions? other) => other is not null && AutoPrepare == other.AutoPrepare && Excluded.Order(StringComparer.Ordinal).SequenceEqual(other.Excluded.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>
    /// Хеш, согласованный со сравнением по значению
    /// </summary>
    /// <returns>Хеш настроек</returns>
    public override int GetHashCode() => HashCode.Combine(AutoPrepare, Excluded.Count);
}
