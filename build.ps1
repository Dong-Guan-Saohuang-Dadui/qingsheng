$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /out:XunfeiLite.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll XunfeiLite.cs Settings.cs Desktop.cs FeatureTests.cs Continuous.cs ContinuousTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }


