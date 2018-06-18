﻿using System;
using System.Reflection;
using PRISM;

namespace DMS_Email_Manager
{
    public class DMSEmailManagerOptions
    {
        private const string PROGRAM_DATE = "June 18, 2018";

        public const string DEFAULT_EMAIL_SERVER = "emailgw.pnl.gov";
        public const string DEFAULT_EMAIL_FROM = "proteomics@pnnl.gov";

        public const int DEFAULT_FONT_SIZE_HEADER = 20;
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


        [Option("I", ArgPosition = 1, HelpText = "Xml file with report definitions")]
        public string ReportDefinitionsFilePath { get; set; }

        [Option("EmailServer", "Server", HelpText = "Email Server")]
        public string EmailServer { get; set; }

        [Option("EmailFrom", "From", HelpText = "Sender e-mail address", HelpShowsDefault = true)]
        public string EmailFrom { get; set; }

        [Option("FontSizeHeader", "HeaderSize", HelpText = "Header text font size", HelpShowsDefault = true, Min = 6, Max = 48)]
        public int FontSizeHeader { get; set; }

        [Option("FontSizeBody", "BodySize", HelpText = "Body text font size", HelpShowsDefault = true, Min = 6, Max = 24)]
        public int FontSizeBody { get; set; }

        [Option("Log", HelpText = "Logging enabled")]
        public bool LogMessages { get; set; }

        [Option("LogDir", HelpText = "Directory to save log files")]
        public string LogDirPath { get; set; }

        [Option("MaxRunTime", "Runtime", "RuntimeHours", "Hours", HelpText = "Run for this many hours, then exit (0 means run indefinitely)", HelpShowsDefault = true, Min = 0, Max = 10000)]
        public int MaxRuntimeHours { get; set; }

        [Option("Preview", HelpText = "Preview the e-mail messages instead of actually sending them")]
        public bool PreviewMode { get; set; }

        public static string GetAppVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";

            return version;
        }

        public void OutputSetOptions()
        {
            Console.WriteLine("DMSEmailManager, version " + GetAppVersion());
            Console.WriteLine();
            Console.WriteLine("Using options:");

            Console.WriteLine(" Report Definitions File: {0}", ReportDefinitionsFilePath);

            Console.WriteLine();
            Console.WriteLine(" Email server: {0}", EmailServer);
            Console.WriteLine(" Sender from:  {0}", EmailFrom);
            Console.WriteLine(" Header text size: {0} pt", FontSizeHeader);
            Console.WriteLine(" Body text size:   {0} pt", FontSizeBody);

            if (MaxRuntimeHours > 0)
            {
                Console.WriteLine();
                Console.WriteLine(" Max runtime: {0} hours", MaxRuntimeHours);
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
                Console.WriteLine(" Previewing e-mail messages without actually sending them");
            }

        }

        public bool ValidateArgs()
        {
            if (string.IsNullOrWhiteSpace(ReportDefinitionsFilePath))
            {
                Console.WriteLine("Report definitions XML file must be defined");
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmailServer))
            {
                Console.WriteLine("Email server must be defined");
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmailFrom))
            {
                Console.WriteLine("Email from address must be defined");
                return false;
            }

            return true;
        }

    }
}