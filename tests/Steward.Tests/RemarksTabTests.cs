using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет вкладку «Замечания» окна «Методические указания» и пометки образцов
/// </summary>
public class RemarksTabTests
{
    /// <summary>
    /// Временная папка замечаний с тремя замечаниями: два предметных (одно выключено) и одно общее
    /// </summary>
    /// <returns>Хранилище и папка, которую нужно удалить после теста</returns>
    private static (RemarkStore Store, string Folder) NewStore()
    {
        var folder = Path.Combine(Path.GetTempPath(), "steward-remarks-" + Guid.NewGuid().ToString("N"));
        var store = new RemarkStore(folder);
        store.Add(new Remark("r1", new DateOnly(2026, 10, 20), "Гейм-дизайн", "gd-1", 1, "Больше примеров из игр", RemarkKindEnum.Content, false));
        store.Add(new Remark("r2", new DateOnly(2026, 10, 22), "Гейм-дизайн", "gd-2", 0, "Шрифт Times 14", RemarkKindEnum.Format, false, Enabled: false));
        store.Add(new Remark("r3", new DateOnly(2026, 10, 25), "Базы данных", "db-1", 0, "Без канцелярита", RemarkKindEnum.Style, true));

        return (store, folder);
    }

    /// <summary>
    /// Окно над хранилищем замечаний: материалов нет
    /// </summary>
    /// <param name="store">Хранилище замечаний</param>
    /// <returns>Модель окна с прочитанными замечаниями</returns>
    private static async Task<GuidesDialogViewModel> Dialog(RemarkStore store)
    {
        var model = new GuidesDialogViewModel(string.Empty, _ => [], @"D:\data\guides", _ => { }, _ => { }, (_, _) => { }, (_, _, _) => string.Empty, (_, _, _) => string.Empty, (_, _, _) => Task.FromResult(string.Empty),
            new RemarkHooks(store.LoadAll, store.Update, store.Remove));
        await model.WhenRemarksIdle();

        return model;
    }

