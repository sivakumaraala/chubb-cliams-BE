param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$claimant = @{ 'X-Demo-User' = 'claimant-alex' }
$officer = @{ 'X-Demo-User' = 'officer-lee'; 'X-Correlation-ID' = ('recovery-' + [Guid]::NewGuid().ToString('N')) }
Push-Location $projectRoot
try {
    docker compose stop kafka
    if ($LASTEXITCODE -ne 0) { throw 'Could not stop the local assessment broker.' }
    Write-Output 'Kafka stopped; checking that claim decisions still commit.'
    $submitted = Invoke-RestMethod "$BaseUrl/api/claims" -Method Post -Headers $claimant -ContentType 'application/json' -Body (@{
        type = 'Property'; incidentDate = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
        description = 'Synthetic incident for broker-outage verification'
        market = 'M02'; currency = 'AUD'; claimedAmount = 2400
    } | ConvertTo-Json)
    $assigned = Invoke-RestMethod "$BaseUrl/api/claims/$($submitted.id)/assignment" -Method Post -Headers $officer -ContentType 'application/json' -Body (@{
        rowVersion = $submitted.rowVersion
    } | ConvertTo-Json)
    $decided = Invoke-RestMethod "$BaseUrl/api/claims/$($submitted.id)/decision" -Method Post -Headers $officer -ContentType 'application/json' -Body (@{
        rowVersion = $assigned.rowVersion; outcome = 'Approved'; approvedAmount = 2100; reason = 'Synthetic decision during broker outage'
    } | ConvertTo-Json)
    if ($decided.status -ne 'Approved') { throw 'Decision failed while broker was stopped.' }
    $waiting = Invoke-RestMethod "$BaseUrl/api/claims/$($submitted.id)/notifications" -Headers $claimant
    if ($waiting.Count -ne 0) { throw 'Unexpected notification before broker restart.' }
    Write-Output 'Decision committed and notification absent while Kafka is stopped.'
    docker compose up -d --wait --wait-timeout 120 kafka
    if ($LASTEXITCODE -ne 0) { throw 'Kafka did not recover.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        $received = Invoke-RestMethod "$BaseUrl/api/claims/$($submitted.id)/notifications" -Headers $claimant
        if ($received.Count -gt 0) { break }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($received.Count -ne 1) { throw 'Expected exactly one recovered notification.' }
    Write-Output "PASS: decision survived broker outage; one notification arrived after restart. Claim $($submitted.reference)"
}
finally {
    # Restore this project's broker even if a verification request fails.
    docker compose up -d --wait --wait-timeout 120 kafka
    Pop-Location
}
