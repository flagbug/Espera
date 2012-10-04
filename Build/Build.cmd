@echo on
call "%VS110COMNTOOLS%vsvars32.bat"

REM Portable project
msbuild.exe /ToolsVersion:4.0 "..\Espera\Espera.sln" /p:configuration=Release

set ReleaseDir=..\Release\

REM Cleanup before we create and copy everything
rmdir %ReleaseDir% /s /q

set ReleaseDirPortable=%ReleaseDir%Portable\
mkdir %ReleaseDirPortable%

set bin=..\Espera\Espera.View\bin\Release\

copy %bin%*.dll %ReleaseDirPortable%
copy %bin%Espera.exe %ReleaseDirPortable%
copy %bin%Espera.exe.config %ReleaseDirPortable%
copy ..\Changelog.txt %ReleaseDirPortable%

REM ClickOnce project
msbuild.exe /target:publish /p:configuration=Release ..\Espera\Espera.sln"

set ReleaseDirSetup=%ReleaseDir%Setup\
mkdir %ReleaseDirSetup%

xcopy /S /Y %bin%\app.publish %ReleaseDirSetup%

pause