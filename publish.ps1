<#
.SYNOPSIS
  Puts the project under git, pushes it to GitHub, enables the CI workflow,
  tags a release (CI then packs and pushes to nuget.org) and opens the
  XrmToolBox portal for registration.

.EXAMPLE
  .\publish.ps1                      # release the version already in the csproj
  .\publish.ps1 -Version 1.2026.11.2 # raise the version first
  .\publish.ps1 -DryRun              # print what would happen, change nothing
  .\publish.ps1 -NoRelease           # git + GitHub + CI only, no tag

  Safe to run again: every step checks what is already done.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Owner = 'mokasha',
    [string]$Repo  = 'XrmToolBox.SecurityModelSync',
    [switch]$NoRelease,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$csproj    = 'src\SecurityModelSync\SecurityModelSync.csproj'
$nuspec    = 'src\SecurityModelSync\SecurityModelSync.nuspec'
$packageId = 'Mohran.XrmToolBox.SecurityModelSync'
$remoteUrl = "https://github.com/$Owner/$Repo.git"
$repoWeb   = "https://github.com/$Owner/$Repo"

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

# Runs a native command and stops the script when it fails.
function Invoke-Native {
    param([string]$Exe, [string[]]$Arguments, [switch]$ReadOnly)
    if ($DryRun -and -not $ReadOnly) {
        Write-Host "   [dry run] $Exe $($Arguments -join ' ')" -ForegroundColor DarkGray
        return
    }
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Exe $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

function Test-Command($name) { [bool](Get-Command $name -ErrorAction SilentlyContinue) }

function Wait-ForUser($message) {
    if ($DryRun) { Write-Host "   [dry run] would wait: $message" -ForegroundColor DarkGray; return }
    Read-Host "$message  Press Enter when done" | Out-Null
}

function Open-Page($url) {
    if ($DryRun) { Write-Host "   [dry run] would open $url" -ForegroundColor DarkGray; return }
    Start-Process $url
}

# ---------------------------------------------------------------- pre-flight
Step 'Checking tools'
foreach ($tool in 'git', 'dotnet') {
    if (-not (Test-Command $tool)) { throw "$tool is not on the PATH." }
}
$hasGh = Test-Command 'gh'
Write-Host "   git, dotnet found. GitHub CLI (gh): $(if ($hasGh) { 'found' } else { 'not found - GitHub steps open in the browser' })"

# ---------------------------------------------------------------- version
Step 'Version'
$csprojText = Get-Content $csproj -Raw
if ($csprojText -notmatch '<Version>([^<]+)</Version>') { throw "No <Version> in $csproj" }
$currentVersion = $Matches[1]

if ($Version -and $Version -ne $currentVersion) {
    if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw "Version '$Version' is not in the form 1.2026.11.2" }
    Write-Host "   $currentVersion -> $Version"
    if (-not $DryRun) {
        $csprojText = $csprojText -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
        [IO.File]::WriteAllText((Resolve-Path $csproj), $csprojText, (New-Object Text.UTF8Encoding $false))
    }
    Write-Host "   Remember to add a line to <releaseNotes> in $nuspec." -ForegroundColor Yellow
} else {
    $Version = $currentVersion
    Write-Host "   $Version (from the csproj)"
}
$tag = "v$Version"

# ---------------------------------------------------------------- build + test
Step 'Release build and engine tests'
Invoke-Native dotnet @('build', 'SecurityModelSync.sln', '-c', 'Release', '--nologo', '-v', 'q') -ReadOnly
Invoke-Native dotnet @('run', '--project', 'tests\EngineTests', '-c', 'Release', '--no-build') -ReadOnly

# ---------------------------------------------------------------- step 3: CI workflow
Step 'CI workflow'
$workflow = '.github\workflows\build.yml'
if (-not $DryRun) {
    New-Item -ItemType Directory -Force (Split-Path $workflow) | Out-Null
    Copy-Item 'ci\github-build.yml' $workflow -Force
}
Write-Host "   ci\github-build.yml -> $workflow"

# ---------------------------------------------------------------- step 2: git
Step 'Git repository'
if (-not (Test-Path '.git')) {
    Invoke-Native git @('init', '-b', 'main')
} else {
    Write-Host '   already a repository'
}

