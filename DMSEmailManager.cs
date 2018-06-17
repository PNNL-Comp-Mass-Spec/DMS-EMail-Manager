using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NodaTime;
using PRISM;

namespace DMS_Email_Manager
{
    public class DMSEmailManager : PRISM.FileProcessor.ProcessFilesBase
    {
        #region "Constants"

        private const string NO_DATA = "No Data Returned";

        private const int TASK_CHECK_INTERVAL_SECONDS = 15;
        private const int TASK_STATUS_FILE_UPDATE_INTERVAL_MINUTES = 1;

        #endregion

        #region "Fields"

        /// <summary>
        /// mReportDefsWatcher_Changed sets this to true if the ReportDefinitions file is updated
        /// </summary>
        private bool mReportDefsFileChanged;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly FileSystemWatcher mReportDefsWatcher;

        /// <summary>
        /// Keys are task names, values are Notification Tasks
        /// </summary>
        private readonly Dictionary<string, NotificationTask> mTasks = new Dictionary<string, NotificationTask>();

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
            ShowMessage("Starting the DMS Email Manager");

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

            mReportDefsFileChanged = false;

            mReportDefsWatcher = new FileSystemWatcher(Options.ReportDefinitionsFilePath);
            mReportDefsWatcher.Changed += ReportDefsWatcher_Changed;
        }


        private void EmailResults(
            TaskResults results,
            EmailMessageSettings mailSettings,
            DataSourceSqlStoredProcedure postMailIdListHook)
        {
            try
            {

                var reportHtml = new StringBuilder();

                if (results.ColumnNames.Count == 0 || results.DataRows.Count == 0)
                {
                    reportHtml.AppendLine(NO_DATA);
                }
                else
                {
                    // Construct the header row
                    reportHtml.AppendLine("<table>");
                    reportHtml.AppendLine("<tr class = table-header>");
                    foreach (var headerCol in results.ColumnNames)
                    {
                        reportHtml.Append("<td>");
                        reportHtml.Append(headerCol);
                        reportHtml.Append("</td>");
                    }
                    reportHtml.AppendLine("</tr>");

                    for (var i = 0; i < results.DataRows.Count; i++)
                    {
                        if (i % 2 == 0)
                        {
                            reportHtml.Append("<tr class = table-row>");
                        }
                        else
                        {
                            reportHtml.Append("<tr class = table-alternate-row>");
                        }

                        var currentRow = results.DataRows[i];
                        foreach (var dataVal in currentRow)
                        {
                            reportHtml.Append("<td>");
                            reportHtml.Append(dataVal);
                            reportHtml.Append("</td>");
                        }

                        reportHtml.AppendLine("</tr>");
                    }
                }

                var formattedRecipients = string.Join(",", mailSettings.Recipients);

                if (Options.PreviewMode)
                {
                    Console.WriteLine();
                    Console.WriteLine("Preview of message to be e-mailed to " + formattedRecipients);
                    Console.WriteLine(reportHtml.ToString());
                    return;
                }

                var emailBody = new StringBuilder();

                emailBody.AppendLine("<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\">");
                emailBody.AppendLine("<html><head>");
                emailBody.AppendLine(GetCssStyle());
                emailBody.AppendLine("</head><body>");

                emailBody.AppendLine("<h3>" + mailSettings.ReportTitle + "</h3>");
                emailBody.AppendLine(reportHtml.ToString());
                emailBody.AppendLine("</body></html>");

                var msg = new System.Net.Mail.MailMessage(Options.EmailFrom, formattedRecipients)
                {
                    BodyEncoding = Encoding.UTF8,
                    IsBodyHtml = true,
                    Subject = mailSettings.Subject,
                    Body = emailBody.ToString()
                };

                var mailClient = new System.Net.Mail.SmtpClient(Options.EmailServer);
                mailClient.Send(msg);

            }
            catch (Exception ex)
            {
                HandleException(string.Format(
                                    "Error e-mailing the results to {0} for report {1}", string.Join(",", mailSettings.Recipients), results.ReportName),
                                ex);
                return;
            }
        }

