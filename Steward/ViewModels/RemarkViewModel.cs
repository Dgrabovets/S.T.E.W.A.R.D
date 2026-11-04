using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Строка вкладки «Замечания»: текст для правки, вид, область, дата, галочки «учитывать» и «для всех предметов», кнопки «Сохранить» и «Удалить». Галочки меняют запись сразу, если запись не удалась, вкладка возвращает их назад
/// </summary>
public sealed class RemarkViewModel : Bindable
{
    /// <summary>
    /// Просит записать новое содержимое замечания: строка и новое содержимое
    /// </summary>
    private readonly Action<RemarkViewModel, Remark> _change;

    /// <summary>
    /// Последнее записанное содержимое
    /// </summary>
    private Remark _saved;

    /// <summary>
    /// Текст в поле правки
    /// </summary>
    private string _text;

    /// <summary>
    /// Галочка «учитывать»
    /// </summary>
    private bool _isEnabled;

    /// <summary>
    /// Галочка «для всех предметов»
    /// </summary>
    private bool _isGlobal;

    /// <summary>
    /// Идёт запись этого замечания
    /// </summary>
    private bool _isBusy;

    /// <summary>
    /// Создаёт строку
    /// </summary>
    /// <param name="remark">Записанное замечание</param>
    /// <param name="change">Просит записать новое содержимое</param>
    /// <param name="remove">Просит удалить замечание</param>
    public RemarkViewModel(Remark remark, Action<RemarkViewModel, Remark> change, Action<RemarkViewModel> remove)
    {
        _saved = remark;
        _text = remark.Text;
        _isEnabled = remark.Enabled;
        _isGlobal = remark.Global;
        _change = change;
        SaveCommand = new RelayCommand(() => _change(this, _saved with { Text = Text }));
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    /// <summary>
    /// Ключ замечания
    /// </summary>
    public string Id => _saved.Id;

    /// <summary>
    /// Последнее записанное содержимое
    /// </summary>
    public Remark Saved => _saved;

    /// <summary>
    /// Текст в поле правки
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (Set(ref _text, value))
                RaiseDirty();
        }
    }

    /// <summary>
    /// Текст поправлен, но ещё не сохранён
    /// </summary>
    public bool IsDirty => !string.Equals(_text, _saved.Text, StringComparison.Ordinal);

    /// <summary>
    /// Вид замечания словом: общее, условие, оформление, содержание, стиль
    /// </summary>
    public string KindTitle => KindName(_saved.Kind);

    /// <summary>
    /// Область: «Все предметы» или название предмета
    /// </summary>
    public string ScopeTitle => _saved.Global ? "Все предметы" : _saved.Subject;

    /// <summary>
    /// Дата записи
    /// </summary>
    public string DateText => _saved.Date.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Строка под текстом: область, вид и дата
    /// </summary>
    public string Meta => $"{ScopeTitle}  ·  {KindTitle}  ·  {DateText}";

    /// <summary>
    /// Имя для экранного диктора: начало текста замечания
    /// </summary>
    public string AccessibleName => "Замечание: " + (_saved.Text.Length > 60 ? _saved.Text[..60] + "…" : _saved.Text);

    /// <summary>
    /// Замечание учитывается при подготовке. Изменение сразу записывается
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isBusy || value == _isEnabled)
            {
                Raise();

                return;
            }

            _isEnabled = value;
            Raise();
            _change(this, _saved with { Enabled = value });
        }
    }

    /// <summary>
    /// Замечание для всех предметов. Изменение сразу переносит его между файлом предмета и общим
    /// </summary>
    public bool IsGlobal
    {
        get => _isGlobal;
        set
        {
            if (_isBusy || value == _isGlobal)
            {
                Raise();

                return;
            }

            _isGlobal = value;
            Raise();
            _change(this, _saved with { Global = value });
        }
    }

    /// <summary>
    /// Идёт запись этого замечания: кнопки и галочки недоступны, а вкладка не принимает новых записей строки, чтобы записи не обгоняли друг друга
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!Set(ref _isBusy, value))
                return;

            Raise(nameof(IsIdle));
        }
    }

    /// <summary>
    /// Запись не идёт: галочки доступны
    /// </summary>
    public bool IsIdle => !_isBusy;

    /// <summary>
    /// Сохраняет поправленный текст
    /// </summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>
    /// Удаляет замечание
    /// </summary>
    public RelayCommand RemoveCommand { get; }

    /// <summary>
    /// Запоминает записанное содержимое. Поправленный, но не сохранённый текст остаётся в поле
    /// </summary>
    /// <param name="remark">Записанное содержимое</param>
    public void Accept(Remark remark)
    {
        var keepDraft = IsDirty && !string.Equals(_text, remark.Text, StringComparison.Ordinal);
        _saved = remark;
        if (!keepDraft)
            _text = remark.Text;

        _isEnabled = remark.Enabled;
        _isGlobal = remark.Global;
        Raise(nameof(Text));
        Raise(nameof(IsEnabled));
        Raise(nameof(IsGlobal));
        Raise(nameof(KindTitle));
        Raise(nameof(ScopeTitle));
        Raise(nameof(DateText));
        Raise(nameof(Meta));
        Raise(nameof(AccessibleName));
        RaiseDirty();
    }

    /// <summary>
    /// Возвращает галочки к записанному: запись не удалась. Набранный текст не трогается
    /// </summary>
    public void Revert()
    {
        _isEnabled = _saved.Enabled;
        _isGlobal = _saved.Global;
        Raise(nameof(IsEnabled));
        Raise(nameof(IsGlobal));
    }

    /// <summary>
    /// Вид замечания словом
    /// </summary>
    /// <param name="kind">Вид</param>
    /// <returns>Слово для списка</returns>
    public static string KindName(RemarkKindEnum kind) => kind switch
    {
        RemarkKindEnum.Condition => "условие",
        RemarkKindEnum.Format => "оформление",
        RemarkKindEnum.Content => "содержание",
        RemarkKindEnum.Style => "стиль",
        _ => "общее",
    };

    /// <summary>
    /// Сообщает, что признак «поправлено» мог измениться: от него зависит кнопка «Сохранить»
    /// </summary>
    private void RaiseDirty() => Raise(nameof(IsDirty));
}
