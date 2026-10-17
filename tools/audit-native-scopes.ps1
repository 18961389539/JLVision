param(
    [string]$SourceRoot = (Join-Path $PSScriptRoot '..\JLVisionLib.Runtime')
)

$ErrorActionPreference = 'Stop'
$files = @(Get-ChildItem -LiteralPath $SourceRoot -Recurse -Filter '*.cs' -File)
$issues = [System.Collections.Generic.List[string]]::new()
$totalProcedures = 0
$totalCompletions = 0

foreach ($file in $files) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    $procedureCount = 0
    $completionCount = 0

    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*IntPtr\s+proc\s*=\s*(?:JlNativeApi\.)?PreCall\(\d+\);\s*$') {
            $procedureCount++
            if (($i + 1) -ge $lines.Length -or
                $lines[$i + 1] -notmatch '^\s*using var nativeCall = JlNativeProcedureScope\.Begin\(proc\);\s*$') {
                $issues.Add("$($file.Name):$($i + 1): PreCall is missing a lifetime scope on the next line")
            }
        }

        if ($line -match '^\s*nativeCall\.Complete\((?:err|procResult|num)\);\s*$') {
            $completionCount++
        }

        if ($line -match '^\s*(?:JlNativeApi\.)?PostCall\(proc,\s*(?:err|procResult|num)\);\s*$') {
            $issues.Add("$($file.Name):$($i + 1): PostCall bypasses nativeCall.Complete")
        }
    }

    if ($procedureCount -ne $completionCount) {
        $issues.Add("$($file.Name): $procedureCount PreCall entries but $completionCount nativeCall.Complete entries")
    }
    $totalProcedures += $procedureCount
    $totalCompletions += $completionCount
}

Write-Host "Audited $($files.Length) source files: $totalProcedures guarded native calls, $totalCompletions completions."
if ($issues.Count -gt 0) {
    foreach ($issue in $issues) { Write-Error $issue }
    throw "$($issues.Count) native lifetime scope violations detected."
}
Write-Host 'Native procedure lifetime scope audit passed.'
