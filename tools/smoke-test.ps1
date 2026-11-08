<#
.SYNOPSIS
    Starts the built game, waits, takes a screenshot of the game window and closes the game.

    Optionally plays a key sequence after the start delay, e.g. to test movement:
    -Keys "F3:0.05; Right:1.5; Up:0.05; wait:0.5"
    Each step is <key>:<seconds held>; "wait" only pauses.
    Keys: Left Right Up Down Space Enter Escape F3 F7 F8 F11 A D W M X J.
    Keys are only sent when the game window is in the foreground, so they never reach another app.

    With -Frames N the keys play while N frames are recorded, for making animated GIFs.

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
    [string]$Configuration = "Debug",
    [string]$Exe = "",   # optional: run this executable instead of the Debug build, e.g. a published one
    [switch]$ClientOnly,  # capture only the game area, without the window frame and title bar
    [int]$Frames = 0,     # when > 0: record this many frames (Out-000.png, Out-001.png, ...) while the keys play
    [int]$FrameMs = 66    # time between recorded frames
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
    F3 = 0x72; F7 = 0x76; F8 = 0x77; F11 = 0x7A; A = 0x41; D = 0x44; W = 0x57; M = 0x4D; X = 0x58; J = 0x4A
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

# Captures the game window (or only its client area) even if other windows cover it.
function Save-Window([IntPtr]$window, [string]$path) {
    $rect = New-Object Native.User32+RECT
    if ($ClientOnly) { [void][Native.User32]::GetClientRect($window, [ref]$rect) }
    else { [void][Native.User32]::GetWindowRect($window, [ref]$rect) }
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    # 2 = PW_RENDERFULLCONTENT (needed for WPF/DirectX), 1 = PW_CLIENTONLY
    [void][Native.User32]::PrintWindow($window, $hdc, $(if ($ClientOnly) { 3 } else { 2 }))
    $graphics.ReleaseHdc($hdc)
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

# Plays the key sequence on a timeline while recording frames at a fixed interval.
function Save-Frames([IntPtr]$window, [string]$sequence) {
    $events = New-Object System.Collections.Generic.List[object]
    $t = 0
    foreach ($step in $sequence.Split(';')) {
        $step = $step.Trim()
        if (-not $step) { continue }
        $name, $time = $step.Split(':')
        $ms = [int]([double]::Parse($time, [Globalization.CultureInfo]::InvariantCulture) * 1000)
        if ($name -ne 'wait') {
            if (-not $virtualKeys.ContainsKey($name)) { throw "Unknown key '$name'" }
            $events.Add(@{ At = $t; Key = [byte]$virtualKeys[$name]; Up = $false })
            $events.Add(@{ At = $t + $ms; Key = [byte]$virtualKeys[$name]; Up = $true })
        }
        $t += $ms
    }

    $base = [IO.Path]::Combine([IO.Path]::GetDirectoryName($Out), [IO.Path]::GetFileNameWithoutExtension($Out))
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $next = 0
    for ($frame = 0; $frame -lt $Frames; $frame++) {
        while ($next -lt $events.Count -and $events[$next].At -le $clock.ElapsedMilliseconds) {
            if ([Native.User32]::GetForegroundWindow() -ne $window) { throw "Game window lost focus; stopped sending keys." }
            $e = $events[$next]
            [Native.User32]::keybd_event($e.Key, 0, $(if ($e.Up) { 2 } else { 0 }), [UIntPtr]::Zero)
            $next++
        }
        Save-Window $window ("{0}-{1:000}.png" -f $base, $frame)
        $wait = ($frame + 1) * $FrameMs - $clock.ElapsedMilliseconds
        if ($wait -gt 0) { Start-Sleep -Milliseconds $wait }
    }

    # Release anything still held.
    for (; $next -lt $events.Count; $next++) {
        if ($events[$next].Up) { [Native.User32]::keybd_event($events[$next].Key, 0, 2, [UIntPtr]::Zero) }
    }
}

$root = Split-Path -Parent $PSScriptRoot
$exe = if ($Exe) { $Exe } else { Join-Path $root "WhereAmI/bin/$Configuration/net10.0-windows/WhereAmI.exe" }
if (-not (Test-Path $exe)) { throw "Game not built: $exe" }

# Start from an unrelated directory to prove assets do not depend on the working directory.
$startArgs = @{ FilePath = $exe; WorkingDirectory = $env:TEMP; PassThru = $true }
if ($GameArgs) { $startArgs.ArgumentList = $GameArgs }
$game = Start-Process @startArgs

try {
    Start-Sleep -Milliseconds ([int]($Seconds * 1000))
    $game.Refresh()
    $window = $game.MainWindowHandle
    if ($game.HasExited -or $window -eq [IntPtr]::Zero) {
        # Capture the whole screen instead, to see what happened (an error dialog, for example).
        Add-Type -AssemblyName System.Windows.Forms
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $screen = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
        $g = [System.Drawing.Graphics]::FromImage($screen)
        $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
        $screen.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $screen.Dispose()
        throw "Game window did not appear (exited: $($game.HasExited)). Screen saved to $Out"
    }

    if ($Keys) {
        [void][Native.User32]::SetForegroundWindow($window)
        Start-Sleep -Milliseconds 300
        if ([Native.User32]::GetForegroundWindow() -ne $window) { throw "Could not bring the game window to the foreground; no keys were sent." }
        if ($Frames -eq 0) { Invoke-KeySequence $Keys $window }
    }

    $client = New-Object Native.User32+RECT
    [void][Native.User32]::GetClientRect($window, [ref]$client)
    if ($Frames -gt 0) {
        Save-Frames $window $Keys
    }
    else {
        Save-Window $window $Out
    }

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
