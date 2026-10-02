param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $projectDir 'scripts\GamePath.ps1')

$resolvedGameDir = Resolve-ProbablyStolenGameDir -ExplicitGameDir $GameDir -ProjectDir $projectDir

$jsonTemplatePath = Join-Path $projectDir 'templates\local.settings.json.template'
$jsonOutputPath = Join-Path $projectDir 'local.settings.json'
$escapedJsonPath = $resolvedGameDir.Replace('\', '\\').Replace('"', '\"')
$json = (Get-Content -LiteralPath $jsonTemplatePath -Raw).Replace('__GAME_DIR__', $escapedJsonPath)
Set-Content -LiteralPath $jsonOutputPath -Value $json -Encoding utf8

$propsTemplatePath = Join-Path $projectDir 'templates\Directory.Build.props.template'
$propsOutputPath = Join-Path $projectDir 'Directory.Build.props'
$escapedXmlPath = [System.Security.SecurityElement]::Escape($resolvedGameDir)
$props = (Get-Content -LiteralPath $propsTemplatePath -Raw).Replace('__GAME_DIR_XML__', $escapedXmlPath)
Set-Content -LiteralPath $propsOutputPath -Value $props -Encoding utf8

Write-Host "本机游戏目录已配置：$resolvedGameDir"
Write-Host "构建配置：$jsonOutputPath"
Write-Host "IDE/MSBuild 配置：$propsOutputPath"
Write-Host '以后可直接运行 .\build.ps1；部署时运行 .\deploy.ps1。'

