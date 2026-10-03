@echo off
rem CtrlV Paste Image - build with built-in .NET Framework csc (no install needed)
rem Output: dist\CtrlV<chinese>.exe (~75 KB)
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "dist" mkdir dist
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:"dist\CtrlV´æÍ¼.exe" /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /win32icon:"paste.ico" "paste_image.cs"
echo done: dist\CtrlV´æÍ¼.exe