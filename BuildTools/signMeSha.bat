rem SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
rem SPDX-License-Identifier: CC0-1.0
if not exist "%~dp0\real.sign" goto noSign
echo signing with 9dbc9dfbbe4588de1caf3ba2b672f94c5316c823 ts: http://timestamp.digicert.com
set "SignToolEXE=%~dp0\signtool.exe"
if exist %SignToolEXE% goto signNow
xcopy "%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.18362.0\x64\signtool.exe" "%~dp0"
:signNow
echo %SignToolEXE%
%SignToolEXE% sign /sha1 9dbc9dfbbe4588de1caf3ba2b672f94c5316c823 /t http://timestamp.digicert.com %1
goto exit
:noSign
echo !!!!!!! Skipped signing: No "%~dp0\real.sign" file found - expected if not an official build.
:exit
