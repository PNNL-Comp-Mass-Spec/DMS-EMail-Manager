using System;
using System.Collections.Generic;
using NodaTime;
using PRISM;

namespace DMS_Email_Manager
{
    /// <summary>
    /// Container for a notification task
    /// Keeps track of the data to retrieve, the frequency to retrieve it, and the recipients to send the results to
    /// </summary>
    /// <remarks>
    /// This class does not send e-mails.
    /// Instead, it raises event TaskResultsAvailable with the query results and e-mail recipients,
    /// allowing the calling class to send the e-mail
    /// </remarks>
    internal class NotificationTask : clsEventNotifier
    {
        #region "Enums"

        /// <summary>
        /// Options for defining how often to retrieve data
        /// </summary>
        public enum FrequencyDelay
        {
            /// <summary>
            /// Retrieve data periodically, using a fixed interval
            /// </summary>
            IntervalBased,

            /// <summary>
            /// Retrieve data at the given time of day, either every day, or on the days specified by DaysOfWeek
            /// </summary>
            AtTimeOfDay
        }

        /// <summary>
        /// Frequencies for retrieving data periodically, using a fixed interval
        /// </summary>
        public enum FrequencyInterval
        {
            Undefined = 0,
            Minutely = 1,
            Hourly = 2,
            Daily = 3,
            Weekly = 4,
            Monthly = 5,
            Yearly = 6
        }

        #endregion

        #region "Events"

        public event ResultsAvailableEventEventHandler TaskResultsAvailable;

        public delegate void ResultsAvailableEventEventHandler(TaskResults results, SortedSet<string> emailList);

        #endregion

        #region "Properties"

        /// <summary>
        /// Data Source
        /// </summary>
        public DataSourceBase DataSource { get; private set; }

        /// <summary>
        /// Days of the week to retrieve data when the DelayType is AtTimeOfDay
        /// </summary>
        /// <remarks>Will run daily if this list is empty</remarks>
        public SortedSet<DayOfWeek> DaysOfWeek { get; } = new SortedSet<DayOfWeek>();

        /// <summary>
        /// Interval to use when the DelayType is IntervalBased
        /// </summary>
        public int DelayInterval { get; private set; }

        /// <summary>
        /// How often to retrieve data
        /// </summary>
        public FrequencyInterval DelayIntervalUnits { get; private set; }

        /// <summary>
        /// Delay period when DelayType is IntervalBased
        /// </summary>
        /// <remarks>Will be Period.Zero if DelayType is AtTimeOfDay or if DelayIntervalUnits is FrequencyInterval.Undefined</remarks>
        public Period DelayPeriod { get; private set; }

        /// <summary>
        /// Return DelayPeriod as a TimeSpan
        /// </summary>
        public TimeSpan DelayPeriodAsTimeSpan => DelayPeriod.ToDuration().ToTimeSpan();

        /// <summary>
        /// Defines when to retrieve data,either at a certain time of day (daily, weekly, monthly, or yearly)
        /// or periodically, using a fixed interval (e.g. every 15 minutes or every 4 hours or every 2 days)
        /// </summary>
        public FrequencyDelay DelayType { get; private set; }

        /// <summary>
        /// List of e-mail addresses to send the query results to
        /// </summary>
        /// <remarks>If empty, will display the results via a MessageEvent</remarks>
        public SortedSet<string> EmailList { get; } = new SortedSet<string>();

        /// <summary>
        /// Last runtime (UTC Date)
        /// </summary>
        public DateTime LastRun { get; set; }

        /// <summary>
        /// Next runtime (UTC Date)
        /// </summary>
        public DateTime NextRun { get; private set; }

        /// <summary>
        /// Unique of this notification task
        /// </summary>
        /// <remarks>Used in log messages</remarks>
        public string TaskID { get;  }

        /// <summary>
        /// Time of day to retrieve data when the FrequencyDelay is AtTimeOfDay
        /// </summary>
        /// <remarks>Will either run daily or on the days specified DaysOfWeek</remarks>
        public LocalTime TimeOfDay { get; set; }

        #endregion

