$files = Get-ChildItem -Path "src\SchoolManagement.Api\Controllers" -Filter "*.cs"
$out = @()
foreach ($f in $files) {
    $out += "=== $($f.Name) ==="
    $lines = Get-Content $f.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '\[Http(Get|Post|Put|Delete|Patch)') {
            $attr = $lines[$i].Trim()
            $method = ""
            for ($j = $i + 1; $j -lt [Math]::Min($i + 5, $lines.Count); $j++) {
                if ($lines[$j] -match 'public async Task<IActionResult>\s+([A-Za-z0-9_]+)') {
                    $method = $matches[1]
                    break
                }
            }
            $out += "  $attr -> $method"
        }
    }
}
$out | Out-File -FilePath "scratch/endpoints_dump.txt" -Encoding utf8
