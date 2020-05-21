rem <projectdir> <targetdir> <targetfilename> <cdePlatform - X64_V3 NETSTD_V20 etc.>
call "%~dp0\signMeSha.bat" "%~2%~3" "%~dp0"
"%~dp0\cdePackager\cdePackager.exe" "%~2%~3" "%~2." "%~2." "%~4"
if not %errorlevel%==0 goto :EOF
