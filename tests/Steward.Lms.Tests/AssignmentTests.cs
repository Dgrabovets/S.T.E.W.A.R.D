using Steward.Lms.Models;
using Steward.Lms.Services;

namespace Steward.Lms.Tests;

/// <summary>
/// Проверяет разбор страницы задания LMS: текст условия, прикреплённые файлы и даты, а также описание задания для агента
/// </summary>
public class AssignmentTests
{
    /// <summary>
    /// Страница задания в том виде, в каком её отдаёт Moodle: описание, файл с условием и даты
    /// </summary>
    private const string Page = """
        <div class="activity-dates"><div><strong>Открыто с:</strong> суббота, 3 октября 2026, 00:00</div><div><strong>Срок сдачи:</strong> пятница, 23 октября 2026, 23:59</div></div>
        <div class="activity-description" id="intro">
            <div class="box py-3 generalbox boxaligncenter"><div class="no-overflow"><p>Разработка интерфейса с применением &laquo;UGUI&raquo;.</p><p>Соберите меню и окно настроек.</p></div><div id="assign_files_tree6ae25"><ul><li><div><div class="fileuploadsubmission"><a target="_blank" href="https://lms.ranepa.ru/pluginfile.php/1722066/mod_assign/introattachment/0/%D0%94%D0%97_UNITY_2.pdf?forcedownload=1">ДЗ_UNITY_2.pdf</a></div></div></li></ul></div></div>
        </div>
        <div role="main"><div class="submissionstatustable"></div></div>
        """;

    /// <summary>
    /// Условие читается абзацами без тегов и без хвоста разметки, файл берётся со ссылки на pluginfile, даты собираются строками
    /// </summary>
    [Fact]
    public void The_assignment_page_gives_text_files_and_dates()
    {
        var details = AssignmentParser.Parse(Page);

        Assert.Equal("Разработка интерфейса с применением «UGUI».\nСоберите меню и окно настроек.", details.Text);
        var file = Assert.Single(details.Files);
        Assert.Equal("ДЗ_UNITY_2.pdf", file.Name);
        Assert.StartsWith("https://lms.ranepa.ru/pluginfile.php/", file.Url, StringComparison.Ordinal);
        Assert.Equal(["Открыто с: суббота, 3 октября 2026, 00:00", "Срок сдачи: пятница, 23 октября 2026, 23:59"], details.Dates.ToArray());
    }

    /// <summary>
    /// Страница без блока описания даёт пустой результат, а не ошибку
    /// </summary>
    [Fact]
    public void A_page_without_a_description_is_empty()
    {
        var details = AssignmentParser.Parse("<html><body>Пусто</body></html>");

        Assert.Empty(details.Text);
        Assert.Empty(details.Files);
    }

    /// <summary>
    /// Отпечаток условия не меняется от повторного чтения и меняется, когда преподаватель поправил текст или срок
    /// </summary>
    [Fact]
    public void The_fingerprint_changes_only_when_the_assignment_changes()
    {
        var first = AssignmentSync.Fingerprint(AssignmentParser.Parse(Page));

        Assert.Equal(first, AssignmentSync.Fingerprint(AssignmentParser.Parse(Page)));
        Assert.NotEqual(first, AssignmentSync.Fingerprint(AssignmentParser.Parse(Page.Replace("Соберите", "Нарисуйте", StringComparison.Ordinal))));
        Assert.NotEqual(first, AssignmentSync.Fingerprint(AssignmentParser.Parse(Page.Replace("23 октября", "30 октября", StringComparison.Ordinal))));
    }

    /// <summary>
    /// Описание задания для агента содержит предмет, срок, условие и пути к файлам
    /// </summary>
    [Fact]
    public void The_description_has_the_subject_the_due_date_the_text_and_the_files()
    {
        var activity = new LmsActivity("1377862", ActivityKindEnum.Assignment, 1, "Разработка компьютерных игр на Unity", "Тема 2", "Задание 2. Разработка UI.", new DateTime(2026, 10, 23), "Ответ не представлен", string.Empty, false, "https://lms.ranepa.ru/mod/assign/view.php?id=1377862");

        var text = AssignmentSync.Describe(activity, AssignmentParser.Parse(Page), [("ДЗ_UNITY_2.pdf", @"D:\data\assignments\1377862\ДЗ_UNITY_2.pdf")]);

        Assert.Contains("# Задание 2. Разработка UI.", text, StringComparison.Ordinal);
        Assert.Contains("Предмет: Разработка компьютерных игр на Unity", text, StringComparison.Ordinal);
        Assert.Contains("Срок сдачи: 23.10.2026", text, StringComparison.Ordinal);
        Assert.Contains("Соберите меню и окно настроек.", text, StringComparison.Ordinal);
        Assert.Contains(@"- ДЗ_UNITY_2.pdf: D:\data\assignments\1377862\ДЗ_UNITY_2.pdf", text, StringComparison.Ordinal);
    }
}
