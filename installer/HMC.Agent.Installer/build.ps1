<# .SYNOPSIS
Builds the HMC Agent MSI installer.

.PARAMETER Version
Version number for the MSI (default: 1.0.0.0)

.PARAMETER OutputDir
Output directory for the MSI (default: ./output)

.PARAMETER Configuration
Build configuration (default: Release)
#>

param(
    [string]$Version = "1.0.0.0",
    [string]$OutputDir = "output",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\.."
$publishDir = "$PSScriptRoot\publish"
$agentSrc = "$root\src\HMC.Agent"
$agentTools = "$agentSrc\tools"

Write-Host "=== HMC Agent MSI Builder ===" -ForegroundColor Cyan

# Step 1: dotnet publish
Write-Host "[1/4] Publishing HMC.Agent (win-x64, $Configuration)..." -ForegroundColor Yellow
dotnet publish $agentSrc `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=embedded `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Write-Host "  -> Published to $publishDir" -ForegroundColor Green

# Step 2: Copy iPerf3 + cygwin1.dll
Write-Host "[2/4] Copying iPerf3 tools..." -ForegroundColor Yellow
$toolsTarget = "$publishDir\tools"
New-Item -ItemType Directory -Path $toolsTarget -Force | Out-Null

@("iperf3.exe", "cygwin1.dll") | ForEach-Object {
    if (Test-Path "$agentTools\$_") {
        Copy-Item "$agentTools\$_" $toolsTarget -Force
        Write-Host "  -> $_ copied" -ForegroundColor Green
    } else {
        Write-Host "  -> WARNING: $_ not found at $agentTools\$_" -ForegroundColor Yellow
        "" | Out-File -Encoding ascii "$toolsTarget\$_"
    }
}

# Step 3: Generate WiX source dynamically
Write-Host "[3/4] Generating WiX components from publish output..." -ForegroundColor Yellow

# Generate XML for all files in publish\ (root, no subdirs except 'tools' and 'runtimes')
function Gen-FileElement($file, $srcPath, $idPrefix) {
    $id = $idPrefix + ($file.Name -replace '[^a-zA-Z0-9]', '_')
    $src = $srcPath -replace '\\', '\\'
    return "                <File Id=`"$id`" Source=`"$src`" />"
}

function Gen-Component($dirPath, $dirRef, $compId, $keyFile, $srcPrefix) {
    $files = Get-ChildItem $dirPath -File -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -notlike "*.pdb"
    }
    if (-not $files -or $files.Count -eq 0) { return @{ Xml = ""; HasFiles = $false } }

    $keyXml = Gen-FileElement $files[0] "$srcPrefix\$($files[0].Name)" "f_"
    $otherXml = ($files | Select-Object -Skip 1 | ForEach-Object {
        Gen-FileElement $_ "$srcPrefix\$($_.Name)" "f_"
    }) -join "`n"

    $xml = @"
            <Component Id="$compId" Guid="$(New-Guid)" $dirRef>
$keyXml
$otherXml
            </Component>
"@
    return @{ Xml = $xml; HasFiles = $true }
}

$componentsXml = @()

# Main publish files
$mainComp = Gen-Component $publishDir "Directory=`"INSTALLFOLDER`"" "MainFiles" "" "publish"
if ($mainComp.HasFiles) { $componentsXml += $mainComp.Xml }

# Tools
$toolsComp = Gen-Component $toolsTarget "Directory=`"ToolsDir`"" "ToolsFiles" "" "publish\tools"
if ($toolsComp.HasFiles) { $componentsXml += $toolsComp.Xml }

