
using System;
using System.Data;

namespace DMS_Email_Manager
{
    internal class DataSourceSqlQuery : DataSourceSql
    {
        /// <summary>
        /// Query to run against the database
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// The data source for this class is a Query
        /// </summary>
        public override string SourceDefinition => Query;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reportName">Report name (used for logging)</param>
        /// <param name="serverName">Server name</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="query">SQL query to run</param>
        /// <param name="simulate">When true, simulate contacting the database</param>
        public DataSourceSqlQuery(
            string reportName,
            string serverName,
            string databaseName,
            string query,
            bool simulate)
        {
            ReportName = reportName;
            ServerName = serverName;
            DatabaseName = databaseName;
            Query = query;
            Simulate = simulate;
            SourceType = DataSourceType.Query;
        }

        /// <summary>
        /// Run a query against a SQL Server database to retrieve the result set
        /// </summary>
        public override TaskResults GetData()
        {
            try
            {
                var results = GetSqlData(CommandType.Text, Query);
                return results;
            }
            catch (Exception ex)
            {
                var errMsg = string.Format("Error retrieving results from database {0} using a query for report {1}",
                                           DatabaseName, ReportName);
                OnErrorEvent(errMsg, ex);

                var results = FormatExceptionAsResults(ReportName, errMsg, ex);
                return results;
            }
        }
    }
}
