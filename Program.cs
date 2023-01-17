using System;
using System.IO;
using System.Reflection;
using System.Threading;
using PRISM;

namespace DMS_Email_Manager
{
    /// <summary>
    /// This program retrieves data from a database at regular intervals
    /// and e-mails that information to the appropriate people
    /// Original concept by Dave Clark and Nate Trimble
    /// Ported to VB.NET in 2010 by Matthew Monroe
    /// Ported to C# in 2018 by Matthew Monroe, including expanding functionality to support running tasks at varying frequencies
    /// </summary>
    internal class Program
    {
        // Ignore Spelling: dayofweeklist, varcharlength, AckEmailAlerts

        private static int Main(string[] args)
        {
            try
            {
                var asmName = typeof(Program).GetTypeInfo().Assembly.GetName();
                var exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                var version = DMSEmailManagerOptions.GetAppVersion();

                var parser = new CommandLineParser<DMSEmailManagerOptions>(asmName.Name, version)
                {
                    ProgramInfo = "This program obtains data from a SQL Server database using queries " +
                                  "defined in an XML file, then e-mails the results of those queries to " +
                                  "one or more addresses.  Reports can be run (and e-mailed) daily, only on " +
                                  "certain days, or on a set interval, e.g. every 6 hours. The program " +
                                  "also supports obtaining data via a stored procedure or via WMI. " +
                                  "The first command line argument must be the path to an XML file with the Email options " +
                                  "and the report definitions. To see an example Report Definitions file, use /E; " +
                                  "to see an extended example Report Definitions file, use /X.",

                    ContactInfo = "Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)" +
                                  Environment.NewLine + Environment.NewLine +
                                  "E-mail: proteomics@pnnl.gov" + Environment.NewLine +
                                  "Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics",

                    UsageExamples =
                    {
                        exeName + " ReportSpecs.xml",
                        exeName + " ReportSpecs.xml /MaxRuntime:24",
                        exeName + " ReportSpecs.xml /RunOnce",
                        exeName + " ReportSpecs.xml /EmailServer:emailgw.pnl.gov",
                        exeName + " ReportSpecs.xml /EmailServer:emailgw.pnl.gov /EmailFrom:proteomics@pnnl.gov"
                    }
                };

                var result = parser.ParseArgs(args);
                var options = result.ParsedResults;

                if (!result.Success)
                {
                    if (parser.CreateParamFileProvided)
                    {
                        return 0;
                    }

                    // Delay for 1500 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                    Thread.Sleep(1500);
                    return -1;
                }

                if (options.ShowExample)
                {
                    ShowExampleReportDefinitionsFile();
                    return 0;
                }

                if (options.ShowExtendedExample)
                {
                    ShowExampleReportDefinitionsFile(true);
                    return 0;
                }

                if (!options.ValidateArgs(out var errorMessage))
                {
                    parser.PrintHelp();

                    Console.WriteLine();
                    ConsoleMsgUtils.ShowWarning("Validation error:");
                    ConsoleMsgUtils.ShowWarning(errorMessage);

                    Thread.Sleep(1500);
                    return -1;
                }

                var converter = new DMSEmailManager(options);

                converter.DebugEvent += DMSEmailManager_DebugEvent;
                converter.ErrorEvent += DMSEmailManager_ErrorEvent;
                converter.StatusEvent += DMSEmailManager_StatusEvent;
                converter.WarningEvent += DMSEmailManager_WarningEvent;

                var success = converter.Start();

                if (!success)
                {
                    ShowErrorMessage("DMSEmailManager.Start returned false");
                    ConsoleMsgUtils.PauseAtConsole(2000, 500);
                }

                ConsoleMsgUtils.PauseAtConsole(750);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in Program->Main", ex);
                ConsoleMsgUtils.PauseAtConsole(2000, 500);
                return -1;
            }

            return 0;
        }

