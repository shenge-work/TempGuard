param(
  [int]$Interval = 3,
  [int]$Threshold = 80,
  [switch]$NoOverlay
)

# WinForms NotifyIcon requires STA apartment. Relaunch self in STA if needed.
if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    $argList = "-STA -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$($MyInvocation.MyCommand.Path)`""
    if ($Interval -ne 3) { $argList += " -Interval $Interval" }
    if ($Threshold -ne 80) { $argList += " -Threshold $Threshold" }
    if ($NoOverlay) { $argList += " -NoOverlay" }
    Start-Process powershell -ArgumentList $argList
    exit
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$scriptDir = Split-Path $MyInvocation.MyCommand.Path -Parent
$logFile   = Join-Path $scriptDir "temp_log.csv"
if (-not (Test-Path $logFile)) {
    "Timestamp,MaxTempC,AllZonesC" | Out-File -FilePath $logFile -Encoding UTF8
}
$script:lastBalloon = [datetime]::MinValue
$script:lastIconKey = ""
$script:prevIcon    = $null

# ---------- Temperature read (ACPI thermal zone via WMI; fallback to perf counter) ----------
function Get-Temps {
    $zones = @()
    try {
        $wmi = Get-WmiObject -Namespace root\wmi -Class MSAcpi_ThermalZoneTemperature -ErrorAction Stop
        foreach ($z in $wmi) { $zones += [math]::Round(($z.CurrentTemperature - 2732) / 10, 1) }
    } catch {
        try {
            $c = Get-Counter '\Thermal Zone Information(*)\Temperature' -ErrorAction Stop
            foreach ($s in $c.CounterSamples) { $zones += [math]::Round($s.CookedValue - 273.15, 1) }
        } catch { }
    }
    return $zones
}

# ---------- Draw a temperature number onto a tray icon (no external files) ----------
function New-TempIcon {
    param([int]$temp, [string]$status)
    $size = 64
    $bmp  = New-Object System.Drawing.Bitmap($size, $size)
    $g    = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode       = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint   = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $bg = switch ($status) {
        'hot'  { [System.Drawing.Color]::FromArgb(183,28,28) }   # red
        'warm' { [System.Drawing.Color]::FromArgb(230,81,0) }    # orange
        'na'   { [System.Drawing.Color]::FromArgb(66,66,66) }    # gray
        default { [System.Drawing.Color]::FromArgb(27,94,32) }   # green
    }
    $g.Clear($bg)

    $txt = if ($status -eq 'na') { 'NA' } else { "$temp" }
    $fs  = if ($status -eq 'na') { 22 } elseif ($temp -ge 100) { 24 } else { 34 }
    $font   = New-Object System.Drawing.Font("Arial", $fs, [System.Drawing.FontStyle]::Bold)
    $fmt   = New-Object System.Drawing.StringFormat
    $fmt.Alignment      = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment  = [System.Drawing.StringAlignment]::Center
    $brush  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $rect   = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString($txt, $font, $brush, $rect, $fmt)

    $g.Dispose(); $font.Dispose(); $brush.Dispose()
    $hicon = $bmp.GetHicon()
    $bmp.Dispose()
    return [System.Drawing.Icon]::FromHandle($hicon)
}

function Set-TrayIcon {
    param([int]$temp, [string]$status)
    $key = "$temp-$status"
    if ($key -eq $script:lastIconKey) { return }   # only redraw when number/color changes
    $new = New-TempIcon -temp $temp -status $status
    $icon.Icon = $new                    # Windows now references the new handle
    if ($script:prevIcon) { $script:prevIcon.Dispose() }  # old handle is free to release
    $script:prevIcon    = $new
    $script:lastIconKey = $key
}

# ---------- Tray icon ----------
$icon = New-Object System.Windows.Forms.NotifyIcon
$icon.Icon = [System.Drawing.SystemIcons]::Information
$icon.Text = "TempGuard starting..."
$icon.Visible = $true

