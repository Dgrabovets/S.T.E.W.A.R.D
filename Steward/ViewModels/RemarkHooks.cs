using Steward.Homework.Models;

namespace Steward.ViewModels;

/// <summary>
/// Как вкладка «Замечания» читает и меняет замечания владельца. Каждый вызов может ждать блокировку файла до 10 секунд, поэтому вкладка делает их в фоновой задаче
/// </summary>
/// <param name="Load">Читает все замечания: общие и всех предметов</param>
/// <param name="Update">Заменяет замечание с тем же ключом; при смене области переносит его в другой файл</param>
/// <param name="Remove">Удаляет замечание по ключу</param>
public sealed record RemarkHooks(Func<IReadOnlyList<Remark>> Load, Action<Remark> Update, Action<string> Remove);
