<#
.SYNOPSIS
    Starts the built game, waits, takes a screenshot of the primary screen and closes the game.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke-test.ps1 -Out shot.png -Seconds 6
#>
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Seconds = 6,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src/WhereAmI/bin/$Configuration/net10.0-windows/WhereAmI.exe"
if (-not (Test-Path $exe)) { throw "Game not built: $exe" }

# Start from an unrelated directory to prove assets do not depend on the working directory.
$game = Start-Process -FilePath $exe -WorkingDirectory $env:TEMP -PassThru
Start-Sleep -Seconds $Seconds

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
