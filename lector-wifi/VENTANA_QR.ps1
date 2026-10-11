param([Parameter(Mandatory=$true)][string]$DataPath)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Grafiplot - Ficha del cliente'
$form.Size = New-Object System.Drawing.Size(580,650)
$form.MinimumSize = New-Object System.Drawing.Size(540,600)
$form.StartPosition = 'CenterScreen'
$form.TopMost = $true
$form.ShowInTaskbar = $true
$form.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#F8FAFC')
$form.Font = New-Object System.Drawing.Font('Segoe UI',11)
function Make-Label($parent, $text, $size, $color, $bold, $x, $y, $width, $height) {
 $label = New-Object System.Windows.Forms.Label
 $label.Text = $text
 $style = [System.Drawing.FontStyle]::Regular
 if ($bold) { $style = [System.Drawing.FontStyle]::Bold }
 $label.Font = New-Object System.Drawing.Font('Segoe UI',$size,$style)
 $label.ForeColor = [System.Drawing.ColorTranslator]::FromHtml($color)
 $label.Location = New-Object System.Drawing.Point($x,$y)
 $label.Size = New-Object System.Drawing.Size($width,$height)
 $label.AutoEllipsis = $true
 $parent.Controls.Add($label)
 return $label
}
$header = New-Object System.Windows.Forms.Panel
$header.Dock = 'Top'; $header.Height = 72
$header.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#EF292D')
$brand = Make-Label $header 'GRAFIPLOT' 22 '#FFFFFF' $true 24 14 300 40
$tag = Make-Label $header 'LECTURA QR' 9 '#FFFFFF' $true 385 27 140 20
$footer = New-Object System.Windows.Forms.Panel
$footer.Dock = 'Bottom'; $footer.Height = 82
$button = New-Object System.Windows.Forms.Button
$button.Text = 'Cerrar y esperar el siguiente QR'
$button.Location = New-Object System.Drawing.Point(24,17)
$button.Size = New-Object System.Drawing.Size(516,48)
$button.Anchor = 'Top,Left,Right'
$button.FlatStyle = 'Flat'; $button.FlatAppearance.BorderSize = 0
$button.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#EF292D')
$button.ForeColor = [System.Drawing.Color]::White
$button.Font = New-Object System.Drawing.Font('Segoe UI',11,[System.Drawing.FontStyle]::Bold)
$footer.Controls.Add($button)
$body = New-Object System.Windows.Forms.Panel
$body.Dock = 'Fill'; $body.Padding = New-Object System.Windows.Forms.Padding(24)
$code = Make-Label $body 'CODIGO QR' 11 '#9333EA' $true 24 14 300 24
$location = Make-Label $body '' 10 '#2563EB' $true 355 16 190 22
$location.Anchor = 'Top,Right'
$name = Make-Label $body '' 23 '#0F172A' $true 24 51 510 64
$name.Anchor = 'Top,Left,Right'
$phone = Make-Label $body '' 11 '#64748B' $false 24 117 510 25
$phone.Anchor = 'Top,Left,Right'
$money = New-Object System.Windows.Forms.Panel
$money.Location = New-Object System.Drawing.Point(24,153)
$money.Size = New-Object System.Drawing.Size(510,95)
$money.Anchor = 'Top,Left,Right'
$money.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#FEF2F2')
$moneyCaption = Make-Label $money 'SALDO PENDIENTE' 9 '#991B1B' $true 16 13 450 22
$debt = Make-Label $money '' 27 '#DC2626' $true 16 36 450 48
$body.Controls.Add($money)
$detailCaption = Make-Label $body 'DETALLE DEL TRABAJO' 10 '#64748B' $true 24 269 450 24
$detail = New-Object System.Windows.Forms.TextBox
$detail.Location = New-Object System.Drawing.Point(24,299)
$detail.Size = New-Object System.Drawing.Size(510,145)
$detail.Anchor = 'Top,Bottom,Left,Right'
$detail.Multiline = $true; $detail.ReadOnly = $true; $detail.ScrollBars = 'Vertical'
$detail.BorderStyle = 'None'; $detail.BackColor = $form.BackColor
$detail.ForeColor = [System.Drawing.ColorTranslator]::FromHtml('#334155')
$detail.Font = New-Object System.Drawing.Font('Segoe UI',12)
$body.Controls.Add($detail)
$form.Controls.Add($body); $form.Controls.Add($footer); $form.Controls.Add($header)
function Read-WindowData {
 $share = [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
 $stream = New-Object System.IO.FileStream($DataPath,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,$share)
 $reader = New-Object System.IO.StreamReader($stream,[System.Text.Encoding]::UTF8)
 try { return ($reader.ReadToEnd() | ConvertFrom-Json) } finally { $reader.Dispose() }
}
$script:eventId = ''; $script:revision = ''; $script:allowClose = $false
function Close-Reading {
 if ($script:eventId) { [System.IO.File]::WriteAllText(($DataPath + '.closed'), $script:eventId, (New-Object System.Text.UTF8Encoding($false))) }
 $form.Hide()
}
$button.Add_Click({ Close-Reading })
$form.Add_FormClosing({param($sender,$event) if (-not $script:allowClose) { $event.Cancel = $true; Close-Reading }})
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 100
$timer.Add_Tick({
 try {
  if (-not [System.IO.File]::Exists($DataPath)) { $script:allowClose = $true; $form.Close(); return }
  $data = Read-WindowData
  if ($data.revision -eq $script:revision) { return }
  $script:revision = $data.revision
  if (-not $data.visible) { $form.Hide(); return }
  $script:eventId = [string]$data.evento
  $d = $data.details
  if ($null -ne $d) {
   $code.Text = 'CODIGO QR  ' + [string]$d.codigo
   $name.Text = [string]$d.nombre
   $location.Text = [string]$d.ubicacion
   $phone.Text = 'Telefono: ' + [string]$d.telefono
   if ([string]::IsNullOrEmpty([string]$d.deuda)) {
    $debt.Text = 'Sin registro'; $moneyCaption.Text = 'RESULTADO'
   } elseif ([decimal]::Parse([string]$d.deuda,[System.Globalization.CultureInfo]::InvariantCulture) -gt 0) {
    $debt.Text = 'Debe S/ ' + [string]$d.deuda; $moneyCaption.Text = 'SALDO PENDIENTE'
   } else { $debt.Text = 'Sin deuda'; $moneyCaption.Text = 'PAGO COMPLETO' }
   $paid = ([string]$d.deuda -eq '0.00')
   $debt.ForeColor = if ($paid) { [System.Drawing.ColorTranslator]::FromHtml('#15803D') } else { [System.Drawing.ColorTranslator]::FromHtml('#DC2626') }
   $money.BackColor = if ($paid) { [System.Drawing.ColorTranslator]::FromHtml('#F0FDF4') } else { [System.Drawing.ColorTranslator]::FromHtml('#FEF2F2') }
   $detail.Text = ([string]$d.descripcion).Replace("`n", "`r`n")
  } else {
   $code.Text = 'LECTURA QR'; $name.Text = 'Resultado de lectura'; $location.Text = ''; $phone.Text = ''
   $moneyCaption.Text = 'RESULTADO'; $debt.Text = 'Ver detalle'; $detail.Text = ([string]$data.text).Replace("`n", "`r`n")
  }
  $form.Show(); $form.TopMost = $true; $form.Activate()
 } catch { }
})
$timer.Start()
$context = New-Object System.Windows.Forms.ApplicationContext
$form.Add_FormClosed({$timer.Stop();$context.ExitThread()})
[System.Windows.Forms.Application]::Run($context)
