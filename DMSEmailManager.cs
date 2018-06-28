using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NodaTime;
using PRISM;
using PRISM.FileProcessor;

namespace DMS_Email_Manager
{
    public class DMSEmailManager : ProcessFilesBase
    {
        #region "Constants"

        private const string DATE_TIME_FORMAT = "yyyy-MM-dd hh:mm:ss tt";

        private const string NO_DATA = "No Data Returned";

        private const int TASK_CHECK_INTERVAL_SECONDS = 15;

        private const string REPORT_STATUS_FILE_NAME = "ReportStatusFile.xml";

        private const int REPORT_STATUS_FILE_UPDATE_INTERVAL_MINUTES = 15;

        #endregion

        #region "Fields"

        /// <summary>
        /// mReportDefsWatcher_Changed sets this to true if the ReportDefinitions file is updated
        /// </summary>
        private bool mReportDefsFileChanged;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly FileSystemWatcher mReportDefsWatcher;

        private bool mRuntimeInfoSaveRequired;

        /// <summary>
        /// Keys are task names, values are Notification Tasks
        /// </summary>
        private readonly Dictionary<string, NotificationTask> mTasks = new Dictionary<string, NotificationTask>();

        /// <summary>
        /// Tracks the last runtime of each notification task
        /// </summary>
        /// <remarks>
        /// This dictionary is populated by ReadReportStatusFile prior to calling ReadReportDefsFile
        /// Once the main loop is entered in Start(), mRuntimeInfo is updated,
        /// but SaveReportStatusFile actually uses mTasks when updating the ReportStatus XML file
        /// </remarks>
        private readonly Dictionary<string, TaskRuntimeInfo> mRuntimeInfo = new Dictionary<string, TaskRuntimeInfo>();

        #endregion

        #region "Properties"

