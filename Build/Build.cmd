@echo on
call "%VS110COMNTOOLS%vsvars32.bat"

msbuild.exe /ToolsVersion:4.0 "..\Espera\Espera.sln" /p:configuration=Release

set ReleaseDirPortable=..\Release\Portable
mkdir %ReleaseDirPortable%

set bin=..\Espera\Espera.View\bin\Release

copy %bin%\Caliburn.Micro.dll %ReleaseDirPortable%\Caliburn.Micro.dll
copy %bin%\Espera.Core.dll %ReleaseDirPortable%\Espera.Core.dll
copy %bin%\Espera.exe %ReleaseDirPortable%\Espera.exe
copy %bin%\Espera.exe.config .%ReleaseDirPortable%\Espera.exe.config
copy %bin%\Google.GData.Client.dll %ReleaseDirPortable%\Google.GData.Client.dll
copy %bin%\Google.GData.Extensions.dll %ReleaseDirPortable%\Google.GData.Extensions.dll
copy %bin%\Google.GData.YouTube.dll %ReleaseDirPortable%\Google.GData.YouTube.dll
copy %bin%\MahApps.Metro.dll %ReleaseDirPortable%\MahApps.Metro.dll
copy %bin%\MoreLinq.dll %ReleaseDirPortable%\MoreLinq.dll
copy %bin%\NAudio.dll %ReleaseDirPortable%\NAudio.dll
copy %bin%\Rareform.dll %ReleaseDirPortable%\Rareform.dll
copy %bin%\System.Windows.Interactivity.dll %ReleaseDirPortable%\System.Windows.Interactivity.dll
copy %bin%\taglib-sharp.dll %ReleaseDirPortable%\taglib-sharp.dll
copy %bin%\YoutubeExtractor.dll %ReleaseDirPortable%\YoutubeExtractor.dll

copy ..\Changelog.txt %ReleaseDirPortable%\Changelog.txt

msbuild.exe /target:publish /p:configuration=Release ..\Espera\Espera.sln"

set ReleaseDirSetup=..\Release\Setup
mkdir %ReleaseDirSetup%

xcopy /S /Y %bin%\app.publish %ReleaseDirSetup%

pause