        private static void ShowExampleReportDefinitionsFile(bool showExtendedExample = false)
        {
            Console.WriteLine();
            Console.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            Console.WriteLine("<reports>");
            Console.WriteLine("    <EmailOptions>");
            Console.WriteLine("        <Server>emailgw.pnl.gov</Server>");
            Console.WriteLine("        <From>proteomics@pnnl.gov</From>");
            Console.WriteLine("        <FontSizeHeader>20</FontSizeHeader>");
            Console.WriteLine("        <FontSizeBody>12</FontSizeBody>");
            Console.WriteLine("    </EmailOptions>");
            Console.WriteLine();
            Console.WriteLine("    <report name=\"Processor Status Warnings\">");
            Console.WriteLine("        <data source=\"gigasax\" catalog=\"DMS_Pipeline\" type=\"query\">");
            Console.WriteLine("            SELECT *");
            Console.WriteLine("            FROM V_Processor_Status_Warnings ORDER BY Processor_name");
            Console.WriteLine("            OFFSET 0 ROWS");
            Console.WriteLine("            FETCH FIRST 500 ROWS ONLY");
            Console.WriteLine("        </data>");
            Console.WriteLine("        <mail to=\"proteomics@pnnl.gov\" ");
            Console.WriteLine("              subject=\"DMS: Processor Status Warnings\" ");
            Console.WriteLine("              title=\"Processor Status Warnings (DMS)\" />");
            Console.WriteLine("        <frequency dayofweeklist=\"Monday,Wednesday,Friday\" ");
            Console.WriteLine("                   type=\"TimeOfDay\" ");
            Console.WriteLine("                   timeOfDay=\"3:00 pm\" />");
            Console.WriteLine("    </report>");
            Console.WriteLine();
            Console.WriteLine("    <report name=\"Email Alerts\"> ");
            Console.WriteLine("         <data server=\"gigasax\" database=\"DMS5\" type=\"query\"> ");
            Console.WriteLine("             SELECT ID, Posted_by, Posting_Time, Alert_Type, Message, Recipients, Alert_State, Alert_State_Name, Last_Affected");
            Console.WriteLine("             FROM ( SELECT ID, Posted_by, Posting_Time, Alert_Type, Message, Recipients, Alert_State, Alert_State_Name, Last_Affected,");
            Console.WriteLine("                           row_number() OVER ( ORDER BY id ) AS RowNum");
            Console.WriteLine("                    FROM V_Email_Alerts");
            Console.WriteLine("                    WHERE alert_state = 1 ");
            Console.WriteLine("                  ) LookupQ");
            Console.WriteLine("             WHERE RowNum <= 500");
            Console.WriteLine("             ORDER BY RowNum");
            Console.WriteLine("         </data> ");
            Console.WriteLine("         <mail to=\"proteomics@pnnl.gov; EMSL-Prism.Users.DMS_Monitoring_Admins@pnnl.gov\"  ");
            Console.WriteLine("               subject=\"DMS: Alerts\"  ");
            Console.WriteLine("               title=\"DMS Alerts\" /> ");
            Console.WriteLine("               mailIfEmpty=\"false\" /> ");
            Console.WriteLine("         <frequency type=\"Interval\"  ");
            Console.WriteLine("                    interval=\"12\" ");
            Console.WriteLine("                    units=\"hours\" /> ");
            Console.WriteLine("         <postMailIdListHook server=\"gigasax\" database=\"DMS5\" procedure=\"AckEmailAlerts\"  ");
            Console.WriteLine("                             parameter=\"alertIDs\" varcharlength=\"4000\" /> ");
            Console.WriteLine("     </report> ");

            if (showExtendedExample)
            {
                Console.WriteLine();
                Console.WriteLine("    <report name=\"MTS Overdue Database Backups\">");
                Console.WriteLine("        <data source=\"pogo\" catalog=\"MTS_Master\" type=\"StoredProcedure\">GetOverdueDatabaseBackups</data>");
                Console.WriteLine("        <mail to=\"proteomics@pnnl.gov\" ");
                Console.WriteLine("              subject=\"MTS Overdue Database Backups\" ");
                Console.WriteLine("              title=\"Report generated automatically on Pogo:\" />");
                Console.WriteLine("        <frequency dayofweeklist=\"Tuesday,Saturday\" ");
                Console.WriteLine("                   type=\"TimeOfDay\" ");
                Console.WriteLine("                   timeOfDay=\"9:00 am\" />");
                Console.WriteLine("    </report>");
                Console.WriteLine();
                Console.WriteLine("    <report name=\"Gigasax Disk Space Report\">");
                Console.WriteLine("        <data source=\"gigasax\" type=\"WMI\">");
                Console.WriteLine("          <![CDATA[SELECT Name, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3]]></data>");
                Console.WriteLine("        <mail to=\"proteomics@pnnl.gov\" ");
                Console.WriteLine("              subject=\"Gigasax Disk Space\" ");
                Console.WriteLine("              title=\"Free space on Gigasax (GB);\" />");
                Console.WriteLine("        <frequency dayofweeklist=\"Wednesday\" ");
                Console.WriteLine("                   type=\"TimeOfDay\" ");
                Console.WriteLine("                   timeOfDay=\"9:15 am\" />");
                Console.WriteLine("        <valuedivisor value=\"1073741824\" round=\"2\" units=\"GB\" />");
                Console.WriteLine("    </report>");
            }

            Console.WriteLine("</reports>");
            Console.WriteLine();
        }

        private static void DMSEmailManager_DebugEvent(string message)
        {
            Console.ForegroundColor = ConsoleMsgUtils.DebugFontColor;
            Console.WriteLine("  " + message);
            Console.ResetColor();
        }

        private static void DMSEmailManager_ErrorEvent(string message, Exception ex)
        {
            ShowErrorMessage(message, ex);
        }

        private static void DMSEmailManager_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void DMSEmailManager_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private static void ShowErrorMessage(string message, Exception ex = null)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }
    }
}