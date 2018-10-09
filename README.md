# DMS EMail Manager

The DMS Email Manager obtains data from a SQL Server database using queries
defined in an XML file, then e-mails the results of those queries to
one or more addresses.  Reports can be run (and e-mailed) daily, only
on certain days, or on a set interval, e.g. every 6 hours. The program 
also supports obtaining data via a stored procedure or via WMI.

## Example Settings File

```xml
<?xml version="1.0" encoding="UTF-8"?>
<reports>
    <EmailOptions>
        <Server>emailgw.pnl.gov</Server>
        <From>proteomics@pnnl.gov</From>
        <FontSizeHeader>20</FontSizeHeader>
        <FontSizeBody>12</FontSizeBody>
    </EmailOptions>

    <report name="Processor Status Warnings">
        <data source="gigasax" catalog="DMS_Pipeline" type="query">
          SELECT TOP 500 * FROM V_Processor_Status_Warnings ORDER BY Processor_name
        </data>
        <mail to="proteomics@pnnl.gov"
              subject="DMS: Processor Status Warnings"
              title="Processor Status Warnings (DMS)" />
        <frequency dayofweeklist="Monday,Wednesday,Friday"
                   type="TimeOfDay"
                   timeOfDay="3:00 pm" />
    </report>

    <report name="Email Alerts">
         <data server="gigasax" database="DMS5" type="query">
             SELECT TOP 500 * FROM V_Email_Alerts Where alert_state = 1
         </data>
         <mail to="proteomics@pnnl.gov; EMSL-Prism.Users.DMS_Monitoring_Admins@pnnl.gov"
               subject="DMS: Alerts"
               title="DMS Alerts" />
         <frequency type="Interval"
                    interval="6"
                    units="hours" />
         <postMailIdListHook server="gigasax" database="DMS5" procedure="AckEmailAlerts"
                             parameter="alertIDs" varcharlength="4000" />
     </report>

    <report name="MTS Overdue Database Backups">
        <data source="pogo" catalog="MTS_Master" type="StoredProcedure">GetOverdueDatabaseBackups</data>
        <mail to="proteomics@pnnl.gov"
              subject="MTS Overdue Database Backups"
              title="Report generated automatically on Pogo:" />
        <frequency dayofweeklist="Tuesday,Saturday"
                   type="TimeOfDay"
                   timeOfDay="9:00 am" />
    </report>

    <report name="Gigasax Disk Space Report">
        <data source="gigasax" type="WMI">
          <![CDATA[SELECT Name, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3]]></data>
        <mail to="proteomics@pnnl.gov"
              subject="Gigasax Disk Space"
              title="Free space on Gigasax (GB);" />
        <frequency dayofweeklist="Wednesday"
                   type="TimeOfDay"
                   timeOfDay="9:15 am" />
        <valuedivisor value="1073741824" round="2" units="GB" />
    </report>
</reports>
```

## Console Switches

The DMS Email Manager is a command line application.  Syntax:

```
DMS_EMail_Manager.exe
  ReportDefsFile.xml [/Server:EmailServer] [/From:Sender]
  [/Log] [/LogDir:LogDirectoryPath]
  [/MaxRuntime:Hours] [/Preview] [/RunOnce] [/Simulate]
  [/E] [/X]
```

The first parameter is the XML file with report definitions.
Use /E to see an example XML file.
Use /X to see an extended example file.

You will normally define the e-mail server and e-mail sender in the Report Definitions file.
Alternatively, define them using /Server and /From at the command line.
However, any settings in the XML file take precedence.

Use /Log to enable logging.  The log file name will includes today's date, 
for example DMS_Email_Manager_log_2018-07-06.txt

Use /MaxRunTime to define the maximum length of time, in hours, that the program should run.
If not specified, the program will run indefinitely (stop with Ctrl+C or via task manager).

Use /Preview to preview the e-mail messages that would be sent

Use /RunOnce to load the report definitions, run each of them once, then exit.
Reports with a timeOfDay setting will be run immediately, regardless of the current time.

Use /Simulate to simulate running a SQL or WMI query (useful when offline)

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov\
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/ or https://github.com/PNNL-Comp-Mass-Spec/

## License

The DMS Email Manager is licensed under the 2-Clause BSD License; 
you may not use this file except in compliance with the License.
You may obtain a copy of the License at https://opensource.org/licenses/BSD-2-Clause

Copyright 2018 Battelle Memorial Institute
