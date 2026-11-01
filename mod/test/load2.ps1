$ErrorActionPreference = 'Stop'
$file = $args[0]; $dir = $args[1]
$asm = [System.Reflection.Assembly]::LoadFrom((Join-Path $dir 'WorldOfTheThreeKingdoms.exe'))
$t = $asm.GetTypes() | Where-Object { $_.Name -eq 'SimpleSerializer' } | Select-Object -First 1
$gs = $asm.GetTypes() | Where-Object { $_.Name -eq 'GameScenario' } | Select-Object -First 1
$m = $t.GetMethods() | Where-Object { $_.Name -eq 'DeserializeJson' -and $_.IsGenericMethodDefinition } | Select-Object -First 1
$gm = $m.MakeGenericMethod($gs)
$text = [IO.File]::ReadAllText($file) -replace "`r", "" -replace "`n", ""
$r = $gm.Invoke($null, @($text, $false, $false))
if ($null -eq $r) { 'DESERIALIZE RETURNED NULL'; exit 2 }
$list = $r.Persons.GameObjects
"persons: " + $list.Count
$new = New-Object System.Collections.ArrayList
foreach ($p in $list) {
  $id = $p.GetType().GetProperty('ID').GetValue($p)
  if ($id -ge 9700 -and $id -le 9719) { [void]$new.Add($p) }
}
"new persons: " + $new.Count
foreach ($p in ($new | Select-Object -First 3)) {
  $ty = $p.GetType()
  $g = { param($n) $ty.GetProperty($n).GetValue($p) }
  "  {0} {1}{2} sex={3} alive={4} avail={5} abil={6} yavail={7} pic={8}" -f (& $g 'ID'), (& $g 'SurName'), (& $g 'GivenName'), (& $g 'Sex'), (& $g 'Alive'), (& $g 'Available'), ((& $g 'BaseCommand')+(& $g 'BaseStrength')+(& $g 'BaseIntelligence')+(& $g 'BasePolitics')+(& $g 'BaseGlamour')), (& $g 'YearAvailable'), (& $g 'PictureIndex')
}
$bios = $r.AllBiographies.Biographys
"bios: " + $bios.Count
foreach ($b in $bios) { if ($b.Key -eq 9700) { "bio9700: " + $b.Value.Name + ' / ' + $b.Value.Brief.Substring(0,20); break } }
"scenario date: " + $r.Date
