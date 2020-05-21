if not exist "%~dp0\real.sign" goto noSign
echo signing with 9dbc9dfbbe4588de1caf3ba2b672f94c5316c823 ts: http://timestamp.digicert.com
%~dp0\signtool sign /sha1 9dbc9dfbbe4588de1caf3ba2b672f94c5316c823 /t http://timestamp.digicert.com %1
goto exit
:noSign
echo !!!!!!! Skipped signing: No "%~dp0\real.sign" file found - expected if not an official build.
:exit
