using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет, что дописывание заметки в Singularity не стирает написанное пользователем
/// </summary>
public class NoteMergeTests
{
    /// <summary>
    /// Старый текст остаётся, новый идёт после пустой строки; повтор не дублируется; пустая или битая старая заметка заменяется новой
    /// </summary>
    [Fact]
    public void A_new_note_is_appended_to_the_old_one()
    {
        const string Old = """[{"insert":"моя запись\n"}]""";
        const string Added = """[{"insert":"работа готова\n"}]""";

        var merged = SingularityClient.MergeNote(Old, Added);

        Assert.StartsWith("""[{"insert":"моя запись""", merged, StringComparison.Ordinal);
        Assert.Contains("работа готова", merged, StringComparison.Ordinal);
        Assert.Equal(Added, SingularityClient.MergeNote(string.Empty, Added));
        Assert.Equal(Added, SingularityClient.MergeNote("не json", Added));
        Assert.Equal(merged, SingularityClient.MergeNote(merged, Added));
    }
}
