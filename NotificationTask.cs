﻿using System;
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
    internal class NotificationTask : EventNotifier
    {
        // Ignore Spelling: DMS

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
            Second = 1,
            Minute = 2,
            Hour = 3,
            Day = 4,
            Week = 5,
            Month = 6,
            Year = 7
        }

        /// <summary>
        /// Task results available event
        /// </summary>
        public event ResultsAvailableEventEventHandler TaskResultsAvailable;

        /// <summary>
        /// Results available event handler
        /// </summary>
        /// <param name="results"></param>
        /// <param name="emailSettings"></param>
        /// <param name="postMailIdListHook"></param>
        public delegate void ResultsAvailableEventEventHandler(
            TaskResults results,
            EmailMessageSettings emailSettings,
            DataSourceSqlStoredProcedure postMailIdListHook);

        /// <summary>
        /// Data Source
        /// </summary>
        public DataSourceBase DataSource { get; private set; }

        /// <summary>
        /// Days of the week to retrieve data when the DelayType is AtTimeOfDay
        /// </summary>
        /// <remarks>Will run daily if this list is empty</remarks>
        public SortedSet<DayOfWeek> DaysOfWeek { get; } = new();

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
        /// Email settings
        /// </summary>
        public EmailMessageSettings EmailSettings { get; private set; }

        /// <summary>
        /// Number of times this task has been executed (for all time)
        /// </summary>
        public int ExecutionCount { get; set; }

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
        public string TaskID { get; }

        /// <summary>
        /// Time of day to retrieve data when the FrequencyDelay is AtTimeOfDay
        /// </summary>
        /// <remarks>Will either run daily or on the days specified DaysOfWeek</remarks>
        public LocalTime TimeOfDay { get; set; }

        /// <summary>
        /// Optional stored procedure to call after a task runs
        /// If the report contains data rows, will send the list of ID values to the given stored procedure
        /// </summary>
        /// <remarks>The first column of data is assumed to have the row ID</remarks>
        public DataSourceSqlStoredProcedure PostMailIdListHook { get; set; }

        /// <summary>
        /// Constructor for running a task periodically, using a fixed interval
        /// </summary>
        /// <remarks>Use property DaysOfWeek to only run this task on certain days</remarks>
        public NotificationTask(
            string taskID,
            DataSourceBase dataSource,
            EmailMessageSettings emailSettings,
            DateTime lastRun,
            int delayInterval = 1,
            FrequencyInterval delayIntervalUnits = FrequencyInterval.Day)
        {
            TaskID = taskID;
            DefineNotification(dataSource, lastRun, emailSettings);

            UpdateRecurringInterval(delayInterval, delayIntervalUnits);
        }

        /// <summary>
        /// Constructor for running a task at a specific time of day, every day
        /// </summary>
        public NotificationTask(
            string taskID,
            DataSourceBase dataSource,
            EmailMessageSettings emailSettings,
            DateTime lastRun,
            LocalTime timeOfDay)
        {
            TaskID = taskID;
            DefineNotification(dataSource, lastRun, emailSettings);

            UpdateTimeOfDayInterval(timeOfDay);
        }

        /// <summary>
        /// Constructor for running a task at a specific time of day, on certain days of the week
        /// </summary>
        public NotificationTask(
            string taskID,
            DataSourceBase dataSource,
            EmailMessageSettings emailSettings,
            DateTime lastRun,
            LocalTime timeOfDay,
            SortedSet<DayOfWeek> daysOfWeek)
        {
            TaskID = taskID;
            DefineNotification(dataSource, lastRun, emailSettings);

            UpdateTimeOfDayInterval(timeOfDay, daysOfWeek);
        }

        private void ComputeNextRunTime()
        {
            DateTime nextRunUtc;

            if (DelayType == FrequencyDelay.AtTimeOfDay)
            {
                DelayPeriod = Period.Zero;

                if (LastRun == DateTime.MinValue)
                {
                    LastRun = ConstructTimeForToday(TimeOfDay).ToUniversalTime();
                }
                else
                {
                    var lastRunLocalTime = LastRun.ToLocalTime();

                    if (lastRunLocalTime.Hour != TimeOfDay.Hour || lastRunLocalTime.Minute != TimeOfDay.Minute)
                    {
                        // Adjust LastRun to be the correct time of day
                        var updatedLastRun = ConstructTimeForDate(LastRun, TimeOfDay).ToUniversalTime();
                        OnStatusEvent("Updating LastRun from {0:g} to {1:g} for report {2}", LastRun.ToLocalTime(), updatedLastRun.ToLocalTime(), TaskID);
                        LastRun = updatedLastRun;
                    }
                }

                var oneDay = new TimeSpan(1, 0, 0, 0);
                nextRunUtc = LastRun.Add(oneDay);

                while (DateTime.UtcNow.Subtract(nextRunUtc).TotalHours > 24)
                {
                    nextRunUtc = nextRunUtc.Add(oneDay);
                }
            }
            else
            {
                DelayPeriod = GetPeriodForInternal(DelayInterval, DelayIntervalUnits);

                if (Equals(DelayPeriod, Period.Zero))
                {
                    OnWarningEvent(
                        "Invalid DelayInterval {0} or DelayIntervalUnits {1} for report {2}; changing to run once a day",
                        DelayInterval, DelayIntervalUnits.ToString(), TaskID);

                    DelayInterval = 1;
                    DelayIntervalUnits = FrequencyInterval.Day;
                    DelayPeriod = Period.FromDays(1);
                }

                if (LastRun == DateTime.MinValue)
                {
                    LastRun = DateTime.UtcNow.Subtract(DelayPeriodAsTimeSpan);
                }

                // Note that if this time is in the past, the task will run the next time RunTaskNowIfRequired is called
                nextRunUtc = LastRun.Add(DelayPeriodAsTimeSpan);
            }

            // We'll consider the DaysOfWeek filter just prior to actually retrieving the data
            NextRun = nextRunUtc.ToUniversalTime();
        }

        private DateTime ConstructTimeForDate(DateTime referenceDate, LocalTime timeOfDay)
        {
            var referenceDateLocal = referenceDate.ToLocalTime();
            var dateTime = new DateTime(referenceDateLocal.Year, referenceDateLocal.Month, referenceDateLocal.Day,
                                        timeOfDay.Hour, timeOfDay.Minute, timeOfDay.Second);
            return dateTime;
        }

        private DateTime ConstructTimeForToday(LocalTime timeOfDay)
        {
            return ConstructTimeForDate(DateTime.Now, timeOfDay);
        }

        private void DefineNotification(DataSourceBase dataSource, DateTime lastRun, EmailMessageSettings emailSettings)
        {
            DataSource = dataSource;
            LastRun = lastRun;
            EmailSettings = emailSettings;
        }

        /// <summary>
        /// Get a human readable description of the frequency delay mode
        /// </summary>
        public string GetFrequencyDescription()
        {
            var dayNames = new List<string>();

            foreach (var dayOfWeek in DaysOfWeek)
            {
                dayNames.Add(dayOfWeek.ToString());
            }

            if (DelayType == FrequencyDelay.AtTimeOfDay)
            {
                if (DaysOfWeek.Count == 0 || DaysOfWeek.Count >= 7)
                    return string.Format("daily at {0}", TimeOfDay.ToString());

                return string.Format("at {0} on {1}", TimeOfDay.ToString(), string.Join(", ", dayNames));
            }

            // DelayType is FrequencyDelay.IntervalBased
            string frequency;

            var plural = DelayInterval == 1 ? "" : "s";

            switch (DelayIntervalUnits)
            {
                case FrequencyInterval.Second:
                    frequency = string.Format("every {0} {1}{2}", DelayInterval, "second", plural);
                    break;
                case FrequencyInterval.Minute:
                    frequency = string.Format("every {0} {1}{2}", DelayInterval, "minute", plural);
                    break;
                case FrequencyInterval.Hour:
                    frequency = string.Format("every {0} {1}{2}", DelayInterval, "hour", plural);
                    break;
                case FrequencyInterval.Day:
                    frequency = string.Format("every {0} {1}{2}", DelayInterval, "day", plural);
                    break;
                case FrequencyInterval.Week:
                    frequency = string.Format("every {0} {1}{2}", DelayInterval, "week", plural);
                    break;
                case FrequencyInterval.Month:
                    frequency = string.Format("every {0} {1}{2}", DelayInterval, "month", plural);
                    break;
                case FrequencyInterval.Year:
                    frequency = string.Format("every {0} {1}{2}", DelayInterval, "year", plural);
                    break;
                default:
                    // Includes FrequencyInterval.Undefined:
                    return "Error: undefined interval";
            }

            if (DaysOfWeek.Count == 0 || DaysOfWeek.Count >= 7)
                return frequency;

            return string.Format("{0} on {1}", frequency, string.Join(", ", dayNames));
        }

        private Period GetPeriodForInternal(int interval, FrequencyInterval intervalUnits)
        {
            // DelayType is FrequencyDelay.IntervalBased
            return intervalUnits switch
            {
                FrequencyInterval.Second => Period.FromSeconds(interval),
                FrequencyInterval.Minute => Period.FromMinutes(interval),
                FrequencyInterval.Hour => Period.FromHours(interval),
                FrequencyInterval.Day => Period.FromDays(interval),
                FrequencyInterval.Week => Period.FromWeeks(interval),
                FrequencyInterval.Month => Period.FromMonths(interval),
                FrequencyInterval.Year => Period.FromYears(interval),
                FrequencyInterval.Undefined => Period.Zero,
                _ => throw new ArgumentOutOfRangeException(nameof(intervalUnits), intervalUnits, "Missing enum in GetPeriodForInternal")
            };
        }

        /// <summary>
        /// Get a runtime info object for this task
        /// </summary>
        public TaskRuntimeInfo GetRuntimeInfo()
        {
            return new TaskRuntimeInfo(LastRun, ExecutionCount)
            {
                NextRun = NextRun,
                SourceType = DataSource.SourceType,
                SourceDefinition = DataSource.SourceDefinition
            };
        }

        /// <summary>
        /// Send the results to the caller via an event
        /// </summary>
        /// <remarks>
        /// If no listeners are subscribed to event TaskResultsAvailable,
        /// will report the results via event StatusEvent
        /// </remarks>
        /// <param name="results">Report data</param>
        private void OnResultsAvailable(
            TaskResults results)
        {
            if (TaskResultsAvailable == null)
            {
                var columnList = string.Join("\t", results.ColumnNames);
                OnStatusEvent(columnList);

                foreach (var dataRow in results.DataRows)
                {
                    var dataValues = string.Join("\t", dataRow);
                    OnStatusEvent(dataValues);
                }

                return;
            }

            TaskResultsAvailable?.Invoke(results, EmailSettings, PostMailIdListHook);
        }

        /// <summary>
        /// Retrieve data from the data source (ignores DaysOfWeek)
        /// Send the results to the email addresses in EmailList
        /// </summary>
        /// <remarks>This method updates NextRun</remarks>
        /// <returns>True if successful, otherwise false</returns>
        public bool RunTaskNow()
        {
            var nextRuntimeMessage = UpdateNextRuntime();

            var success = RunTask();

            OnDebugEvent(nextRuntimeMessage);

            return success;
        }

        /// <summary>
        /// Retrieve data from the data source
        /// Send the results to the email addresses in EmailList
        /// </summary>
        /// <remarks>This method updates LastRun, but it does not update NextRun</remarks>
        /// <returns>True if successful, otherwise false</returns>
        private bool RunTask()
        {
            try
            {
                Console.WriteLine();
                OnStatusEvent("Retrieving data for report " + TaskID);

                var results = DataSource.GetData();
                ExecutionCount++;
                LastRun = DateTime.UtcNow;

                OnResultsAvailable(results);

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
        /// <remarks>
        /// Will return false either because NextRun has not been reached
        /// or because data retrieval is disabled on this day of the week
        /// </remarks>
        /// <returns>True if data is retrieved, false if it is not retrieved</returns>
        public bool RunTaskNowIfRequired()
        {
            if (NextRun > DateTime.UtcNow)
                return false;

            var scheduledRunTime = NextRun;

            var nextRuntimeMessage = UpdateNextRuntime();

            if (DaysOfWeek.Count == 0 || DaysOfWeek.Contains(scheduledRunTime.DayOfWeek))
            {
                RunTask();

                OnDebugEvent(nextRuntimeMessage);

                // Return true, even if RunTaskNow returned false
                return true;
            }

            OnDebugEvent(nextRuntimeMessage);

            // Do not run tasks on this day of the week
            // However, do update LastRun
            LastRun = DateTime.UtcNow;

            return false;
        }

        private string UpdateNextRuntime()
        {
            if (DelayType == FrequencyDelay.AtTimeOfDay)
            {
                NextRun = ConstructTimeForToday(TimeOfDay).AddDays(1).ToUniversalTime();
            }
            else
            {
                // Running a task at a regular interval
                if (DelayPeriod.ToDuration().TotalSeconds < 1)
                {
                    // DelayPeriod is 0
                    // Default to run daily
                    NextRun = DateTime.UtcNow.Add(new TimeSpan(1, 0, 0, 0));
                }
                else
                {
                    NextRun = DateTime.UtcNow.Add(DelayPeriodAsTimeSpan);
                }
            }

            var nextRunLocalTime = NextRun.ToLocalTime();

            return string.Format("Report {0} will next run on {1:d} at {2:h:mm:ss tt}", TaskID, nextRunLocalTime, nextRunLocalTime);
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
        /// Run at a specific time of day, on the specified days, or every day if daysOfWeek is empty
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