$menu = New-Object System.Windows.Forms.ContextMenuStrip
$itemShow = $menu.Items.Add("Show/Hide overlay temp")
$itemExit = $menu.Items.Add("Exit TempGuard")
$icon.ContextMenuStrip = $menu

# ---------- Overlay window (transparent, topmost, click-through via TransparencyKey) ----------
$overlay = $null
$script:overlayVisible = $false
$script:labelRef = $null
if (-not $NoOverlay) {
    try {
        $overlay = New-Object System.Windows.Forms.Form
        $overlay.FormBorderStyle   = 'None'
        $overlay.BackColor         = [System.Drawing.Color]::Black
        $overlay.TransparencyKey   = [System.Drawing.Color]::Black
        $overlay.Opacity           = 0.72
        $overlay.TopMost           = $true
        $overlay.ShowInTaskbar     = $false
        $overlay.Width             = 92
        $overlay.Height            = 26
        $overlay.StartPosition     = 'Manual'
        $overlay.Location          = [System.Drawing.Point]::new([System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Width - 102, 8)
        $label = New-Object System.Windows.Forms.Label
        $label.Dock           = 'Fill'
        $label.TextAlign      = 'MiddleCenter'
        $label.ForeColor      = [System.Drawing.Color]::White
        $label.BackColor      = [System.Drawing.Color]::Transparent
        $label.Font           = New-Object System.Drawing.Font("Consolas", 11, [System.Drawing.FontStyle]::Bold)
        $label.Text           = "--C"
        $overlay.Controls.Add($label)
        $overlay.Show()
        $script:overlayVisible = $true
        $script:labelRef = $label
    } catch { $overlay = $null }
}

$itemShow.Add_Click({
    if ($overlay) {
        $script:overlayVisible = -not $script:overlayVisible
        if ($script:overlayVisible) { $overlay.Show() } else { $overlay.Hide() }
    }
})
$itemExit.Add_Click({
    $icon.Visible = $false
    if ($script:prevIcon) { $script:prevIcon.Dispose() }
    [System.Windows.Forms.Application]::Exit()
})

# ---------- Refresh logic ----------
function Update-Status {
    $zones = Get-Temps
    if ($zones.Count -eq 0) {
        $icon.Text = "TempGuard: temp N/A (run as Admin)"
        Set-TrayIcon -temp 0 -status 'na'
        if ($overlay -and $script:overlayVisible) { $script:labelRef.Text = "N/A" }
        return
    }
    $max = [int]([math]::Round(($zones | Measure-Object -Maximum).Maximum))
    $ts  = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    "$ts,$max," + ($zones -join '|') | Out-File -FilePath $logFile -Append -Encoding UTF8

    $txt = "TempGuard  CPU zone: $($max)C (limit $Threshold)"
    $icon.Text = if ($txt.Length -gt 63) { $txt.Substring(0, 63) } else { $txt }

    $status = if ($max -ge $Threshold) { 'hot' } elseif ($max -ge 70) { 'warm' } else { 'ok' }
    Set-TrayIcon -temp $max -status $status

    if ($overlay -and $script:overlayVisible) {
        $script:labelRef.Text = "$($max)C"
        $script:labelRef.ForeColor = if ($max -ge $Threshold) { [System.Drawing.Color]::Red } else { [System.Drawing.Color]::White }
    }

    if ($max -ge $Threshold) {
        if (((Get-Date) - $script:lastBalloon).TotalSeconds -gt 30) {
            $icon.ShowBalloonTip(4000, "Thermal warning", "Current $($max)C reached threshold $($Threshold)C, check cooling!", [System.Windows.Forms.ToolTipIcon]::Warning)
            [System.Media.SystemSounds]::Exclamation.Play()
            $script:lastBalloon = Get-Date
        }
    }
}

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = $Interval * 1000
$timer.Add_Tick({ Update-Status })
$timer.Start()
Update-Status

[System.Windows.Forms.Application]::Run()
