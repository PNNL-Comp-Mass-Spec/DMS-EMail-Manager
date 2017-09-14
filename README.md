# DMS EMail Manager

The DMS Email Manager obtains data from a SQL Server database using queries
defined in a settings file, e-mailing the results of the queries to one or more people.
Reports can be e-mailed daily or only on certain days. It also supports calling
a stored procedure or querying WMI.

## Example Settings File

```xml
<?xml version="1.0" encoding="UTF-8"?>
<reports>
    <report name="Processor Status Warnings">
        <data source="gigasax" catalog="DMS_Pipeline" type="query">
          SELECT * FROM V_Processor_Status_Warnings ORDER BY Processor_name
        </data>
        <mail server="emailgw.pnl.gov" from="dms@pnl.gov"
        to="proteomics@pnnl.gov"
        subject="Processor Status Warnings"
        title="Processor Status Warnings" />
        <styles>
            <style type="text/css" media="all">
            body { font: 12px Verdana, Arial, Helvetica, sans-serif; margin: 20px; }
            h3 { font: 20px Verdana, Arial, Helvetica, sans-serif; }
            table { margin: 4px; border-style: ridge; border-width: 2px; }
            .table-header { color: white; background-color: #8080FF; }
            .table-row { background-color: #D8D8FF; vertical-align:top;}
            .table-alternate-row { background-color: #C0C0FF; vertical-align:top;}
            </style>
        </styles>
        <frequency daily="false" dayofweeklist="Monday,Wednesday,Friday" />
    </report>

    <report name="MTS Overdue Database Backups">
        <data source="pogo" catalog="MTS_Master" type="StoredProcedure">GetOverdueDatabaseBackups</data>
        <mail server="emailgw.pnl.gov" from="dms@pnl.gov"
        to="proteomics@pnnl.gov" subject="MTS Overdue Database Backups"
        title="Report generated automatically on Pogo:" />
        <styles>
            <style type="text/css" media="all">body { font: 12px Verdana, Arial,
            Helvetica, sans-serif; margin: 20px; } h3 { font: 20px Verdana, Arial,
            Helvetica, sans-serif; } table { margin: 4px; border-style: ridge;
            border-width: 2px; } .table-header { color: white; background-color:
            #8080FF; } .table-row { background-color: #D8D8FF; } .table-alternate-row
            { background-color: #C0C0FF; }
            </style>
        </styles>
        <frequency daily="false" dayofweeklist="Tuesday,Saturday" />
    </report>

    <report name="Gigasax Disk Space Report">
        <data source="gigasax" type="WMI">
          <![CDATA[SELECT Name, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3]]></data>
        <mail server="emailgw.pnl.gov" from="dms@pnl.gov"
         to="proteomics@pnnl.gov; matthew.monroe@pnnl.gov" subject="Gigasax Disk Space"
         title="Free space on Gigasax (GiB):" />
        <styles>
            <style type="text/css" media="all">
            body { font: 12px Verdana, Arial, Helvetica, sans-serif; margin: 20px; }
            h3 { font: 20px Verdana, Arial, Helvetica, sans-serif; }
            table { margin: 4px; border-style: ridge; border-width: 2px; }
            .table-header { color: white; background-color: #8080FF; }
            .table-row { background-color: #D8D8FF; }
            .table-alternate-row { background-color: #C0C0FF; }
            </style>
        </styles>
        <frequency daily="false" dayofweeklist="Wednesday" />
        <valuedivisor value="1073741824" round="2" units="GiB" />
    </report>
</reports>
```

## Console Switches

The DMS Email Manager is a command line application.  Syntax:

```
DMS_EMail_Manager.exe
  SettingsFile.xml [/X] [/Preview]
```

The Settings file contains the report specs

Use /X to see an extended set of example report specs

Use /Preview to preview the data that would be e-mailed

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com\
Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov or http://www.sysbio.org/resources/staff/

## License

The DMS Email Manager is licensed under the Apache License, Version 2.0; you may not use
this file except in compliance with the License.  You may obtain a copy of the License at
https://opensource.org/licenses/Apache-2.0
