using System;

namespace DMS_Email_Manager
{
    internal class TaskRuntimeInfo
    {
        /// <summary>
        /// Number of times the task has run
        /// </summary>
        public int ExecutionCount { get; set; }

        /// <summary>
        /// Last runtime (UTC-based)
        /// </summary>
        public DateTime LastRun { get; set; }

        /// <summary>
        /// Next runtime (UTC-based)
        /// </summary>
        public DateTime NextRun { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lastRun"></param>
        /// <param name="executionCount"></param>
        public TaskRuntimeInfo(DateTime lastRun, int executionCount = 0)
        {
            LastRun = lastRun;
            NextRun = DateTime.MinValue;
            ExecutionCount = executionCount;
        }
    }
}