        public DMSEmailManagerOptions Options { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Options</param>
        public DMSEmailManager(DMSEmailManagerOptions options)
        {
            Options = options;

            // Configure logging
            LogMessagesToFile = Options.LogMessages;

            if (!string.IsNullOrWhiteSpace(Options.LogDirPath))
            {
                LogFolderPath = Options.LogDirPath;
            }

            mLogFileUsesDateStamp = true;
            LogMessage("Starting the DMS Email Manager");

            if (string.IsNullOrWhiteSpace(Options.ReportDefinitionsFilePath))
            {
                var errMsg = "Report definitions file not defined";
                ShowErrorMessage(errMsg);
                throw new ArgumentException(errMsg, nameof(Options.ReportDefinitionsFilePath));
            }

            var reportDefinitionsFile = new FileInfo(Options.ReportDefinitionsFilePath);
            if (!reportDefinitionsFile.Exists)
            {
                var errMsg = "Report definitions file not found: " + Options.ReportDefinitionsFilePath;
                ShowErrorMessage(errMsg);
                throw new FileNotFoundException(errMsg);
            }

            if (string.IsNullOrWhiteSpace(reportDefinitionsFile.DirectoryName))
            {
                var errMsg = "Unable to determine the parent directory of the report definitions file: " + Options.ReportDefinitionsFilePath;
                ShowErrorMessage(errMsg);
                throw new FileNotFoundException(errMsg);
            }

            mReportDefsFileChanged = false;

            mReportDefsWatcher = new FileSystemWatcher(reportDefinitionsFile.DirectoryName, reportDefinitionsFile.Name);
            mReportDefsWatcher.Changed += ReportDefsWatcher_Changed;
            mReportDefsWatcher.EnableRaisingEvents = true;
        }

        private void AddUpdateRuntimeInfo(KeyValuePair<string, NotificationTask> task)
        {
            if (mRuntimeInfo.TryGetValue(task.Key, out var taskRuntimeInfo))
            {
                taskRuntimeInfo.LastRun = task.Value.LastRun;
                taskRuntimeInfo.NextRun = task.Value.NextRun;
                taskRuntimeInfo.ExecutionCount = task.Value.ExecutionCount;
            }
            else
            {
                var newRuntimeInfo = new TaskRuntimeInfo(task.Value.LastRun, task.Value.ExecutionCount)
                {
                    NextRun = task.Value.NextRun
                };
                mRuntimeInfo.Add(task.Key, newRuntimeInfo);
            }

            mRuntimeInfoSaveRequired = true;

        }

        private void EmailResults(
            TaskResults results,
            EmailMessageSettings mailSettings,
            DataSourceSqlStoredProcedure postMailIdListHook)
        {
            try
            {

                var titleHtml = "<h3>" + mailSettings.ReportTitle + "</h3>";
                var dataHtml = new StringBuilder();

                if (results.ColumnNames.Count == 0 || results.DataRows.Count == 0)
                {
                    dataHtml.AppendLine(NO_DATA);
                }
                else
                {
                    // Construct the header row
                    dataHtml.AppendLine("<table>");
                    dataHtml.Append("<tr class = table-header>");
                    foreach (var headerCol in results.ColumnNames)
                    {
                        dataHtml.Append("<td>");
                        dataHtml.Append(headerCol);
                        dataHtml.Append("</td>");
                    }
                    dataHtml.AppendLine("</tr>");

                    for (var i = 0; i < results.DataRows.Count; i++)
                    {
                        if (i % 2 == 0)
                        {
                            dataHtml.Append("<tr class = table-row>");
                        }
                        else
                        {
                            dataHtml.Append("<tr class = table-alternate-row>");
                        }

                        var currentRow = results.DataRows[i];
                        foreach (var dataVal in currentRow)
                        {
                            dataHtml.Append("<td>");
                            dataHtml.Append(dataVal);
                            dataHtml.Append("</td>");
                        }

                        dataHtml.AppendLine("</tr>");
                    }

                    dataHtml.AppendLine("</table>");
                }

                string reportInfo;
                if (results.DataRows.Count == 0)
                {
                    // Report 'Processor Status Warnings' had no data
                    reportInfo = string.Format("Report '{0}' had no data", results.ReportName);
                }
                else
                {
                    var rowCountUnits = results.DataRows.Count == 1 ? "row" : "rows";

                    // Report 'Processor Status Warnings' had 2 rows of data
                    reportInfo = string.Format("Report '{0}' had {1} {2} of data",
                                               results.ReportName, results.DataRows.Count, rowCountUnits);
                }

                if (mailSettings.Recipients.Count == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(reportInfo);
                    Console.WriteLine(dataHtml.ToString());
                    return;
                }

                var formattedRecipients = mailSettings.GetRecipients(",");

                string reportAndEmailInfo;
                bool sendMail;

                if (results.DataRows.Count == 0 && !mailSettings.MailIfEmpty)
                {
                    // Do not mail this report since it's empty
                    reportAndEmailInfo = string.Format("{0}; e-mail will not be sent ({1:h:mm:ss tt})", reportInfo, DateTime.Now);
                    sendMail = false;
                }
                else
                {
                    var emailAction = Options.PreviewMode ? "would be sent to" : "sent to";

                    // Report 'Processor Status Warnings' had 2 rows of data; e-mail sent to proteomics@pnnl.gov
                    // Report 'Processor Status Warnings' had 2 rows of data; e-mail would be sent to proteomics@pnnl.gov
                    // Report 'Processor Status Warnings' had no data; e-mail sent to proteomics@pnnl.gov
                    // Report 'Processor Status Warnings' had no data; e-mail would be sent to proteomics@pnnl.gov
                    reportAndEmailInfo = string.Format("{0}; e-mail {1} {2}", reportInfo, emailAction, formattedRecipients);
                    sendMail = true;
                }

                if (Options.PreviewMode)
                {
                    Console.WriteLine();
                    Console.WriteLine(reportAndEmailInfo);
                    if (results.DataRows.Count > 0)
                    {
                        Console.WriteLine(titleHtml);
                        Console.WriteLine(dataHtml.ToString());
                    }
                    return;
                }

                if (!sendMail)
                {
                    Console.WriteLine();
                    Console.WriteLine(reportAndEmailInfo);
                    return;
                }

                var emailBody = new StringBuilder();

                emailBody.AppendLine("<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\">");
                emailBody.AppendLine("<html>");
                emailBody.AppendLine("<head>");
                emailBody.Append(GetCssStyle());
                emailBody.AppendLine("</head>");
                emailBody.AppendLine("<body>");

                emailBody.AppendLine(titleHtml);
                emailBody.AppendLine(dataHtml.ToString());
                emailBody.AppendLine("</body>");
                emailBody.AppendLine("</html>");

                var msg = new System.Net.Mail.MailMessage(Options.EmailFrom, formattedRecipients)
                {
                    BodyEncoding = Encoding.UTF8,
                    IsBodyHtml = true,
                    Subject = mailSettings.Subject,
                    Body = emailBody.ToString()
                };

                var mailClient = new System.Net.Mail.SmtpClient(Options.EmailServer);
                mailClient.Send(msg);

                LogMessage(reportInfo);

            }
            catch (Exception ex)
            {
                HandleException(string.Format("Error e-mailing the results to {0} for report '{1}'",
                                              mailSettings.GetRecipients(","), results.ReportName),
                                ex);
                return;
            }

            SendResultIDsToPostMailHook(results, postMailIdListHook);
        }

        private XElement GetChildElement(string reportName, XContainer report, string childNodeName, bool warnIfMissing = true)
        {
            var childNode = report.Elements(childNodeName).FirstOrDefault();
            if (childNode != null)
                return childNode;

            if (warnIfMissing)
                ShowWarning(string.Format("Ignoring report definition '{0}'; missing the {1} element", reportName, childNodeName));

            return null;

        }

        private int GetChildElementValue(XContainer node, string childNodeName, int valueIfMissing)
        {
            var dataValue = GetChildElementValue(node, childNodeName, valueIfMissing.ToString());
            if (string.IsNullOrWhiteSpace(dataValue) || !int.TryParse(dataValue, out var value))
                return valueIfMissing;

            return value;
        }

        private DateTime GetChildElementValue(XContainer node, string childNodeName, DateTime valueIfMissing)
        {
            var dataValue = GetChildElementValue(node, childNodeName, valueIfMissing.ToString(DATE_TIME_FORMAT));

            if (string.IsNullOrWhiteSpace(dataValue) || !DateTime.TryParse(dataValue, out var value))
                return valueIfMissing;

            if (dataValue.EndsWith("Z"))
            {
                // Value is currently local time; change back to UTC

                try
                {

                    var roundTripBased = DateTime.ParseExact(dataValue, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    return roundTripBased.ToUniversalTime();
                }
                catch (Exception)
                {
                    return value.ToUniversalTime();
                }

            }

            return value;
        }

        private string GetChildElementValue(XContainer node, string childNodeName, string valueIfMissing)
        {
            var childNode = node.Elements(childNodeName).FirstOrDefault();
            if (childNode == null)
                return valueIfMissing;

            return childNode.Value;
        }

        private string GetCssStyle()
        {
            var cssStyle = new StringBuilder();

            cssStyle.AppendLine("<style type=\"text/css\" media=\"all\">");
            cssStyle.AppendLine(string.Format("body {{ font: {0}px Verdana, Arial, Helvetica, sans-serif; margin: 20px; }}", Options.FontSizeBody));
            cssStyle.AppendLine(string.Format("h3 {{ font: {0}px Verdana, Arial, Helvetica, sans-serif; }}", Options.FontSizeHeader));
            cssStyle.AppendLine("table { margin: 4px; border-style: ridge; border-width: 2px; }");
            cssStyle.AppendLine(".table-header { color: white; background-color: #8080FF; }");
            cssStyle.AppendLine(".table-row { background-color: #D8D8FF; vertical-align:top;}");
            cssStyle.AppendLine(".table-alternate-row { background-color: #C0C0FF; vertical-align:top;}");
            cssStyle.AppendLine("</style>");

            return cssStyle.ToString();
        }

        private string GetElementAttribValue(XElement node, string attribName, string defaultValue)
        {
            if (!node.HasAttributes)
                return defaultValue;

            var attrib = node.Attributes(attribName).FirstOrDefault();

            if (attrib == null)
                return defaultValue;

            return attrib.Value;
        }

        private int GetElementAttribValue(XElement node, string attribName, int defaultValue)
        {
            var valueText = GetElementAttribValue(node, attribName, defaultValue.ToString());
            if (int.TryParse(valueText, out var value))
                return value;

            return defaultValue;
        }

        private long GetElementAttribValue(XElement node, string attribName, long defaultValue)
        {
            var valueText = GetElementAttribValue(node, attribName, defaultValue.ToString());
            if (int.TryParse(valueText, out var value))
                return value;

            return defaultValue;
        }

        private bool GetElementAttribValue(XElement node, string attribName, bool defaultValue)
        {
            var valueText = GetElementAttribValue(node, attribName, string.Empty);
            if (bool.TryParse(valueText, out var value))
                return value;

            return defaultValue;
        }

        public override string GetErrorMessage()
        {
            return "GetErrorMessage is not supported";
        }

        /// <summary>
        /// Start processing (calls Start)
        /// </summary>
        /// <param name="inputFilePath">Report definitions file</param>
        /// <param name="outputFolderPath">Ignored</param>
        /// <param name="parameterFilePath">Ignored</param>
        /// <param name="resetErrorCode">Ignored</param>
        /// <returns></returns>
        public override bool ProcessFile(string inputFilePath, string outputFolderPath, string parameterFilePath, bool resetErrorCode)
        {
            Options.ReportDefinitionsFilePath = inputFilePath;
            var success = Start();
            return success;
        }

        private bool ReadReportDefsFile(bool firstLoad = false)
        {
            const string DEFAULT_TIME_OF_DAY = "7:00 am";

            var notifySettingOverride = firstLoad;

            try
            {
                if (firstLoad)
                {
                    Console.WriteLine();
                    Console.WriteLine("Reading report definitions");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Updating report definitions");
                }

                var existingTasks = new SortedSet<string>();
                foreach (var task in mTasks)
                {
                    existingTasks.Add(task.Key);
                    AddUpdateRuntimeInfo(task);
                }

                // Re-populate mTasks every time we read the report definitions file
                mTasks.Clear();

                var doc = XDocument.Load(Options.ReportDefinitionsFilePath);

                var emailInfo = doc.Elements("reports").Elements("EmailInfo").FirstOrDefault();
                if (emailInfo == null)
                {
                    LogMessage("The Report definitions file is missing the <EmailInfo> section; using defaults", eMessageTypeConstants.Debug);
                    Options.EmailServer = DMSEmailManagerOptions.DEFAULT_EMAIL_SERVER;
                    Options.EmailFrom = DMSEmailManagerOptions.DEFAULT_EMAIL_FROM;
                    Options.FontSizeHeader = DMSEmailManagerOptions.DEFAULT_FONT_SIZE_HEADER;
                    Options.FontSizeBody = DMSEmailManagerOptions.DEFAULT_FONT_SIZE_BODY;
                }
                else
                {
                    // Read the email options
                    var emailServer = GetElementAttribValue(emailInfo, "Server", DMSEmailManagerOptions.DEFAULT_EMAIL_SERVER);
                    WarnIfOverride(notifySettingOverride, Options.EmailServer, emailServer, "email server name");
                    Options.EmailServer = emailServer;

                    var emailFrom = GetElementAttribValue(emailInfo, "From", DMSEmailManagerOptions.DEFAULT_EMAIL_FROM);
                    WarnIfOverride(notifySettingOverride, Options.EmailFrom, emailFrom, "email sender name");
                    Options.EmailFrom = emailFrom;

                    var fontSizeHeader = GetElementAttribValue(emailInfo, "FontSizeHeader", DMSEmailManagerOptions.DEFAULT_FONT_SIZE_HEADER);
                    WarnIfOverride(notifySettingOverride, Options.FontSizeHeader.ToString(), fontSizeHeader.ToString(), "header font size");
                    Options.FontSizeHeader = fontSizeHeader;

                    var fontSizeBody = GetElementAttribValue(emailInfo, "FontSizeBody", DMSEmailManagerOptions.DEFAULT_FONT_SIZE_BODY);
                    WarnIfOverride(notifySettingOverride, Options.FontSizeBody.ToString(), fontSizeBody.ToString(), "body font size");
                    Options.FontSizeBody = fontSizeBody;
                }

                // Validate the e-mail options
                Options.EmailServer = ValidateNotEmpty(Options.EmailServer, DMSEmailManagerOptions.DEFAULT_EMAIL_SERVER);
                Options.EmailFrom = ValidateNotEmpty(Options.EmailFrom, DMSEmailManagerOptions.DEFAULT_EMAIL_FROM);
                Options.FontSizeHeader = ValidateNonZero(Options.FontSizeHeader, DMSEmailManagerOptions.DEFAULT_FONT_SIZE_HEADER);
                Options.FontSizeBody = ValidateNonZero(Options.FontSizeBody, DMSEmailManagerOptions.DEFAULT_FONT_SIZE_BODY);

                var reportDefs = doc.Elements("reports").Elements("report").ToList();
                foreach (var report in reportDefs)
                {
                    if (!report.HasAttributes)
                    {
                        ShowWarning("Ignoring report definition without any attributes");
                        continue;
                    }

                    var reportNameAttrib = report.Attribute("name");
                    if (reportNameAttrib == null)
                    {
                        ShowWarning("Ignoring report definition without a 'name' attribute");
                        continue;
                    }

                    // ReportName is the TaskID
                    var reportName = reportNameAttrib.Value;
                    if (string.IsNullOrWhiteSpace(reportName))
                    {
                        ShowWarning("Ignoring report definition with an empty string 'name' attribute");
                        continue;
                    }

                    if (mTasks.ContainsKey(reportName))
                    {
                        ShowWarning(string.Format("Duplicate report named '{0}' in the report definition file; only using the first instance",
                                                  reportName));
                        continue;
                    }

                    DateTime lastRun;
                    int executionCount;
                    if (mRuntimeInfo.TryGetValue(reportName, out var taskRuntimeInfo))
                    {
                        lastRun = taskRuntimeInfo.LastRun;
                        executionCount = taskRuntimeInfo.ExecutionCount;
                    }
                    else
                    {
                        lastRun = DateTime.MinValue;
                        executionCount = 0;

                        // Note that SourceType and SourceDefinition will be updated below via mRuntimeInfo[reportName].UpdateDataSource()
                        mRuntimeInfo.Add(reportName, new TaskRuntimeInfo(lastRun));
                    }

                    var dataSourceInfo = GetChildElement(reportName, report, "data");
                    var mailInfo = GetChildElement(reportName, report, "mail");
                    var frequencyInfo = GetChildElement(reportName, report, "frequency");

                    // WMI reports support XML of the form <valuedivisor value="1073741824" round="2" units="GB" />
                    // the <valuedivisor> section will not be present for other reports
                    var divisorInfo = GetChildElement(reportName, report, "valuedivisor", false);

                    if (dataSourceInfo == null || mailInfo == null || frequencyInfo == null)
                        continue;

                    var sourceServerLegacy = GetElementAttribValue(dataSourceInfo, "source", string.Empty);
                    var sourceServer = GetElementAttribValue(dataSourceInfo, "server", sourceServerLegacy);
                    var sourceHost = GetElementAttribValue(dataSourceInfo, "host", sourceServer);

                    var sourceDBLegacy = GetElementAttribValue(dataSourceInfo, "catalog", string.Empty);
                    var sourceDB = GetElementAttribValue(dataSourceInfo, "database", sourceDBLegacy);

                    var sourceType = GetElementAttribValue(dataSourceInfo, "type", string.Empty);

                    var query = dataSourceInfo.Value;
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        ShowWarning(string.Format("Ignoring report definition '{0}'; query (or procedure name) not defined in the data element", reportName));
                        continue;
                    }

                    DataSourceBase dataSource;
                    var sourceTypeLcase = sourceType.ToLower().Trim();
                    switch (sourceTypeLcase)
                    {
                        case "query":
                        case "table":
                        case "view":
                        case "procedure":
                        case "storedprocedure":
                        case "sp":
                        case "sproc":
                            if (string.IsNullOrWhiteSpace(sourceServer))
                            {
                                ShowWarning(string.Format("Ignoring report definition '{0}'; server not defined in the data element", reportName));
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(sourceDB))
                            {
                                ShowWarning(string.Format("Ignoring report definition '{0}'; database not defined in the data element", reportName));
                                continue;
                            }

                            if (sourceTypeLcase == "procedure" || sourceTypeLcase == "storedprocedure" || sourceTypeLcase == "sp" ||
                                sourceTypeLcase == "sproc")
                            {
                                dataSource = new DataSourceSqlStoredProcedure(reportName, sourceServer, sourceDB, query);
                            }
                            else
                            {
                                dataSource = new DataSourceSqlQuery(reportName, sourceServer, sourceDB, query);
                            }

                            break;

                        case "wmi":
                            if (string.IsNullOrWhiteSpace(sourceHost))
                            {
                                ShowWarning(string.Format("Ignoring report definition '{0}'; server or host not defined in the data element", reportName));
                                continue;
                            }

                            var wmiDataSource = new DataSourceWMI(reportName, sourceServer, query);

                            if (divisorInfo != null)
                            {
                                var valueDivisor = GetElementAttribValue(divisorInfo, "value", 0L);
                                var roundDigits = GetElementAttribValue(divisorInfo, "round", 0);
                                var valueUnits = GetElementAttribValue(divisorInfo, "units", string.Empty);

                                wmiDataSource.ValueDivisor = valueDivisor;
                                if (roundDigits >= byte.MinValue && roundDigits <= byte.MaxValue)
                                {
                                    wmiDataSource.DivisorRoundDigits = (byte)roundDigits;
                                }

                                wmiDataSource.DivisorUnits = valueUnits;
                            }

                            dataSource = wmiDataSource;

                            break;

                        default:
                            ShowWarning(string.Format("Ignoring report definition '{0}'; invalid type {1} in the data element; should be {2}",
                                                      reportName, sourceType, "query, procedure, or wmi"));
                            continue;
                    }

                    mRuntimeInfo[reportName].UpdateDataSource(dataSource);

                    var mailRecipients = GetElementAttribValue(mailInfo, "to", string.Empty);

                    var sepChars = new[] { ',', ';' };
                    var emailList = mailRecipients.Split(sepChars);
                    if (emailList.Length == 0)
                    {
                        ShowWarning(string.Format("Ignoring report definition '{0}'; invalid to list {1} in the mail element; should be a comma separated list of e-mail addresses",
                                                  reportName, sourceType));
                        continue;
                    }

                    var mailSubject = GetElementAttribValue(mailInfo, "subject", string.Empty);
                    var reportTitle = GetElementAttribValue(mailInfo, "title", string.Empty);

                    var mailIfEmpty = GetElementAttribValue(mailInfo, "mailIfEmpty", true);

                    // This is a legacy setting
                    var legacyFrequencyDaily = GetElementAttribValue(frequencyInfo, "daily", false);

                    var daysOfWeekText = GetElementAttribValue(frequencyInfo, "dayofweeklist", string.Empty);

                    var daysOfWeek = new SortedSet<DayOfWeek>();
                    if (legacyFrequencyDaily)
                    {
                        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
                        {
                            daysOfWeek.Add(day);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(daysOfWeekText))
                    {
                        foreach (var day in daysOfWeekText.Split(sepChars))
                        {
                            var trimmedDay = day.Trim();

                            if (string.IsNullOrWhiteSpace(trimmedDay))
                                continue;

                            // Allow the weekday names to be abbreviations
                            // Only examine the first three characters of the day's name
                            DayOfWeek dayOfWeek;
                            switch (trimmedDay.ToLower().Substring(0, 3))
                            {
                                case "sun":
                                    dayOfWeek = DayOfWeek.Sunday;
                                    break;
                                case "mon":
                                    dayOfWeek = DayOfWeek.Monday;
                                    break;
                                case "tue":
                                    dayOfWeek = DayOfWeek.Tuesday;
                                    break;
                                case "wed":
                                    dayOfWeek = DayOfWeek.Wednesday;
                                    break;
                                case "thu":
                                    dayOfWeek = DayOfWeek.Thursday;
                                    break;
                                case "fri":
                                    dayOfWeek = DayOfWeek.Friday;
                                    break;
                                case "sat":
                                    dayOfWeek = DayOfWeek.Saturday;
                                    break;
                                default:
                                    continue;
                            }

                            if (daysOfWeek.Contains(dayOfWeek))
                                continue;

                            daysOfWeek.Add(dayOfWeek);
                        }
                    }

                    var delayTypeText = GetElementAttribValue(frequencyInfo, "type", string.Empty);

                    var emailSettings = new EmailMessageSettings(emailList, mailSubject, reportTitle, mailIfEmpty);

                    NotificationTask task;
                    string assumedTimeOfDay;

                    if (string.IsNullOrWhiteSpace(delayTypeText))
                    {
                        ShowWarning(string.Format("Type attribute not found for the frequency element for report '{0}'; " +
                                                  "will assume type=\"TimeOfDay\" and timeOfDay=\"{1}\"", reportName, DEFAULT_TIME_OF_DAY));
                        assumedTimeOfDay = DEFAULT_TIME_OF_DAY;
                    }
                    else
                    {
                        assumedTimeOfDay = string.Empty;
                    }

                    if (delayTypeText.ToLower().Contains("time") || !string.IsNullOrWhiteSpace(assumedTimeOfDay))
                    {
                        // Run a report at a given time, every day, or certain days of the week

                        var timeOfDayText = GetElementAttribValue(frequencyInfo, "timeOfDay", assumedTimeOfDay);

                        LocalTime timeOfDay;
                        if (string.IsNullOrWhiteSpace(timeOfDayText))
                        {
                            ShowWarning(string.Format("timeOfDay attribute not found for the frequency element for report '{0}'; will assume {1}", reportName, DEFAULT_TIME_OF_DAY));
                            TryParseTimeOfDay(DEFAULT_TIME_OF_DAY, out timeOfDay);
                        }
                        else if (!TryParseTimeOfDay(timeOfDayText, out timeOfDay))
                        {
                            ShowWarning(string.Format("Ignoring report definition '{0}'; invalid timeOfDay {1}; should be a time like '7:00 am' or '13:00'",
                                                      reportName, timeOfDayText));
                            continue;
                        }

                        task = new NotificationTask(reportName, dataSource, emailSettings, lastRun, timeOfDay, daysOfWeek)
                        {
                            ExecutionCount = executionCount
                        };
                    }
                    else if (delayTypeText.ToLower().Contains("interval"))
                    {
                        // Run a report on a given interval

                        var delayInterval = GetElementAttribValue(frequencyInfo, "interval", string.Empty);
                        var delayIntervalUnits = GetElementAttribValue(frequencyInfo, "units", string.Empty);

                        if (!int.TryParse(delayInterval, out var interval))
                        {
                            ShowWarning(string.Format("Ignoring report definition '{0}'; invalid interval {1}; should be an integer",
                                                      reportName, delayInterval));
                            continue;
                        }

                        var lcaseUnits = delayIntervalUnits.ToLower();
                        NotificationTask.FrequencyInterval intervalUnits;

                        if (lcaseUnits.Contains("second"))
                            intervalUnits = NotificationTask.FrequencyInterval.Second;
                        else if (lcaseUnits.Contains("minute"))
                            intervalUnits = NotificationTask.FrequencyInterval.Minute;
                        else if (lcaseUnits.Contains("hour"))
                            intervalUnits = NotificationTask.FrequencyInterval.Hour;
                        else if (lcaseUnits.Contains("daily") || lcaseUnits.Contains("day"))
                            intervalUnits = NotificationTask.FrequencyInterval.Day;
                        else if (lcaseUnits.Contains("week"))
                            intervalUnits = NotificationTask.FrequencyInterval.Week;
                        else if (lcaseUnits.Contains("month"))
                            intervalUnits = NotificationTask.FrequencyInterval.Month;
                        else if (lcaseUnits.Contains("year"))
                            intervalUnits = NotificationTask.FrequencyInterval.Year;
                        else
                        {
                            ShowWarning(string.Format("Ignoring report definition '{0}'; invalid interval units {1}; should be {2}",
                                                      reportName, delayInterval, "seconds, minutes, hours, days, weeks, months, or years"));
                            continue;
                        }

                        task = new NotificationTask(reportName, dataSource, emailSettings, lastRun, interval, intervalUnits)
                        {
                            ExecutionCount = executionCount
                        };
                    }
                    else
                    {
                        ShowWarning(string.Format("Invalid frequency type {0} for report '{1}'; should be TimeOfDay or Interval", delayTypeText, reportName));
                        continue;
                    }

                    var postMailHookInfo = GetChildElement(reportName, report, "postMailIdListHook", false);
                    if (postMailHookInfo != null)
                    {
                        var postMailServer = GetElementAttribValue(postMailHookInfo, "server", string.Empty);
                        var postMailDatabase = GetElementAttribValue(postMailHookInfo, "database", string.Empty);
                        var postMailProcedure = GetElementAttribValue(postMailHookInfo, "procedure", string.Empty);

                        // This is the name of the first parameter of the stored procedure
                        var paramName = GetElementAttribValue(postMailHookInfo, "parameter", string.Empty);
                        var varcharLength = GetElementAttribValue(postMailHookInfo, "varcharlength", 0);

                        if (string.IsNullOrWhiteSpace(postMailServer))
                        {
                            ShowWarning(string.Format("Error in report definition '{0}'; server not defined in the postMailHook element", reportName));
                        }
                        else if (string.IsNullOrWhiteSpace(postMailDatabase))
                        {
                            ShowWarning(string.Format("Error in report definition '{0}'; database not defined in the postMailHook element", reportName));
                        }
                        else if (string.IsNullOrWhiteSpace(postMailProcedure))
                        {
                            ShowWarning(string.Format("Error in report definition '{0}'; procedure not defined in the postMailHook element", reportName));
                        }
                        else if (string.IsNullOrWhiteSpace(paramName))
                        {
                            ShowWarning(string.Format("Error in report definition '{0}'; parameter name not defined in the postMailHook element", reportName));
                        }
                        else
                        {
                            var postMailHook = new DataSourceSqlStoredProcedure(reportName, postMailServer, postMailDatabase, postMailProcedure)
                            {
                                StoredProcParameter = paramName
                            };

                            if (varcharLength > 0)
                            {
                                postMailHook.StoredProcParamLength = varcharLength;
                                postMailHook.StoredProcParamType = SqlDbType.VarChar;
                            }

                            task.PostMailIdListHook = postMailHook;
                        }
                    }

                    mTasks.Add(reportName, task);

                    var frequencyDescription = task.GetFrequencyDescription();

                    string spacePadding;
                    if (reportName.Length < maxReportNameLength)
                        spacePadding = new string(' ', maxReportNameLength - reportName.Length);
                    else
                        spacePadding = string.Empty;

                    Console.WriteLine();
                    LogMessage(string.Format("Added report '{0}'{1} running {2}, e-mailing {3}",
                                              reportName, spacePadding, frequencyDescription, task.EmailSettings.GetRecipients(",")));

                    var nextRunLocalTime = task.NextRun.ToLocalTime();

                    LogMessage(string.Format("will next run on {0:d} at {1:h:mm:ss tt}", nextRunLocalTime, nextRunLocalTime), eMessageTypeConstants.Debug);

                    RegisterEvents(task);
                    task.TaskResultsAvailable += Task_TaskResultsAvailable;

                }

                foreach (var reportName in existingTasks)
                {
                    if (!mTasks.ContainsKey(reportName))
                    {
                        Console.WriteLine();
                        LogMessage(string.Format("Removed report '{0}'", reportName));
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error reading the report definitions file", ex);
                return false;
            }


        }

        /// <summary>
        /// Read an XML file tracking ReportName, LastRun, NextRun, SourceType, and SourceQuery
        /// </summary>
        private void ReadReportStatusFile()
        {

            var currentTask = "Constructing the report status file path";

            try
            {
                var reportStatusFile = new FileInfo(Path.Combine(GetAppFolderPath(), REPORT_STATUS_FILE_NAME));

                if (!reportStatusFile.Exists)
                {
                    ShowWarning("Report status file not found: " + reportStatusFile.FullName);
                    return;
                }

                currentTask = "Caching the XML in file " + reportStatusFile.FullName;

                var doc = XDocument.Load(reportStatusFile.FullName);

                var loadedTaskNames = new SortedSet<string>();

                currentTask = "Reading XML data";

                var reportStatus = doc.Elements("Reports").Elements("Report").ToList();
                foreach (var report in reportStatus)
                {
                    if (!report.HasAttributes)
                    {
                        ShowWarning("Ignoring report status without any attribute");
                        continue;
                    }

                    var reportNameAttrib = report.Attribute("name");
                    if (reportNameAttrib == null)
                    {
                        ShowWarning("Ignoring report status without a 'name' attribute");
                        continue;
                    }

                    var reportName = reportNameAttrib.Value;
                    if (string.IsNullOrWhiteSpace(reportName))
                    {
                        ShowWarning("Ignoring report definition with an empty string 'name' attribute");
                        continue;
                    }

                    if (mRuntimeInfo.ContainsKey(reportName) && loadedTaskNames.Contains(reportName))
                    {
                        ShowWarning(string.Format("Duplicate report named '{0}' in the report status file; only using the first instance",
                                                  reportName));
                        continue;
                    }

                    var lastRunUtc = GetChildElementValue(report, "LastRunUTC", DateTime.MinValue);
                    var nextRunUtc = GetChildElementValue(report, "LastRunUTC", DateTime.MinValue);
                    var executionCount = GetChildElementValue(report, "ExecutionCount", 0);
                    var sourceType = GetChildElementValue(report, "SourceType", string.Empty);
                    var sourceQuery = GetChildElementValue(report, "SourceQuery", string.Empty);

                    var runtimeInfo = new TaskRuntimeInfo(lastRunUtc, executionCount) {
                        NextRun = nextRunUtc,
                        SourceDefinition = sourceQuery
                    };

                    if (Enum.TryParse<DataSourceBase.DataSourceType>(sourceType, true, out var dataSourceType))
                    {
                        runtimeInfo.SourceType = dataSourceType;
                    }

                    if (mRuntimeInfo.ContainsKey(reportName))
                        mRuntimeInfo[reportName] = runtimeInfo;
                    else
                        mRuntimeInfo.Add(reportName, runtimeInfo);

                    loadedTaskNames.Add(reportName);
                }
            }
            catch (Exception ex)
            {
                HandleException("Error reading the report status file, current task " + currentTask, ex);
            }
        }

        /// <summary>
        /// Run all tasks now, provided dayofweeklist includes today's date (or is empty)
        /// </summary>
        private void RunAllTasks()
        {
            foreach (var task in mTasks)
            {
                try
                {
                    if (task.Value.DaysOfWeek.Count > 0 && !task.Value.DaysOfWeek.Contains(DateTime.Now.DayOfWeek))
                    {
                        // Skip this report because it is not set to run today
                        continue;
                    }

                    var taskRun = task.Value.RunTaskNow();

                    if (!taskRun)
                        continue;

                    AddUpdateRuntimeInfo(task);
                }
                catch (Exception ex)
                {
                    HandleException(string.Format("Error running task '{0}'", task.Key), ex);
                }

            }
        }

        /// <summary>
        /// Run any tasks that need to be run
        /// </summary>
        /// <returns>The number of tasks that were run</returns>
        private void RunElapsedTasks()
        {

            foreach (var task in mTasks)
            {
                try
                {
                    var taskRun = task.Value.RunTaskNowIfRequired();

                    if (!taskRun)
                        continue;

                    AddUpdateRuntimeInfo(task);
                }
                catch (Exception ex)
                {
                    HandleException(string.Format("Error running task '{0}'", task.Key), ex);
                }

            }
        }

        /// <summary>
        /// Save an XML file tracking ReportName, LastRun, NextRun, SourceType, and SourceQuery
        /// </summary>
        private void SaveReportStatusFile()
        {

            var currentTask = "Constructing the temp file path";

            try
            {
                var reportStatusFileTemp = new FileInfo(Path.Combine(GetAppFolderPath(), REPORT_STATUS_FILE_NAME + ".tmp"));

                currentTask = "Opening the temp status info file for writing";

                // Generate the XML using LINQ to XML https://stackoverflow.com/a/2076568/1179467

                // Use .ToString("O") to guarantee that the correct universal time is loaded in ReadReportStatusFile
                // when we use DateTime.ParseExact(dataValue, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                // For more info, seee https://stackoverflow.com/a/12064151/1179467

                var masterDoc = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("Reports",
                        mTasks.Select(task =>
                            new XElement("Report",
                                new XAttribute("name", task.Key),
                                new XElement("LastRunUTC", task.Value.LastRun.ToString("O")),
                                new XElement("NextRunUTC", task.Value.NextRun.ToString("O")),
                                new XElement("ExecutionCount", task.Value.ExecutionCount),
                                new XElement("SourceType", task.Value.DataSource.SourceType),
                                new XElement("SourceQuery", task.Value.DataSource.SourceDefinition)
                                ))
                        )
                    );

                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    OmitXmlDeclaration = false
                };

                using (var fileWriter = new StreamWriter(new FileStream(reportStatusFileTemp.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
                {
                    using (var xmlWriter = XmlWriter.Create(fileWriter, settings))
                    {
                        masterDoc.Save(xmlWriter);
                    }
                }

                /*
                 * Older style XML generation method using XmlTextWriter

                    var reportStatusFileTemp2 = new FileInfo(Path.Combine(GetAppFolderPath(), REPORT_STATUS_FILE_NAME + ".tmp"));

                    var writer = new XmlTextWriter(reportStatusFileTemp2.FullName, System.Text.Encoding.UTF8);
                    writer.WriteStartDocument(true);
                    writer.Formatting = Formatting.Indented;
                    writer.Indentation = 2;
                    writer.WriteStartElement("Reports");
                    foreach (var task in mTasks)
                    {
                        writer.WriteStartElement("Report");
                        writer.WriteAttributeString("name", task.Key);

                        writer.WriteStartElement("LastRunUTC");
                        writer.WriteString(task.Value.LastRun.ToString("O"));
                        writer.WriteEndElement();

                        writer.WriteStartElement("NextRunUTC");
                        writer.WriteString(task.Value.NextRun.ToString("O"));
                        writer.WriteEndElement();

                        writer.WriteStartElement("ExecutionCount");
                        writer.WriteString(task.Value.ExecutionCount.ToString());
                        writer.WriteEndElement();

                        writer.WriteStartElement("SourceType");
                        writer.WriteString(task.Value.DataSource.SourceType.ToString());
                        writer.WriteEndElement();

                        writer.WriteStartElement("SourceQuery");
                        writer.WriteString(task.Value.DataSource.SourceDefinition);
                        writer.WriteEndElement();

                        writer.WriteEndElement(); // </Report>
                    }

                    writer.WriteEndElement();   // </Reports>
                    writer.WriteEndDocument();
                    writer.Close();

                 *
                 */

                // Backup the current reportStatusFile
                currentTask = "Preparing to backup the reportStatusFile";
                var reportStatusFile = new FileInfo(Path.Combine(GetAppFolderPath(), REPORT_STATUS_FILE_NAME));
                var reportStatusFileOld = new FileInfo(Path.Combine(GetAppFolderPath(), REPORT_STATUS_FILE_NAME + ".old"));

                if (reportStatusFile.Exists)
                {
                    currentTask = string.Format("Backing up {0} to create {1}", reportStatusFile.Name, reportStatusFileOld.Name);
                    reportStatusFile.CopyTo(reportStatusFileOld.FullName, true);
                    reportStatusFile.Delete();
                }

                currentTask = string.Format("Renaming {0} to {1}", reportStatusFileTemp.Name, reportStatusFile.Name);
                reportStatusFileTemp.MoveTo(reportStatusFile.FullName);

                mRuntimeInfoSaveRequired = false;
            }
            catch (Exception ex)
            {
                HandleException("Error saving the report status file, current task " + currentTask, ex);

            }
        }

        private void SendResultIDsToPostMailHook(TaskResults results, DataSourceSqlStoredProcedure postMailIdListHook)
        {

            try
            {
                if (postMailIdListHook == null)
                    return;

                if (string.IsNullOrWhiteSpace(postMailIdListHook.ServerName) ||
                    string.IsNullOrWhiteSpace(postMailIdListHook.DatabaseName) ||
                    string.IsNullOrWhiteSpace(postMailIdListHook.StoredProcedureName))
                {
                    ShowWarning(string.Format(
                                    "Skipping sending the results ID list to the post mail stored procedure for report '{0}' " +
                                    "since Server, Database, or Stored Procedure name is empty", results.ReportName));
                    return;
                }

                if (string.IsNullOrWhiteSpace(postMailIdListHook.StoredProcParameter))
                {
                    ShowWarning(string.Format(
                                    "Skipping sending the results ID list to the post mail stored procedure for report '{0}' " +
                                    "since the storced procedure parameter name is not defined; " +
                                    "define the first parameter name using parameter in the postMailIdListHook section", results.ReportName));
                    return;
                }


                var resultIdList = new List<string>();

                foreach (var dataRow in results.DataRows)
                {
                    if (dataRow.Count == 0 || string.IsNullOrWhiteSpace(dataRow[0]))
                        continue;

                    resultIdList.Add(dataRow[0]);
                }

                var connStr = string.Format("Data Source={0};Initial Catalog={1};Integrated Security=SSPI;Connection Timeout={2};",
                                            postMailIdListHook.ServerName, postMailIdListHook.DatabaseName, DataSourceSql.CONNECTION_TIMEOUT_SECONDS);

                var cmd = new SqlCommand(postMailIdListHook.StoredProcedureName)
                {
                    CommandType = CommandType.StoredProcedure
                };

                var paramName = "@" + postMailIdListHook.StoredProcParameter;
                var sqlParam = new SqlParameter(paramName, postMailIdListHook.StoredProcParamType, postMailIdListHook.StoredProcParamLength)
                {
                    Value = string.Join(",", resultIdList)
                };

                cmd.Parameters.Add(sqlParam);

                var spRunner = new clsExecuteDatabaseSP(connStr)
                {
                    TimeoutSeconds = DataSourceSql.QUERY_TIMEOUT_SECONDS
                };

                var errorCode = spRunner.ExecuteSP(cmd, 1);

                if (errorCode != 0)
                {
                    ShowWarning(string.Format(
                                    "Procedure {0} in database {1} on server {2} returned error code {3}",
                                    postMailIdListHook.StoredProcedureName,
                                    postMailIdListHook.DatabaseName,
                                    postMailIdListHook.ServerName,
                                    errorCode));
                }
            }
            catch (Exception ex)
            {
                HandleException(string.Format(
                                    "Error sending the results ID list to the post mail stored procedure for report '{0}'", results.ReportName),
                                ex);

            }
        }

        /// <summary>
        /// Read task definitions then monitor the tasks and run them at the specified time
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            var startTime = DateTime.UtcNow;

            var lastTaskCheckTime = DateTime.UtcNow;
            var lastStatusWriteTime = DateTime.UtcNow;

            try
            {
                var stopTime = Options.MaxRuntimeHours > 0 ? startTime.AddHours(Options.MaxRuntimeHours) : DateTime.MaxValue;

                ReadReportStatusFile();

                mReportDefsFileChanged = false;
                var success = ReadReportDefsFile(true);
                if (!success)
                    return false;

                Console.WriteLine();
                Options.OutputSetOptions(false);

                if (Options.RunOnce)
                {
                    RunAllTasks();
                    return true;
                }

                while (stopTime > DateTime.UtcNow)
                {
                    ConsoleMsgUtils.SleepSeconds(1);

                    if (mReportDefsFileChanged)
                    {
                        mReportDefsFileChanged = false;
                        ReadReportDefsFile();
                    }

                    if (DateTime.UtcNow.Subtract(lastTaskCheckTime).TotalSeconds >= TASK_CHECK_INTERVAL_SECONDS)
                    {
                        lastTaskCheckTime = DateTime.UtcNow;
                        RunElapsedTasks();
                    }

                    if (DateTime.UtcNow.Subtract(lastStatusWriteTime).TotalMinutes >= REPORT_STATUS_FILE_UPDATE_INTERVAL_MINUTES)
                    {
                        lastStatusWriteTime = DateTime.UtcNow;
                        if (mRuntimeInfoSaveRequired)
                        {
                            SaveReportStatusFile();
                        }
                    }
                }

                if (mRuntimeInfoSaveRequired)
                {
                    SaveReportStatusFile();
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error in main loop", ex);

                if (mRuntimeInfoSaveRequired)
                {
                    SaveReportStatusFile();
                }
                return false;
            }

        }

        private bool TryParseTimeOfDay(string timeOfDayText, out LocalTime timeOfDay)
        {
            if (!DateTime.TryParse(timeOfDayText, out var parsedTime))
            {
                timeOfDay = LocalTime.MinValue;
                return false;
            }

            timeOfDay = new LocalTime(parsedTime.Hour, parsedTime.Minute, parsedTime.Second);
            return true;
        }

        private int ValidateNonZero(int value, int defaultValue)
        {
            return value == 0 ? defaultValue : value;
        }

        private string ValidateNotEmpty(string value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private void WarnIfOverride(bool notifySettingOverride, string currentValue, string newValue, string valueDescription)
        {
            if (!notifySettingOverride)
                return;

            if (string.IsNullOrWhiteSpace(currentValue) || currentValue == "0" || newValue == currentValue)
                return;

            var warningMsg = string.Format("Overriding {0} using value in the report definitions file: {1}", valueDescription, newValue);
            ShowWarning(warningMsg, false);
        }

        #region "Event Handlers"

        private void ReportDefsWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            mReportDefsFileChanged = true;
        }

        private void Task_TaskResultsAvailable(
            TaskResults results,
            EmailMessageSettings emailSettings,
            DataSourceSqlStoredProcedure postMailIdListHook)
        {
            EmailResults(results, emailSettings, postMailIdListHook);
        }

        #endregion
    }
}
