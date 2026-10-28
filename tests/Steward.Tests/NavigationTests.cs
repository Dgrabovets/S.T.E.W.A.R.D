using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет левую панель единого окна: группы вкладок, системные вкладки внизу и переключение страниц
/// </summary>
public class NavigationTests
{
    /// <summary>
    /// Вкладки стоят по группам «Моя жизнь» и «Учёба», а «Статус» и «Журнал» лежат отдельно внизу
    /// </summary>
    [Fact]
    public void Tabs_are_grouped_by_meaning_with_system_tabs_apart()
    {
        var model = new MainViewModel();

        Assert.Equal(["Моя жизнь", "Учёба"], model.NavGroups.Select(group => group.Title).ToArray());
        Assert.Equal(["Почта", "Рилсы"], model.NavGroups[0].Items.Select(item => item.Title).ToArray());
        Assert.Equal(["Расписание", "Расшифровки", "Домашние задания"], model.NavGroups[1].Items.Select(item => item.Title).ToArray());
        Assert.Equal(["Статус", "Журнал"], model.SystemItems.Select(item => item.Title).ToArray());
    }

    /// <summary>
    /// Выбор вкладки открывает её страницу и снимает отметку с прежней
    /// </summary>
    [Fact]
    public void Picking_a_tab_opens_its_page_and_clears_the_previous_one()
    {
        var model = new MainViewModel();
        var transcripts = model.NavGroups[1].Items.Single(item => item.Page == AppPageEnum.Transcripts);
        var status = model.SystemItems.Single(item => item.Page == AppPageEnum.Status);

        Assert.True(status.IsSelected);

        transcripts.IsSelected = true;

        Assert.Equal(AppPageEnum.Transcripts, model.Page);
        Assert.True(model.IsTranscriptsPage);
        Assert.True(transcripts.IsSelected);
        Assert.False(status.IsSelected);
    }
}
