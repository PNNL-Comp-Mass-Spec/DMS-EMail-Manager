﻿
using System;
using System.Collections.Generic;
using System.Data;

namespace DMS_Email_Manager
{
    internal class DataSourceSqlStoredProcedure : DataSourceSql
    {

        /// <summary>
        /// Stored procedure name
        /// </summary>
        public string StoredProcedureName { get; set; }

        /// <summary>
        /// First parameter name for the stored procedure
        /// </summary>
        /// <remarks>Used by SendResultIDsToPostMailHook</remarks>
        public string StoredProcParameter { get; set; }

        /// <summary>
        /// Field size of the StoredProcParameter (defaults to 2000)
        /// </summary>
        /// <remarks>Used by SendResultIDsToPostMailHook</remarks>
        public int StoredProcParamLength { get; set; }

        /// <summary>
        /// Field type of the StoredProcParameter (defaults to SqlDbType.VarChar)
        /// </summary>
        /// <remarks>Used by SendResultIDsToPostMailHook</remarks>
        public SqlDbType StoredProcParamType { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reportName">Report name (used for logging)</param>
        /// <param name="serverName"></param>
        /// <param name="databaseName"></param>
        /// <param name="storedProcedureName"></param>
        public DataSourceSqlStoredProcedure(string reportName, string serverName, string databaseName, string storedProcedureName)
        {
            ReportName = reportName;
            ServerName = serverName;
            DatabaseName = databaseName;
            StoredProcedureName = storedProcedureName;
            SourceType = DataSourceType.StoredProcedure;

            StoredProcParameter = string.Empty;
            StoredProcParamLength = 2000;
            StoredProcParamType = SqlDbType.VarChar;
        }

        /// <summary>
        /// Call a SQL Server stored procedure to retrieve the resultset (or resultsets)
        /// </summary>
        /// <returns></returns>
        public override TaskResults GetData()
        {
            try
            {
                var results = GetSqlData(CommandType.StoredProcedure, StoredProcedureName);
                return results;
            }
            catch (Exception ex)
            {
                var errMsg = string.Format("Error retrieving results from stored procedure {0} in database {1} for report {2}",
                                           StoredProcedureName, DatabaseName, ReportName);
                OnErrorEvent(errMsg, ex);

                var results = new TaskResults(ReportName);
                results.DefineColumns(new List<string> {"Error"});
                results.AddDataRow(new List<string> { errMsg });
                return results;
            }
        }
    }
}