if ($DryRun -and -not (Test-Path '.git')) {
    Write-Host '   [dry run] git add -A; git commit' -ForegroundColor DarkGray
} else {
    Invoke-Native git @('add', '-A')
    $pending = git status --porcelain
    if ($pending) {
        Write-Host '   Files to commit:'
        $pending | ForEach-Object { Write-Host "     $_" }
        $hasCommits = $true
        git rev-parse --verify --quiet HEAD | Out-Null
        if ($LASTEXITCODE -ne 0) { $hasCommits = $false }
        $message = if ($hasCommits) { "Release $Version" } else { "Security Model Sync $Version" }
        Invoke-Native git @('commit', '-q', '-m', $message)
    } else {
        Write-Host '   nothing to commit'
    }
}

# ---------------------------------------------------------------- step 2: GitHub
Step "GitHub repository $Owner/$Repo"
$hasOrigin = $false
if (Test-Path '.git') { $hasOrigin = [bool](git remote | Where-Object { $_ -eq 'origin' }) }
if (-not $hasOrigin) { Invoke-Native git @('remote', 'add', 'origin', $remoteUrl) }

$repoExists = $false
if (-not $DryRun) {
    git ls-remote $remoteUrl 2>$null | Out-Null
    $repoExists = ($LASTEXITCODE -eq 0)
}
if (-not $repoExists) {
    if ($hasGh) {
        Invoke-Native gh @('repo', 'create', "$Owner/$Repo", '--public',
            '--description', 'XrmToolBox tool: compare and copy the security model between two Dataverse organizations')
    } else {
        Write-Host '   Create the repository in the browser: public, and EMPTY (no README, no .gitignore, no licence).'
        Open-Page "https://github.com/new?name=$Repo&visibility=public"
        Wait-ForUser '   Create the repository.'
    }
}
Invoke-Native git @('push', '-u', 'origin', 'main')

if ($NoRelease) {
    Write-Host "`nDone. Pushed to $repoWeb - no release was tagged (-NoRelease)." -ForegroundColor Green
    return
}

# ---------------------------------------------------------------- step 3: nuget secret
Step 'NUGET_API_KEY repository secret'
$secretSet = $false
if ($hasGh -and -not $DryRun) {
    $secretSet = [bool](gh secret list --repo "$Owner/$Repo" | Select-String '^NUGET_API_KEY\b')
}
if ($secretSet) {
    Write-Host '   already set'
} else {
    Write-Host '   1. Create an API key on nuget.org (scope: Push, glob pattern: Mohran.XrmToolBox.*).'
    Open-Page 'https://www.nuget.org/account/apikeys'
    if ($hasGh) {
        Write-Host '   2. Paste the key at the prompt below (it is sent to GitHub only, not stored on disk).'
        Invoke-Native gh @('secret', 'set', 'NUGET_API_KEY', '--repo', "$Owner/$Repo")
    } else {
        Write-Host '   2. Add it as a repository secret named NUGET_API_KEY on the page that opens.'
        Open-Page "$repoWeb/settings/secrets/actions/new"
        Wait-ForUser '   Save the secret.'
    }
}

# ---------------------------------------------------------------- step 4: release
Step "Release $tag"
$tagExists = $false
if (Test-Path '.git') { $tagExists = [bool](git tag --list $tag) }
if ($tagExists) {
    Write-Host "   Tag $tag already exists. To release again, run with a higher -Version." -ForegroundColor Yellow
} else {
    Write-Host "   Pushing tag $tag makes CI publish $packageId $Version to nuget.org."
    Write-Host '   A published NuGet version cannot be deleted or replaced.' -ForegroundColor Yellow
    $answer = if ($DryRun) { 'y' } else { Read-Host "   Publish $Version now? (y/n)" }
    if ($answer -ne 'y') {
        Write-Host "   Not released. Run the script again when ready." -ForegroundColor Yellow
        return
    }
    Invoke-Native git @('tag', '-a', $tag, '-m', "Release $Version")
    Invoke-Native git @('push', 'origin', $tag)
    Write-Host '   Watch the build:'
    Open-Page "$repoWeb/actions"
}

# ---------------------------------------------------------------- step 5: Tool Library
Step 'Register in the XrmToolBox Tool Library'
Write-Host "   When the build is green and https://www.nuget.org/packages/$packageId shows $Version"
Write-Host '   (nuget.org indexing takes a few minutes):'
Write-Host '   sign in on the XrmToolBox portal, open your tools, add a new tool and enter the package id:'
Write-Host "       $packageId" -ForegroundColor Green
Write-Host '   Registration is done once; later versions are picked up from nuget.org.'
Open-Page 'https://www.xrmtoolbox.com'

Write-Host "`nDone." -ForegroundColor Green
