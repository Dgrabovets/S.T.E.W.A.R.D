namespace Steward.Status.Rows;

/// <summary>
/// Готовая к показу строка задачи
/// </summary>
/// <param name="Id">Идентификатор задачи</param>
/// <param name="Title">Название задачи</param>
/// <param name="Headline">Короткий статус рядом с названием</param>
/// <param name="Detail">Подробности под названием</param>
/// <param name="Tone">Цвет строки</param>
/// <param name="Awaiting">Сколько результатов ждёт решения пользователя</param>
/// <param name="Enabled">Включена ли задача. Выключенная не влияет на общий статус</param>
public sealed record JobRow(string Id, string Title, string Headline, string Detail, RowToneEnum Tone, int Awaiting, bool Enabled);
