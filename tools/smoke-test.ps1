<#
.SYNOPSIS
    Starts the built game, waits, takes a screenshot of the primary screen and closes the game.

    Optionally plays a key sequence after the start delay, e.g. to test movement:
    -Keys "F3:0.05; Right:1.5; Up:0.05; wait:0.5"
    Each step is <key>:<seconds held>; "wait" only pauses.
    Keys: Left Right Up Down Space Enter Escape F3 A D W M.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke-test.ps1 -Out shot.png -Seconds 6
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke-test.ps1 -Out shot.png -Keys "Right:1.5; Up:0.05"
#>
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [double]$Seconds = 6,
    [string]$Keys = "",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace Native -Name Keyboard -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);
'@

$virtualKeys = @{
    Left = 0x25; Up = 0x26; Right = 0x27; Down = 0x28; Space = 0x20; Enter = 0x0D; Escape = 0x1B
    F3 = 0x72; A = 0x41; D = 0x44; W = 0x57; M = 0x4D
}

function Invoke-KeySequence([string]$sequence) {
    foreach ($step in $sequence.Split(';')) {
        $step = $step.Trim()
        if (-not $step) { continue }
        $name, $time = $step.Split(':')
        $ms = [int]([double]::Parse($time, [Globalization.CultureInfo]::InvariantCulture) * 1000)
        if ($name -eq 'wait') { Start-Sleep -Milliseconds $ms; continue }
        if (-not $virtualKeys.ContainsKey($name)) { throw "Unknown key '$name'" }
        $vk = [byte]$virtualKeys[$name]
        [Native.Keyboard]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)          # key down
        Start-Sleep -Milliseconds $ms
        [Native.Keyboard]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)          # key up
    }
}

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src/WhereAmI/bin/$Configuration/net10.0-windows/WhereAmI.exe"
if (-not (Test-Path $exe)) { throw "Game not built: $exe" }

# Start from an unrelated directory to prove assets do not depend on the working directory.
$game = Start-Process -FilePath $exe -WorkingDirectory $env:TEMP -PassThru
Start-Sleep -Milliseconds ([int]($Seconds * 1000))
if ($Keys -and -not $game.HasExited) { Invoke-KeySequence $Keys }

$alive = -not $game.HasExited
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

if ($alive) {
    Stop-Process -Id $game.Id
    "OK: game was running after $Seconds s. Screenshot: $Out ($($bounds.Width)x$($bounds.Height))"
}
else {
    "FAIL: game exited early with code $($game.ExitCode). Screenshot: $Out"
    exit 1
}