        /// <summary>
        /// Constructor for running a task periodically, using a fixed interval
        /// </summary>
        public NotificationTask(
            string taskID,
            DataSourceBase dataSource,
            DateTime lastRun,
            IEnumerable<string> emailList,
            int delayInterval = 1,
            FrequencyInterval delayIntervalUnits = FrequencyInterval.Hourly)
        {
            TaskID = taskID;
            DefineNotification(dataSource, lastRun, emailList);

            UpdateRecurringInterval(delayInterval, delayIntervalUnits);
        }

        /// <summary>
        /// Constructor for running a task at a specific time of day, every day
        /// </summary>
        public NotificationTask(
            string taskID,
            DataSourceBase dataSource,
            IEnumerable<string> emailList,
            DateTime lastRun,
            LocalTime timeOfDay)
        {
            TaskID = taskID;
            DefineNotification(dataSource, lastRun, emailList);

            UpdateTimeOfDayInterval(timeOfDay);
        }

        /// <summary>
        /// Constructor for running a task at a specific time of day, on certain days of the week
        /// </summary>
        public NotificationTask(
            string taskID,
            DataSourceBase dataSource,
            IEnumerable<string> emailList,
            DateTime lastRun,
            LocalTime timeOfDay,
            SortedSet<DayOfWeek> daysOfWeek)
        {
            TaskID = taskID;
            DefineNotification(dataSource, lastRun, emailList);

            UpdateTimeOfDayInterval(timeOfDay, daysOfWeek);
        }

        private void ComputeNextRunTime()
        {
            if (DelayType == FrequencyDelay.AtTimeOfDay)
            {
                DelayPeriod = Period.Zero;

                if (LastRun == DateTime.MinValue)
                {
                    LastRun = ConstructTimeForToday(TimeOfDay).AddDays(-1).ToUniversalTime();
                }

                var nextRun = ConstructTimeForToday(TimeOfDay);
                while (nextRun < DateTime.Now)
                {
                    nextRun = nextRun.AddDays(1);
                }

                // Note that we'll consider the DaysOfWeek filter just prior to actually retrieving the data
                NextRun = nextRun.ToUniversalTime();

                return;
            }

            switch (DelayIntervalUnits)
            {
                case FrequencyInterval.Minutely:
                    DelayPeriod = Period.FromMinutes(DelayInterval);
                    break;
                case FrequencyInterval.Hourly:
                    DelayPeriod = Period.FromHours(DelayInterval);
                    break;
                case FrequencyInterval.Daily:
                    DelayPeriod = Period.FromDays(DelayInterval);
                    break;
                case FrequencyInterval.Weekly:
                    DelayPeriod = Period.FromWeeks(DelayInterval);
                    break;
                case FrequencyInterval.Monthly:
                    DelayPeriod = Period.FromMonths(DelayInterval);
                    break;
                case FrequencyInterval.Yearly:
                    DelayPeriod = Period.FromYears(DelayInterval);
                    break;
                default:
                    // Includes FrequencyInterval.Undefined:
                    DelayPeriod = Period.Zero;
                    break;

            }

            if (LastRun == DateTime.MinValue)
            {
                LastRun = ConstructTimeForToday(TimeOfDay).Add(-DelayPeriodAsTimeSpan).ToUniversalTime();
            }

        }

        private DateTime ConstructTimeForToday(LocalTime timeOfDay)
        {
            var currentLocalTime = DateTime.Now;
            var dateTime = new DateTime(currentLocalTime.Year, currentLocalTime.Month, currentLocalTime.Day, timeOfDay.Hour, timeOfDay.Minute, 0);
            return dateTime;
        }


        private void DefineNotification(DataSourceBase dataSource, DateTime lastRun, IEnumerable<string> emailList)
        {
            DataSource = dataSource;
            LastRun = lastRun;

            EmailList.Clear();
            foreach (var item in emailList)
            {
                if (EmailList.Contains(item))
                    continue;

                EmailList.Add(item);
            }

        }

        /// <summary>
        /// Send the results to the caller via an event
        /// </summary>
        /// <param name="results">Report data</param>
        /// <param name="emailList">List of e-mail recipients</param>
        /// <remarks>
        /// If no listeners are subscribed to event TaskResultsAvailable,
        /// will report the results via event StatusEvent
        /// </remarks>
        private void OnResultsAvailable(TaskResults results, SortedSet<string> emailList)
        {
            if (TaskResultsAvailable == null)
            {
                var columnList = string.Join("\t", results.ColumnNames);
                OnStatusEvent(columnList);

                foreach (var dataRow in results.DataRows)
                {
                    var dataVals = string.Join("\t", dataRow);
                    OnStatusEvent(dataVals);
                }

                return;
            }

            TaskResultsAvailable?.Invoke(results, emailList);
        }

