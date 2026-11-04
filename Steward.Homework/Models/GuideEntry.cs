namespace Steward.Homework.Models;

/// <summary>
/// Что это за материал предмета
/// </summary>
public enum GuideKindEnum
{
    /// <summary>
    /// Методические указания: требования к структуре, объёму и оформлению работ
    /// </summary>
    Guide,

    /// <summary>
    /// Образец: работа, оформленная по указаниям правильно, ориентир по стилю и структуре
    /// </summary>
    Example,
}

/// <summary>
/// Материал предмета для подготовки заданий: файл в папке приложения, файл или папка по пути на диске или ссылка в интернете
/// </summary>
/// <param name="Id">Короткий уникальный ключ</param>
/// <param name="Subject">Название предмета, к которому относится материал</param>
/// <param name="Kind">Методические указания или образец</param>
/// <param name="Title">Название для списка: имя файла или папки</param>
/// <param name="Path">Путь на диске: к файлу в папке приложения или к файлу и папке по ссылке на диск</param>
/// <param name="Url">Не используется: раньше здесь хранилась ссылка в интернете. Поле оставлено, чтобы старые записи читались</param>
/// <param name="Note">Пояснение для списка</param>
/// <param name="Enabled">Материал учитывается при подготовке заданий. Выключенный остаётся в списке</param>
/// <param name="Manual">Образец принёс владелец сам, а не взят из готового черновика. Необязательное поле: в старых записях его нет</param>
/// <param name="Added">Когда запись добавлена или заменена (UTC). Нужно, чтобы выбрать последние образцы. Необязательное поле: в старых записях его нет</param>
/// <param name="Origin">Откуда файл скопирован: путь к оригиналу. По нему программа замечает, что оригинал изменился, и обновляет копию. Необязательное поле: в старых записях его нет</param>
/// <param name="OriginStamp">Размер и время изменения оригинала на момент копирования, строкой. Необязательное поле</param>
public sealed record GuideEntry(string Id, string Subject, GuideKindEnum Kind, string Title, string Path, string Url, string Note, bool Enabled = true, bool Manual = false, DateTime? Added = null, string? Origin = null, string? OriginStamp = null);
