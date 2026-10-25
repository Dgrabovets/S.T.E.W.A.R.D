namespace Steward.Reels.Models;

/// <summary>
/// Скачанный рилс: файл со звуком и то, что известно о ролике
/// </summary>
/// <param name="MediaPath">Путь к скачанному файлу</param>
/// <param name="Title">Название ролика, если оно есть</param>
/// <param name="Author">Автор или пустая строка</param>
/// <param name="Caption">Подпись под рилсом или пустая строка</param>
public sealed record DownloadedReel(string MediaPath, string Title, string Author, string Caption);