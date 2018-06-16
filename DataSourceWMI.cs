using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMS_Email_Manager
{
    internal class DataSourceWMI : DataSourceBase
    {

        /// <summary>
        /// WMI host to contact
        /// </summary>
        public string HostName { get; internal set; }

        /// <summary>
        /// Query to run against WMI
        /// </summary>
        public string Query { get; internal set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="title"></param>
        /// <param name="wmiHostName"></param>
        /// <param name="query"></param>
        public DataSourceWMI(string title, string wmiHostName, string query)
        {
            ReportTitle = title;
            HostName = wmiHostName;
            Query = query;
            SourceType = DataSourceType.WMI;
        }

        /// <summary>
        /// Contact WMI to retrieve the data
        /// </summary>
        /// <returns></returns>
        public override TaskResults GetData()
        {

            // ToDo: Possibly implement dividing values by a divisor and showing custom units
            // For now, set this to 0 to disable this feature
            float valueDivisor = 0;
            byte roundDigits = 1;
            var units = string.Empty;

            try
            {
                var results = new TaskResults(ReportTitle);

                var wmiPath = @"\\" + HostName + @"\root\cimv2";

                var oMs = new System.Management.ManagementScope(wmiPath);
                var oQuery = new System.Management.ObjectQuery(Query);
                var oSearcher = new System.Management.ManagementObjectSearcher(oMs, oQuery);
                var oReturnCollection = oSearcher.Get();

                var resultSets = 0;

                foreach (var mo in oReturnCollection)
                {
                    resultSets++;

                    var columnNames = new List<string>();
                    foreach (var prop in mo.Properties)
                    {
                        columnNames.Add(prop.Name);
                    }

                    if (resultSets == 1)
                        results.DefineColumns(columnNames);
                    else
                    {
                        results.ParseColumnsAddnlResultSet(columnNames);
                    }

                    var dataValues = new List<string>();
                    foreach (var prop in mo.Properties)
                    {

                        try
                        {
                            if (prop.Value == null)
                                continue;

                            var valueText = prop.Value.ToString();
                            if (Math.Abs(valueDivisor) > float.Epsilon && double.TryParse(valueText, out var value))
                            {
                                //  The value is a number; round the value and append units
                                var formattedValue = PRISM.StringUtilities.DblToString(value / valueDivisor, roundDigits);
                                if (string.IsNullOrWhiteSpace(units))
                                    dataValues.Add(formattedValue);
                                else
                                    dataValues.Add(formattedValue + " " + units);

                            }
                            else
                            {
                                dataValues.Add(valueText);
                            }


                        }
                        catch (Exception ex)
                        {
                            //  Unable to translate data into string; ignore errors here
                            OnErrorEvent(string.Format("Error retrieving results from WMI on host {0} for report {1}", HostName, ReportTitle), ex);
                        }
                    }

                    results.AddDataRow(dataValues);
                }


                return results;
            }
            catch (Exception ex)
            {
                var errMsg = string.Format("Error retrieving results from WMI on host {0} for report {1}",
                                           HostName, ReportTitle);
                OnErrorEvent(errMsg, ex);

                var results = new TaskResults(ReportTitle);
                results.DefineColumns(new List<string> { "Error" });
                results.AddDataRow(new List<string> { errMsg });
                return results;
            }
        }
    }
}
