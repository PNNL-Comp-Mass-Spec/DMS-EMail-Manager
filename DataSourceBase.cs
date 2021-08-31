using System;
using System.Collections.Generic;
using PRISM;

namespace DMS_Email_Manager
{
    /// <summary>
    /// Base class for retrieving data from SQL Server or WMI
    /// </summary>
    internal abstract class DataSourceBase : EventNotifier
    {
        public enum DataSourceType
        {
            Query = 0,
            StoredProcedure = 1,
            WMI = 2
        }

        /// <summary>
        /// Data source definition (query or stored procedure name)
        /// </summary>
        public abstract string SourceDefinition { get; }

        /// <summary>
        /// Data source type
        /// </summary>
        public DataSourceType SourceType { get; internal set; }

        /// <summary>
        /// Report name (used for logging)
        /// </summary>
        public string ReportName { get; internal set; }

        public bool Simulate { get; internal set; }

        /// <summary>
        /// Retrieve data from this data source
        /// </summary>
        public abstract TaskResults GetData();

        protected TaskResults FormatExceptionAsResults(string reportName, string errMsg, Exception ex)
        {
            var results = new TaskResults(reportName);
            results.DefineColumns(new List<string> { "Error" });

            results.AddDataRow(new List<string> { errMsg });
            results.AddDataRow(new List<string> { ex.Message });
            results.AddDataRow(new List<string> { StackTraceFormatter.GetExceptionStackTrace(ex) });

            return results;
        }
    }
}
