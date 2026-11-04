# Проверка живого окна приложения без мыши пользователя: снимок окна, раскрытие блоков журнала, проверка анимации сфер, загрузка процессора.
# Подключение: . .\Steward.DevTools\Scripts\UiProbe.ps1
# Вспомогательная библиотека собирается в data\tmp на диске репозитория, а не в профиле пользователя.

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$tmp = Join-Path $repo "data\tmp"
New-Item -ItemType Directory -Force $tmp | Out-Null
$dll = Join-Path $tmp "uiprobe.dll"

if (-not (Test-Path $dll)) {
    $source = @"
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class UiProbeNative {
 [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, int f);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
 [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
 public static Bitmap Grab(IntPtr h) { RECT r; GetWindowRect(h, out r); var b = new Bitmap(r.R - r.L, r.B - r.T); using (var g = Graphics.FromImage(b)) { IntPtr dc = g.GetHdc(); PrintWindow(h, dc, 2); g.ReleaseHdc(dc); } return b; }
 public static long Diff(Bitmap a, Bitmap b, int x, int y, int w, int hgt) { long d = 0; for (int i = x; i < x + w; i++) for (int j = y; j < y + hgt; j++) { var p = a.GetPixel(i, j); var q = b.GetPixel(i, j); d += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B); } return d; }
 public static int HitTest(IntPtr h, int x, int y) { return (int)SendMessage(h, 0x84, IntPtr.Zero, (IntPtr)((y << 16) | (x & 0xFFFF))); }
}
"@
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing -OutputAssembly $dll | Out-Null
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -Path $dll

# Главное окно приложения (заголовок «Задачи»). Если окно свёрнуто, разворачивает его
function Get-StewardWindow {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Задачи")
    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
    if ($null -eq $window) { throw "Окно приложения не найдено: запустите Steward.exe --open Journal" }

    $pattern = $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    if ($pattern.Current.WindowVisualState -eq 'Minimized') { $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal); Start-Sleep -Milliseconds 800 }

    return $window
}

# Сохраняет снимок окна в PNG (путь по умолчанию data\tmp\live.png)
function Save-StewardShot($path = (Join-Path $tmp "live.png")) {
    $window = Get-StewardWindow
    ([UiProbeNative]::Grab([IntPtr]$window.Current.NativeWindowHandle)).Save($path)

    return $path
}

# Все переключатели окна по порядку: разделы журнала, блоки настроек
function Get-StewardToggles {
    $window = Get-StewardWindow
    $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
    foreach ($button in $buttons) {
        $pattern = $null
        if ($button.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) { $pattern }
    }
}

# Прокручивает первую прокручиваемую область окна (0..100 процентов)
function Set-StewardScroll([double]$percent) {
    $window = Get-StewardWindow
    foreach ($element in $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        $pattern = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$pattern) -and $pattern.Current.VerticallyScrollable) { $pattern.SetScrollPercent(-1, $percent); return }
    }
}

# Меняется ли картинка в области окна за 350 мс: 0 значит анимация стоит. Область задаётся в пикселях окна
function Measure-StewardAnimation([int]$x, [int]$y, [int]$width, [int]$height) {
    $handle = [IntPtr](Get-StewardWindow).Current.NativeWindowHandle
    $first = [UiProbeNative]::Grab($handle); Start-Sleep -Milliseconds 350; $second = [UiProbeNative]::Grab($handle)

    return [UiProbeNative]::Diff($first, $second, $x, $y, $width, $height)
}

# Загрузка процессора приложением в процентах одного ядра за заданное число секунд
function Measure-StewardCpu([int]$seconds = 10) {
    $process = Get-Process Steward; $before = $process.CPU; Start-Sleep $seconds; $process.Refresh()

    return [math]::Round((($process.CPU - $before) / $seconds) * 100, 1)
}

# Что система отвечает на наведение курсора в точку окна: 11 правый край, 15 нижний, 17 угол, 1 внутри (окно растягивается за края)
function Test-StewardHit([int]$x, [int]$y) {
    return [UiProbeNative]::HitTest([IntPtr](Get-StewardWindow).Current.NativeWindowHandle, $x, $y)
}
