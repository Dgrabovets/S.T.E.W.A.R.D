using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет сокращение имён и обрезку текста
/// </summary>
public class MailTextTests
{
    /// <summary>
    /// Фамилия остаётся, имя и отчество становятся инициалами, лишние слова отбрасываются
    /// </summary>
    [Theory]
    [InlineData("Иванов Иван Иванович", "Иванов И. И.")]
    [InlineData("Иванов Иван", "Иванов И.")]
    [InlineData("Иванов Иван Иванович Младший", "Иванов И. И.")]
    public void ShortName_keeps_the_surname_and_initials(string fullName, string expected) =>
        Assert.Equal(expected, MailText.ShortName(fullName));

    /// <summary>
    /// Адрес и имя из одного слова не меняются
    /// </summary>
    [Theory]
    [InlineData("noreply@lms.example.com")]
    [InlineData("Иванов")]
    [InlineData("Иван Иванов <ivan@example.com>")]
    public void ShortName_leaves_an_address_and_a_single_word_alone(string value) =>
        Assert.Equal(value, MailText.ShortName(value));

    /// <summary>
    /// Короткий текст остаётся как есть, длинный обрезается с многоточием без хвостового пробела
    /// </summary>
    [Fact]
    public void Truncate_cuts_only_long_text_and_trims_the_tail()
    {
        Assert.Equal("коротко", MailText.Truncate("коротко", 10));
        Assert.Equal("один два…", MailText.Truncate("один два три", 9));
    }
}