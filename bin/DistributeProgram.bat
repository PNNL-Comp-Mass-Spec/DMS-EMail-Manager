@echo off
echo Copying to Gigasax and CaptureTaskManagerDistribution
echo.

xcopy DMS_Email_Manager.exe \\gigasax\dms_programs\DMSEMailManager /d /y /f
xcopy DMS_Email_Manager.pdb \\gigasax\dms_programs\DMSEMailManager /d /y /f
xcopy *.dll                 \\gigasax\dms_programs\DMSEMailManager /d /y /f

xcopy DMS_Email_Manager.exe \\cbdms\dms_programs\DMSEMailManager /d /y /f
xcopy DMS_Email_Manager.pdb \\cbdms\dms_programs\DMSEMailManager /d /y /f
xcopy *.dll                 \\cbdms\dms_programs\DMSEMailManager /d /y /f

xcopy DMS_Email_Manager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\DMSEMailManager /d /y /f
xcopy DMS_Email_Manager.pdb \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\DMSEMailManager /d /y /f
xcopy *.dll                 \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\DMSEMailManager /d /y /f

pause