        /// <summary>
        /// Retrieve data from the data source
        /// Send the results to the email addresses in EmailList
        /// </summary>
        /// <returns>True if successful, otherwise false</returns>
        /// <remarks></remarks>
        public bool RunTask()
        {

            UpdateNextRuntime();

            var success = RunTask(false);
            return success;
        }

        /// <summary>
        /// Retrieve data from the data source
        /// Send the results to the email addresses in EmailList
        /// </summary>
        /// <returns>True if successful, otherwise false</returns>
        /// <remarks></remarks>
        public bool RunTask(bool updateNextRun)
        {
            try
            {
                var results = DataSource.GetData();

                OnResultsAvailable(results, EmailList);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error running notification task " + TaskID, ex);
                return false;
            }

        }

        /// <summary>
        /// Retrieve data from the data source if the NextRun time has been surpassed
        /// </summary>
        /// <returns>True if data is retrieved, false if it is not retrieved</returns>
        /// <remarks>
        /// Will return false either because NextRun has not been reached
        /// or because data retrieval is disabled on this day of the week)
        /// </remarks>
        public bool RunTaskNowIfRequired()
        {
            if (NextRun > DateTime.UtcNow)
                return false;

            UpdateNextRuntime();

            if (DaysOfWeek.Count == 0 || DaysOfWeek.Contains(DateTime.Now.DayOfWeek))
            {
                RunTask(false);
                return true;
            }

            // Do not run tasks on this day of the week
            return false;

        }

        private void UpdateNextRuntime()
        {
            if (DelayType == FrequencyDelay.AtTimeOfDay)
            {
                NextRun = ConstructTimeForToday(TimeOfDay).AddDays(1).ToUniversalTime();
            }
            else
            {
                // Running a task at a regular interval
                if (DelayPeriod.Seconds == 0)
                {
                    // Default to run daily
                    NextRun = DateTime.UtcNow.Add(new TimeSpan(1, 0, 0, 0));
                }
                else
                {
                    NextRun = DateTime.UtcNow.Add(DelayPeriodAsTimeSpan);
                }
            }
        }

        /// <summary>
        /// Run a task periodically, using a fixed interval (e.g. every 15 minutes or every 4 hours or every 2 days)
        /// </summary>
        /// <param name="delayInterval"></param>
        /// <param name="delayIntervalUnits"></param>
        public void UpdateRecurringInterval(int delayInterval, FrequencyInterval delayIntervalUnits)
        {
            DelayType = FrequencyDelay.IntervalBased;
            DelayInterval = delayInterval;
            DelayIntervalUnits = delayIntervalUnits;
            TimeOfDay = LocalTime.MinValue;

            ComputeNextRunTime();
        }

        /// <summary>
        /// Run at a specific time of day, every day
        /// </summary>
        /// <param name="timeOfDay"></param>
        public void UpdateTimeOfDayInterval(LocalTime timeOfDay)
        {
            UpdateTimeOfDayInterval(timeOfDay, new SortedSet<DayOfWeek>());
        }

        /// <summary>
        /// Run at a specific time of day, on the specified days, or every day of daysOfWeek is empty
        /// </summary>
        /// <param name="timeOfDay"></param>
        /// <param name="daysOfWeek"></param>
        public void UpdateTimeOfDayInterval(LocalTime timeOfDay, SortedSet<DayOfWeek> daysOfWeek)
        {
            DelayType = FrequencyDelay.AtTimeOfDay;
            DelayInterval = 0;
            DelayIntervalUnits = FrequencyInterval.Undefined;
            TimeOfDay = timeOfDay;

            DaysOfWeek.Clear();

            if (daysOfWeek.Count == 0)
            {
                foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
                {
                    DaysOfWeek.Add(day);
                }
            }
            else
            {
                foreach (var day in daysOfWeek)
                {
                    DaysOfWeek.Add(day);
                }
            }

            ComputeNextRunTime();
        }


    }

}