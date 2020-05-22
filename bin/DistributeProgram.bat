@echo off
echo Copying to Gigasax and CaptureTaskManagerDistribution
echo.

xcopy DMS_Email_Manager.exe \\gigasax\dms_programs\DMSEMailManager /d /y
xcopy *.dll                 \\gigasax\dms_programs\DMSEMailManager /d /y

xcopy DMS_Email_Manager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\DMSEMailManager /d /y
xcopy *.dll                 \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\DMSEMailManager/d /y

pause