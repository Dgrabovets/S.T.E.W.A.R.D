using Steward.Mail.Common;
using Steward.Mail.Models;
using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет, какие задачи получаются из писем
/// </summary>
public class MailTaskBuilderTests
{
    /// <summary>
    /// Начало дня, с которого начинаются задачи, со смещением часового пояса
    /// </summary>
    private static readonly DateTimeOffset Today = new(2026, 10, 25, 0, 0, 0, TimeSpan.FromHours(1));

    /// <summary>
    /// Настройки с проектом назначения
    /// </summary>
    private static readonly AppSettings Settings = new() { Singularity = new SingularitySettings { ProjectId = "P-1" } };

    /// <summary>
    /// Отзыв называется по имени преподавателя и заданию из темы, приоритет обычный
    /// </summary>
    [Fact]
    public void Feedback_takes_the_teacher_and_the_assignment_from_the_subject()
    {
        var mail = Samples.Mail("1", subject: "Пользователь Петров Пётр Петрович дал отзыв на Ваш ответ на задание «Задание 2».");

        var task = MailTaskBuilder.Build(mail, MailCategoryEnum.TeacherFeedback, Settings, Today)!;

        Assert.Equal("Отзыв преподавателя (Петров П. П.): «Задание 2» — посмотреть", task.Title);
        Assert.Equal(1, task.Priority);
        Assert.Equal("2026-10-25T00:00:00+01:00", task.Start);
        Assert.Equal("P-1", task.ProjectId);
    }

    /// <summary>
    /// Если тема не похожа на отзыв, в названии остаётся сама тема
    /// </summary>
    [Fact]
    public void Feedback_with_an_unknown_subject_keeps_the_subject()
    {
        var task = MailTaskBuilder.Build(Samples.Mail("1", subject: "Оценка выставлена"), MailCategoryEnum.TeacherFeedback, Settings, Today)!;

        Assert.Equal("Отзыв преподавателя — посмотреть: Оценка выставлена", task.Title);
    }

    /// <summary>
    /// Личное письмо даёт задачу с высоким приоритетом, длинная тема обрезается
    /// </summary>
    [Fact]
    public void A_personal_reply_gets_a_high_priority_and_a_truncated_subject()
    {
        var mail = Samples.Mail("1", subject: new string('я', 120));

        var task = MailTaskBuilder.Build(mail, MailCategoryEnum.PersonalReply, Settings, Today)!;

        Assert.StartsWith("Пришёл ответ от Иванов И. И., нужно ответить: «", task.Title, StringComparison.Ordinal);
        Assert.EndsWith("…»", task.Title, StringComparison.Ordinal);
        Assert.Equal(0, task.Priority);
    }

    /// <summary>
    /// Начало задачи записано датой, временем и смещением, как требует API, а время у задачи выключено. Дата без времени даёт ответ 400
    /// </summary>
    [Fact]
    public void The_start_has_a_time_and_an_explicit_offset_and_the_task_has_no_time()
    {
        var task = MailTaskBuilder.Build(Samples.Mail("1"), MailCategoryEnum.PersonalReply, Settings, Today)!;

        Assert.Matches("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}[+-]\\d{2}:\\d{2}$", task.Start);
        Assert.False(task.UseTime);
    }

    /// <summary>
    /// У задачи есть внешний ключ письма, по нему SingularityApp узнаёт, что задача уже создана
    /// </summary>
    [Fact]
    public void Every_task_carries_the_external_key_of_its_mail()
    {
        var mail = Samples.Mail("<abc@mail.test>");

        Assert.Equal("mail:<abc@mail.test>", MailTaskBuilder.Build(mail, MailCategoryEnum.PersonalReply, Settings, Today)!.ExternalId);
        Assert.Equal("mail:<abc@mail.test>", MailTaskBuilder.Build(mail, MailCategoryEnum.TeacherFeedback, Settings, Today)!.ExternalId);
    }
    /// <summary>
    /// Справочное письмо задачи не создаёт
    /// </summary>
    [Fact]
    public void Info_creates_no_task() =>
        Assert.Null(MailTaskBuilder.Build(Samples.Mail("1"), MailCategoryEnum.Info, Settings, Today));

    /// <summary>
    /// Заметка записана дельтой Quill, как в расписании. В ней нет отправителя и темы (они уже в названии), есть время получения, текст собеседника без цитаты, линия и ссылка «Письмо» с подписью
    /// </summary>
    [Fact]
    public void The_note_is_a_delta_with_the_time_the_reply_a_divider_and_a_labelled_letter_link()
    {
        var mail = Samples.Mail("1", subject: "Re: Вопрос про зачёт", body: "Зачёт перенесён на пятницу.\n\n25.10.2026, 11:19, \"Данила\" <student@example.com>:\n> когда зачёт?");

        var note = MailTaskBuilder.Build(mail, MailCategoryEnum.PersonalReply, Settings, Today)!.Note!;
        var visible = NoteOps.Visible(note);

        Assert.StartsWith("Получено: ", visible, StringComparison.Ordinal);
        Assert.Contains("Зачёт перенесён на пятницу.", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("когда зачёт", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("ivanov@example.com", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("От:", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("Тема:", visible, StringComparison.Ordinal);
        Assert.Equal(1, NoteOps.Dividers(note));
        Assert.Equal([("Письмо", $"https://mail.360.yandex.ru/#search?request={Uri.EscapeDataString("Вопрос про зачёт")}")], NoteOps.Links(note));
        Assert.EndsWith("Письмо\n", visible, StringComparison.Ordinal);
    }

    /// <summary>
    /// В заметке отзыва ссылка «Задание» на LMS стоит перед ссылкой «Письмо», а текста письма нет
    /// </summary>
    [Fact]
    public void A_feedback_note_links_to_the_assignment_before_the_letter_and_skips_the_body()
    {
        var mail = Samples.Mail("1", body: "Пользователь Петров дал отзыв. Откройте https://lms.ranepa.ru/mod/assign/view.php?id=1323404 чтобы увидеть.");

        var note = MailTaskBuilder.Build(mail, MailCategoryEnum.TeacherFeedback, Settings, Today)!.Note!;

        Assert.Equal(["Задание", "Письмо"], NoteOps.Links(note).Select(link => link.Label).ToArray());
        Assert.Equal("https://lms.ranepa.ru/mod/assign/view.php?id=1323404", NoteOps.Links(note)[0].Url);
        Assert.DoesNotContain("Пользователь Петров", NoteOps.Visible(note), StringComparison.Ordinal);
    }}