using System;
using System.Reflection;
using PRISM;

namespace DMS_Email_Manager
{
    public class DMSEmailManagerOptions
    {
        private const string PROGRAM_DATE = "June 15, 2018";

        public const string DEFAULT_EMAIL_SERVER = "emailgw.pnl.gov";
        public const string DEFAULT_EMAIL_FROM = "proteomics@pnnl.gov";

        /// <summary>
        /// Constructor
        /// </summary>
        public DMSEmailManagerOptions()
        {
            TaskDefinitionsFilePath = string.Empty;
            EmailServer = DEFAULT_EMAIL_SERVER;
            EmailFrom = DEFAULT_EMAIL_FROM;
            FontSizeBody = 12;
            FontSizeHeader = 20;
            MaxRuntimeHours = 0;
        }


        [Option("I", ArgPosition = 1, HelpText = "Xml file with report definitions")]
        public string TaskDefinitionsFilePath { get; set; }

        [Option("EmailServer", "Server", HelpText = "EMail Server")]
        public string EmailServer { get; set; }

        [Option("EmailFrom", "From", HelpText = "Sender e-mail address", HelpShowsDefault = true)]
        public string EmailFrom { get; set; }

        [Option("FontSizeHeader", "HeaderSize", HelpText = "Header text font size", HelpShowsDefault = true, Min = 6, Max = 48)]
        public int FontSizeHeader { get; set; }

        [Option("FontSizeBody", "BodySize", HelpText = "Body text font size", HelpShowsDefault = true, Min = 6, Max = 24)]
        public int FontSizeBody { get; set; }

        [Option("MaxRunTime", "Runtime", "RuntimeHours", "Hours", HelpText = "Run for this many hours, then exit (0 means run indefinitely)", HelpShowsDefault = true, Min = 0, Max = 10000)]
        public int MaxRuntimeHours { get; set; }

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

            Console.WriteLine(" Task Definitions File: {0}", TaskDefinitionsFilePath);
            Console.WriteLine(" Email server: {0}", EmailServer);
            Console.WriteLine(" Sender from:  {0}", EmailFrom);
            Console.WriteLine(" Header text size: {0} pt", FontSizeHeader);
            Console.WriteLine(" Body text size:   {0} pt", FontSizeBody);

            if (MaxRuntimeHours > 0)
            {
                Console.WriteLine();
                Console.WriteLine(" Max runtime: {0} hours", MaxRuntimeHours);
            }

        }

        public bool ValidateArgs()
        {
            if (string.IsNullOrWhiteSpace(TaskDefinitionsFilePath))
            {
                Console.WriteLine("Task definitions XML file must be defined");
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
