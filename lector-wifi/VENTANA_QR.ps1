param([Parameter(Mandatory=$true)][string]$DataPath)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Grafiplot - QR recibido'
$form.Width = 520
$form.Height = 460
$form.StartPosition = 'CenterScreen'
$form.TopMost = $true
$form.ShowInTaskbar = $true
$form.Font = New-Object System.Drawing.Font('Segoe UI',12)
$text = New-Object System.Windows.Forms.TextBox
$text.Multiline = $true
$text.ReadOnly = $true
$text.ScrollBars = 'Vertical'
$text.Dock = 'Fill'
$text.BackColor = [System.Drawing.Color]::White
$form.Controls.Add($text)
$button = New-Object System.Windows.Forms.Button
$button.Text = 'Cerrar y esperar el siguiente QR'
$button.Height = 48
$button.Dock = 'Bottom'
$form.Controls.Add($button)
$script:eventId = ''
$script:revision = ''
$script:allowClose = $false
function Close-Reading {
 if ($script:eventId) {
  [System.IO.File]::WriteAllText(($DataPath + '.closed'), $script:eventId, (New-Object System.Text.UTF8Encoding($false)))
 }
 $form.Hide()
}
$button.Add_Click({ Close-Reading })
$form.Add_FormClosing({
 param($sender,$event)
 if (-not $script:allowClose) { $event.Cancel = $true; Close-Reading }
})
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 400
$timer.Add_Tick({
 try {
  if (-not [System.IO.File]::Exists($DataPath)) { $script:allowClose = $true; $form.Close(); return }
  $data = [System.IO.File]::ReadAllText($DataPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
  if ($data.revision -eq $script:revision) { return }
  $script:revision = $data.revision
  if (-not $data.visible) { $form.Hide(); return }
  $script:eventId = [string]$data.evento
  $text.Text = ([string]$data.text).Replace("`n", "`r`n")
  $form.Show()
  $form.TopMost = $true
  $form.Activate()
 } catch { }
})
$timer.Start()
# Run a message loop while the form starts hidden.
$context = New-Object System.Windows.Forms.ApplicationContext
$form.Add_FormClosed({$timer.Stop();$context.ExitThread()})
[System.Windows.Forms.Application]::Run($context)
