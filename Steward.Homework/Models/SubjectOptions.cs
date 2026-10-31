namespace Steward.Homework.Models;

/// <summary>
/// Настройки предмета для домашних заданий: готовить ли работы сами и откуда брать задания
/// </summary>
/// <param name="AutoPrepare">Фоновый процесс сам готовит работы по заданиям этого предмета. По умолчанию выключено: кнопка «Подготовить задание» работает всегда</param>
/// <param name="TaskSource">Откуда у предмета берутся задания</param>
public sealed record SubjectOptions(bool AutoPrepare = false, TaskSourceEnum TaskSource = TaskSourceEnum.Auto);
