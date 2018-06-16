
using System;
using System.Data;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace DMS_Email_Manager
{
    internal class DataSourceSqlQuery : DataSourceSql
    {

        /// <summary>
        /// Query to run against the databsae
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="title"></param>
        /// <param name="serverName"></param>
        /// <param name="databaseName"></param>
        /// <param name="query"></param>
        public DataSourceSqlQuery(string title, string serverName, string databaseName, string query)
        {
            ReportTitle = title;
            ServerName = serverName;
            DatabaseName = databaseName;
            Query = query;
            SourceType = DataSourceType.Query;
        }

        /// <summary>
        /// Run a query against a SQL Server database to retrieve the resultset
        /// </summary>
        /// <returns></returns>
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
                                           DatabaseName, ReportTitle);
                OnErrorEvent(errMsg, ex);

                var results = new TaskResults(ReportTitle);
                results.DefineColumns(new List<string> { "Error" });
                results.AddDataRow(new List<string> { errMsg });
                return results;
            }
        }
    }
}
