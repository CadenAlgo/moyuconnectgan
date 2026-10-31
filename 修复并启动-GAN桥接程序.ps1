[CmdletBinding()]
param([switch]$ElevatedWorker)

$ErrorActionPreference = 'Stop'
function Write-Step([string]$Message) { Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" }

if (-not $ElevatedWorker) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    Write-Step 'Requesting administrator permission to reset the Realtek Bluetooth adapter.'
    $argumentLine = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '" -ElevatedWorker'
    if ($isAdmin) {
        $worker = Start-Process -FilePath 'powershell.exe' -ArgumentList $argumentLine -Wait -PassThru
    } else {
        $worker = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argumentLine -Wait -PassThru
    }
    if ($worker.ExitCode -ne 0) { throw "Bluetooth recovery worker failed (exit $($worker.ExitCode)); bridge was not relaunched." }
    $launcher = Join-Path $PSScriptRoot '启动-GAN桥接程序.bat'
    if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw "Normal bridge launcher is missing: $launcher" }
    Write-Step 'Bluetooth reset completed. Starting the bridge through the normal launcher.'
    Start-Process -FilePath $env:ComSpec -ArgumentList @('/c', '"' + $launcher + '"') -WorkingDirectory $PSScriptRoot
    exit 0
}

Write-Step 'Stopping the bridge process so it releases its GATT service.'
Get-Process -Name GanBridgeProbe -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$adapters = @(Get-PnpDevice -Class Bluetooth -ErrorAction Stop | Where-Object {
    $_.FriendlyName -eq 'Realtek Bluetooth Adapter' -and $_.InstanceId -like 'USB\VID_13D3&PID_3612*'
})
if ($adapters.Count -ne 1) {
    $found = $adapters | ForEach-Object { "$($_.FriendlyName) [$($_.InstanceId)]" }
    throw "Expected exactly one matching Realtek USB Bluetooth adapter; found $($adapters.Count). No device was changed. $($found -join '; ')"
}
$adapter = $adapters[0]
Write-Step "Target adapter: $($adapter.FriendlyName) [$($adapter.InstanceId)]; PnP status=$($adapter.Status)."
$pnputil = Join-Path (Join-Path $env:WINDIR 'System32') 'pnputil.exe'
$restarted = $false
if (Test-Path -LiteralPath $pnputil) {
    Write-Step 'Trying the Windows PnPUtil device restart.'
    & $pnputil /restart-device $adapter.InstanceId
    if ($LASTEXITCODE -eq 0) { $restarted = $true; Write-Step 'PnPUtil restart command succeeded.' }
    else { Write-Step "PnPUtil returned exit code $LASTEXITCODE; falling back to Disable/Enable-PnpDevice." }
}
if (-not $restarted) {
    Disable-PnpDevice -InstanceId $adapter.InstanceId -Confirm:$false -ErrorAction Stop
    Start-Sleep -Seconds 3
    Enable-PnpDevice -InstanceId $adapter.InstanceId -Confirm:$false -ErrorAction Stop
    Write-Step 'Adapter Disable/Enable cycle completed.'
}
$deadline = (Get-Date).AddSeconds(30)
$ready = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 700
    $current = Get-PnpDevice -InstanceId $adapter.InstanceId -ErrorAction SilentlyContinue
    if ($current -and $current.Status -eq 'OK') { $ready = $true; break }
}
if (-not $ready) { throw 'Adapter did not return to PnP status OK within 30 seconds. Restart Windows; bridge was not relaunched.' }
Write-Step 'Adapter is back in PnP status OK.'
try {
    Restart-Service -Name bthserv -Force -ErrorAction Stop
    Write-Step 'Bluetooth Support Service restarted.'
} catch {
    Write-Step "Could not restart bthserv cleanly: $($_.Exception.Message)"
    $service = Get-Service -Name bthserv -ErrorAction SilentlyContinue
    if (-not $service -or $service.Status -ne 'Running') {
        try { Start-Service -Name bthserv -ErrorAction Stop; Write-Step 'bthserv started.' }
        catch { throw "Bluetooth Support Service cannot be started: $($_.Exception.Message)" }
    } else { Write-Step 'bthserv is still running; continuing after the adapter reset.' }
}
Start-Sleep -Seconds 3
Write-Step 'Adapter and Bluetooth service reset finished.'
