param($Context)

$ErrorActionPreference = 'Stop'
$report = [ordered]@{ commands = [ordered]@{}; shots = [ordered]@{}; inputs = [ordered]@{} }
$observerId = [int]$Context.Actor.id
$script:selection = 0

function Save-Report {
    $report | ConvertTo-Json -Depth 12 | Set-Content "$($Context.Evidence)/scenario.json" -Encoding utf8
}

function Server([string]$Command) {
    $reply = (Invoke-LabServer $Command) -join ' '
    $report.commands[$Command] = $reply
    return $reply
}

function Spawn-Dummy([string]$Name) {
    $known = @(Observe | ForEach-Object id)
    $null = Server "/dummy spawn $Name"
    $deadline = (Get-Date).AddSeconds(20)
    do {
        $actor = @(Observe | Where-Object { $_.dummy -and $_.ready -and $_.id -notin $known })[0]
        if ($actor) { return $actor }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    throw "Dummy $Name did not become ready."
}

function Follow([string]$Name) {
    $button = @(Get-LabUi | Where-Object { $_.type -eq 'Button' -and $_.text -match [regex]::Escape($Name) })
    if ($button.Count -ne 1) { throw "Expected one native spectator button for $Name, got $($button.Count)." }
    $rect = $button[0].rect
    $script:selection++
    $null = Invoke-LabInput @{
        id = "select-$script:selection"; frames = 180; inputFrames = 2; keys = @(323)
        cursorX = ($rect.x0 + $rect.x1) / 2; cursorY = ($rect.y0 + $rect.y1) / 2; capture = $true; audio = $true
    }
}

function Record([string]$Name, [int]$Frames = 180) {
    $report.inputs[$Name] = Invoke-LabInput @{ id = $Name; frames = $Frames; capture = $true; audio = $true }
    $report.shots[$Name] = Invoke-LabScreenshot -Name $Name
    Save-Report
}

function Read-SurfacePair {
    return @(
        Wait-LabActor { param($actor) $actor.ready -and $actor.position.y -gt 200 } 'SpinA not on the Surface' -ActorId $a.id
        Wait-LabActor { param($actor) $actor.ready -and $actor.position.y -gt 200 } 'SpinB not on the Surface' -ActorId $b.id
    )
}

function Try-FramePair {
    $pair = @(Read-SurfacePair)
    $dx = $pair[0].position.x - $pair[1].position.x
    $dz = $pair[0].position.z - $pair[1].position.z
    $distance = [Math]::Sqrt($dx * $dx + $dz * $dz)
    $faceA = Server "/spinprobe face $($a.id) $($b.id)"
    $faceB = Server "/spinprobe face $($b.id) $($a.id)"
    $report.placement = @{ distance = $distance; actors = @(Read-SurfacePair); faceA = $faceA; faceB = $faceB }
    return $distance -ge 2 -and $distance -le 16 -and $faceA -match '\bCLEAR\b' -and $faceB -match '\bCLEAR\b'
}

$a = $null
$b = $null
$flipped = $false
try {
    $existing = @(Observe | Where-Object { $_.dummy -and $_.ready -and $_.role -in @('NtfPrivate', 'NtfCaptain') })
    if ($existing.Count -eq 2) {
        $a = @($existing | Where-Object role -eq 'NtfPrivate')[0]
        $b = @($existing | Where-Object role -eq 'NtfCaptain')[0]
        if (!$a -or !$b -or !(Try-FramePair)) { throw 'Retained actors no longer share a clear Surface view.' }
    } else {
    if ((Server '/roundtime') -match 'not started') {
        $lobby = Server '/lobbylock'
        if ($lobby -match 'Lobby lock disabled' -and $lobby -notmatch 'locked the solo lobby') { $null = Server '/lobbylock' }
        if ((Server '/forcestart') -notmatch 'Forced round start') { throw 'Could not start the round.' }
        $deadline = (Get-Date).AddSeconds(20)
        do { Start-Sleep -Milliseconds 500; $round = Server '/roundtime' } until ($round -match 'Round time:' -or (Get-Date) -gt $deadline)
        if ($round -notmatch 'Round time:') { throw 'Round time did not start.' }
        $null = Server '/decontamination disable'
    }
    foreach ($faction in 'NTF', 'Chaos') { $null = Server "/wave forcepause $faction true" }

    $a = Spawn-Dummy 'SpinA'
    $b = Spawn-Dummy 'SpinB'
    $null = Server "/forcerole $($a.id) NtfPrivate"
    $null = Server "/forcerole $($b.id) NtfCaptain"
    foreach ($target in $a, $b) { $null = Server "/god $($target.id) enable" }
    $null = Read-SurfacePair

    if (!(Try-FramePair)) {
        foreach ($name in 'ESCAPE_PRIMARY', 'ESCAPE_SECONDARY') {
            if ((Server "/spinprobe open $name") -notmatch "\bOPEN $name\b") { throw "Could not open native door $name." }
        }
        Start-Sleep -Milliseconds 1500
        $null = Server "/doortp $($a.id) ESCAPE_PRIMARY"
        $null = Server "/doortp $($b.id) ESCAPE_SECONDARY"
        $null = Invoke-LabInput @{ frames = 90 }
        if (!(Try-FramePair)) { throw 'Native Surface door anchors did not give a clear line of sight.' }
        foreach ($actor in (Read-SurfacePair)) {
            $p = $actor.position
            $report["capsule-$($actor.id)"] = Server "labplacecheck $($p.x) $($p.y) $($p.z) $($actor.role)"
        }
    }

    $null = Server "/forcerole $observerId ClassD"
    $null = Wait-LabActor { param($actor) $actor.ready -and $actor.role -eq 'ClassD' } 'Client failed to become Class-D' -ActorId $observerId
    $null = Server "/god $observerId disable"
    $null = Server "/kill $observerId"
    $null = Wait-LabActor { param($actor) $actor.ready -and $actor.role -eq 'Spectator' } 'Client failed to become a spectator' -ActorId $observerId
    Start-Sleep -Seconds 8
    }
    Follow 'SpinA'
    Record 'surface-upright-baseline' 90

    $report.start = Server '/spinvisual all on 2400 1'
    if ($report.start -notmatch '\b2\b') { throw "Expected two spins: $($report.start)" }
    Record 'surface-follow-a'
    Follow 'SpinB'
    Record 'surface-follow-b'
    $report.stop = Server '/spinvisual all off'

    Follow 'SpinA'
    Record 'surface-before-flip' 90
    $report.inversion = Server "/spinprobe flip $($b.id) $observerId 1"
    if ($report.inversion -notmatch '\bSCALE\b') { throw "Inversion probe failed: $($report.inversion)" }
    $flipped = $true
    Record 'surface-inverted'
    $report.restore = Server "/spinprobe flip $($b.id) $observerId 0"
    $flipped = $false
    if ($report.restore -notmatch '\bSCALE\b') { throw "Restore probe failed: $($report.restore)" }
    Record 'surface-restored' 90
} catch {
    $report.failure = $_.ToString()
    throw
} finally {
    if ($flipped) { $null = Server "/spinprobe flip $($b.id) $observerId 0" }
    $null = Server '/spinvisual all off'
    Save-Report
}
