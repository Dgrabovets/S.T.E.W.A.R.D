using System.Text.Json.Nodes;
using Steward.Hosting;
using Steward.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Плашки состояния настроек на «Статусе» и переключатель «Записывать в Singularity»: ключи читаются без учёта регистра, файл не меняется без нажатия «Сохранить»
/// </summary>
public class SettingsProbeTests
{
    /// <summary>
    /// Настоящий файл бота пишет ключи с большой буквы: владелец, браузер и запись в Singularity видны так же, как при ключах с маленькой
    /// </summary>
    /// <param name="owner">Как записан ключ номера владельца</param>
    /// <param name="browser">Как записан ключ браузера</param>
    /// <param name="create">Как записан ключ записи в Singularity</param>
    [Theory]
    [InlineData("AllowedUserId", "CookiesFromBrowser", "CreateTasks")]
    [InlineData("allowedUserId", "cookiesFromBrowser", "createTasks")]
    public void Keys_are_read_in_any_case(string owner, string browser, string create)
    {
        var root = Directory.CreateTempSubdirectory("steward-probe-");
        try
        {
            var profiles = Directory.CreateDirectory(Path.Combine(root.FullName, "Profiles")).FullName;
            Write(SettingsProbe.ReelsFile(root.FullName), $$"""{"BotToken": "t", "{{owner}}": 123456789, "{{browser}}": "firefox"}""");
            Write(SettingsProbe.LmsFile(root.FullName), $$"""{"Login": "l", "Password": "p", "{{create}}": true}""");

            var reels = SettingsProbe.Reels(root.FullName, profiles);
            var lms = SettingsProbe.Transcripts(root.FullName);

            Assert.Equal(("Владелец бота", "задан"), (reels[1].Label, reels[1].Value));
            Assert.Equal(("Вход в Instagram", "firefox", PanelToneEnum.Running), (reels[2].Label, reels[2].Value, reels[2].Tone));
            Assert.Equal(("Запись в Singularity", "включена"), (lms[2].Label, lms[2].Value));
            Assert.Equal("задан", lms[0].Value);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Нулевой или нечисловой владелец не задан, Firefox без папки профилей даёт «нет профиля», другой браузер профиля не требует, пустой браузер — «не настроен»
    /// </summary>
    [Fact]
    public void Owner_and_browser_edge_cases()
    {
        var root = Directory.CreateTempSubdirectory("steward-probe-");
        try
        {
            var missing = Path.Combine(root.FullName, "НетПрофилей");

            Write(SettingsProbe.ReelsFile(root.FullName), """{"AllowedUserId": 0, "CookiesFromBrowser": "firefox"}""");
            var zero = SettingsProbe.Reels(root.FullName, missing);
            Assert.Equal("не задан", zero[1].Value);
            Assert.Equal(("нет профиля firefox", PanelToneEnum.Attention), (zero[2].Value, zero[2].Tone));

            Write(SettingsProbe.ReelsFile(root.FullName), """{"AllowedUserId": "123", "CookiesFromBrowser": "chrome"}""");
            var text = SettingsProbe.Reels(root.FullName, missing);
            Assert.Equal("не задан", text[1].Value);
            Assert.Equal(("chrome", PanelToneEnum.Running), (text[2].Value, text[2].Tone));

            Write(SettingsProbe.ReelsFile(root.FullName), """{"AllowedUserId": 5, "CookiesFromBrowser": ""}""");
            var empty = SettingsProbe.Reels(root.FullName, missing);
            Assert.Equal("задан", empty[1].Value);
            Assert.Equal("не настроен", empty[2].Value);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Запись в Singularity выключена, если ключа нет, он false или записан строкой
    /// </summary>
    /// <param name="json">Содержимое файла настроек LMS</param>
    [Theory]
    [InlineData("""{"Login": "l"}""")]
    [InlineData("""{"CreateTasks": false}""")]
    [InlineData("""{"CreateTasks": "true"}""")]
    public void Singularity_writing_is_off_unless_the_flag_is_true(string json)
    {
        var root = Directory.CreateTempSubdirectory("steward-probe-");
        try
        {
            Write(SettingsProbe.LmsFile(root.FullName), json);

            var chip = SettingsProbe.Transcripts(root.FullName)[2];

            Assert.Equal(("выключена", PanelToneEnum.Attention), (chip.Value, chip.Tone));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Переключатель читает CreateTasks, не меняет файл при открытии, а при сохранении пишет настоящий true и не трогает остальные ключи
    /// </summary>
    [Fact]
    public void The_toggle_reads_and_writes_a_real_boolean()
    {
        var directory = Directory.CreateTempSubdirectory("steward-toggle-");
        try
        {
            var file = Path.Combine(directory.FullName, "lmssettings.json");
            const string ORIGINAL = """{"Login": "x", "Password": "секрет", "CreateTasks": false, "MaxFetchPerRun": 6}""";
            File.WriteAllText(file, ORIGINAL);
            var toggle = new SettingFieldViewModel("CreateTasks", "Записывать в Singularity", string.Empty, toggle: true);
            var settings = new JsonSettingsViewModel(file, string.Empty, [new SettingsSectionViewModel("SingularityApp", [toggle, new SettingFieldViewModel("MaxFetchPerRun", "Занятий", string.Empty, number: true)])], () => { });

            Assert.False(toggle.IsOn);
            Assert.False(toggle.IsPlain);
            Assert.Equal(ORIGINAL, File.ReadAllText(file));

            toggle.IsOn = true;
            settings.SaveCommand.Execute(null);

            var written = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            Assert.Equal(System.Text.Json.JsonValueKind.True, written["CreateTasks"]!.GetValueKind());
            Assert.Equal("секрет", (string?)written["Password"]);
            Assert.Equal(6, (int?)written["MaxFetchPerRun"]);
            Assert.DoesNotContain(written, pair => pair.Key == "createTasks");

            settings.Load();
            Assert.True(toggle.IsOn);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Если ключа CreateTasks в файле нет, сохранение других полей его не добавляет: выключенный по умолчанию флаг остаётся отсутствующим
    /// </summary>
    [Fact]
    public void A_missing_flag_stays_missing_after_saving_other_fields()
    {
        var directory = Directory.CreateTempSubdirectory("steward-toggle-");
        try
        {
            var file = Path.Combine(directory.FullName, "lmssettings.json");
            File.WriteAllText(file, """{"Login": "x", "MaxFetchPerRun": 6}""");
            var toggle = new SettingFieldViewModel("CreateTasks", "Записывать в Singularity", string.Empty, toggle: true);
            var number = new SettingFieldViewModel("MaxFetchPerRun", "Занятий", string.Empty, number: true);
            var settings = new JsonSettingsViewModel(file, string.Empty, [new SettingsSectionViewModel("Выгрузка", [toggle, number])], () => { });

            number.Value = "4";
            settings.SaveCommand.Execute(null);

            var written = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            Assert.False(written.ContainsKey("CreateTasks"));
            Assert.Equal(4, (int?)written["MaxFetchPerRun"]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// В настройках LMS есть переключатель «Записывать в Singularity» на ключе CreateTasks, а «Программы для MD-файлов» больше нет
    /// </summary>
    [Fact]
    public void Lms_settings_have_the_toggle_and_no_md_editor()
    {
        var root = Directory.CreateTempSubdirectory("steward-lms-settings-");
        try
        {
            var model = new MainViewModel();
            model.AttachSettings(root.FullName, () => { });

            var fields = model.LmsSettings!.Sections.SelectMany(section => section.Fields).ToArray();

            var toggle = Assert.Single(fields, field => field.IsToggle);
            Assert.Equal(("CreateTasks", "Записывать в Singularity"), (toggle.Path, toggle.Title));
            Assert.DoesNotContain(fields, field => field.Path == "MdEditor");
            Assert.False(File.Exists(SettingsProbe.LmsFile(root.FullName)));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Записывает файл настроек, создавая папку
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <param name="json">Содержимое</param>
    private static void Write(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }
}
