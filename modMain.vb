Option Strict On

' Program written in 2004 by Dave Clark and Nate Trimble
' Ported to .NET 2008 in 2010 by Matthew Monroe

Module modMain

	Private Const PROGRAM_DATE As String = "September 15, 2011"
    Private Const DEFAULT_SETTINGS_FILE_NAME As String = "DMS_EMail_Manager_Settings.xml"

	Private myLogger As PRISM.Logging.ILogger

    Private mXMLSettingsFilePath As String = String.Empty
    Private mPreviewMode As Boolean = False
    Private mShowExtendedXMLExample As Boolean

    Public Function Main() As Integer

        Dim intReturnCode As Integer
        Dim objParseCommandLine As New clsParseCommandLine
        Dim blnProceed As Boolean

        Dim command As String = Microsoft.VisualBasic.Command

        intReturnCode = 0

        mXMLSettingsFilePath = ""
        mPreviewMode = False
        mShowExtendedXMLExample = False

        Try
            blnProceed = False
            If objParseCommandLine.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
            End If

            If Not blnProceed OrElse _
                 objParseCommandLine.NeedToShowHelp OrElse _
                 objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount = 0 OrElse _
                 mXMLSettingsFilePath.Length = 0 Then
                ShowProgramHelp()
                intReturnCode = -1
            Else
                GenerateReports(mXMLSettingsFilePath)
                intReturnCode = 0
            End If

        Catch ex As Exception
            Console.WriteLine("Error occurred in modMain->Main: " & ControlChars.NewLine & ex.Message)
            intReturnCode = -1
        End Try

        ' Sleep for 750 msec
        System.Threading.Thread.Sleep(750)

    End Function

    Private Function FormatReportGeneric(ByRef columnHeaders() As String, ByRef reportRows() As String) As String

        Dim reportContents As String
        Dim i As Integer
        Dim j As Integer

        Dim lastCharPos As Integer
        Dim matchCount As Integer

        If columnHeaders(0) Is Nothing Then
            reportContents = "No Data Returned"
            Return reportContents
        End If

        ' Construct the header row
        reportContents = "<table>"
        reportContents &= "<tr class = table-header>"
        For i = 0 To columnHeaders.Length - 1
            reportContents &= "<td>" & columnHeaders(i) & "</td>"
        Next i
        reportContents &= "</tr>" & vbCrLf

        ' Append each row in reportRows to reportContents
        ' While doing this, make sure each row has columnHeaderCount sets of <td></td> pairs
        For i = 0 To reportRows.Length - 1
            If Not reportRows(i) Is Nothing Then
                lastCharPos = -1
                matchCount = 0
                Do
                    lastCharPos = reportRows(i).IndexOf("</td>", lastCharPos + 1)
                    If lastCharPos >= 0 Then matchCount += 1
                Loop While lastCharPos >= 0

                For j = matchCount + 1 To columnHeaders.Length
                    reportRows(i) &= "<td></td>"
                Next j

                reportRows(i) &= "</tr>" & vbCrLf
                reportContents &= reportRows(i)
            End If
        Next i

        reportContents &= "</table>" & vbCrLf

        Return reportContents

    End Function

    Private Function FormatSQLReport(ByVal sqlReader As System.Data.SqlClient.SqlDataReader) As String
        Dim rowStr As String
        Dim reportRowCount As Integer
        Dim reportRows() As String
        Dim columnHeaderCount As Integer
        Dim columnHeaders() As String
        Dim i As Integer

        ' Note; we assemble the report using FormatReportGeneric
        columnHeaderCount = 0
        ReDim columnHeaders(0)

        reportRowCount = 0
        ReDim reportRows(0)
        Do
            Do While sqlReader.Read()

                ' Alternate row colors
                If (reportRowCount Mod 2) = 0 Then
                    rowStr = "<tr class = table-row>"
                Else
                    rowStr = "<tr class = table-alternate-row>"
                End If

                For i = 0 To sqlReader.FieldCount - 1
                    rowStr &= "<td>"

                    Try
                        If Not sqlReader.IsDBNull(i) Then
                            rowStr &= sqlReader.GetValue(i).ToString
                        End If
                    Catch ex As Exception
                        ' Unable to translate data into string; ignore errors here
                        Debug.WriteLine("Exception in GetReport: " & ex.Message)
                    End Try

                    rowStr &= "</td>"
                Next i

                ' Note: we'll append </tr> to the row below

                ReDim Preserve reportRows(reportRowCount)
                reportRows(reportRowCount) = rowStr
                reportRowCount += 1
            Loop

            ' See if we need to read the column headers
            ' If the first row in the dataset doesn't contain data for all the columns, then it won't contain all the column headers
            ' For each row, keep checking whether we have all the headers or not
            If sqlReader.FieldCount > columnHeaderCount Then
                columnHeaderCount = sqlReader.FieldCount
                ReDim columnHeaders(columnHeaderCount - 1)
                For i = 0 To columnHeaderCount - 1
                    columnHeaders(i) = sqlReader.GetName(i)
                Next i
            End If

        Loop Until Not sqlReader.NextResult

        Return FormatReportGeneric(columnHeaders, reportRows)
    End Function

    Private Sub GenerateReports(ByVal strSettingsFilePath As String)

        Dim logFilePath As String
        Dim programName As String
        Dim dirName As String

        programName = System.IO.Path.GetFileNameWithoutExtension(GetAppPath)
        dirName = System.IO.Path.GetDirectoryName(GetAppPath)
        logFilePath = System.IO.Path.Combine(dirName, programName & ".log")

        Try
			myLogger = New PRISM.Logging.clsFileLogger(logFilePath)
        Catch ex As Exception
            ShowError("Error initializing log file", False)
        End Try

        If System.IO.File.Exists(strSettingsFilePath) Then
            Console.WriteLine("Processing settings file " & strSettingsFilePath)
            ProcessXMLFile(strSettingsFilePath)
        Else
            ShowError("XML settings file not found: " & strSettingsFilePath)
        End If
      
    End Sub

    Private Function GetXMLAttribute(ByRef xn As System.Xml.XmlNode, ByRef xPath As String, ByRef attributeName As String, _
                Optional ByRef defaultValue As String = "") As String
        Dim subNode As System.Xml.XmlNode
        Dim attribute As System.Xml.XmlNode

        subNode = xn.SelectSingleNode(xPath)
        If Not subNode Is Nothing Then
            If subNode.Attributes.Count > 0 Then
                attribute = subNode.Attributes.GetNamedItem(attributeName)
                If Not attribute Is Nothing Then
                    GetXMLAttribute = attribute.Value()
                    Exit Function
                End If
            End If
        End If
        GetXMLAttribute = defaultValue
        Exit Function
    End Function

    Private Function GetWMIReport(ByVal xn_report As System.Xml.XmlNode) As String
        Dim rowStr As String
        Dim reportRowCount As Integer
        Dim reportRows() As String
        Dim columnHeaderCount As Integer
        Dim columnHeaders() As String
        Dim wmiPath As String
        Dim queryStr As String
        Dim mo As System.Management.ManagementObject
        Dim prop As System.Management.PropertyData

        Dim i As Integer
        Dim testValue As String
        Dim valueDivisor As Double
        Dim roundDigits As Integer
        Dim units As String = String.Empty

        ' Note; we assemble the report using FormatReportGeneric
        columnHeaderCount = 0
        ReDim columnHeaders(0)

        reportRowCount = 0
        ReDim reportRows(0)

        wmiPath = "\\" & GetXMLAttribute(xn_report, "data", "source") & "\root\cimv2"
        Dim oMs As New System.Management.ManagementScope(wmiPath)
        queryStr = xn_report.SelectSingleNode("data").InnerText()
        Dim oQuery As New System.Management.ObjectQuery(queryStr)
        Dim oSearcher As New System.Management.ManagementObjectSearcher(oMs, oQuery)
        Dim oReturnCollection As System.Management.ManagementObjectCollection = oSearcher.Get()

        LookupValueDivisors(xn_report, valueDivisor, roundDigits, units)

        For Each mo In oReturnCollection
            If mo.Properties.Count > columnHeaderCount Then
                columnHeaderCount = mo.Properties.Count
                ReDim columnHeaders(columnHeaderCount - 1)
                i = 0
                For Each prop In mo.Properties
                    If i < columnHeaderCount Then columnHeaders(i) = prop.Name()
                    i += 1
                Next prop
            End If

            ' Alternate row colors
            If (reportRowCount Mod 2) = 0 Then
                rowStr = "<tr class = table-row>"
            Else
                rowStr = "<tr class = table-alternate-row>"
            End If

            For Each prop In mo.Properties
                rowStr &= "<td>"

                Try
                    If Not IsNothing(prop.Value()) Then
                        testValue = prop.Value().ToString
                        If valueDivisor <> 0 AndAlso IsNumeric(testValue) Then
                            testValue = Math.Round(CDbl(testValue) / valueDivisor, roundDigits).ToString & " " & units
                        End If
                        rowStr &= testValue
                    End If
                Catch ex As Exception
                    ' Unable to translate data into string; ignore errors here
                    Debug.WriteLine("Exception in GetWMIReport: " & ex.Message)
                End Try

                rowStr &= "</td>"
            Next prop

            ' Note: we'll append </tr> to the row below

            ReDim Preserve reportRows(reportRowCount)
            reportRows(reportRowCount) = rowStr

            reportRowCount += 1
        Next mo

        Return FormatReportGeneric(columnHeaders, reportRows)
    End Function

    Private Sub ShowError(ByVal strMessage As String)
        ShowError(strMessage, True)
    End Sub

    Private Sub ShowError(ByVal strMessage As String, ByVal blnLogToFile As Boolean)
        Console.WriteLine(strMessage)
        If blnLogToFile Then
            Try
                If Not myLogger Is Nothing Then
					myLogger.PostEntry(strMessage, PRISM.Logging.ILogger.logMsgType.logError, True)
                End If
            Catch ex As Exception
                Console.WriteLine("Error writing to log file")
            End Try
        End If
    End Sub

    Private Function GetSQLReport(ByVal xn_report As System.Xml.XmlNode, ByVal cmdType As CommandType) As String
        Dim connStr As String
        Dim sql As String
        Dim dbConn As System.Data.SqlClient.SqlConnection
        Dim sqlReader As System.Data.SqlClient.SqlDataReader
        Dim sqlCMD As System.Data.SqlClient.SqlCommand
        Dim report As String

        connStr = "Data Source=" & GetXMLAttribute(xn_report, "data", "source") & ";"
        connStr &= "Initial Catalog=" & GetXMLAttribute(xn_report, "data", "catalog") & ";"
        connStr &= "Integrated Security=SSPI;"
        connStr &= "Connection Timeout=120;"
        dbConn = New System.Data.SqlClient.SqlConnection(connStr)
        sql = xn_report.SelectSingleNode("data").InnerText()
        sqlCMD = New System.Data.SqlClient.SqlCommand(sql, dbConn)
        sqlCMD.CommandType = cmdType
        sqlCMD.CommandTimeout = 600
        Try
            dbConn.Open()
            sqlReader = sqlCMD.ExecuteReader()
            report = FormatSQLReport(sqlReader)
            sqlReader.Close()
            dbConn.Close()
        Catch ex As Exception
            ShowError("Exception getting report from database: " & ex.Message)
            report = "Exception getting report from database: " & ex.Message
        End Try
        Return report
    End Function

    Private Sub LookupValueDivisors(ByVal xn_report As System.Xml.XmlNode, ByRef valueDivisor As Double, ByRef roundDigits As Integer, ByRef units As String)

        Dim testValue As String

        Try
            testValue = GetXMLAttribute(xn_report, "valuedivisor", "value").ToString
            If IsNumeric(testValue) Then
                valueDivisor = CDbl(testValue)
            Else
                ' Value of 0 means do not divide values by a number
                valueDivisor = 0
            End If

            Try
                testValue = GetXMLAttribute(xn_report, "valuedivisor", "round").ToString
                If IsNumeric(testValue) Then
                    roundDigits = CInt(testValue)
                Else
                    roundDigits = 2
                End If
            Catch ex As Exception
                roundDigits = 2
            End Try

            units = GetXMLAttribute(xn_report, "valuedivisor", "units").ToString
        Catch ex As Exception
            ' Ignore any errors here
            valueDivisor = 0
            roundDigits = 2
            units = ""
        End Try

    End Sub

    Private Sub ProcessReportSection(ByVal xn_report As System.Xml.XmlNode)
        Dim beginStr As String
        Dim titleStr As String

        Dim strReportName As String = String.Empty
        Dim strReportType As String
        Dim strReportText As String = String.Empty
        Dim strSkipMessage As String = "??"

        Dim objClient As System.Net.Mail.SmtpClient
        Dim msg As System.Net.Mail.MailMessage
        Dim strFrom As String
        Dim strTo As String

        Dim reportFrequency As String
        Dim generateReport As Boolean
        Dim dayOfWeekList As String

        Console.WriteLine()

        ' See if we should run this report today
        Try
            reportFrequency = GetXMLAttribute(xn_report, "frequency", "daily")
            If reportFrequency.ToLower = "false" Then
                ' Do not generate the report daily
                ' Compare the first three letters of today's day of the week with the list in dayOfWeekList
                ' Thus, dayOfWeekList can contain either abbreviated day names or full day names, and the separation character doesn't matter
                dayOfWeekList = GetXMLAttribute(xn_report, "frequency", "dayofweeklist")
                If dayOfWeekList.ToLower.IndexOf(System.DateTime.Now().DayOfWeek.ToString.ToLower.Substring(0, 3)) >= 0 Then
                    generateReport = True
                Else
                    generateReport = False
                    strSkipMessage = "'dayofweeklist' does not contain " & Now.DayOfWeek.ToString
                End If
            Else
                ' Generate the report daily
                generateReport = True
            End If
        Catch ex As Exception
            ShowError("Exception reading frequency or dayofweeklist section from XML file: " & ex.Message)
            generateReport = True
        End Try

        Try
            strReportName = xn_report.Attributes.GetNamedItem("name").Value
        Catch ex As Exception
            ShowError("Exception reading report name from XML file: " & ex.Message)
        End Try

        If Not generateReport Then
            Console.WriteLine("Skipping report '" & strReportName & "' since " & strSkipMessage)
        Else


            strReportType = GetXMLAttribute(xn_report, "data", "type")
            Select Case strReportType.ToLower()
                Case "WMI".ToLower()
                    Console.WriteLine("Running report '" & strReportName & "' using WMI")
                    strReportText = GetWMIReport(xn_report)
                Case "query".ToLower()
                    Console.WriteLine("Running report '" & strReportName & "' using a query")
                    strReportText = GetSQLReport(xn_report, CommandType.Text)
                Case "StoredProcedure".ToLower()
                    Console.WriteLine("Running report '" & strReportName & "' using a stored procedure")
                    strReportText = GetSQLReport(xn_report, CommandType.StoredProcedure)
                Case Else
                    ' abort, retry, ignore?
                    ShowError("Unknown report type: " & strReportType)
                    strReportText = "Unknown report type: " & strReportType
            End Select

            If strReportText.Length > 0 Then

                strFrom = GetXMLAttribute(xn_report, "mail", "from")
                strTo = GetXMLAttribute(xn_report, "mail", "to")
                msg = New System.Net.Mail.MailMessage(strFrom, strTo)

                msg.BodyEncoding = System.Text.Encoding.ASCII
                msg.IsBodyHtml = True
                msg.Subject = GetXMLAttribute(xn_report, "mail", "subject")

                titleStr = "<h3>" & GetXMLAttribute(xn_report, "mail", "title") & "</h3>" & vbCrLf
                beginStr = "<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01//EN"" ""http://www.w3.org/TR/html4/strict.dtd"">" & vbCrLf
                beginStr = beginStr & "<html><head>"
                beginStr = beginStr & xn_report.SelectSingleNode("styles").InnerXml()
                beginStr = beginStr & "</head><body>" & vbCrLf
                strReportText = beginStr & titleStr & strReportText & "</body></html>"

                msg.Body = strReportText

                Try
                    If mPreviewMode Then
                        Console.WriteLine()
                        Console.WriteLine("Preview of data to be e-mailed to " & strTo)
                        Console.WriteLine(strReportText)
                    Else
                        objClient = New System.Net.Mail.SmtpClient(GetXMLAttribute(xn_report, "mail", "server"))
                        objClient.Send(msg)
                    End If
                Catch Ex As Exception
                    ShowError("Exception sending mail message: " & Ex.Message)
                End Try
            End If
        End If

        If mPreviewMode Then
            System.Threading.Thread.Sleep(500)
        End If

    End Sub

    Private Sub ProcessXMLFile(ByVal fileName As String)
        Dim m_XmlDoc As New System.Xml.XmlDocument
        Dim xn_reports As System.Xml.XmlNode
        Dim i As Integer

        If System.IO.File.Exists(fileName) Then
            Try
                m_XmlDoc.Load(fileName)
                xn_reports = m_XmlDoc.SelectSingleNode("/reports")
                If xn_reports.ChildNodes.Count > 0 Then
                    'For Each xn_report In xn_reports.ChildNodes
                    For i = 0 To xn_reports.ChildNodes.Count - 1
                        ProcessReportSection(xn_reports.ChildNodes.ItemOf(i))
                    Next i
                Else
                    ShowError("Configuration file contains no report sections to process.")
                End If
            Catch Ex As Exception
                ShowError("Exception reading XML file: " & Ex.Message)
            End Try
        Else
            ShowError("Could not locate file: " & fileName)
        End If
    End Sub

    Private Function GetAppPath() As String
        Return System.Reflection.Assembly.GetExecutingAssembly().Location
    End Function

    Private Function GetAppVersion() As String
        'Return System.Windows.Forms.Application.ProductVersion & " (" & PROGRAM_DATE & ")"

        Return System.Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(ByVal objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
        Dim strValidParameters() As String = New String() {"I", "X", "P"}

        Try
            ' Make sure no invalid parameters are present
            If objParseCommandLine.InvalidParametersPresent(strValidParameters) Then
                Return False
            Else
                With objParseCommandLine
                    ' Query objParseCommandLine to see if various parameters are present
                    If .RetrieveValueForParameter("I", strValue) Then
                        mXMLSettingsFilePath = strValue
                    ElseIf .NonSwitchParameterCount > 0 Then
                        mXMLSettingsFilePath = .RetrieveNonSwitchParameter(0)
                    End If

                    If .RetrieveValueForParameter("P", strValue) Then mPreviewMode = True
                    If .RetrieveValueForParameter("X", strValue) Then mShowExtendedXMLExample = True
                End With

                Return True
            End If

        Catch ex As Exception
            Console.WriteLine("Error parsing the command line parameters: " & ControlChars.NewLine & ex.Message)
        End Try

    End Function

    Private Sub ShowProgramHelp()

        Try

            Console.WriteLine()
            Console.WriteLine("Syntax: " & System.IO.Path.GetFileName(GetAppPath()) & " SettingsFileName.xml [/X] [/P]")
            Console.WriteLine()
            Console.WriteLine("This program uses the specified settings file to generate and e-mail reports.  Reports can be e-mailed daily or only on certain days. " & _
                              "Shown below is an example settings file; to see an extended example, use the /X switch at the command line. " & _
                              "To preview the reports that would be e-mailed, use the /P switch.")

            Console.WriteLine()
            Console.WriteLine("<?xml version=""1.0"" encoding=""UTF-8""?>")
            Console.WriteLine("<reports>")
            Console.WriteLine("    <report name=""Processor Status Warnings"">")
            Console.WriteLine("        <data source=""gigasax"" catalog=""DMS_Pipeline"" type=""query"">")
            Console.WriteLine("          SELECT * FROM V_Processor_Status_Warnings ORDER BY Processor_name")
            Console.WriteLine("        </data>")
            Console.WriteLine("        <mail server=""emailgw.pnl.gov"" from=""dms@pnl.gov"" ")
            Console.WriteLine("        to=""matthew.monroe@pnl.gov"" ")
            Console.WriteLine("        subject=""Processor Status Warnings"" ")
            Console.WriteLine("        title=""Processor Status Warnings"" />")
            Console.WriteLine("        <styles>")
            Console.WriteLine("            <style type=""text/css"" media=""all"">")
            Console.WriteLine("         body { font: 12px Verdana, Arial, Helvetica, sans-serif; margin: 20px; }")
            Console.WriteLine("         h3 { font: 20px Verdana, Arial, Helvetica, sans-serif; }")
            Console.WriteLine("         table { margin: 4px; border-style: ridge; border-width: 2px; }")
            Console.WriteLine("         .table-header { color: white; background-color: #8080FF; }")
            Console.WriteLine("         .table-row { background-color: #D8D8FF; vertical-align:top;}")
            Console.WriteLine("         .table-alternate-row { background-color: #C0C0FF; vertical-align:top;}")
            Console.WriteLine("            </style>")
            Console.WriteLine("        </styles>")
            Console.WriteLine("        <frequency daily=""false"" dayofweeklist=""Monday,Wednesday,Friday"" />")
            Console.WriteLine("    </report>")

            If mShowExtendedXMLExample Then
                Console.WriteLine()
                Console.WriteLine("    <report name=""Gigasax Disk Space Report"">")
                Console.WriteLine("        <data source=""gigasax"" type=""WMI""><![CDATA[SELECT Name, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3]]></data>")
                Console.WriteLine("        <mail server=""emailgw.pnl.gov"" from=""dms@pnl.gov"" to=""dave.clark@pnl.gov"" subject=""Gigasax Disk Space"" title=""Free space on Gigasax (GiB):"" />")
                Console.WriteLine("        <styles>")
                Console.WriteLine("            <style type=""text/css"" media=""all"">")
                Console.WriteLine("         body { font: 12px Verdana, Arial, Helvetica, sans-serif; margin: 20px; }")
                Console.WriteLine("         h3 { font: 20px Verdana, Arial, Helvetica, sans-serif; }")
                Console.WriteLine("         table { margin: 4px; border-style: ridge; border-width: 2px; }")
                Console.WriteLine("         .table-header { color: white; background-color: #8080FF; }")
                Console.WriteLine("         .table-row { background-color: #D8D8FF; }")
                Console.WriteLine("         .table-alternate-row { background-color: #C0C0FF; }")
                Console.WriteLine("            </style>")
                Console.WriteLine("        </styles>")
                Console.WriteLine("        <frequency daily=""false"" dayofweeklist=""Wednesday"" />")
                Console.WriteLine("        <valuedivisor value=""1073741824"" round=""2"" units=""GiB"" />")
                Console.WriteLine("    </report>")
            End If
            Console.WriteLine("</reports>")

            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2010")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com")
            Console.WriteLine("Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/")

            ' Delay for 1500 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            System.Threading.Thread.Sleep(750)

        Catch ex As Exception
            Console.WriteLine("Error displaying the program syntax: " & ex.Message)
        End Try

    End Sub

End Module