# Runtimes (if any)
if (Test-Path "$publishDir\runtimes") {
    $runtimeDirs = Get-ChildItem "$publishDir\runtimes" -Directory
    foreach ($rd in $runtimeDirs) {
        $nativeDirs = Get-ChildItem $rd.FullName -Directory
        foreach ($nd in $nativeDirs) {
            $rid = "f_run_" + ($rd.Name -replace '[^a-zA-Z0-9]', '_') + "_" + ($nd.Name -replace '[^a-zA-Z0-9]', '_')
            $srcRel = "publish\runtimes\$($rd.Name)\$($nd.Name)"
            $rcomp = Gen-Component $nd.FullName "Directory=`"INSTALLFOLDER`"" $rid "" $srcRel
            if ($rcomp.HasFiles) { $componentsXml += $rcomp.Xml }
        }
    }
}

$allComponents = $componentsXml -join "`n"
$componentRefs = ([regex]::Matches($allComponents, 'Component Id="([^"]+)"') | ForEach-Object {
    "      <ComponentRef Id=`"$($_.Groups[1].Value)`" />"
}) -join "`n"

Write-Host "  -> Generated $(([regex]::Matches($allComponents, 'Component Id="')).Count) components" -ForegroundColor Green

# Generate the complete .wxs
$wxsContent = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">

  <?define ProductName="HMC Agent" ?>
  <?define Manufacturer="HMC" ?>
  <?define UpgradeCode="8F3A6B12-C4D5-4E2A-9B1F-7C8D3E5F6A10" ?>

  <Package Name="HMC Agent"
           Manufacturer="`$(var.Manufacturer)"
           Version="$Version"
           UpgradeCode="`$(var.UpgradeCode)"
           Language="1033"
           Codepage="1252">

    <Media Id="1" Cabinet="Agent.cab" EmbedCab="yes" />
    <MajorUpgrade DowngradeErrorMessage="A newer version of HMC Agent is already installed." />

    <SummaryInformation Keywords="Installer" Description="HMC Agent Installer"
                        Manufacturer="`$(var.Manufacturer)" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="HMC_FOLDER" Name="HMC">
        <Directory Id="INSTALLFOLDER" Name="Agent">

$allComponents

          <Directory Id="LogsDir" Name="logs">
            <Component Id="LogsDirComp" Guid="$(New-Guid)">
              <CreateFolder />
            </Component>
          </Directory>

          <Directory Id="ToolsDir" Name="tools" />

        </Directory>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="CommonAppDataFolder">
      <Directory Id="PdHmc" Name="HMC">
        <Directory Id="PdAgent" Name="Agent">
          <Directory Id="PdLogs" Name="logs">
            <Component Id="PdLogsComp" Guid="$(New-Guid)">
              <CreateFolder />
            </Component>
          </Directory>
        </Directory>
      </Directory>
    </StandardDirectory>

    <!-- Service install/uninstall -->
    <SetProperty Id="InstallService" Value='"[SystemFolder]sc.exe" create "HMC Agent" binPath= "[INSTALLFOLDER]HMC.Agent.exe" start= auto DisplayName= "HMC Agent"' Before="InstallService" Sequence="execute" />
    <CustomAction Id="InstallService" BinaryRef="Wix4UtilCA_`$(sys.BUILDARCHSHORT)" DllEntry="WixQuietExec64" Execute="deferred" Return="check" Impersonate="no" />

    <SetProperty Id="StartService" Value='"[SystemFolder]sc.exe" start "HMC Agent"' Before="StartService" Sequence="execute" />
    <CustomAction Id="StartService" BinaryRef="Wix4UtilCA_`$(sys.BUILDARCHSHORT)" DllEntry="WixQuietExec64" Execute="deferred" Return="ignore" Impersonate="no" />

    <SetProperty Id="StopService" Value='"[SystemFolder]sc.exe" stop "HMC Agent"' Before="StopService" Sequence="execute" />
    <CustomAction Id="StopService" BinaryRef="Wix4UtilCA_`$(sys.BUILDARCHSHORT)" DllEntry="WixQuietExec64" Execute="deferred" Return="ignore" Impersonate="no" />

    <SetProperty Id="UninstallService" Value='"[SystemFolder]sc.exe" delete "HMC Agent"' Before="UninstallService" Sequence="execute" />
    <CustomAction Id="UninstallService" BinaryRef="Wix4UtilCA_`$(sys.BUILDARCHSHORT)" DllEntry="WixQuietExec64" Execute="deferred" Return="ignore" Impersonate="no" />

    <InstallExecuteSequence>
      <Custom Action="StopService" After="InstallInitialize" Condition="REMOVE=&quot;ALL&quot;" />
      <Custom Action="UninstallService" After="StopService" Condition="REMOVE=&quot;ALL&quot;" />
      <Custom Action="InstallService" After="InstallFiles" Condition="NOT Installed AND NOT REMOVE" />
      <Custom Action="StartService" After="InstallService" Condition="NOT Installed AND NOT REMOVE" />
    </InstallExecuteSequence>

    <Feature Id="MainFeature" Title="HMC Agent" Level="1" ConfigurableDirectory="INSTALLFOLDER"
             Description="HMC Host Monitoring Agent">
$componentRefs
      <ComponentRef Id="LogsDirComp" />
      <ComponentRef Id="PdLogsComp" />
    </Feature>

    <UI Id="WixUI_InstallDir">
      <TextStyle Id="WixUI_Font_Normal" FaceName="Segoe UI" Size="9" />
      <TextStyle Id="WixUI_Font_Bold" FaceName="Segoe UI" Size="9" Bold="yes" />
      <TextStyle Id="WixUI_Font_Title" FaceName="Segoe UI" Size="12" Bold="yes" />
      <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />
    </UI>

    <Property Id="MSIUSEREALADMINDETECTION" Value="1" />

  </Package>
</Wix>
"@

$generatedWxs = "$PSScriptRoot\Generated.wxs"
$wxsContent | Set-Content $generatedWxs
Write-Host "  -> Generated WiX source: $generatedWxs" -ForegroundColor Green

# Step 4: Build MSI via dotnet build (resolves WiX extensions from NuGet)
Write-Host "[4/4] Building MSI via dotnet build..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Remove old .wxs files to avoid conflicts with generated one
Remove-Item "$PSScriptRoot\Package.wxs" -Force -ErrorAction SilentlyContinue

# Build the wixproj (picks up Generated.wxs from same directory)
dotnet build "$PSScriptRoot\HMC.Agent.Installer.wixproj" `
    -c Release `
    -p:OutputPath="$OutputDir" 2>&1

if ($LASTEXITCODE -ne 0) { throw "WiX build failed" }

# Find the MSI (dotnet build may put it in a subfolder)
$msiFile = Get-ChildItem "$OutputDir" -Filter "*.msi" -Recurse | Select-Object -First 1
if (-not $msiFile) { throw "MSI not found in output directory" }
$msiPath = $msiFile.FullName
$msiSize = $msiFile.Length

# Clean up generated files
Remove-Item "$PSScriptRoot\Generated.wxs" -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
Write-Host "  MSI:  $msiPath" -ForegroundColor Green
Write-Host "  Size: $([math]::Round($msiSize / 1MB, 1)) MB" -ForegroundColor Green
Write-Host "  Version: $Version" -ForegroundColor Green
