using System.Collections.Generic;

namespace DMS_Email_Manager
{
    internal class TaskResults
    {
        /// <summary>
        /// Column names
        /// </summary>
        public List<string> ColumnNames { get; }

        /// <summary>
        /// Data rows
        /// </summary>
        public List<List<string>> DataRows { get; }

        /// <summary>
        /// Report title
        /// </summary>
        public string ReportTitle { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reportTitle"></param>
        public TaskResults(string reportTitle)
        {
            ReportTitle = reportTitle;
            ColumnNames = new List<string>();
            DataRows = new List<List<string>>();

        }

        /// <summary>
        /// Append a data row
        /// </summary>
        /// <param name="dataRow"></param>
        public void AddDataRow(List<string> dataRow)
        {
            DataRows.Add(dataRow);

            if (dataRow.Count <= ColumnNames.Count)
                return;

            var startColIndex = ColumnNames.Count;
            for (var colIndex = startColIndex; colIndex < dataRow.Count; colIndex++)
            {
                ColumnNames.Add(string.Format("Column{0}", colIndex + 1));
            }
        }

        /// <summary>
        /// Update the list of column names
        /// Call this method for the first recordset to be included in a report
        /// </summary>
        /// <param name="columns"></param>
        public void DefineColumns(List<string> columns)
        {
            ColumnNames.Clear();
            ColumnNames.AddRange(columns);
        }

        /// <summary>
        /// Call this method when a report includes results from multiple recordsets
        /// Send the columns for the new recordset (all of the columns)
        /// New columns will be appended to ColumnNames if and only if ColumnNames has fewer columns than columns)
        /// </summary>
        /// <param name="columns"></param>
        public void ParseColumnsAddnlResultSet(List<string> columns)
        {
            var startColIndex = ColumnNames.Count;
            for (var colIndex = startColIndex; colIndex < columns.Count; colIndex++)
            {
                ColumnNames.Add(columns[colIndex]);
            }
        }

    }
}
