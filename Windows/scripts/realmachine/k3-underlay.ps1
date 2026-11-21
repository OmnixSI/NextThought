# K3 下层窗口探针：一个无边框 WinForms 窗口，记录收到的每一次鼠标左键按下。
# 由 k3-click-through.ps1 启动；岛窗口是 Topmost 且默认 click-through，所以岛透明区域的点击
# 应当落到这个窗口；岛面板区域的点击应当被岛收走。
param(
    [Parameter(Mandatory = $true)][string]$LogPath,
    [int]$Seconds = 240
)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
if (Test-Path $LogPath) { Remove-Item $LogPath -Force -ErrorAction SilentlyContinue }
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$form = New-Object System.Windows.Forms.Form
$form.Text = 'K3 Underlay'
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
$form.Bounds = New-Object System.Drawing.Rectangle 0, 0, $bounds.Width, $bounds.Height
$form.BackColor = [System.Drawing.Color]::FromArgb(32, 120, 220)
# Topmost：盖住应用主窗口；岛窗口也是 Topmost 且后显示，会排在它上面，正好成为“下层窗口”。
$form.TopMost = $true
$form.ShowInTaskbar = $false
$form.Add_MouseDown({
    param($sender, $eventArgs)
    Add-Content -Path $LogPath -Value ((Get-Date -Format 'HH:mm:ss.fff') + ' down') -Encoding UTF8
})
$form.Add_Shown({ $form.Activate() })
$form.Show()
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = $Seconds * 1000
$timer.Add_Tick({ $timer.Stop(); $form.Close() })
$timer.Start()
[System.Windows.Forms.Application]::Run($form)
