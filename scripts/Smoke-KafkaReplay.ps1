[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [ValidateRange(15, 180)][int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$topic = 'claims.decisions.v1'
$group = 'claims.notifications.v1'
$headers = @{ 'X-Demo-User' = 'claimant-alex' }

function Read-GroupOffset {
    $lines = @(& docker compose exec -T kafka /opt/kafka/bin/kafka-consumer-groups.sh `
        --bootstrap-server kafka:19092 --describe --group $group 2>&1)
    if ($LASTEXITCODE -ne 0) { return $null }
    foreach ($line in $lines) {
        $columns = ([string]$line).Trim() -split '\s+'
        if ($columns.Count -ge 6 -and $columns[0] -eq $group -and
            $columns[1] -eq $topic -and $columns[2] -eq '0' -and
            $columns[3] -match '^\d+$' -and $columns[4] -match '^\d+$' -and
            $columns[5] -match '^\d+$') {
            return [pscustomobject]@{
                Current = [long]$columns[3]
                End = [long]$columns[4]
                Lag = [long]$columns[5]
            }
        }
    }
    return $null
}

Push-Location $workspace
try {
    # Read health only. This test never starts/stops containers or changes consumer offsets.
    do {
        $containerIds = @(& docker compose ps --quiet kafka 2>&1)
        if ($LASTEXITCODE -ne 0) { throw 'Docker could not be accessed. Run this local verification with Docker process access.' }
        $containerId = if ($LASTEXITCODE -eq 0 -and $containerIds.Count -eq 1) { [string]$containerIds[0] } else { '' }
        $health = if ($containerId) { & docker inspect --format '{{.State.Health.Status}}' $containerId 2>$null } else { '' }
        if ($health -eq 'healthy') { break }
        if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'Kafka did not become healthy before the replay-test deadline.' }
        Start-Sleep -Seconds 2
    } while ($true)

    $page = Invoke-RestMethod -Uri "$BaseUrl/api/claims?page=1&pageSize=100" -Headers $headers
    $selectedClaim = $null
    $selectedDecision = $null
    $selectedNotification = $null
    foreach ($claim in $page.items) {
        if ($claim.status -ne 'Approved') { continue }
        $notifications = Invoke-RestMethod -Uri "$BaseUrl/api/claims/$($claim.id)/notifications" -Headers $headers
        if ($notifications.Count -ne 1) { continue }
        $decision = @($claim.decisions | Where-Object {
            $_.outcome -eq 'Approved' -and $_.id -eq $notifications[0].eventId
        })
        if ($decision.Count -eq 1) {
            $selectedClaim = $claim
            $selectedDecision = $decision[0]
            $selectedNotification = $notifications[0]
            break
        }
    }
    if ($null -eq $selectedClaim) {
        throw 'No approved synthetic claimant-alex claim with exactly one matching decision notification was found. Run the workflow smoke first.'
    }
    if ($selectedClaim.ownerId -ne 'claimant-alex') { throw 'Replay input is not owned by the expected synthetic demo identity.' }

    # Establish a caught-up baseline so a zero lag observed before consumption cannot pass this test.
    do {
        $baseline = Read-GroupOffset
        if ($null -ne $baseline -and $baseline.Lag -eq 0) { break }
        if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'The notification consumer did not reach zero lag before replay.' }
        Start-Sleep -Seconds 2
    } while ($true)

    $replay = [ordered]@{
        eventId = $selectedNotification.eventId
        version = 1
        claimId = $selectedClaim.id
        claimReference = $selectedClaim.reference
        ownerId = $selectedClaim.ownerId
        outcome = $selectedDecision.outcome
        occurredAtUtc = $selectedDecision.occurredAtUtc
        correlationId = "replay-$([Guid]::NewGuid().ToString('N'))"
    }
    $json = $replay | ConvertTo-Json -Compress
    $record = "$($selectedClaim.id)|$json"
    $producerOutput = @($record | & docker compose exec -T kafka /opt/kafka/bin/kafka-console-producer.sh `
        --bootstrap-server kafka:19092 --topic $topic --producer-property acks=all `
        --property parse.key=true --property 'key.separator=|' 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Kafka console producer failed; duplicate consumption was not verified.' }

    $minimumOffset = $baseline.End + 1
    do {
        $observed = Read-GroupOffset
        if ($null -ne $observed -and $observed.End -ge $minimumOffset -and
            $observed.Current -ge $minimumOffset -and $observed.Lag -eq 0) { break }
        if ([DateTimeOffset]::UtcNow -ge $deadline) {
            throw 'The consumer did not commit through the acknowledged replay record before the deadline. The test has not passed.'
        }
        Start-Sleep -Seconds 2
    } while ($true)

    $after = Invoke-RestMethod -Uri "$BaseUrl/api/claims/$($selectedClaim.id)/notifications" -Headers $headers
    if ($after.Count -ne 1 -or $after[0].eventId -ne $selectedNotification.eventId -or
        $after[0].id -ne $selectedNotification.id) {
        throw 'Notification identity/count changed after the consumer committed through replay.'
    }

    [pscustomobject]@{
        Result = 'PASS'
        Verification = 'Same event ID republished through live Kafka; consumer committed through the new record; original notification preserved.'
        ClaimId = $selectedClaim.id
        EventId = $selectedNotification.eventId
        OriginalNotificationId = $selectedNotification.id
        NotificationCount = $after.Count
        BaselineEndOffset = $baseline.End
        FinalCurrentOffset = $observed.Current
        FinalEndOffset = $observed.End
        FinalLag = $observed.Lag
        Limit = 'Reconstructed version-1 event with a fresh correlation ID; not a byte-identical replay of the original record. Verifies this single local partition/consumer flow.'
    }
}
finally { Pop-Location }
