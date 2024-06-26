﻿using System;
using System.Collections.Generic;

namespace DMS_Email_Manager
{
    internal class DataSourceWMI : DataSourceBase
    {
        // Ignore Spelling: DMS, wmi

        /// <summary>
        /// WMI host to contact
        /// </summary>
        public string HostName { get; internal set; }

        /// <summary>
        /// Query to run against WMI
        /// </summary>
        public string Query { get; internal set; }

        /// <summary>
        /// The data source for this class is a WMI Query
        /// </summary>
        public override string SourceDefinition => Query;

        /// <summary>
        /// Optional value to divide WMI report values by
        /// </summary>
        /// <remarks>
        /// For example, set ValueDivisor to 1073741824 and set DivisorUnits to "GB"
        /// to report disk free space and disk usage in gigabytes
        /// </remarks>
        public double ValueDivisor { get; set; }

        /// <summary>
        /// Number of digits after the decimal point to round values
        /// </summary>
        /// <remarks>Only used if ValueDivisor is non-zero</remarks>
        public byte DivisorRoundDigits { get; set; }

        /// <summary>
        /// Units for the metric (units can be any user-defined string)
        /// </summary>
        /// <remarks>Only used if ValueDivisor is non-zero</remarks>
        public string DivisorUnits { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reportName">Report name (used for logging)</param>
        /// <param name="wmiHostName"></param>
        /// <param name="query"></param>
        /// <param name="simulate">When true, simulate contacting the database</param>
        public DataSourceWMI(
            string reportName,
            string wmiHostName,
            string query,
            bool simulate)
        {
            ReportName = reportName;
            HostName = wmiHostName;
            Query = query;
            Simulate = simulate;
            SourceType = DataSourceType.WMI;
        }

        /// <summary>
        /// Contact WMI to retrieve the data
        /// </summary>
        public override TaskResults GetData()
        {
            try
            {
                var results = new TaskResults(ReportName);

                // ReSharper disable once StringLiteralTypo
                var wmiPath = @"\\" + HostName + @"\root\cimv2";

                if (Simulate)
                {
                    results.DefineColumns(new List<string> { "WMIPath" });
                    results.AddDataRow(new List<string> { wmiPath });
                    return results;
                }

                var scope = new System.Management.ManagementScope(wmiPath);
                var query = new System.Management.ObjectQuery(Query);
                var searcher = new System.Management.ManagementObjectSearcher(scope, query);
                var wmiResults = searcher.Get();

                var resultSets = 0;

                foreach (var wmiValue in wmiResults)
                {
                    resultSets++;

                    var columnNames = new List<string>();

                    foreach (var prop in wmiValue.Properties)
                    {
                        columnNames.Add(prop.Name);
                    }

                    if (resultSets == 1)
                    {
                        results.DefineColumns(columnNames);
                    }
                    else
                    {
                        results.ParseColumnsAddnlResultSet(columnNames);
                    }

                    var dataValues = new List<string>();

                    foreach (var prop in wmiValue.Properties)
                    {
                        try
                        {
                            if (prop.Value == null)
                                continue;

                            var valueText = prop.Value.ToString();

                            if (Math.Abs(ValueDivisor) > float.Epsilon && double.TryParse(valueText, out var value))
                            {
                                // The value is a number; round the value and append units
                                var formattedValue = PRISM.StringUtilities.DblToString(value / ValueDivisor, DivisorRoundDigits);

                                if (string.IsNullOrWhiteSpace(DivisorUnits))
                                    dataValues.Add(formattedValue);
                                else
                                    dataValues.Add(formattedValue + " " + DivisorUnits);
                            }
                            else
                            {
                                dataValues.Add(valueText);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Unable to translate data into string; ignore errors here
                            OnErrorEvent(string.Format("Error retrieving results from WMI on host {0} for report {1}", HostName, ReportName), ex);
                        }
                    }

                    results.AddDataRow(dataValues);
                }

                return results;
            }
            catch (Exception ex)
            {
                var errMsg = string.Format("Error retrieving results from WMI on host {0} for report {1}",
                                           HostName, ReportName);
                OnErrorEvent(errMsg, ex);

                var results = FormatExceptionAsResults(ReportName, errMsg, ex);
                return results;
            }
        }
    }
}
