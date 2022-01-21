using System;
using PRISM;

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
        /// Data source definition (query or stored procedure name)
        /// </summary>
        public string SourceDefinition { get; set; }

        /// <summary>
        /// Data source type
        /// </summary>
        public DataSourceBase.DataSourceType SourceType { get; internal set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lastRun">Last runtime (UTC-based)</param>
        /// <param name="executionCount">Number of times the task has run</param>
        public TaskRuntimeInfo(DateTime lastRun, int executionCount = 0)
        {
            LastRun = lastRun;
            NextRun = DateTime.MinValue;
            ExecutionCount = executionCount;

            SourceType = DataSourceBase.DataSourceType.Query;
            SourceDefinition = string.Empty;
        }

        /// <summary>
        /// Update the data source type and definition
        /// </summary>
        /// <param name="dataSource"></param>
        public void UpdateDataSource(DataSourceBase dataSource)
        {
            try
            {
                if (SourceType != dataSource.SourceType)
                {
                    SourceType = dataSource.SourceType;
                }

                if (string.IsNullOrWhiteSpace(SourceDefinition) || !SourceDefinition.Equals(dataSource.SourceDefinition))
                {
                    SourceDefinition = dataSource.SourceDefinition;
                }
            }
            catch (Exception ex)
            {
                // Show a warning, but ignore this error
                ConsoleMsgUtils.ShowWarning("Exception in TaskRuntimeInfo.UpdateDataSource: " + ex.Message);
            }
        }
    }
}