#requires -Version 7.0

# GitHub CLI is mocked: these checks never contact or modify GitHub.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$publisher = Join-Path $PSScriptRoot 'Publish-GitHubRelease.ps1'
$fixture = Join-Path $root ('output/PrereleaseChecks/' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $fixture -Force
$assets = @('SuessLabs.SamElla-win-x64-Setup.exe', 'SuessLabs.SamElla-0.7.108-full.nupkg',
  'releases.win-x64.json', 'SamElla-v0.7.0.zip') | ForEach-Object {
  $path = Join-Path $fixture $_
  [IO.File]::WriteAllText($path, 'test artifact')
  $path
}
$global:PrereleaseTestChecks = 0

function Check([bool]$Condition, [string]$Message)
{
  if (-not $Condition)
  {
    throw "FAIL: $Message"
  }

  $global:PrereleaseTestChecks++
  Write-Host "PASS: $Message"
}

function Reset-Mock([string]$Channel = 'Develop')
{
  $global:PrereleaseTestState = @{
    IsPrerelease = $Channel -eq 'Develop'
    ExpectedTag = if ($Channel -eq 'Develop') { 'v0.7.0.108-pre' } else { 'v0.7.0.108' }
    TagCommit = $null; Release = $null; Uploaded = $false; FailUpload = $false
    FailApi = $false; Annotated = $false
    Calls = [Collections.Generic.List[object]]::new()
  }
}

function gh
{
  $arguments = @($args)
  $state = $global:PrereleaseTestState
  $state.Calls.Add($arguments)
  $global:LASTEXITCODE = 0
  if ($arguments[0] -eq 'api')
  {
    if ($state.FailApi)
    {
      $global:LASTEXITCODE = 1
      return 'HTTP 403: permission denied'
    }

    if ($arguments[1] -like '*/git/refs' -and $arguments -contains 'POST')
    {
      Check ($arguments -contains "ref=refs/tags/$($state.ExpectedTag)") 'A version-specific tag is explicitly created before the draft'
      $state.TagCommit = ($arguments | Where-Object { $_ -like 'sha=*' }) -replace '^sha=', ''
      return '{}'
    }

    if ($arguments[1] -like '*/git/ref/tags/*' -and $state.TagCommit)
    {
      $type = if ($state.Annotated) { 'tag' } else { 'commit' }
      return @{ object = @{ type = $type; sha = $state.TagCommit } } | ConvertTo-Json -Compress
    }

    if ($arguments[1] -like '*/git/tags/*')
    {
      return @{ object = @{ type = 'commit'; sha = $state.TagCommit } } | ConvertTo-Json -Compress
    }

    if ($arguments[1] -like '*/releases/tags/*' -and $state.Release)
    {
      return $state.Release | ConvertTo-Json -Compress
    }

    $global:LASTEXITCODE = 1
    return 'HTTP 404: Not Found'
  }

  if ($arguments[0] -ne 'release')
  {
    throw 'Unexpected GitHub command.'
  }

  switch ($arguments[1])
  {
    'create' {
      $preFlag = if ($state.IsPrerelease) { '--prerelease' } else { '--prerelease=false' }
      Check ($arguments -contains '--draft' -and $arguments -contains $preFlag -and $arguments -contains '--latest=false') 'Release starts as a non-latest draft with the requested channel'
      $target = [Array]::IndexOf($arguments, '--target')
      Check ($arguments -contains '--verify-tag' -and $state.TagCommit -eq $arguments[$target + 1]) 'The release requires an existing tag pointing at the tested commit'
      $state.Release = @{ draft = $true; prerelease = $state.IsPrerelease }
    }
    'upload' {
      if ($state.FailUpload)
      {
        $global:LASTEXITCODE = 1
        return 'Simulated asset upload failure'
      }

      Check ($arguments -contains '--clobber' -and @($assets | Where-Object { $_ -notin $arguments }).Count -eq 0) 'Upload includes every asset and safely resumes a draft'
      $state.Uploaded = $true
    }
    'edit' {
      Check $state.Uploaded 'Release becomes public only after all assets upload'
      $preFlag = if ($state.IsPrerelease) { '--prerelease' } else { '--prerelease=false' }
      $latestFlag = if ($state.IsPrerelease) { '--latest=false' } else { '--latest=true' }
      Check ($arguments -contains '--draft=false' -and $arguments -contains $preFlag -and $arguments -contains $latestFlag) 'Develop stays a prerelease; Stable is explicitly published as Latest'
      $state.Release.draft = $false
    }
    default { throw 'Unexpected release command.' }
  }
}

function Publish([switch]$ExpectFailure, [string[]]$Files = $assets, [string]$Channel = 'Develop')
{
  $failure = $null
  try
  {
    & $publisher -Repository 'example/game' -Commit ('a' * 40) -Version '0.7.0.108' -PullRequestNumber 69 -Channel $Channel -AssetPaths $Files
  }
  catch
  {
    $failure = $_.Exception.Message
  }

  if ($ExpectFailure -and -not $failure)
  {
    throw 'Expected publication to fail.'
  }

  if (-not $ExpectFailure -and $failure)
  {
    throw $failure
  }

  return $failure
}

try
{
  Reset-Mock
  $null = Publish
  Check ($global:PrereleaseTestState.TagCommit -eq ('a' * 40)) 'The new tag targets the tested version-bump commit'
  $count = $global:PrereleaseTestState.Calls.Count
  $null = Publish
  $later = @($global:PrereleaseTestState.Calls | Select-Object -Skip $count)
  Check (@($later | Where-Object { $_[0] -eq 'release' }).Count -eq 0) 'Rerunning a completed prerelease makes no writes'

  Reset-Mock
  $global:PrereleaseTestState.FailUpload = $true
  $failure = Publish -ExpectFailure
  Check ($failure -like '*upload*' -and $global:PrereleaseTestState.Release.draft) 'A failed upload leaves the release unpublished'
  $global:PrereleaseTestState.FailUpload = $false
  $null = Publish
  Check (-not $global:PrereleaseTestState.Release.draft) 'Retry resumes and publishes the existing draft'
  Check (@($global:PrereleaseTestState.Calls | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'create' }).Count -eq 1) 'Retry does not create another release or tag'

  Reset-Mock
  $global:PrereleaseTestState.TagCommit = 'b' * 40
  $failure = Publish -ExpectFailure
  Check ($failure -like '*never moved*') 'A conflicting existing tag is rejected'

  Reset-Mock
  $global:PrereleaseTestState.TagCommit = 'a' * 40
  $global:PrereleaseTestState.Release = @{ draft = $false; prerelease = $false }
  $failure = Publish -ExpectFailure
  Check ($failure -like '*will not be changed*') 'An existing stable release is never converted'

  Reset-Mock
  $global:PrereleaseTestState.FailApi = $true
  $failure = Publish -ExpectFailure
  Check ($failure -like '*403*') 'Permission failures stop publication instead of being treated as missing releases'

  Reset-Mock
  $failure = Publish -ExpectFailure -Files @($assets[0])
  Check ($failure -like '*requires*' -and $global:PrereleaseTestState.Calls.Count -eq 0) 'Missing package artifacts fail before any GitHub calls'

  Reset-Mock
  $global:PrereleaseTestState.TagCommit = 'a' * 40
  $global:PrereleaseTestState.Annotated = $true
  $null = Publish
  Check (-not $global:PrereleaseTestState.Release.draft) 'An existing annotated tag is resolved and verified'
  Reset-Mock -Channel Stable
  $null = Publish -Channel Stable
  Check ($global:PrereleaseTestState.TagCommit -eq ('a' * 40)) 'Master release tags the exact tested merge commit'
  Check (-not $global:PrereleaseTestState.Release.prerelease -and -not $global:PrereleaseTestState.Release.draft) 'Master publishes a public stable release'
  $count = $global:PrereleaseTestState.Calls.Count
  $null = Publish -Channel Stable
  $later = @($global:PrereleaseTestState.Calls | Select-Object -Skip $count)
  Check (@($later | Where-Object { $_[0] -eq 'release' }).Count -eq 0) 'Rerunning an older stable release does not move Latest'

  Reset-Mock -Channel Stable
  $global:PrereleaseTestState.FailUpload = $true
  $null = Publish -Channel Stable -ExpectFailure
  Check ($global:PrereleaseTestState.Release.draft -and -not $global:PrereleaseTestState.Release.prerelease) 'Failed stable uploads remain unpublished drafts'
  $global:PrereleaseTestState.FailUpload = $false
  $null = Publish -Channel Stable
  Check (-not $global:PrereleaseTestState.Release.draft) 'A stable draft resumes and publishes as Latest'

  Reset-Mock -Channel Stable
  $global:PrereleaseTestState.TagCommit = 'a' * 40
  $global:PrereleaseTestState.Release = @{ draft = $false; prerelease = $true }
  $failure = Publish -Channel Stable -ExpectFailure
  Check ($failure -like '*will not be changed*') 'An existing prerelease is not silently converted to stable'
  Write-Host "All $global:PrereleaseTestChecks release publishing checks passed. Fixtures: $fixture"
}
finally
{
  Remove-Variable -Scope Global -Name PrereleaseTestState, PrereleaseTestChecks -ErrorAction SilentlyContinue
}
