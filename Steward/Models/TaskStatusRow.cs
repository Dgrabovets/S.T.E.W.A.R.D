namespace Steward.Models;

/// <summary>
/// Одна строка списка задач на панели
/// </summary>
/// <param name="Id">Идентификатор задачи. По нему запускается кнопка «Запустить»</param>
/// <param name="Name">Название задачи, например «Почта»</param>
/// <param name="Headline">Короткий статус рядом с названием</param>
/// <param name="Detail">Подробности под названием. Могут быть пустыми</param>
/// <param name="Tone">Цвет точки: работает, остановлена, ждёт решения или сбой</param>
/// <param name="Muted">
/// <see langword="true"/> для выключенной задачи: она не влияет на общий статус
/// </param>
/// <param name="Working">Задача выполняется прямо сейчас</param>
/// <param name="CanRun">
/// <see langword="true"/>, если задачу можно запустить кнопкой
/// </param>
public sealed record TaskStatusRow(string Id, string Name, string Headline, string Detail, PanelToneEnum Tone, bool Muted = false, bool CanRun = false, bool Working = false);
