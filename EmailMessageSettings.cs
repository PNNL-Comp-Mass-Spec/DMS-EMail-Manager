using System.Collections.Generic;
using System.Linq;

namespace DMS_Email_Manager
{
    internal class EmailMessageSettings
    {
        /// <summary>
        /// List of e-mail addresses to send the query results to
        /// </summary>
        /// <remarks>If empty, will display the results via a MessageEvent</remarks>
        public SortedSet<string> Recipients { get; }

        /// <summary>
        /// Mail subject
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Report title
        /// </summary>
        public string ReportTitle { get; set; }

        /// <summary>
        /// Send an e-mail even if the report has no rows of data
        /// </summary>
        public bool MailIfEmpty { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="emailList"></param>
        /// <param name="mailSubject"></param>
        /// <param name="reportTitle"></param>
        /// <param name="mailIfEmpty"></param>
        public EmailMessageSettings(IEnumerable<string> emailList, string mailSubject, string reportTitle, bool mailIfEmpty)
        {
            Recipients = new SortedSet<string>();

            foreach (var mailAddress in emailList.Distinct())
            {
                Recipients.Add(mailAddress.Trim());
            }

            Subject = mailSubject;

            ReportTitle = reportTitle;

            MailIfEmpty = mailIfEmpty;
        }

        /// <summary>
        /// Concatenate the e-mail addresses in Recipients using the specified separator
        /// </summary>
        /// <param name="separator"></param>
        public string GetRecipients(string separator = ", ")
        {
            return string.Join(separator, Recipients);
        }
    }
}
