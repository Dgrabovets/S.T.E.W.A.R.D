using System.Globalization;
using System.Text.RegularExpressions;
using Steward.Monitoring.Models;

namespace Steward.Monitoring.Implementations;

/// <summary>
/// Переводит хвост журнала Docker в короткие записи о прогонах
/// </summary>
public static partial class LogJournal
{
    /// <summary>
    /// Префикс имени контейнера в выводе <c>docker compose logs</c>
    /// </summary>
    [GeneratedRegex(@"^schedule-\d+\s+\|\s*", RegexOptions.CultureInvariant)]
    private static partial Regex ContainerPrefix();

    /// <summary>
    /// Метка времени Docker в начале строки
    /// </summary>
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampPattern();

    /// <summary>
    /// Строка категории Microsoft.Extensions.Logging
    /// </summary>
    [GeneratedRegex(@"^(info|warn|fail|crit|dbug|trce):\s+\S+\[\d+\]\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CategoryPattern();

    /// <summary>
    /// Итог прогона синхронизации
    /// </summary>
    [GeneratedRegex(@"^Sync finished: created=(\d+) updated=(\d+) archived=(\d+) unarchived=(\d+) completed=(\d+) skipped=(\d+)(?: deleted=(\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex FinishedPattern();

    /// <summary>
    /// Внешний идентификатор занятия
    /// </summary>
    [GeneratedRegex(@"up:[a-z0-9-]+:(\d{4})-(\d{2})-(\d{2}):(\d{2})(\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex ExternalIdPattern();

    /// <summary>
    /// Адрес, который не нужен в человеческой записи
    /// </summary>
    [GeneratedRegex(@"https?://\S+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    /// <summary>
    /// Сколько последних прогонов держать в окне
    /// </summary>
    private const int ENTRY_LIMIT = 24;

    #region Reading

    /// <summary>
    /// Собирает записи от новых к старым и выбрасывает служебные строки
    /// </summary>
    /// <param name="logs">
    /// Журнал контейнера с метками времени
    /// </param>
    /// <returns>
    /// Записи для окна. Пустой список, если полезных прогонов нет
    /// </returns>
    public static IReadOnlyList<LogEntry> Read(string logs)
    {
        var entries = new List<LogEntry>();
        var cycle = new Cycle();

        foreach (var message in Messages(logs))
        {
            if (message.Text.Equals("Sync failed", StringComparison.Ordinal))
            {
                cycle.Time = message.Time;
                cycle.Failed = true;
                continue;
            }

            if (cycle.Failed && cycle.Failure is null && !IsNoise(message.Text))
            {
                cycle.Time = message.Time;
                cycle.Failure = HumanizeFailure(message.Text);
                entries.Add(Problem(cycle));
                cycle = new Cycle();
                continue;
            }

            if (FinishedPattern().Match(message.Text) is { Success: true } finished)
            {
                cycle.Time = message.Time;
                cycle.HasFinish = true;
                if (int.TryParse(finished.Groups[6].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var skipped))
                    cycle.Skipped = skipped;
                RememberDeleted(cycle, finished.Groups[7]);
                Emit(entries, cycle);
                cycle = new Cycle();
                continue;
            }

            if (TryChange(message.Text, out var change, out var skippedOne))
            {
                cycle.Time = message.Time;
                if (skippedOne)
                    cycle.Skipped++;
                else if (change is not null)
                    cycle.Changes.Add(change);
            }
        }

        if (cycle.Failed)
            entries.Add(Problem(cycle));

        if (entries.Count > ENTRY_LIMIT)
            entries.RemoveRange(0, entries.Count - ENTRY_LIMIT);

        entries.Reverse();
        return entries;
    }

    /// <summary>
    /// Достаёт текст сообщения, пропуская категорию логгера и кадры стека
    /// </summary>
    /// <param name="logs">
    /// Сырой журнал
    /// </param>
    /// <returns>
    /// Сообщения по порядку
    /// </returns>
    private static IEnumerable<Message> Messages(string logs)
    {
        var lines = logs.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var raw in lines)
        {
            var line = ContainerPrefix().Replace(raw.Trim(), string.Empty);
            if (line.Length == 0 || CategoryPattern().IsMatch(StripTimestamp(line)))
                continue;

            var body = StripTimestamp(line).Trim();
            if (body.Length == 0 || body.StartsWith("at ", StringComparison.Ordinal) || body.StartsWith("---", StringComparison.Ordinal))
                continue;

            var time = TryTimestamp(line, out var timestamp) ? timestamp.ToLocalTime() : DateTimeOffset.Now;
            yield return new Message(time, body);
        }
    }

    /// <summary>
    /// Убирает метку времени из строки
    /// </summary>
    /// <param name="line">
    /// Строка журнала
    /// </param>
    /// <returns>
    /// Текст без метки
    /// </returns>
    private static string StripTimestamp(string line)
    {
        var match = TimestampPattern().Match(line);
        if (!match.Success)
            return line;

        return line[(match.Index + match.Length)..];
    }

    /// <summary>
    /// Читает метку времени Docker
    /// </summary>
    /// <param name="line">
    /// Строка журнала
    /// </param>
    /// <param name="timestamp">
    /// Момент в UTC, если метка найдена
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если метка прочитана
    /// </returns>
    private static bool TryTimestamp(string line, out DateTimeOffset timestamp)
    {
        var match = TimestampPattern().Match(line);
        if (!match.Success || !DateTimeOffset.TryParse(match.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp))
        {
            timestamp = default;
            return false;
        }

        return true;
    }

    #endregion Reading

    #region Noise

    /// <summary>
    /// Сообщает, что строка относится к транспорту, а не к расписанию
    /// </summary>
    /// <param name="text">
    /// Текст сообщения
    /// </param>
    /// <returns>
    /// <see langword="true"/> для запросов HTTP и строк запуска приложения
    /// </returns>
    private static bool IsNoise(string text) =>
        text.StartsWith("Start processing HTTP", StringComparison.Ordinal)
        || text.StartsWith("End processing HTTP", StringComparison.Ordinal)
        || text.StartsWith("Sending HTTP", StringComparison.Ordinal)
        || text.StartsWith("Received HTTP", StringComparison.Ordinal)
        || text.StartsWith("Application started", StringComparison.Ordinal)
        || text.StartsWith("Application is shutting down", StringComparison.Ordinal)
        || text.StartsWith("Hosting environment", StringComparison.Ordinal)
        || text.StartsWith("Content root path", StringComparison.Ordinal)
        || text.StartsWith("Parsed ", StringComparison.Ordinal);

    #endregion Noise

    #region Changes

    /// <summary>
    /// Переводит известное действие синхронизации
    /// </summary>
    /// <param name="text">
    /// Текст сообщения
    /// </param>
    /// <param name="change">
    /// Человеческая строка. Пусто для пропуска уже выполненной задачи
    /// </param>
    /// <param name="skipped">
    /// <see langword="true"/>, если задача уже была выполнена и её не трогали
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если строка распознана
    /// </returns>
    private static bool TryChange(string text, out string? change, out bool skipped)
    {
        change = null;
        skipped = false;

        if (text.StartsWith("Skipped completed task ", StringComparison.Ordinal))
        {
            skipped = true;
            return true;
        }

        if (text.StartsWith("Created ", StringComparison.Ordinal))
        {
            change = Action("Добавлено", text["Created ".Length..]);
            return change is not null;
        }

        if (text.StartsWith("Updated ", StringComparison.Ordinal))
        {
            change = Action("Обновлено", text["Updated ".Length..]);
            return change is not null;
        }

        if (text.StartsWith("Deleted completed future task ", StringComparison.Ordinal))
        {
            change = $"Удалено {When(text)}. Время ещё не прошло";
            return true;
        }

        if (text.StartsWith("Completed past lesson ", StringComparison.Ordinal))
        {
            change = $"Отмечено выполненным {When(text)}";
            return true;
        }

        if (text.StartsWith("Completed removed lesson ", StringComparison.Ordinal))
        {
            change = $"Убрано из таблицы {When(text)}";
            return true;
        }

        if (text.StartsWith("Unarchived ", StringComparison.Ordinal))
        {
            change = $"Возвращено из архива {When(text)}";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Собирает строку о создании или обновлении занятия
    /// </summary>
    /// <param name="verb">
    /// Глагол на русском
    /// </param>
    /// <param name="rest">
    /// Внешний идентификатор и название
    /// </param>
    /// <returns>
    /// Строка с названием. Пусто, если идентификатор не распознан
    /// </returns>
    private static string? Action(string verb, string rest)
    {
        var match = ExternalIdPattern().Match(rest);
        if (!match.Success)
            return null;

        var title = rest[(match.Index + match.Length)..].Trim();
        return title.Length == 0 ? $"{verb} {When(rest)}" : $"{verb} «{title}»";
    }

    /// <summary>
    /// Достаёт день и время из внешнего идентификатора
    /// </summary>
    /// <param name="text">
    /// Текст, в котором есть идентификатор
    /// </param>
    /// <returns>
    /// Строка вида <c>16.10 в 20:10</c>. Если идентификатора нет, слово «занятие»
    /// </returns>
    private static string When(string text)
    {
        var match = ExternalIdPattern().Match(text);
        if (!match.Success)
            return "занятие";

        return $"{match.Groups[3].Value}.{match.Groups[2].Value} в {match.Groups[4].Value}:{match.Groups[5].Value}";
    }

    #endregion Changes

    #region Failures

    /// <summary>
    /// Убирает из текста сбоя адреса, тип исключения и служебные коды
    /// </summary>
    /// <param name="raw">
    /// Строка исключения
    /// </param>
    /// <returns>
    /// Короткая причина для человека
    /// </returns>
    private static string HumanizeFailure(string raw)
    {
        var text = raw.Trim();
        var arrow = text.IndexOf("--->", StringComparison.Ordinal);
        if (arrow >= 0)
            text = text[..arrow].Trim();

        if (text.Contains("HttpRequestException", StringComparison.Ordinal)
            || text.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || text.Contains("No such host", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
            return "Не удалось связаться с сервисом расписания";

        if (text.Contains("401", StringComparison.Ordinal) || text.Contains("403", StringComparison.Ordinal))
            return "Нет доступа к сервису расписания";

        if (text.Contains("Singularity 400", StringComparison.Ordinal) || text.Contains(" 400", StringComparison.Ordinal))
            return "Сервис расписания отклонил запрос";

        var marker = text.IndexOf("Exception:", StringComparison.Ordinal);
        if (marker >= 0)
            text = text[(marker + "Exception:".Length)..].Trim();

        text = UrlPattern().Replace(text, string.Empty).Trim();
        if (text.Length > 160)
            text = string.Concat(text.AsSpan(0, 160).Trim(), "…");

        return text.Length == 0 ? "Синхронизация остановилась" : text;
    }

    #endregion Failures

    #region Entries

    /// <summary>
    /// Дописывает удаление из итога, если подробная строка не попала в хвост журнала
    /// </summary>
    /// <param name="cycle">
    /// Текущий прогон
    /// </param>
    /// <param name="deletedGroup">
    /// Группа <c>deleted</c> из строки итога
    /// </param>
    private static void RememberDeleted(Cycle cycle, Group deletedGroup)
    {
        if (!deletedGroup.Success
            || !int.TryParse(deletedGroup.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var deleted)
            || deleted <= 0
            || cycle.Changes.Exists(line => line.StartsWith("Удалено ", StringComparison.Ordinal)))
            return;

        cycle.Changes.Add(deleted == 1
            ? "Удалена задача, время которой ещё не прошло"
            : $"Удалено задач: {deleted}. Время ещё не прошло");
    }

    /// <summary>
    /// Кладёт итог прогона в список и схлопывает подряд идущие тихие прогоны
    /// </summary>
    /// <param name="entries">
    /// Уже собранные записи, от старых к новым
    /// </param>
    /// <param name="cycle">
    /// Только что закончившийся прогон
    /// </param>
    private static void Emit(List<LogEntry> entries, Cycle cycle)
    {
        if (cycle.Failed || cycle.Failure is not null)
        {
            entries.Add(Problem(cycle));
            return;
        }

        if (cycle.Changes.Count == 0)
        {
            var detail = cycle.Skipped > 0
                ? AlreadyDone(cycle.Skipped)
                : "Новых правок в таблице нет";
            if (entries.Count > 0 && entries[^1].Tone == LogTone.Quiet && entries[^1].Detail == detail)
            {
                var previous = entries[^1];
                entries[^1] = previous with { Time = cycle.Time, Repeat = previous.Repeat + 1 };
                return;
            }

            entries.Add(new LogEntry(cycle.Time, LogTone.Quiet, "Всё совпало", detail, 1));
            return;
        }

        if (cycle.Skipped > 0)
            cycle.Changes.Add(AlreadyDone(cycle.Skipped));

        entries.Add(new LogEntry(cycle.Time, LogTone.Update, "Появились правки", string.Join('\n', cycle.Changes), 1));
    }

    /// <summary>
    /// Склоняет фразу о уже выполненных задачах
    /// </summary>
    /// <param name="count">
    /// Сколько задач пропущено
    /// </param>
    /// <returns>
    /// Фраза для карточки
    /// </returns>
    private static string AlreadyDone(int count) =>
        count == 1
            ? "Одно прошедшее занятие уже закрыто"
            : $"Прошедшие занятия уже закрыты: {count}";

    /// <summary>
    /// Строит запись о сбое
    /// </summary>
    /// <param name="cycle">
    /// Прогон, в котором случился сбой
    /// </param>
    /// <returns>
    /// Запись с понятной причиной
    /// </returns>
    private static LogEntry Problem(Cycle cycle) =>
        new(cycle.Time, LogTone.Problem, "Не удалось обновить", cycle.Failure ?? "Синхронизация остановилась", 1);

    #endregion Entries

    #region Cycles

    /// <summary>
    /// Одно полезное сообщение журнала
    /// </summary>
    /// <param name="Time">
    /// Местное время строки
    /// </param>
    /// <param name="Text">
    /// Текст без метки времени
    /// </param>
    private sealed record Message(DateTimeOffset Time, string Text);

    /// <summary>
    /// Незакрытый прогон, пока не встретилась строка итога или сбоя
    /// </summary>
    private sealed class Cycle
    {
        /// <summary>
        /// Время последней строки прогона
        /// </summary>
        public DateTimeOffset Time { get; set; }

        /// <summary>
        /// Человеческие строки об изменениях
        /// </summary>
        public List<string> Changes { get; } = [];

        /// <summary>
        /// Сколько уже выполненных задач пропущено
        /// </summary>
        public int Skipped { get; set; }

        /// <summary>
        /// Прогон помечен как сорвавшийся
        /// </summary>
        public bool Failed { get; set; }

        /// <summary>
        /// Понятная причина сбоя
        /// </summary>
        public string? Failure { get; set; }

        /// <summary>
        /// В журнале есть строка <c>Sync finished</c>
        /// </summary>
        public bool HasFinish { get; set; }
    }

    #endregion Cycles
}
