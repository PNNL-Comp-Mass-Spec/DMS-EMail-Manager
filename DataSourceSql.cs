using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DMS_Email_Manager
{
    internal abstract class DataSourceSql : DataSourceBase
    {
        public const int CONNECTION_TIMEOUT_SECONDS = 120;
        public const int QUERY_TIMEOUT_SECONDS = 600;

        /// <summary>
        /// SQL Server name
        /// </summary>
        public string ServerName { get; internal set; }

        /// <summary>
        /// Database name
        /// </summary>
        public string DatabaseName { get; internal set; }

        protected TaskResults GetSqlData(CommandType commandType, string queryOrProcedureName)
        {
            var connStr = string.Format("Data Source={0};Initial Catalog={1};Integrated Security=SSPI;Connection Timeout={2};",
                                        ServerName, DatabaseName, CONNECTION_TIMEOUT_SECONDS);

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

            using (var dbConn = new SqlConnection(connStr))
            {
                using (var sqlCmd = new SqlCommand(queryOrProcedureName, dbConn))
                {
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
                }
            }

            return results;
        }

    }
}
