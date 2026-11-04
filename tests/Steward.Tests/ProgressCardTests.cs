using Steward.Status.Progress;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет блок «Что сейчас выполняется» на карточке процесса: когда он показывается и что в нём написано
/// </summary>
public class ProgressCardTests
{
    /// <summary>
    /// Шаги запуска
    /// </summary>
    private static readonly string[] Stages = ["Занятия", "Расшифровки", "Разбор", "Задания", "Работы", "Заметки"];

    /// <summary>
    /// Текущее время в тестах
    /// </summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Строка называет шаг, счётчик и что обрабатывается, пройденные шаги отмечены, доля считается по всему запуску, время идёт от начала
    /// </summary>
    [Fact]
    public void The_block_names_the_stage_the_counter_and_the_lesson()
    {
        var progress = new RunProgress(Stages, 2, 1, 3, "Гейм-дизайн, 25.10.2026", Now.AddMinutes(-4), Now);

        var card = new ProgressCardViewModel(progress, Now);

        Assert.Equal("Разбор: 2 из 3, Гейм-дизайн, 25.10.2026", card.Line);
        Assert.Equal("идёт 4 мин", card.Elapsed);
        Assert.Equal([true, true, false, false, false, false], card.Steps.Select(step => step.IsPassed).ToArray());
        Assert.Equal("Разбор", card.Steps.Single(step => step.IsCurrent).Title);
        Assert.Equal((2 + 1.0 / 3) / 6, card.Share, 6);
    }

    /// <summary>
    /// Без счётчика строка пишет только шаг и пояснение, а счётчик не вылезает за общее число, пока шаг дорабатывает последнюю единицу
    /// </summary>
    [Fact]
    public void Without_a_counter_only_the_stage_and_detail_are_written_and_the_counter_never_exceeds_the_total()
    {
        Assert.Equal("Занятия, Список занятий в LMS", new ProgressCardViewModel(new RunProgress(Stages, 0, 0, 0, "Список занятий в LMS", Now, Now), Now).Line);
        Assert.Equal("Расшифровки: 2 из 2, Лекция", new ProgressCardViewModel(new RunProgress(Stages, 1, 2, 2, "Лекция", Now, Now), Now).Line);
        Assert.Equal("меньше минуты", new ProgressCardViewModel(new RunProgress(Stages, 0, 0, 0, string.Empty, Now, Now), Now).Elapsed.Replace("идёт ", string.Empty, StringComparison.Ordinal));
        Assert.Equal("идёт 1 ч 05 мин", new ProgressCardViewModel(new RunProgress(Stages, 0, 0, 0, string.Empty, Now.AddMinutes(-65), Now), Now).Elapsed);
    }

    /// <summary>
    /// Блок показывается, только если процесс правда работает и файл свежий: оборвавшийся запуск не оставляет вечный прогресс
    /// </summary>
    [Fact]
    public void The_block_is_shown_only_for_a_working_process_with_a_fresh_file()
    {
        var fresh = new RunProgress(Stages, 1, 0, 0, string.Empty, Now.AddMinutes(-3), Now.AddMinutes(-1));
        var stale = fresh with { Updated = Now.AddMinutes(-(ProgressCardViewModel.FRESH_MINUTES + 1)) };

        Assert.NotNull(ProgressCardViewModel.Create(fresh, working: true, Now));
        Assert.Null(ProgressCardViewModel.Create(fresh, working: false, Now));
        Assert.Null(ProgressCardViewModel.Create(stale, working: true, Now));
        Assert.Null(ProgressCardViewModel.Create(null, working: true, Now));
    }
}
