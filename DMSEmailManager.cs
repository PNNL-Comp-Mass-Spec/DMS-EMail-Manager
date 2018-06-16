using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRISM;
using static DMS_Email_Manager.NotificationTask;

namespace DMS_Email_Manager
{
    public class DMSEmailManager : clsEventNotifier
    {
        #region "Constans"

        private const int TASK_CHECK_INTERVAL_SECONDS = 15;
        private const int TASK_STATUS_FILE_UPDATE_INTERVAL_MINUTES = 1;

        #endregion

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

            if (string.IsNullOrWhiteSpace(Options.TaskDefinitionsFilePath))
            {
                throw new ArgumentException("TaskDefinitionsFile not defined", nameof(Options.TaskDefinitionsFilePath));
            }

            var taskDefinitionsFile = new FileInfo(Options.TaskDefinitionsFilePath);
            if (!taskDefinitionsFile.Exists)
            {
                throw new FileNotFoundException("TaskDefinitionsFile not found: " + Options.TaskDefinitionsFilePath);
            }

            mReportDefsFileChanged = false;

            mReportDefsWatcher = new FileSystemWatcher(Options.TaskDefinitionsFilePath);
            mReportDefsWatcher.Changed += mReportDefsWatcher_Changed;
        }

        private void ReadReportDefsFile()
        {
            var taskID = "Test";

            var reportTitle = "Test query";
            var serverName = "gigasax";
            var databaseName = "dms5";
            var query = "SELECT * FROM T_Log_Entries WHERE type = 'Error'";

            var dataSource = new DataSourceSqlQuery(reportTitle, serverName, databaseName, query);
            var lastRun = DateTime.MinValue;
            var emailList = new SortedSet<string> {"proteomics@pnnl.gov"};

            var delayInterval = 60;
            var delayIntervalUnits = FrequencyInterval.Hourly;

            var task = new NotificationTask(taskID, dataSource, lastRun, emailList, delayInterval, delayIntervalUnits);

            if (mTasks.ContainsKey(taskID))
                mTasks[taskID]= task;
            else
                mTasks.Add(taskID, task);


        }

        private void RunElapsedTasks()
        {
        }

        private void SaveTaskStatusFile()
        {
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
                ReadReportDefsFile();

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
                OnErrorEvent("Error in main loop", ex);
                return false;
            }

        }


        private void mReportDefsWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            mReportDefsFileChanged = true;
        }
    }
}
