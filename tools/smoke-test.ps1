<#
.SYNOPSIS
    Starts the built game, waits, takes a screenshot of the game window and closes the game.

    Optionally plays a key sequence after the start delay, e.g. to test movement:
    -Keys "F3:0.05; Right:1.5; Up:0.05; wait:0.5"
    Each step is <key>:<seconds held>; "wait" only pauses.
    Keys: Left Right Up Down Space Enter Escape F3 F11 A D W M.
    Keys are only sent when the game window is in the foreground, so they never reach another app.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke-test.ps1 -Out shot.png -Seconds 6
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke-test.ps1 -Out shot.png -Keys "Right:1.5; Up:0.05"
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke-test.ps1 -Out shot.png -GameArgs "--windowed 800x600"
#>
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [double]$Seconds = 6,
    [string]$Keys = "",
    [string]$GameArgs = "",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace Native -Name User32 -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr hWnd);
[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool GetClientRect(System.IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool PrintWindow(System.IntPtr hWnd, System.IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
public struct RECT { public int Left, Top, Right, Bottom; }
'@

# Work in physical pixels so window sizes are reported correctly on scaled displays.
[void][Native.User32]::SetProcessDPIAware()

$virtualKeys = @{
    Left = 0x25; Up = 0x26; Right = 0x27; Down = 0x28; Space = 0x20; Enter = 0x0D; Escape = 0x1B
    F3 = 0x72; F11 = 0x7A; A = 0x41; D = 0x44; W = 0x57; M = 0x4D
}

function Invoke-KeySequence([string]$sequence, [IntPtr]$window) {
    foreach ($step in $sequence.Split(';')) {
        $step = $step.Trim()
        if (-not $step) { continue }
        $name, $time = $step.Split(':')
        $ms = [int]([double]::Parse($time, [Globalization.CultureInfo]::InvariantCulture) * 1000)
        if ($name -eq 'wait') { Start-Sleep -Milliseconds $ms; continue }
        if (-not $virtualKeys.ContainsKey($name)) { throw "Unknown key '$name'" }
        if ([Native.User32]::GetForegroundWindow() -ne $window) { throw "Game window lost focus; stopped sending keys." }
        $vk = [byte]$virtualKeys[$name]
        [Native.User32]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)          # key down
        Start-Sleep -Milliseconds $ms
        [Native.User32]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)          # key up
    }
}

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src/WhereAmI/bin/$Configuration/net10.0-windows/WhereAmI.exe"
if (-not (Test-Path $exe)) { throw "Game not built: $exe" }

# Start from an unrelated directory to prove assets do not depend on the working directory.
$startArgs = @{ FilePath = $exe; WorkingDirectory = $env:TEMP; PassThru = $true }
if ($GameArgs) { $startArgs.ArgumentList = $GameArgs }
$game = Start-Process @startArgs

try {
    Start-Sleep -Milliseconds ([int]($Seconds * 1000))
    $game.Refresh()
    $window = $game.MainWindowHandle
    if ($game.HasExited -or $window -eq [IntPtr]::Zero) { throw "Game window did not appear (exited: $($game.HasExited))." }

    if ($Keys) {
        [void][Native.User32]::SetForegroundWindow($window)
        Start-Sleep -Milliseconds 300
        if ([Native.User32]::GetForegroundWindow() -ne $window) { throw "Could not bring the game window to the foreground; no keys were sent." }
        Invoke-KeySequence $Keys $window
    }

    # Capture just the game window, even if other windows cover it.
    $rect = New-Object Native.User32+RECT
    [void][Native.User32]::GetWindowRect($window, [ref]$rect)
    $client = New-Object Native.User32+RECT
    [void][Native.User32]::GetClientRect($window, [ref]$client)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [void][Native.User32]::PrintWindow($window, $hdc, 2)   # 2 = PW_RENDERFULLCONTENT, needed for WPF/DirectX
    $graphics.ReleaseHdc($hdc)
    $bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()

    $game.Refresh()
    if ($game.HasExited) {
        "FAIL: game exited with code $($game.ExitCode). Screenshot: $Out"
        exit 1
    }

    "OK: game running. Client area $($client.Right)x$($client.Bottom) px. Screenshot: $Out"
}
finally {
    if (-not $game.HasExited) { Stop-Process -Id $game.Id }
}
