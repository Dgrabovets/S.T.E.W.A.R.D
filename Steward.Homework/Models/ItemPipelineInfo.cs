using Steward.Homework.Services;

namespace Steward.Homework.Models;

/// <summary>
/// Что карточка показывает о задании: шаг конвейера, файл работы, оценка уверенности, источник и подсказка, что будет дальше
/// </summary>
/// <param name="Step">Шаг конвейера, выведенный из файлов и отметок</param>
/// <param name="WorkPath">Готовый файл задания или пусто, если работы нет</param>
/// <param name="Assessment">Оценка уверенности: вопрос владельцу или пометка «проверь формулировку»</param>
/// <param name="EvidenceVerified">Цитата задания найдена в расшифровке</param>
/// <param name="LinkedToLms">Задание связано с работой LMS: у него цифровой номер работы, как его понимает оценка уверенности</param>
/// <param name="Hint">Одна строка о том, что ждёт задание или что пошло не так; пусто, если сказать нечего</param>
/// <param name="HintIsProblem">Подсказка говорит об ошибке, а не об ожидании</param>
public sealed record ItemPipelineInfo(PipelineStepEnum Step, string WorkPath, ItemAssessment Assessment, bool EvidenceVerified, bool LinkedToLms, string Hint, bool HintIsProblem = false);
