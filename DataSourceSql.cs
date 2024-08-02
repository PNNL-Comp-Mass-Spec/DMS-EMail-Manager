using System.Collections.Generic;
using System.Data;
using PRISMDatabaseUtils;

namespace DMS_Email_Manager
{
    internal abstract class DataSourceSql : DataSourceBase
    {
        // Ignore Spelling: DMS, Sql

        /// <summary>
        /// Query timeout, in seconds
        /// </summary>
        public const int QUERY_TIMEOUT_SECONDS = 600;

        /// <summary>
        /// Server name
        /// </summary>
        public string ServerName { get; internal set; }

        /// <summary>
        /// Server type
        /// </summary>
        public DbServerTypes ServerType { get; internal set; }

        /// <summary>
        /// Database name
        /// </summary>
        public string DatabaseName { get; internal set; }

        /// <summary>
        /// Database user
        /// </summary>
        public string DatabaseUser { get; internal set; }

        protected TaskResults GetSqlData(CommandType commandType, string queryOrProcedureName)
        {
            var connectionString = DbToolsFactory.GetConnectionString(ServerType, ServerName, DatabaseName, DatabaseUser, string.Empty);

            var results = new TaskResults(ReportName);

            if (Simulate)
            {
                switch (commandType)
                {
                    case CommandType.Text:
                        results.DefineColumns(new List<string> { "SQL_Query" });
                        break;

                    case CommandType.StoredProcedure:
                        results.DefineColumns(new List<string> { "Stored Procedure" });
                        break;

                    default:
                        results.DefineColumns(new List<string> { commandType.ToString() });
                        break;
                }

                results.AddDataRow(new List<string> { queryOrProcedureName.Trim().Replace("\t", " ") });
                return results;
            }

            var applicationName = string.Format("DMSEmailManager_{0}", ServerName);

            var connectionStringToUse = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, applicationName);

            var dbTools = DbToolsFactory.GetDBTools(connectionStringToUse, QUERY_TIMEOUT_SECONDS, debugMode: false);
            RegisterEvents(dbTools);

            if (commandType != CommandType.StoredProcedure)
            {
                OnDebugEvent("Running query on server {0}, database {1}:\n{2}", ServerName, DatabaseName, queryOrProcedureName);

                var success = dbTools.GetQueryResultsDataTable(queryOrProcedureName, out var resultSet);

                if (success)
                {
                    StoreResults(results, resultSet);
                }
                else
                {
                    OnWarningEvent("GetQueryResultsDataTable returned false for query: {0}", queryOrProcedureName);
                }

                return results;
            }

            OnDebugEvent("Calling procedure on server {0}, database {1}: {2}", ServerName, DatabaseName, queryOrProcedureName);

            var cmd = dbTools.CreateCommand(queryOrProcedureName, CommandType.StoredProcedure);

            // Optionally add procedure arguments

            // dbTools.AddParameter(cmd, "@argumentName", SqlType.VarChar, 128, "ArgumentValue");
            // var jobNumberParam = dbTools.AddParameter(cmd, "@jobNumber", SqlType.Int, 0, ParameterDirection.InputOutput);
            // var messageParam = dbTools.AddParameter(cmd, "@message", SqlType.VarChar, 512, string.Empty, ParameterDirection.InputOutput);
            // var returnCodeParam = dbTools.AddParameter(cmd, "@returnCode", SqlType.VarChar, 64, string.Empty, ParameterDirection.InputOutput);

            // Execute the SP
            var resCode = dbTools.ExecuteSPDataTable(cmd, out var resultSetSP);

            // var returnCode = DBToolsBase.GetReturnCode(returnCodeParam);

            // if (resCode == 0 && returnCode == 0)
            if (resCode == 0)
            {
                StoreResults(results, resultSetSP);
            }
            else
            {
                OnWarningEvent("Procedure {0} returned a non-zero result code: {1}", queryOrProcedureName, resCode);
            }
            // else if (returnCode != 0)
            // {
            //     OnWarningEvent("Procedure {0} returned a non-zero return code: {1}", queryOrProcedureName, returnCode);
            // }

            return results;

            /*
            using var dbConn = new SqlConnection(connectionStringToUse);
            using var sqlCmd = new SqlCommand(queryOrProcedureName, dbConn);

            sqlCmd.CommandType = commandType;
            sqlCmd.CommandTimeout = QUERY_TIMEOUT_SECONDS;

            dbConn.Open();
            var reader = sqlCmd.ExecuteReader();

            var resultSetAvailable = true;
            var resultSets = 0;
            while (resultSetAvailable)
            {
                resultSets++;

                var columnNames = new List<string>();

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    columnNames.Add(reader.GetName(i));
                }

                if (resultSets == 1)
                {
                    results.DefineColumns(columnNames);
                }
                else
                {
                    results.ParseColumnsAddnlResultSet(columnNames);
                }

                while (reader.Read())
                {
                    var dataValues = new List<string>();

                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.IsDBNull(i))
                            dataValues.Add(string.Empty);
                        else
                            dataValues.Add(reader.GetValue(i).ToString());
                    }

                    results.AddDataRow(dataValues);
                }

                resultSetAvailable = reader.NextResult();
            }

            return results;

            */
        }

        private void StoreResults(TaskResults results, DataTable dataTable)
        {
            var columnNames = new List<string>();

            foreach (DataColumn column in dataTable.Columns)
            {
                columnNames.Add(column.ColumnName);
            }

            results.DefineColumns(columnNames);

            var rowValues = new List<string>();
            var columnCount = columnNames.Count;

            foreach (DataRow resultRow in dataTable.Rows)
            {
                rowValues.Clear();

                for (var i = 0; i < columnCount; i++)
                {
                    rowValues.Add(resultRow[i].CastDBVal<string>());
                }

                results.AddDataRow(rowValues);
            }
        }
    }
}
