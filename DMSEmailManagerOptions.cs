using System;
using System.Reflection;
using PRISM;

namespace DMS_Email_Manager
{
    /// <summary>
    /// E-mail manager options
    /// </summary>
    public class DMSEmailManagerOptions
    {
        // Ignore Spelling: Ctrl

        private const string PROGRAM_DATE = "April 29, 2022";

        /// <summary>
        /// Default e-mail server
        /// </summary>
        public const string DEFAULT_EMAIL_SERVER = "emailgw.pnl.gov";

        /// <summary>
        /// Default sender e-mail address
        /// </summary>
        public const string DEFAULT_EMAIL_FROM = "proteomics@pnnl.gov";

        /// <summary>
        /// Font size for the header
        /// </summary>
        public const int DEFAULT_FONT_SIZE_HEADER = 20;

        /// <summary>
        /// font size for the body
        /// </summary>
        public const int DEFAULT_FONT_SIZE_BODY = 12;

        /// <summary>
        /// Constructor
        /// </summary>
        public DMSEmailManagerOptions()
        {
            ReportDefinitionsFilePath = string.Empty;

            // Set these to empty strings or 0 for now
            // They will be updated when the report definitions file is read
            // Defaults will be used if not defined in the report definitions file
            EmailServer = string.Empty;
            EmailFrom = string.Empty;
            FontSizeHeader = 0;
            FontSizeBody = 0;

            LogMessages = false;
            LogDirPath = string.Empty;
            MaxRuntimeHours = 0;
        }

        /// <summary>
        /// Report definitions file
        /// </summary>
        [Option("I", ArgPosition = 1, HelpText = "XML file with report definitions", HelpShowsDefault = false)]
        public string ReportDefinitionsFilePath { get; set; }

        /// <summary>
        /// E-mail server name
        /// </summary>
        [Option("EmailServer", "Server", HelpText = "Email Server", HelpShowsDefault = false)]
        public string EmailServer { get; set; }

        /// <summary>
        /// Sender e-mail address
        /// </summary>
        [Option("EmailFrom", "From", HelpText = "Sender e-mail address", HelpShowsDefault = false)]
        public string EmailFrom { get; set; }

        /// <summary>
        /// Font size for the header
        /// </summary>
        [Option("FontSizeHeader", "HeaderSize", HelpText = "Header text font size",
            HelpShowsDefault = true, Min = 6, Max = 48)]
        public int FontSizeHeader { get; set; }

        /// <summary>
        /// Font size for the body
        /// </summary>
        [Option("FontSizeBody", "BodySize", HelpText = "Body text font size",
            HelpShowsDefault = true, Min = 6, Max = 24)]
        public int FontSizeBody { get; set; }

        /// <summary>
        /// When true, log messages to a file
        /// </summary>
        [Option("Log", HelpText = "Logging enabled")]
        public bool LogMessages { get; set; }

        /// <summary>
        /// Log file directory
        /// </summary>
        [Option("LogDir", HelpText = "Directory to save log files")]
        public string LogDirPath { get; set; }

        /// <summary>
        /// Maximum runtime, in hours
        /// </summary>
        [Option("MaxRuntimeHours", "MaxRunTime", "Runtime", "RuntimeHours", "Hours",
            HelpText = "Run for this many hours, then exit (0 means run indefinitely)",
            HelpShowsDefault = true, Min = 0, Max = 10000)]
        public int MaxRuntimeHours { get; set; }

        /// <summary>
        /// When true, preview messages that would be sent
        /// </summary>
        [Option("Preview", "P", HelpText = "Preview the e-mail messages instead of actually sending them")]
        public bool PreviewMode { get; set; }

        /// <summary>
        /// When true, run each report once, then exit
        /// </summary>
        [Option("RunOnce", "Once", HelpText = "Load the report definitions, run each of them once, then exit the program; ignores timeOfDay")]
        public bool RunOnce { get; set; }

        /// <summary>
        /// When true, simulate contacting the database
        /// </summary>
        [Option("Simulate", "Sim", HelpText = "Simulate contacting the database or running queries")]
        public bool Simulate { get; set; }

        /// <summary>
        /// Show an example XML report definitions file at the console
        /// </summary>
        [Option("E", HelpText = "View an example XML report definitions file", HelpShowsDefault = false)]
        public bool ShowExample { get; set; }

        /// <summary>
        /// Show an extended example XML report definitions file at the console
        /// </summary>
        [Option("X", HelpText = "View an extended example XML report definitions file", HelpShowsDefault = false)]
        public bool ShowExtendedExample { get; set; }

        /// <summary>
        /// Obtain a string with the program version and date
        /// </summary>
        public static string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        /// <summary>
        /// Show the current options at the console
        /// </summary>
        /// <param name="showVersion"></param>
        public void OutputSetOptions(bool showVersion = true)
        {
            if (showVersion)
            {
                Console.WriteLine("DMSEmailManager, version " + GetAppVersion());
                Console.WriteLine();
            }

            Console.WriteLine("Using options:");

            Console.WriteLine(" Report Definitions File: {0}", ReportDefinitionsFilePath);

            Console.WriteLine();
            Console.WriteLine(" Email server: {0}", EmailServer);
            Console.WriteLine(" Sender from:  {0}", EmailFrom);
            Console.WriteLine(" Header text size: {0} pt", FontSizeHeader);
            Console.WriteLine(" Body text size:   {0} pt", FontSizeBody);

            if (RunOnce)
            {
                Console.WriteLine();
                Console.WriteLine(" Running each report once, then exiting");
            }
            else if (MaxRuntimeHours == 0)
            {
                Console.WriteLine();
                Console.WriteLine(" Running indefinitely (stop with Ctrl+C or using the task manager)");
            }
            else if (MaxRuntimeHours > 0)
            {
                Console.WriteLine();
                var runtimeUnits = MaxRuntimeHours == 1 ? "hour" : "hours";

                Console.WriteLine(" Max runtime: {0} {1}", MaxRuntimeHours, runtimeUnits);
            }

            if (LogMessages)
            {
                if (string.IsNullOrWhiteSpace(LogDirPath))
                {
                    Console.WriteLine(" Logging messages to the current directory");
                }
                else
                {
                    Console.WriteLine(" Logging messages to directory " + LogDirPath);
                }
            }

            if (PreviewMode)
            {
                Console.WriteLine();
                Console.WriteLine(" Previewing e-mail messages without actually sending them");
            }

            if (Simulate)
            {
                Console.WriteLine();
                Console.WriteLine(" Simulating database / WMI calls");
            }
        }

        /// <summary>
        /// Validate arguments
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public bool ValidateArgs(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(ReportDefinitionsFilePath))
            {
                errorMessage = "Report definitions XML file must be defined";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
