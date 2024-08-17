#!/usr/bin/env pwsh

Param(
    [ValidateNotNullOrEmpty()]
    [string]$Target = "FullBuild",

    [ValidateNotNullOrEmpty()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$DotnetVerbosity = "minimal"
)

#######################################################################
# SHARED VARIABLES

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$repositoryDir = (Get-Item $PSScriptRoot).FullName
$srcDir = Join-Path $repositoryDir "src"
$dotnetSolutionFile = Get-ChildItem -Path $srcDir -Filter "*.sln" | Select-Object -First 1
$buildCoreVersionFile = Join-Path $repositoryDir "version.yaml"
$buildCoreVersion = $(Get-Content "$buildCoreVersionFile").Substring("version:".Length).Trim()
$buildVersion = "$buildCoreVersion$VersionSuffix"

$dockerComposeProject = "poc"

# This build system expects following solution layout:
# solution_root/
#   build.ps1                  -- PowerShell build CLI
#   docker-compose.yaml
#   src/
#     Project1/
#       Project1.csproj        -- project base filename matches directory name
#     Project1.Tests/
#       Project1.Tests.csproj  -- tests projects are xUnit-based; project name must have suffix '.Tests'
#     Project2/
#       Project2.fsproj
#       imagename.Dockerfile   -- if the `*.Dockerfile` is present, we'll build Docker image `imagename`
#     Project2.Tests/
#       Project2.Tests.fsproj
#     SolutionName.sln         -- only one '.sln' file in 'src'
#   version.yaml               -- core part of solution SemVer

#######################################################################
# LOGGING

Function LogInfo {
    Param([ValidateNotNullOrEmpty()] [string]$Message)
    Write-Host -ForegroundColor Green $Message
}

Function LogWarning {
    Param([ValidateNotNullOrEmpty()] [string]$Message)
    Write-Host -ForegroundColor Yellow "*** $Message"
}

Function LogError {
    Param([ValidateNotNullOrEmpty()] [string]$Message)
    Write-Host -ForegroundColor Red "*** $Message"
}

Function LogStep {
    Param([ValidateNotNullOrEmpty()] [string]$Message)
    Write-Host -ForegroundColor Yellow "--- STEP: $Message"
}

Function LogTarget {
    Param([ValidateNotNullOrEmpty()] [string]$Message)
    Write-Host -ForegroundColor Green "--- TARGET: $Message"
}

Function LogCmd {
    Param([ValidateNotNullOrEmpty()] [string]$Message)
    Write-Host -ForegroundColor Yellow "--- $Message"
}

#######################################################################
# STEPS

Function PreludeStep_ValidateDotNetCli {
    LogStep "Prelude: .NET CLI"
    $dotnetCmd = Get-Command "dotnet" -ErrorAction Ignore
    if (-Not $dotnetCmd) {
        throw ".NET CLI 'dotnet' command not found."
    }
}

Function PreludeStep_ValidateDockerCli {
    LogStep "Prelude: Docker CLI"
    $dockerCmd = Get-Command "docker" -ErrorAction Ignore
    if (-Not $dockerCmd) {
        throw "Docker CLI 'docker' command not found."
    }
}

Function Step_PruneBuild {
    LogStep "PruneBuild"

    $pruneDir = $repositoryDir
    LogWarning "Pruning $pruneDir build artifacts"

    # Prune nested directories
    'bin', 'obj', 'publish' | ForEach-Object {
        Get-ChildItem -Path $pruneDir -Filter $_ -Directory -Recurse | ForEach-Object { $_.Delete($true) }
    }

    # Prune nested files
    '*.trx', '*.fsx.lock', '*.Tests_*.xml' | ForEach-Object {
        Get-ChildItem -Path $pruneDir -Filter $_ -File -Recurse | ForEach-Object { $_.Delete() }
    }

    # Prune top-level items
    '.fable', '.ionide', 'build', 'project', 'target', 'node_modules' | ForEach-Object {
        if (Test-Path $_) {
            Remove-Item -Path $(Join-Path $pruneDir $_) -Recurse -Force
        }
    }
}

Function Step_PruneDocker {
    LogStep "PruneDocker"

    $pruneDir = $repositoryDir
    LogWarning "Pruning $pruneDir Docker artifacts"

    foreach ($resource in @('container', 'image', 'volume', 'network')) {
        LogCmd "docker $resource prune -f"
        & docker $resource prune -f | Out-Null
    }

    $dockerImages = $(docker image ls --format "{{.Repository}}")
    foreach ($image in $dockerImages) {
        if ($image.StartsWith("${dockerComposeProject}-")) {
            LogCmd "docker image rm $image"
            & docker image rm $image | Out-Null
        }
    }

    $dockerVolumes = $(docker volume ls --format "{{.Name}}")
    foreach ($volume in $dockerVolumes) {
        if ($volume.StartsWith("${dockerComposeProject}_")) {
            LogCmd "docker volume rm $volume"
            & docker volume rm $volume | Out-Null
        }
    }
}

Function Step_DotnetClean {
    LogStep "dotnet clean $dotnetSolutionFile --verbosity $DotnetVerbosity"
    & dotnet clean "$dotnetSolutionFile" --verbosity $DotnetVerbosity
}

Function Step_DotnetRestore {
    LogStep "dotnet restore $dotnetSolutionFile --verbosity $DotnetVerbosity"
    & dotnet restore "$dotnetSolutionFile" --verbosity $DotnetVerbosity
}

Function Step_DotnetBuild {
    LogStep "dotnet build $dotnetSolutionFile --no-restore --configuration $Configuration --verbosity $DotnetVerbosity /p:Version=$buildVersion"
    & dotnet build "$dotnetSolutionFile" --no-restore --configuration $Configuration --verbosity $DotnetVerbosity /p:Version=$buildVersion
}

Function Step_DotnetPublish {
    Param([ValidateNotNullOrEmpty()] [string]$ProjectFile, [ValidateNotNullOrEmpty()] [string]$PublishOutput)
    LogStep "dotnet publish $ProjectFile --output $PublishOutput --configuration $Configuration --verbosity $DotnetVerbosity /p:Version=$buildVersion"
    & dotnet publish "$ProjectFile" --output "$PublishOutput" --configuration $Configuration --verbosity $DotnetVerbosity /p:Version=$buildVersion
}

Function Step_DotnetTest {
    Param([ValidateNotNullOrEmpty()] [string]$ProjectFile)
    LogStep "dotnet test $ProjectFile --no-build --configuration $Configuration --test-adapter-path:. --logger:xunit"
    & dotnet test "$ProjectFile" --no-build --configuration $Configuration --test-adapter-path:. --logger:xunit
}

Function Step_DockerComposeStart {
    LogStep "docker compose -p $dockerComposeProject up --build --abort-on-container-exit"
    Start-Process -FilePath "docker" -ArgumentList "compose", "-p", $dockerComposeProject, "up", "--build", "--abort-on-container-exit" -WorkingDirectory "$repositoryDir" -Wait
}

Function Step_DockerComposeStartDetached {
    LogStep "docker compose -p $dockerComposeProject up --build -d"
    Start-Process -FilePath "docker" -ArgumentList "compose", "-p", $dockerComposeProject, "up", "--build", "-d" -WorkingDirectory "$repositoryDir"
}

Function Step_DockerComposeStop {
    LogStep "docker compose -p $dockerComposeProject down"
    Start-Process -FilePath "docker" -ArgumentList "compose", "-p", $dockerComposeProject, "-f", "./docker-compose.yaml", "down" -WorkingDirectory "$repositoryDir" -Wait
}


#######################################################################
# PRELUDE TARGET
# Special target that is called automatically

Function Target_Prelude {
    LogTarget "Prelude"

    PreludeStep_ValidateDotNetCli
    PreludeStep_ValidateDockerCli
}


#######################################################################
# TARGETS

Function Target_Dotnet_Clean {
    LogTarget "DotNet.Clean"
    Step_DotnetClean
}

Function Target_Dotnet_Restore {
    LogTarget "DotNet.Restore"
    Step_DotnetRestore
}

Function Target_Dotnet_Build {
    DependsOn "Dotnet_Restore"

    LogTarget "DotNet.Build"
    Step_DotnetBuild
}

Function Target_Dotnet_Test {
    DependsOn "Dotnet.Build"

    LogTarget "DotNet.Test"
    $projects = Get-ChildItem -Path $srcDir -Filter "*.Tests.csproj" -Recurse -File
    foreach ($projectFile in $projects) {
        Step_DotnetTest $projectFile
    }
}

Function Target_Dotnet_Publish {
    DependsOn "Dotnet.Build"

    LogTarget "DotNet.Publish"
    $dockerfiles = Get-ChildItem -Path $srcDir -Filter "*.Dockerfile" -Recurse -File
    foreach ($dockerFile in $dockerfiles) {
        LogInfo "Dockerfile found: $dockerFile"
        $projectDirectory = $dockerFile.Directory
        $projectFile = Get-ChildItem -Path $projectDirectory -Filter "*.?sproj" | Select-Object -First 1
        $publishOutput = [System.IO.Path]::Combine($projectDirectory, "bin", "publish")
        Step_DotnetPublish $projectFile $publishOutput
    }
}

Function Target_DockerCompose_Start {
    DependsOn "Dotnet.Publish"

    LogTarget "DockerCompose.Start"
    try {
        Step_DockerComposeStart
    }
    finally {
        # ensure proper cleanup
        Step_DockerComposeStop
    }
}

Function Target_DockerCompose_StartDetached {
    DependsOn "Dotnet.Publish"

    LogTarget "DockerCompose.StartDetached"
    Step_DockerComposeStartDetached
}

Function Target_DockerCompose_Stop {
    LogTarget "DockerCompose.Stop"
    Step_DockerComposeStop
}

Function Target_FullBuild {
    DependsOn "Dotnet.Build"
    DependsOn "Dotnet.Test"
    DependsOn "Dotnet.Publish"

    LogTarget "FullBuild"
    LogInfo "DONE"
}


#######################################################################
# DEPENDENCIES TRACKING

$targetCalls = @{ }
Function DependsOn {
    Param([ValidateNotNullOrEmpty()] [string]$Target)
    if (-Not $targetCalls.ContainsKey($Target)) {
        Invoke_BuildTarget $Target
        $targetCalls.Add($Target, $(Get-Date))
    }
}

Function Invoke_BuildTarget {
    Param([ValidateNotNullOrEmpty()] [string]$Target)
    $normalizedTarget = $Target.Replace(".", "_")
    Invoke-Expression "Target_$normalizedTarget"
}

#######################################################################
# MAIN ENTRY POINT

$exitResult = 0

$currentLocation = Get-Location
try {
    LogInfo "*** BUILD: $Target ($Configuration) in $repositoryDir"

    switch ($Target) {
        "Prune" { Step_PruneBuild }
        "Prune.Docker" { Step_PruneDocker }
        default {
            DependsOn "Prelude"
            Invoke_BuildTarget $Target
        }
    }
    Write-Host ""
    LogInfo "DONE"
}
catch [System.Exception] {
    $errorMessage = "$_"
    if ($errorMessage.StartsWith("The term 'Target_$Target' is not recognized") -Or
        $errorMessage.StartsWith("The term 'Target_$normalizedTarget' is not recognized")) {
        LogError("Target $Target is not recognized")
    }
    else {
        LogError($errorMessage)
    }
    $exitResult = 1
}
finally {
    $stopwatch.Stop()
    LogInfo "*** Completed in: $($stopwatch.Elapsed)"
    Set-Location $currentLocation
}

Exit $exitResult
