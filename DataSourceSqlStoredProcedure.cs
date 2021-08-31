using System;
using System.Data;
using PRISMDatabaseUtils;

namespace DMS_Email_Manager
{
    internal class DataSourceSqlStoredProcedure : DataSourceSql
    {
        // Ignore Spelling: StoredProc

        /// <summary>
        /// The data source for this class is a stored procedure name
        /// </summary>
        public override string SourceDefinition => StoredProcedureName;

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
        public SqlType StoredProcParamType { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reportName">Report name (used for logging)</param>
        /// <param name="serverName">Server name</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="storedProcedureName">Stored procedure name</param>
        /// <param name="simulate">When true, simulate contacting the database</param>
        public DataSourceSqlStoredProcedure(
            string reportName,
            string serverName,
            string databaseName,
            string storedProcedureName,
            bool simulate)
        {
            ReportName = reportName;
            ServerName = serverName;
            DatabaseName = databaseName;
            StoredProcedureName = storedProcedureName;
            Simulate = simulate;

            SourceType = DataSourceType.StoredProcedure;

            StoredProcParameter = string.Empty;
            StoredProcParamLength = 2000;
            StoredProcParamType = SqlType.VarChar;
        }

        /// <summary>
        /// Call a SQL Server stored procedure to retrieve the result set (or result sets)
        /// </summary>
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

                var results = FormatExceptionAsResults(ReportName, errMsg, ex);
                return results;
            }
        }
    }
}
