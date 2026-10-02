$content = Get-Content 'src/SchoolManagement.Infrastructure/Persistence/ApplicationDbContext.cs'
$tables = @()
for ($i = 0; $i -lt $content.Length; $i++) {
    if ($content[$i] -match 'b\.ToTable\("([^"]+)",\s*"([^"]+)"\)') {
        $tables += "$($matches[2]).$($matches[1])"
    } elseif ($content[$i] -match 'b\.ToTable\("([^"]+)"\)') {
        $tables += "$($matches[1])"
    }
}
$tables | Sort-Object | Out-File 'scratch/db_tables_list.txt'
