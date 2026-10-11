param([Parameter(Mandatory=$true)][string]$DataPath)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Grafiplot - Ficha del cliente'
$form.Size = New-Object System.Drawing.Size(580,760)
$form.MinimumSize = New-Object System.Drawing.Size(540,700)
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
$footer.Dock = 'Bottom'; $footer.Height = 142
$button = New-Object System.Windows.Forms.Button
$button.Text = 'Cerrar y esperar el siguiente QR'
$button.Location = New-Object System.Drawing.Point(24,77)
$button.Size = New-Object System.Drawing.Size(516,48)
$button.Anchor = 'Top,Left,Right'
$button.FlatStyle = 'Flat'; $button.FlatAppearance.BorderSize = 0
$button.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#EF292D')
$button.ForeColor = [System.Drawing.Color]::White
$button.Font = New-Object System.Drawing.Font('Segoe UI',11,[System.Drawing.FontStyle]::Bold)
$footer.Controls.Add($button)
$registerButton = New-Object System.Windows.Forms.Button
$registerButton.Text = 'Agregar datos de este QR'
$registerButton.Location = New-Object System.Drawing.Point(24,8)
$registerButton.Size = New-Object System.Drawing.Size(516,35)
$registerButton.Anchor = 'Top,Left,Right'; $registerButton.Visible = $false
$footer.Controls.Add($registerButton)
$saveStatus = Make-Label $footer '' 9 '#2563EB' $false 24 46 516 28
$saveStatus.Anchor = 'Top,Left,Right'
$script:qrCode = ''; $script:draft = @{}; $script:actionId = ''
$registerButton.Add_Click({
 $script:editDialog = New-Object System.Windows.Forms.Form
 $script:editDialog.Text = 'Registrar cliente / trabajo - QR ' + $script:qrCode
 $script:editDialog.Size = New-Object System.Drawing.Size(520,660); $script:editDialog.StartPosition = 'CenterParent'
 $script:editDialog.TopMost = $true; $script:editDialog.Font = $form.Font; $script:editDialog.FormBorderStyle = 'FixedDialog'; $script:editDialog.MaximizeBox = $false
 $script:editFields = @('nombre','telefono','descripcion','ubicacion','hojas','total','pago')
 $captions = @('Nombre del cliente *','Telefono','Detalle del trabajo *','Ubicacion *','Cantidad de hojas *','Total S/ *','Pagado S/ *')
 $script:editInputs = @{}; $y = 15
 for ($i=0; $i -lt $script:editFields.Count; $i++) {
  $lbl = Make-Label $script:editDialog $captions[$i] 10 '#334155' $true 18 $y 460 24
  $box = New-Object System.Windows.Forms.TextBox
  $box.Location = New-Object System.Drawing.Point(18,($y+25)); $box.Size = New-Object System.Drawing.Size(464,28)
  $box.MaxLength = 1200; $key = $script:editFields[$i]
  if ($script:draft.ContainsKey($key)) { $box.Text = $script:draft[$key] }
  elseif ($key -eq 'hojas') { $box.Text = '1' } elseif ($key -eq 'ubicacion') { $box.Text = 'E1' } elseif ($key -eq 'pago') { $box.Text = '0' }
  $script:editInputs[$key] = $box; $script:editDialog.Controls.Add($box); $y += 70
 }
 $save = New-Object System.Windows.Forms.Button; $save.Text = 'Guardar en Grafiplot'
 $save.Location = New-Object System.Drawing.Point(18,520); $save.Size = New-Object System.Drawing.Size(464,42)
 $script:editDialog.Controls.Add($save)
 $script:editError = Make-Label $script:editDialog '' 9 '#DC2626' $false 18 568 464 45
 $save.Add_Click({
  $values = @{codigo=$script:qrCode}
  foreach ($key in $script:editFields) { $values[$key] = $script:editInputs[$key].Text.Trim() }
  if (-not $values.nombre -or -not $values.descripcion -or -not $values.ubicacion) { $script:editError.Text = 'Completa nombre, detalle y ubicacion.'; return }
  foreach ($key in @('total','pago','hojas')) {
   $values[$key] = $values[$key].Replace(',','.')
   if ($values[$key] -notmatch '^\d+(\.\d{1,2})?$') { $script:editError.Text = 'Revisa '+$key; return }
  }
  if ($values.hojas -notmatch '^\d+$' -or $values.hojas -eq '0' -or $values.hojas.Length -gt 7 -or $values.total.Length -gt 12 -or $values.pago.Length -gt 12) { $script:editError.Text = 'Revisa cantidades e importes.'; return }
  if ([decimal]::Parse($values.pago,[cultureinfo]::InvariantCulture) -gt [decimal]::Parse($values.total,[cultureinfo]::InvariantCulture)) { $script:editError.Text = 'El pago no puede superar el total.'; return }
  $script:draft = $values
  if (-not $script:actionId) { $script:actionId = [guid]::NewGuid().ToString() }
  $payload = @{evento=$script:eventId;id=$script:actionId;data=$values} | ConvertTo-Json -Depth 5 -Compress
  try {
   [System.IO.File]::WriteAllText(($DataPath+'.action.tmp'),$payload,(New-Object System.Text.UTF8Encoding($false)))
   Move-Item -LiteralPath ($DataPath+'.action.tmp') -Destination ($DataPath+'.action') -Force
   $saveStatus.Text = 'Guardando... Manten Grafiplot abierto e inicia sesion.'
   $registerButton.Enabled = $false; $button.Enabled = $false; $script:editDialog.Close()
  } catch { $script:editError.Text = 'No se pudo enviar. Vuelve a intentar.' }
 })
 [System.IO.File]::WriteAllText(($DataPath+'.editing'),'1')
 try { $script:editDialog.ShowDialog($form) | Out-Null } finally { Remove-Item -LiteralPath ($DataPath+'.editing') -ErrorAction SilentlyContinue; $script:editDialog.Dispose() }
})
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
 if ([System.IO.File]::Exists($DataPath+'.action')) { return }
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
  if ($script:eventId -ne [string]$data.evento) { $script:draft = @{}; $script:actionId = ''; $saveStatus.Text = '' }
  $script:eventId = [string]$data.evento
  $button.Enabled = -not [System.IO.File]::Exists($DataPath+'.action')
  $registerButton.Enabled = $button.Enabled
  if ($data.status) { $saveStatus.Text = [string]$data.status }
  $d = $data.details
  if ($null -ne $d) {
   $script:qrCode = [string]$d.codigo
   $registerButton.Visible = [string]::IsNullOrEmpty([string]$d.deuda)
   if (-not $registerButton.Visible) { $saveStatus.Text = 'Registro disponible en Grafiplot' }
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
   $displayDetail = if ($d.completo) { [string]$d.completo } else { [string]$d.descripcion }
   $detail.Text = $displayDetail.Replace("`n", "`r`n")
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