        private XElement GetChildElement(string reportName, XContainer report, string childNodeName, bool warnIfMissing = true)
        {
            var childNode = report.Elements(childNodeName).FirstOrDefault();
            if (childNode != null)
                return childNode;

            if (warnIfMissing)
                ShowWarning(string.Format("Ignoring report definition {0}; missing the {1} element", reportName, childNodeName));

            return null;

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

        private string GetCssStyle()
        {
            var cssStyle = "<style type=\"text / css\" media=\"all\" > " +
                           "body { font: 12px Verdana, Arial, Helvetica, sans-serif; margin: 20px; }" +
                           "h3 { font: 20px Verdana, Arial, Helvetica, sans-serif; }" +
                           "table { margin: 4px; border-style: ridge; border-width: 2px; }" +
                           ".table-header { color: white; background-color: #8080FF; }" +
                           ".table-row { background-color: #D8D8FF; vertical-align:top;}" +
                           ".table-alternate-row { background-color: #C0C0FF; vertical-align:top;}" +
                           "</style>";

            return cssStyle;
        }

        private void LoadTaskStatusFile()
        {

            // ToDo: Read a file tracking TaskID  task RuntimeInfo
            throw new NotImplementedException();

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

        private bool ReadReportDefsFile(bool notifySettingOverride = false)
        {
            try
            {
                var doc = XDocument.Load(Options.ReportDefinitionsFilePath);

                var emailInfo = doc.Elements("reports").Elements("EmailInfo").FirstOrDefault();
                if (emailInfo != null)
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
                        ShowWarning("Ignoring report definition without a name attribute");
                        continue;
                    }

                    var reportNameAttrib = report.Attribute("name");
                    if (reportNameAttrib == null)
                    {
                        ShowWarning("Ignoring report definition without a name attribute");
                        continue;
                    }

                    // ReportName will be used for TaskID
                    var reportName = reportNameAttrib.Value;
                    if (string.IsNullOrWhiteSpace(reportName))
                    {
                        ShowWarning("Ignoring report definition with an empty string name attribute");
                        continue;
                    }

                    DateTime lastRun;
                    if (mRuntimeInfo.TryGetValue(reportName, out var taskRuntimeInfo))
                    {
                        lastRun = taskRuntimeInfo.LastRun;
                    }
                    else
                    {
                        lastRun = DateTime.UtcNow;
                        mRuntimeInfo.Add(reportName, new TaskRuntimeInfo(lastRun));
                    }

                    var dataSourceInfo = GetChildElement(reportName, report, "data");
                    var mailInfo = GetChildElement(reportName, report, "mail");
                    var frequencyInfo = GetChildElement(reportName, report, "frequency");

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
                        ShowWarning(string.Format("Ignoring report definition {0}; query (or procedure name) not defined in the data element", reportName));
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
                        case "sp":
                        case "sproc":
                            if (string.IsNullOrWhiteSpace(sourceServer))
                            {
                                ShowWarning(string.Format("Ignoring report definition {0}; server not defined in the data element", reportName));
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(sourceDB))
                            {
                                ShowWarning(string.Format("Ignoring report definition {0}; database not defined in the data element", reportName));
                                continue;
                            }

                            if (sourceTypeLcase == "procedure" || sourceTypeLcase == "sp" || sourceTypeLcase == "sproc")
                                dataSource = new DataSourceSqlStoredProcedure(reportName, sourceServer, sourceDB, query);
                            else
                                dataSource = new DataSourceSqlQuery(reportName, sourceServer, sourceDB, query);

                            break;

                        case "wmi":
                            if (string.IsNullOrWhiteSpace(sourceHost))
                            {
                                ShowWarning(string.Format("Ignoring report definition {0}; server or host not defined in the data element", reportName));
                                continue;
                            }

                            dataSource = new DataSourceWMI(reportName, sourceServer, query);
                            break;

                        default:
                            ShowWarning(string.Format("Ignoring report definition {0}; invalid type {1} in the data element; should be {2}",
                                                      reportName, sourceType, "query, procedure, or wmi"));
                            continue;
                    }

                    var mailRecipients = GetElementAttribValue(mailInfo, "to", string.Empty);

                    var sepChars = new[] { ',', ';' };
                    var emailList = mailRecipients.Split(sepChars);
                    if (emailList.Length == 0)
                    {
                        ShowWarning(string.Format("Ignoring report definition {0}; invalid to list {1} in the mail element; should be a comma separated list of e-mail addresses",
                                                  reportName, sourceType));
                        continue;
                    }

                    var mailSubject = GetElementAttribValue(mailInfo, "subject", string.Empty);
                    var reportTitle = GetElementAttribValue(mailInfo, "title", string.Empty);

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
                            if (!Enum.TryParse<DayOfWeek>(day, true, out var dayOfWeek) ||
                                daysOfWeek.Contains(dayOfWeek))
                                continue;

                            daysOfWeek.Add(dayOfWeek);
                        }
                    }

