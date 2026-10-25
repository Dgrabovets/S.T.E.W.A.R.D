namespace Steward.Reels.Models;

/// <summary>
/// Итог одной проверки бота
/// </summary>
/// <param name="Accepted">Сколько рилсов превращено в идеи</param>
/// <param name="Failed">Сколько рилсов не удалось обработать</param>
/// <param name="Ignored">Сколько сообщений пропущено: чужие, без ссылки</param>
/// <param name="Awaiting">Сколько всего идей ждёт решения</param>
public sealed record ReelsResult(int Accepted, int Failed, int Ignored, int Awaiting);