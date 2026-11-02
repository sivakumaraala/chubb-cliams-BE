param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$claimant = @{ 'X-Demo-User' = 'claimant-alex' }
$officer = @{ 'X-Demo-User' = 'officer-lee' }
$correlation = 'smoke-' + [Guid]::NewGuid().ToString('N')
$officer['X-Correlation-ID'] = $correlation
$submitted = Invoke-RestMethod "$BaseUrl/api/claims" -Method Post -Headers $claimant -ContentType 'application/json' -Body (@{
    type = 'Motor'; incidentDate = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
    description = 'Synthetic incident for the local workflow verification'
    market = 'M01'; currency = 'SGD'; claimedAmount = 1500
} | ConvertTo-Json)
$assigned = Invoke-RestMethod "$BaseUrl/api/claims/$($submitted.id)/assignment" -Method Post -Headers $officer -ContentType 'application/json' -Body (@{
    rowVersion = $submitted.rowVersion
} | ConvertTo-Json)
$decided = Invoke-RestMethod "$BaseUrl/api/claims/$($submitted.id)/decision" -Method Post -Headers $officer -ContentType 'application/json' -Body (@{
    rowVersion = $assigned.rowVersion; outcome = 'Approved'; approvedAmount = 1250; reason = 'Synthetic assessment reason'
} | ConvertTo-Json)
if ($decided.status -ne 'Approved' -or $decided.reserveAmount -ne 1250) { throw 'Decision or reserve did not match expected result.' }
$history = Invoke-RestMethod "$BaseUrl/api/claims/$($submitted.id)/history" -Headers $claimant
if ($history.Count -ne 3) { throw 'Expected submission, assignment, and decision history.' }
$deadline = [DateTime]::UtcNow.AddSeconds(45)
do {
    $notifications = Invoke-RestMethod "$BaseUrl/api/claims/$($submitted.id)/notifications" -Headers $claimant
    if ($notifications.Count -gt 0) { break }
    Start-Sleep -Milliseconds 500
} while ([DateTime]::UtcNow -lt $deadline)
if ($notifications.Count -ne 1) { throw 'Expected one Kafka-created notification within 45 seconds.' }
Write-Output "PASS: submit -> pickup -> approval -> history -> Kafka notification. Claim $($submitted.reference); correlation $correlation"
