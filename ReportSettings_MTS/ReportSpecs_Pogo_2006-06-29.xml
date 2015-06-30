<?xml version="1.0" encoding="UTF-8"?>
<reports>
    <report name="MTS Current Activity Report">
        <data source="pogo" catalog="MTS_Master" type="StoredProcedure">GetCurrentActivitySummary</data>
        <mail server="pnl.gov" from="dms@pnl.gov" to="grkiebel@pnl.gov;matthew.monroe@pnl.gov;samuel.purvine@pnl.gov" subject="MTS Current Activity Report" title="Report generated automatically:" />
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
        <frequency daily="true" dayofweeklist="" />
    </report>
    <report name="MTS Error Report">
        <data source="pogo" catalog="MTS_Master" type="StoredProcedure">GetErrorsFromActiveDBLogs</data>
        <mail server="pnl.gov" from="dms@pnl.gov" to="grkiebel@pnl.gov;matthew.monroe@pnl.gov;samuel.purvine@pnl.gov" subject="MTS Error Report - Previous 24 hour summary" title="Report generated automatically:" />
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
        <frequency daily="true" dayofweeklist="" />
    </report>
    <report name="MTS Overdue Database Backups">
        <data source="pogo" catalog="MTS_Master" type="StoredProcedure">GetOverdueDatabaseBackups</data>
        <mail server="pnl.gov" from="dms@pnl.gov" to="grkiebel@pnl.gov;matthew.monroe@pnl.gov" subject="MTS Overdue Database Backups" title="Report generated automatically:" />
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
        <frequency daily="false" dayofweeklist="Tuesday,Saturday" />
    </report>
	<report name="Pogo Disk Space Report">
        <data source="pogo" type="WMI"><![CDATA[SELECT Name, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3]]></data>
        <mail server="pnl.gov" from="dms@pnl.gov" to="grkiebel@pnl.gov;dave.clark@pnl.gov;matthew.monroe@pnl.gov" subject="Pogo Disk Space" title="Free space on Pogo:" />
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
        <valuedivisor value="1000000000" round="2" units="GB" />
    </report>
	<report name="PrismDev Disk Space Report">
        <data source="PrismDev" type="WMI"><![CDATA[SELECT Name, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3]]></data>
        <mail server="pnl.gov" from="dms@pnl.gov" to="grkiebel@pnl.gov;dave.clark@pnl.gov;matthew.monroe@pnl.gov" subject="PrismDev Disk Space" title="Free space on PrismDev:" />
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
        <valuedivisor value="1000000000" round="2" units="GB" />
    </report>
	<report name="Albert Disk Space Report">
        <data source="Albert" type="WMI"><![CDATA[SELECT Name, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3]]></data>
        <mail server="pnl.gov" from="dms@pnl.gov" to="grkiebel@pnl.gov;dave.clark@pnl.gov;matthew.monroe@pnl.gov" subject="Albert Disk Space" title="Free space on Albert:" />
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
        <valuedivisor value="1000000000" round="2" units="GB" />
    </report>
</reports>