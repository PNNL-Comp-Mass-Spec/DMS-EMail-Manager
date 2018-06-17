using PRISM;

namespace DMS_Email_Manager
{
    /// <summary>
    /// Base class for retrieving data from SQL Server or WMI
    /// </summary>
    internal abstract class DataSourceBase : clsEventNotifier
    {
        public enum DataSourceType
        {
            Query = 0,
            StoredProcedure = 1,
            WMI = 2
        }

        /// <summary>
        /// Data source type
        /// </summary>
        public DataSourceType SourceType { get; internal set; }

        /// <summary>
        /// Report name (used for logging)
        /// </summary>
        public string ReportName { get; internal set; }

        /// <summary>
        /// Retrieve data from this data source
        /// </summary>
        /// <returns></returns>
        public abstract TaskResults GetData();

    }
}