                    var delayTypeText = GetElementAttribValue(frequencyInfo, "type", string.Empty);

                    var emailSettings = new EmailMessageSettings(emailList, mailSubject, reportTitle);

                    NotificationTask task;

                    if (delayTypeText.ToLower().Contains("time"))
                    {
                        // Run a report at a given time, every day, or certain days of the week

                        var timeOfDayText = GetElementAttribValue(frequencyInfo, "timeOfDay", string.Empty);

                        LocalTime timeOfDay;
                        if (string.IsNullOrWhiteSpace(timeOfDayText))
                        {
                            ShowWarning(string.Format("timeOfDay attribute not found for the frequency element for report {0}; will assume 7:00 am", reportName));
                            timeOfDay = new LocalTime(7, 0);
                        }
                        else if (!TryParseTimeOfDay(timeOfDayText, out timeOfDay))
                        {
                            ShowWarning(string.Format("Ignoring report definition {0}; invalid timeOfDay {1}; should be a time like '7:00 am' or '13:00'",
                                                      reportName, timeOfDayText));
                            continue;
                        }

                        task = new NotificationTask(reportName, dataSource, emailSettings, lastRun, timeOfDay, daysOfWeek);

                    }
                    else if (delayTypeText.ToLower().Contains("interval"))
                    {
                        // Run a report on a given interval

                        var delayInterval = GetElementAttribValue(frequencyInfo, "interval", string.Empty);
                        var delayIntervalUnits = GetElementAttribValue(frequencyInfo, "units", string.Empty);

                        if (!int.TryParse(delayInterval, out var interval))
                        {
                            ShowWarning(string.Format("Ignoring report definition {0}; invalid interval {1}; should be an integer",
                                                      reportName, delayInterval));
                            continue;
                        }

                        var lcaseUnits = delayIntervalUnits.ToLower();
                        NotificationTask.FrequencyInterval intervalUnits;

                        if (lcaseUnits.Contains("minute"))
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
                            ShowWarning(string.Format("Ignoring report definition {0}; invalid interval units {1}; should be {2}",
                                                      reportName, delayInterval, "minutes, hours, days, weeks, months, or years"));
                            continue;
                        }

                        task = new NotificationTask(reportName, dataSource, emailSettings, lastRun, interval, intervalUnits);
                    }
                    else
                    {
                        ShowWarning(string.Format("Invalid frequency type {0} for report {1}; should be TimeOfDay or Interval", delayTypeText, reportName));
                        continue;
                    }

                    if (mTasks.ContainsKey(reportName))
                        mTasks[reportName] = task;
                    else
                        mTasks.Add(reportName, task);

                    RegisterEvents(task);
                    task.TaskResultsAvailable += Task_TaskResultsAvailable;

                }


                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error reading the report definitions file", ex);
                return false;
            }


        }

        private void RunElapsedTasks()
        {
            foreach (var task in mTasks)
            {
                try
                {
                    task.Value.RunTaskNowIfRequired();
                }
                catch (Exception ex)
                {
                    HandleException("Error running task " + task.Key, ex);
                }


            }
        }

        private void SaveTaskStatusFile()
        {
            // ToDo: Save a file listing TaskID, LastRun, and possibly SourceType and SourceQuery

            throw new NotImplementedException();
        }
        }

        public bool Start()
        {
            var startTime = DateTime.UtcNow;

            var lastTaskCheckTime = DateTime.UtcNow;
            var lastStatusWriteTime = DateTime.UtcNow;

            try
            {
                DateTime stopTime;
                if (Options.MaxRuntimeHours > 0)
                    stopTime = startTime.AddHours(Options.MaxRuntimeHours);
                else
                {
                    stopTime = DateTime.MaxValue;
                }

                mReportDefsFileChanged = false;
                var success = ReadReportDefsFile(true);
                if (!success)
                    return false;

                Options.OutputSetOptions();

                LoadTaskStatusFile();

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

                    if (DateTime.UtcNow.Subtract(lastStatusWriteTime).TotalMinutes >= TASK_STATUS_FILE_UPDATE_INTERVAL_MINUTES)
                    {
                        lastStatusWriteTime = DateTime.UtcNow;
                        SaveTaskStatusFile();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error in main loop", ex);
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

            timeOfDay = new LocalTime(parsedTime.Hour, parsedTime.Minute);
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

            if (string.IsNullOrWhiteSpace(currentValue) || newValue == currentValue)
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
