# Scratch DXF inspector for reverse-engineering the client's lift-blanket
# reference drawings. Not shipped — lives under the gitignored reference/.
param([Parameter(Mandatory=$true)][string]$Path)

$lines = Get-Content -LiteralPath $Path

# Locate the ENTITIES section by its 0/SECTION ... 2/ENTITIES header.
$entStart = -1; $entEnd = -1
for ($i=0; $i -lt $lines.Count-3; $i++) {
  if ($lines[$i].Trim() -eq '0' -and $lines[$i+1].Trim() -eq 'SECTION' -and $lines[$i+3].Trim() -eq 'ENTITIES') { $entStart = $i }
  if ($entStart -ge 0 -and $lines[$i].Trim() -eq '0' -and $lines[$i+1].Trim() -eq 'ENDSEC' -and $i -gt $entStart) { $entEnd = $i; break }
}

function Get-Entities($lines, $start, $end) {
  $ents = New-Object System.Collections.ArrayList
  $cur = $null
  $i = $start
  while ($i -lt $end) {
    $code = $lines[$i].Trim()
    $val  = $lines[$i+1].Trim()
    if ($code -eq '0') {
      if ($cur -and $cur.Type -ne 'SECTION') { [void]$ents.Add($cur) }
      $cur = @{ Type = $val; codes = @{} }
    } elseif ($cur) {
      if (-not $cur.codes.ContainsKey($code)) { $cur.codes[$code] = New-Object System.Collections.ArrayList }
      [void]$cur.codes[$code].Add($val)
    }
    $i += 2
  }
  if ($cur -and $cur.Type -ne 'SECTION') { [void]$ents.Add($cur) }
  return $ents
}

function L($e) { if ($e.codes.ContainsKey('8')) { $e.codes['8'][0] } else { '?' } }

$ents = Get-Entities $lines $entStart $entEnd

Write-Host "=== $Path ===" -ForegroundColor Cyan
Write-Host "Entities: $($ents.Count)"

Write-Host "`n--- CUT polylines (LWPOLYLINE) ---" -ForegroundColor Yellow
foreach ($e in $ents) {
  if ($e.Type -ne 'LWPOLYLINE') { continue }
  $xs = $e.codes['10']; $ys = $e.codes['20']
  $flag = if ($e.codes.ContainsKey('70')) { $e.codes['70'][0] } else { '?' }
  Write-Host ("  [{0}] verts={1} closed-flag={2}" -f (L $e), $xs.Count, $flag)
  for ($k=0; $k -lt $xs.Count; $k++) {
    "      ({0,10:N1}, {1,10:N1})" -f [double]$xs[$k], [double]$ys[$k]
  }
}

Write-Host "`n--- LINE segments ---" -ForegroundColor Yellow
foreach ($e in $ents) {
  if ($e.Type -ne 'LINE') { continue }
  $x1=[double]$e.codes['10'][0]; $y1=[double]$e.codes['20'][0]
  $x2=[double]$e.codes['11'][0]; $y2=[double]$e.codes['21'][0]
  $orient = if ([math]::Abs($x2-$x1) -lt 0.5) { 'V' } elseif ([math]::Abs($y2-$y1) -lt 0.5) { 'H' } else { '/' }
  $len = [math]::Sqrt([math]::Pow($x2-$x1,2)+[math]::Pow($y2-$y1,2))
  "  [{0,-15}] {1} ({2,9:N1},{3,9:N1})->({4,9:N1},{5,9:N1}) len={6,8:N1}" -f (L $e),$orient,$x1,$y1,$x2,$y2,$len
}

Write-Host "`n--- DIMENSION entities ---" -ForegroundColor Yellow
foreach ($e in $ents) {
  if ($e.Type -ne 'DIMENSION') { continue }
  $txt = if ($e.codes.ContainsKey('1')) { $e.codes['1'][0] } else { '<measured>' }
  $defX = if ($e.codes.ContainsKey('10')) { $e.codes['10'][0] } else { '?' }
  $defY = if ($e.codes.ContainsKey('20')) { $e.codes['20'][0] } else { '?' }
  "  [{0}] text='{1}' def=({2},{3})" -f (L $e),$txt,$defX,$defY
}

Write-Host "`n--- TEXT entities ---" -ForegroundColor Yellow
foreach ($e in $ents) {
  if ($e.Type -ne 'TEXT') { continue }
  $x=[double]$e.codes['10'][0]; $y=[double]$e.codes['20'][0]
  $h=if ($e.codes.ContainsKey('40')){$e.codes['40'][0]}else{'?'}
  $t=if ($e.codes.ContainsKey('1')){$e.codes['1'][0]}else{''}
  "  [{0,-15}] ({1,9:N1},{2,9:N1}) h={3,-7} '{4}'" -f (L $e),$x,$y,$h,$t
}
