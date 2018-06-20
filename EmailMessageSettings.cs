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
        /// Constructor
        /// </summary>
        /// <param name="emailList"></param>
        /// <param name="mailSubject"></param>
        /// <param name="reportTitle"></param>
        public EmailMessageSettings(ICollection<string> emailList, string mailSubject, string reportTitle)
        {
            Recipients = new SortedSet<string>();
            foreach (var mailAddress in emailList.Distinct())
            {
                Recipients.Add(mailAddress);
            }

            Subject = mailSubject;

            ReportTitle = reportTitle;
        }

        /// <summary>
        /// Concatenate the e-mail addresses in Recipients using the specified separator
        /// </summary>
        /// <param name="separator"></param>
        /// <returns></returns>
        public string GetRecipients(string separator = ", ")
        {
            return string.Join(separator, Recipients);
        }
    }
}