    /// <summary>
    /// Удаляет временную папку теста
    /// </summary>
    /// <param name="folder">Папка</param>
    private static void Clean(string folder)
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    /// <summary>
    /// Список показывает предметные и общие замечания, новые выше, с видом, областью и датой; вкладка видна и без замечаний пишет, откуда они берутся
    /// </summary>
    [Fact]
    public async Task The_tab_lists_subject_and_global_remarks()
    {
        var (store, folder) = NewStore();
        try
        {
            var model = await Dialog(store);
            model.ShowRemarksCommand.Execute(null);

            Assert.True(model.IsRemarksTab);
            Assert.False(model.IsGuidesTab);
            Assert.False(model.CanAddFiles);
            Assert.True(model.HasRemarksTab);
            Assert.Equal("Замечания · 3", model.RemarksTitle);
            Assert.Equal(["r3", "r2", "r1"], model.Remarks.Select(row => row.Id).ToArray());
            var global = model.Remarks[0];
            Assert.Equal(("Без канцелярита", "стиль", "Все предметы", true, "25.10.2026"), (global.Text, global.KindTitle, global.ScopeTitle, global.IsGlobal, global.DateText));
            Assert.Equal(("оформление", "Гейм-дизайн", false), (model.Remarks[1].KindTitle, model.Remarks[1].ScopeTitle, model.Remarks[1].IsEnabled));

            var empty = new GuidesDialogViewModel(string.Empty, _ => [], @"D:\data\guides", _ => { }, _ => { }, (_, _) => { }, (_, _, _) => string.Empty, (_, _, _) => string.Empty, (_, _, _) => Task.FromResult(string.Empty),
                new RemarkHooks(() => [], _ => { }, _ => { }));
            await empty.WhenRemarksIdle();
            Assert.True(empty.RemarksEmpty);

            var none = new GuidesDialogViewModel(string.Empty, _ => [], @"D:\data\guides", _ => { }, _ => { }, (_, _) => { }, (_, _, _) => string.Empty, (_, _, _) => string.Empty, (_, _, _) => Task.FromResult(string.Empty));
            Assert.False(none.HasRemarksTab);
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Отключение, правка текста, смена области и удаление записываются в хранилище
    /// </summary>
    [Fact]
    public async Task Toggle_edit_scope_change_and_delete_are_saved()
    {
        var (store, folder) = NewStore();
        try
        {
            var model = await Dialog(store);
            var first = model.Remarks.Single(row => row.Id == "r1");

            first.IsEnabled = false;
            await model.WhenRemarksIdle();
            Assert.False(store.Load("Гейм-дизайн").Single(remark => remark.Id == "r1").Enabled);

            first = model.Remarks.Single(row => row.Id == "r1");
            first.Text = "Больше примеров из настольных игр";
            Assert.True(first.IsDirty);
            first.SaveCommand.Execute(null);
            await model.WhenRemarksIdle();
            Assert.Equal("Больше примеров из настольных игр", store.Load("Гейм-дизайн").Single(remark => remark.Id == "r1").Text);
            Assert.False(model.Remarks.Single(row => row.Id == "r1").IsDirty);

            // Пустой текст не сохраняется
            first = model.Remarks.Single(row => row.Id == "r1");
            first.Text = "   ";
            first.SaveCommand.Execute(null);
            await model.WhenRemarksIdle();
            Assert.Equal("Больше примеров из настольных игр", store.Load("Гейм-дизайн").Single(remark => remark.Id == "r1").Text);
            Assert.True(model.HasMessage);

            // Область: в общий файл и обратно
            var second = model.Remarks.Single(row => row.Id == "r2");
            second.IsGlobal = true;
            await model.WhenRemarksIdle();
            Assert.DoesNotContain(store.Load("Гейм-дизайн"), remark => remark.Id == "r2");
            Assert.True(store.LoadAll().Single(remark => remark.Id == "r2").Global);
            Assert.Equal("Все предметы", model.Remarks.Single(row => row.Id == "r2").ScopeTitle);

            model.Remarks.Single(row => row.Id == "r2").IsGlobal = false;
            await model.WhenRemarksIdle();
            Assert.Contains(store.Load("Гейм-дизайн"), remark => remark.Id == "r2");

            model.Remarks.Single(row => row.Id == "r3").RemoveCommand.Execute(null);
            await model.WhenRemarksIdle();
            Assert.DoesNotContain(store.LoadAll(), remark => remark.Id == "r3");
            Assert.Equal(["r2", "r1"], model.Remarks.Select(row => row.Id).ToArray());
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Файл замечаний занят: владелец видит сообщение, список и переключатели остаются прежними, а набранный текст не пропадает
    /// </summary>
    [Fact]
    public async Task A_locked_store_shows_a_message_and_keeps_the_list_and_the_edit()
    {
        var (store, folder) = NewStore();
        try
        {
            var model = await Dialog(store);
            var subjectFile = Path.Combine(folder, RemarkStore.SafeName("Гейм-дизайн") + ".json");
            var row = model.Remarks.Single(item => item.Id == "r1");

            using (new FileStream(subjectFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                row.IsEnabled = false;
                await model.WhenRemarksIdle();
                Assert.True(row.IsEnabled);
                Assert.Contains("Не удалось", model.Message, StringComparison.Ordinal);

                row.Text = "Новый текст";
                row.SaveCommand.Execute(null);
                await model.WhenRemarksIdle();
                Assert.Equal("Новый текст", row.Text);
                Assert.True(row.IsDirty);

                row.IsGlobal = true;
                await model.WhenRemarksIdle();
                Assert.False(row.IsGlobal);

                row.RemoveCommand.Execute(null);
                await model.WhenRemarksIdle();

                Assert.Equal(["r3", "r2", "r1"], model.Remarks.Select(item => item.Id).ToArray());
                Assert.Same(row, model.Remarks.Single(item => item.Id == "r1"));
            }

            var saved = store.Load("Гейм-дизайн").Single(remark => remark.Id == "r1");
            Assert.Equal(("Больше примеров из игр", true, false), (saved.Text, saved.Enabled, saved.Global));
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Образцы из сданных работ помечены «сдано» или «сдано, файл указан вручную», ручные образцы из «Обзора» без пометки; отключение и удаление работают как раньше
    /// </summary>
    [Fact]
    public void Samples_are_marked_submitted_or_manual()
    {
        var entries = new List<GuideEntry>
        {
            new("sample:gd-1:1", "Гейм-дизайн", GuideKindEnum.Example, "gd-1-1.docx", @"D:\data\homework\samples\Гейм-дизайн\gd-1-1.docx", string.Empty, string.Empty),
            new("sample:gd-2:1", "Гейм-дизайн", GuideKindEnum.Example, "gd-2-1.docx", @"D:\data\homework\samples\Гейм-дизайн\gd-2-1.docx", string.Empty, string.Empty, Manual: true),
            new("e1", "Гейм-дизайн", GuideKindEnum.Example, "Образец.docx", @"E:\Образец.docx", string.Empty, string.Empty),
        };
        var enabled = new List<(string, bool)>();
        var model = new GuidesDialogViewModel(string.Empty, _ => entries, @"D:\data\homework\guides", _ => { }, id => entries.RemoveAll(entry => entry.Id == id), (id, value) => enabled.Add((id, value)), (_, _, _) => string.Empty, (_, _, _) => string.Empty, (_, _, _) => Task.FromResult(string.Empty));

        Assert.Equal(["Сдано · Гейм-дизайн", "Сдано · Гейм-дизайн · файл указан вручную", string.Empty], model.Examples.Select(row => row.Mark).ToArray());
        Assert.Equal([true, true, false], model.Examples.Select(row => row.HasMark).ToArray());

        model.Examples[0].IsEnabled = false;
        model.Examples[1].RemoveCommand.Execute(null);

        Assert.Equal([("sample:gd-1:1", false)], enabled);
        Assert.Equal(["sample:gd-1:1", "e1"], model.Examples.Select(row => row.Id).ToArray());
    }
}
