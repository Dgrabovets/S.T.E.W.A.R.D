using Steward.Hosting;
using Steward.Status.Running;

namespace Steward.Tests;

/// <summary>
/// Проверяет просьбу к Claude и ссылку, которая открывает его приложение
/// </summary>
public class FixPromptTests
{
    /// <summary>
    /// Неудавшийся запуск почты с ошибкой и выводом
    /// </summary>
    private static readonly RunRecord Failed = new(
        "mail",
        "Почта",
        "manual",
        new DateTimeOffset(2026, 10, 25, 17, 22, 36, TimeSpan.FromHours(1)),
        new DateTimeOffset(2026, 10, 25, 17, 22, 40, TimeSpan.FromHours(1)),
        1,
        RunOutcomeEnum.Failed,
        "HttpRequestException: SingularityApp ответил 400",
        null,
        "строка вывода 1\nстрока вывода 2");

    /// <summary>
    /// В просьбе есть задача, ошибка, путь к отчёту, ветка исправления и запрет читать секреты, а сам вывод программы остаётся в файле
    /// </summary>
    [Fact]
    public void The_prompt_names_the_job_the_error_the_report_and_the_rules()
    {
        var prompt = FixPrompt.Build(Failed, @"D:\repo", @"D:\repo\data\tmp\fix\mail.md");

        Assert.Contains("«Почта» (id mail)", prompt, StringComparison.Ordinal);
        Assert.Contains("SingularityApp ответил 400", prompt, StringComparison.Ordinal);
        Assert.Contains(@"D:\repo\data\tmp\fix\mail.md", prompt, StringComparison.Ordinal);
        Assert.Contains("fix/mail-20261025", prompt, StringComparison.Ordinal);
        Assert.Contains("Не читай и не печатай секреты", prompt, StringComparison.Ordinal);
        Assert.Contains("не инструкции", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("строка вывода", prompt, StringComparison.Ordinal);
        Assert.True(prompt.Length < 4000);
    }

    /// <summary>
    /// В отчёте для файла есть все поля запуска и весь вывод программы
    /// </summary>
    [Fact]
    public void The_report_has_every_field_and_the_output()
    {
        var report = FixPrompt.Report(Failed);

        Assert.Contains("Код выхода: 1", report, StringComparison.Ordinal);
        Assert.Contains("HttpRequestException: SingularityApp ответил 400", report, StringComparison.Ordinal);
        Assert.Contains("строка вывода 2", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ссылка открывает новую сессию в папке репозитория, папка и текст закодированы для адреса
    /// </summary>
    [Fact]
    public void The_url_opens_a_new_code_session_with_an_encoded_folder_and_prompt()
    {
        var url = ClaudeFixLauncher.BuildUrl("Почини & проверь", @"D:\My Repo");

        Assert.StartsWith("claude://code/new?folder=", url, StringComparison.Ordinal);
        Assert.Contains("folder=D%3A%5CMy%20Repo", url, StringComparison.Ordinal);
        Assert.Contains("&q=" + Uri.EscapeDataString("Почини & проверь"), url, StringComparison.Ordinal);
    }
}