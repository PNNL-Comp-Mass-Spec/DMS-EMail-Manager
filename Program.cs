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
    /// Originally concept by Dave Clark and Nate Trimble
    /// Ported to VB.NET in 2010 by Matthew Monroe
    /// Ported to C# in 2018 by Matthew Monroe, including expanding functionality to support running tasks at varying frequencies
    /// </summary>
    class Program
    {
        private static DMSEmailManagerOptions mOptions;

        static int Main(string[] args)
        {
            mOptions = new DMSEmailManagerOptions();

            try
            {
                var asmName = typeof(Program).GetTypeInfo().Assembly.GetName();
                var exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                var version = DMSEmailManagerOptions.GetAppVersion();

                var parser = new CommandLineParser<DMSEmailManagerOptions>(asmName.Name, version)
                {
                    ProgramInfo = "This program retrieves data from a SQL Server database or from WMI at regular intervals, " +
                                  "or at a certain time of day, and e-mails that data to a given set of recipients.",

                    ContactInfo = "Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2018" +
                                  Environment.NewLine + Environment.NewLine +
                                  "E-mail: proteomics@pnnl.gov" + Environment.NewLine +
                                  "Website: https://panomics.pnnl.gov/ or https://omics.pnl.gov or https://github.com/PNNL-Comp-Mass-Spec",

                    UsageExamples = {
                        exeName + " ReportSpecs.xml",
                        exeName + " ReportSpecs.xml /MaxRuntime:24",
                        exeName + " ReportSpecs.xml /EmailServer:emailgw.pnl.gov",
                        exeName + " ReportSpecs.xml /EmailServer:emailgw.pnl.gov /EmailFrom:proteomics@pnnl.gov"
                    }
                };

                var parseResults = parser.ParseArgs(args);
                var options = parseResults.ParsedResults;

                if (!parseResults.Success)
                {
                    Thread.Sleep(1500);
                    return -1;
                }

                if (!options.ValidateArgs())
                {
                    parser.PrintHelp();
                    Thread.Sleep(1500);
                    return -1;
                }

                var converter = new DMSEmailManager(mOptions);

                converter.ErrorEvent += DMSEmailManager_ErrorEvent;
                converter.StatusEvent += DMSEmailManager_StatusEvent;
                converter.WarningEvent += DMSEmailManager_WarningEvent;

                var success = converter.Start();

                if (!success)
                {
                    ShowErrorMessage("DMSEmailManager.Start returned false");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in Program->Main", ex);
                return -1;
            }

            return 0;

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