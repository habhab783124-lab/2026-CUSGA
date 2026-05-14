param(
    [string]$TaskCardJsonPath = "docs/current-task-card.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $TaskCardJsonPath)) {
    Write-Error "Task gate failed: missing $TaskCardJsonPath"
    exit 1
}

$json = Get-Content -Path $TaskCardJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

$failures = New-Object System.Collections.Generic.List[string]

if ([string]::IsNullOrWhiteSpace($json.status)) {
    $failures.Add("status 不能为空")
}

if ([string]::IsNullOrWhiteSpace($json.task_source)) {
    $failures.Add("task_source 不能为空")
}

if ($null -eq $json.do_scope -or $json.do_scope.Count -eq 0) {
    $failures.Add("do_scope 不能为空")
}

if ($null -eq $json.not_do_scope -or $json.not_do_scope.Count -eq 0) {
    $failures.Add("not_do_scope 不能为空")
}

if ($null -eq $json.affected_targets -or $json.affected_targets.Count -eq 0) {
    $failures.Add("affected_targets 不能为空")
}

if ($null -eq $json.success_criteria -or $json.success_criteria.Count -eq 0) {
    $failures.Add("success_criteria 不能为空")
}

if (-not $json.approved) {
    $failures.Add("approved 必须为 true")
}

if ($failures.Count -gt 0) {
    Write-Output "TASK_GATE: FAILED"
    foreach ($failure in $failures) {
        Write-Output "- $failure"
    }
    exit 1
}

Write-Output "TASK_GATE: PASSED"
Write-Output "status=$($json.status)"
Write-Output "task_source=$($json.task_source)"
exit 0
