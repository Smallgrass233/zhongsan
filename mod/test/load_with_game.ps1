$ErrorActionPreference = 'Stop'
$file = $args[0]
$dir = $args[1]
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
$new = @($list | Where-Object { $_.ID -ge 9700 -and $_.ID -le 9719 })
"new persons: " + $new.Count
$new | Select-Object -First 3 | ForEach-Object { "  {0} {1}{2} sex={3} alive={4} avail={5} abil={6} yavail={7}" -f $_.ID, $_.SurName, $_.GivenName, $_.Sex, $_.Alive, $_.Available, ($_.BaseCommand + $_.BaseStrength + $_.BaseIntelligence + $_.BasePolitics + $_.BaseGlamour), $_.YearAvailable }
$bios = $r.AllBiographies.Biographys
"bios: " + $bios.Count
$b = $bios | Where-Object { $_.Key -eq 9700 } | Select-Object -First 1
"bio9700: " + $b.Value.Name + ' / ' + $b.Value.Brief
"skills of one: " + ($new[0].SkillsString)
