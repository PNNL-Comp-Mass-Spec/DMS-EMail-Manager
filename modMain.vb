Option Strict On

Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Xml

' Program written in 2004 by Dave Clark and Nate Trimble
' Ported to .NET 2008 in 2010 by Matthew Monroe

Module modMain

    Private Const PROGRAM_DATE As String = "November 11, 2016"
    Private Const NO_DATA As String = "No Data Returned"

    Private myLogger As PRISM.Logging.ILogger

    Private mXMLSettingsFilePath As String = String.Empty
    Private mPreviewMode As Boolean = False
    Private mShowExtendedXMLExample As Boolean

    Public Function Main() As Integer

        Dim objParseCommandLine As New clsParseCommandLine
        Dim blnProceed As Boolean

        mXMLSettingsFilePath = String.Empty
        mPreviewMode = False
        mShowExtendedXMLExample = False

        Try
            blnProceed = False
            If objParseCommandLine.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
            End If

            If Not blnProceed OrElse
                 objParseCommandLine.NeedToShowHelp OrElse
                 objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount = 0 OrElse
                 mXMLSettingsFilePath.Length = 0 Then
                ShowProgramHelp()
            Else
                GenerateReports(mXMLSettingsFilePath)
            End If

        Catch ex As Exception
            Console.WriteLine("Error occurred in modMain->Main: " & ControlChars.NewLine & ex.Message)
            Return -1
        End Try

        ' Sleep for 750 msec
        Threading.Thread.Sleep(750)

        Return 0

    End Function

    Private Function FormatReportGeneric(columnHeaders As ICollection(Of String), reportRows As IEnumerable(Of String)) As String

        Dim reportContents = New StringBuilder()

        If columnHeaders.Count = 0 Then
            Return NO_DATA
        End If

        ' Construct the header row
        reportContents.AppendLine("<table>")
        reportContents.AppendLine("<tr class = table-header>")
        For Each header In columnHeaders
            reportContents.Append("<td>" & header & "</td>")
        Next
        reportContents.AppendLine("</tr>")

        ' Append each row in reportRows to reportContents
        ' While doing this, make sure each row has columnHeaders.Count sets of <td></td> pairs
        For Each row In reportRows
            If String.IsNullOrWhiteSpace(row) Then Continue For

            reportContents.Append(row)

            Dim lastCharPos = -1
            Dim matchCount = 0
            Do
                lastCharPos = row.IndexOf("</td>", lastCharPos + 1, StringComparison.Ordinal)
                If lastCharPos >= 0 Then matchCount += 1
            Loop While lastCharPos >= 0

            ' Add any missing fields as empty <td></td> pairs
            ' This will result in mis-aligned report data, but at least all of the data will be there
            For i = matchCount + 1 To columnHeaders.Count
                reportContents.Append("<td></td>")
            Next

            reportContents.AppendLine("</tr>")

        Next

        reportContents.AppendLine("</table>")

        Return reportContents.ToString()

    End Function

    Private Function FormatSQLReport(sqlReader As IDataReader, <Out()> ByRef reportHasData As Boolean) As String

        Dim columnHeaders = New List(Of String)
        Dim reportRows = New List(Of String)
        Dim resultSets = 1

        Do
            Do While sqlReader.Read()

                Dim rowStr As String

                ' Alternate row colors
                If (reportRows.Count Mod 2) = 0 Then
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
                        ShowError("Exception in GetReport: " & ex.Message)
                    End Try

                    rowStr &= "</td>"
                Next i

                ' Note: we'll append </tr> to the row in FormatReportGeneric
                reportRows.Add(rowStr)

                ' See if we need to read the column headers
                ' sqlReader will typically only have one ResultSet, so typically the data in columnHeaders will match sqlReader.FieldCount
                If sqlReader.FieldCount > columnHeaders.Count Then
                    If resultSets = 1 OrElse reportRows.Count = 0 Then
                        ' Define all of the columns
                        columnHeaders.Clear()
                        For i = 0 To sqlReader.FieldCount - 1
                            columnHeaders.Add(sqlReader.GetName(i))
                        Next
                    Else
                        ' Only append new columns
                        For i = columnHeaders.Count To sqlReader.FieldCount - 1
                            columnHeaders.Add(sqlReader.GetName(i))
                        Next
                    End If

                End If
            Loop

            resultSets += 1
        Loop Until Not sqlReader.NextResult

        If reportRows.Count = 0 Then
            reportHasData = False
            Return NO_DATA
        End If

        reportHasData = True
        Return FormatReportGeneric(columnHeaders, reportRows)
    End Function

    Private Sub GenerateReports(strSettingsFilePath As String)

        Dim logFilePath As String
        Dim programName As String
        Dim dirName As String

        programName = Path.GetFileNameWithoutExtension(GetAppPath)
        dirName = Path.GetDirectoryName(GetAppPath)
        logFilePath = Path.Combine(dirName, programName & ".log")

        Try
            myLogger = New PRISM.Logging.clsFileLogger(logFilePath)
        Catch ex As Exception
            ShowError("Error initializing log file", False)
        End Try

        If File.Exists(strSettingsFilePath) Then
            Console.WriteLine("Processing settings file " & strSettingsFilePath)
            ProcessXMLFile(strSettingsFilePath)
        Else
            ShowError("XML settings file not found: " & strSettingsFilePath)
        End If

    End Sub

    Private Function GetXMLAttribute(xn As XmlNode, xPath As String, attributeName As String, Optional defaultValue As String = "") As String
        Dim subNode As XmlNode
        Dim attribute As XmlNode

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

    Private Function GetWMIReport(xn_report As XmlNode, <Out()> ByRef reportHasData As Boolean) As String

        Dim valueDivisor As Double
        Dim roundDigits As Integer
        Dim units As String = String.Empty

        Dim columnHeaders = New List(Of String)
        Dim reportRows = New List(Of String)

        Dim wmiPath = "\\" & GetXMLAttribute(xn_report, "data", "source") & "\root\cimv2"
        Dim oMs As New Management.ManagementScope(wmiPath)
        Dim queryStr = xn_report.SelectSingleNode("data").InnerText()
        Dim oQuery As New Management.ObjectQuery(queryStr)
        Dim oSearcher As New Management.ManagementObjectSearcher(oMs, oQuery)
        Dim oReturnCollection As Management.ManagementObjectCollection = oSearcher.Get()

        LookupValueDivisors(xn_report, valueDivisor, roundDigits, units)

        For Each mo In oReturnCollection
            If mo.Properties.Count > columnHeaders.Count Then
                columnHeaders.Clear()
                For Each prop In mo.Properties
                    columnHeaders.Add(prop.Name())
                Next prop
            End If

            Dim rowStr As String

            ' Alternate row colors
            If (reportRows.Count Mod 2) = 0 Then
                rowStr = "<tr class = table-row>"
            Else
                rowStr = "<tr class = table-alternate-row>"
            End If

            For Each prop In mo.Properties
                rowStr &= "<td>"

                Try
                    If Not IsNothing(prop.Value()) Then
                        Dim value = prop.Value().ToString
                        If Math.Abs(valueDivisor) > Single.Epsilon AndAlso IsNumeric(value) Then
                            ' The value is a number; round the value and append units
                            value = Math.Round(CDbl(value) / valueDivisor, roundDigits).ToString & " " & units
                        End If
                        rowStr &= value
                    End If
                Catch ex As Exception
                    ' Unable to translate data into string; ignore errors here
                    ShowError("Exception in GetWMIReport: " & ex.Message)
                End Try

                rowStr &= "</td>"
            Next prop

            ' Note: we'll append </tr> to the row in FormatReportGeneric
            reportRows.Add(rowStr)
        Next

        If reportRows.Count = 0 Then
            reportHasData = False
            Return NO_DATA
        End If

        reportHasData = True
        Return FormatReportGeneric(columnHeaders, reportRows)

    End Function

    Private Sub ShowError(strMessage As String)
        ShowError(strMessage, True)
    End Sub

    Private Sub ShowError(strMessage As String, blnLogToFile As Boolean)
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

    Private Function GetSQLReport(xn_report As XmlNode, cmdType As CommandType, <Out()> ByRef reportHasData As Boolean) As String

        Dim report As String

        Dim connStr = "Data Source=" & GetXMLAttribute(xn_report, "data", "source") & ";" &
            "Initial Catalog=" & GetXMLAttribute(xn_report, "data", "catalog") & ";" &
            "Integrated Security=SSPI;" &
            "Connection Timeout=120;"

        Try

            Using dbConn = New SqlClient.SqlConnection(connStr)
                Dim sqlQuery = xn_report.SelectSingleNode("data").InnerText()
                Using sqlCMD = New SqlClient.SqlCommand(sqlQuery, dbConn)
                    sqlCMD.CommandType = cmdType
                    sqlCMD.CommandTimeout = 600

                    dbConn.Open()
                    Dim sqlReader = sqlCMD.ExecuteReader()
                    report = FormatSQLReport(sqlReader, reportHasData)
                End Using
            End Using

        Catch ex As Exception
            ' Set this to true so that the exception message is returned in the e-mail
            reportHasData = True
            report = "Exception getting report from database: " & ex.Message
            ShowError(report)
        End Try

        Return report

    End Function

    Private Sub LookupValueDivisors(
      xn_report As XmlNode,
      <Out()> ByRef valueDivisor As Double,
      <Out()> ByRef roundDigits As Integer,
      <Out()> ByRef units As String)

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
            units = String.Empty
        End Try

    End Sub

    Private Sub MailReport(xn_report As XmlNode, reportText As String)

        Dim strFrom = GetXMLAttribute(xn_report, "mail", "from")
        Dim strTo = GetXMLAttribute(xn_report, "mail", "to")
        Dim msg = New Net.Mail.MailMessage(strFrom, strTo) With {
            .BodyEncoding = Encoding.ASCII,
            .IsBodyHtml = True,
            .Subject = GetXMLAttribute(xn_report, "mail", "subject")
        }

        Dim titleStr = "<h3>" & GetXMLAttribute(xn_report, "mail", "title") & "</h3>" & vbCrLf
        Dim beginStr = "<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01//EN"" ""http://www.w3.org/TR/html4/strict.dtd"">" & vbCrLf
        beginStr = beginStr & "<html><head>"
        beginStr = beginStr & xn_report.SelectSingleNode("styles").InnerXml()
        beginStr = beginStr & "</head><body>" & vbCrLf

        msg.Body = beginStr & titleStr & reportText & "</body></html>"

        Try
            If mPreviewMode Then
                Console.WriteLine()
                Console.WriteLine("Preview of data to be e-mailed to " & strTo)
                Console.WriteLine(reportText)
            Else
                Dim objClient = New Net.Mail.SmtpClient(GetXMLAttribute(xn_report, "mail", "server"))
                objClient.Send(msg)
            End If
        Catch ex As Exception
            ShowError("Exception sending mail message: " & ex.Message)
        End Try

    End Sub

    Private Sub ProcessReportSection(xn_report As XmlNode)

        Dim strReportName As String = String.Empty
        Dim strSkipMessage = "??"

        Dim generateReport As Boolean

        Console.WriteLine()

        ' See if we should run this report today
        Try
            Dim reportFrequency = GetXMLAttribute(xn_report, "frequency", "daily")
            If reportFrequency.ToLower = "false" Then
                ' Do not generate the report daily
                ' Compare the first three letters of today's day of the week with the list in dayOfWeekList
                ' Thus, dayOfWeekList can contain either abbreviated day names or full day names, and the separation character doesn't matter
                Dim dayOfWeekList = GetXMLAttribute(xn_report, "frequency", "dayofweeklist")
                If dayOfWeekList.ToLower.IndexOf(DateTime.Now().DayOfWeek.ToString.ToLower.Substring(0, 3), StringComparison.Ordinal) >= 0 Then
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

            Dim reportText As String
            Dim reportHasData = False
            Dim strReportType = GetXMLAttribute(xn_report, "data", "type")

            Select Case strReportType.ToLower()
                Case "WMI".ToLower()
                    Console.WriteLine("Running report '" & strReportName & "' using WMI")
                    reportText = GetWMIReport(xn_report, reportHasData)
                Case "query".ToLower()
                    Console.WriteLine("Running report '" & strReportName & "' using a query")
                    reportText = GetSQLReport(xn_report, CommandType.Text, reportHasData)
                Case "StoredProcedure".ToLower()
                    Console.WriteLine("Running report '" & strReportName & "' using a stored procedure")
                    reportText = GetSQLReport(xn_report, CommandType.StoredProcedure, reportHasData)
                Case Else
                    ' abort, retry, ignore?
                    ShowError("Unknown report type: " & strReportType)
                    reportText = "Unknown report type: " & strReportType
            End Select

            If Not reportHasData Then
                Console.WriteLine("Skipping report '" & strReportName & "' since no data was returned")
                Return
            End If

            MailReport(xn_report, reportText)

            If mPreviewMode Then
                Threading.Thread.Sleep(500)
            End If
        End If

    End Sub

    Private Sub ProcessXMLFile(filePath As String)
        Dim m_XmlDoc As New XmlDocument

        If Not File.Exists(filePath) Then
            ShowError("Could not locate file: " & filePath)
            Return
        End If

        Try
            m_XmlDoc.Load(filePath)
            Dim xn_reports = m_XmlDoc.SelectSingleNode("/reports")
            If xn_reports.ChildNodes.Count > 0 Then
                For Each xn_report As XmlNode In xn_reports.ChildNodes
                    ProcessReportSection(xn_report)
                Next
            Else
                ShowError("Configuration file contains no report sections to process.")
            End If
        Catch ex As Exception
            ShowError("Exception reading XML file: " & ex.Message)
            ShowError(PRISM.clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex))
        End Try

    End Sub

    Private Function GetAppPath() As String
        Return Reflection.Assembly.GetExecutingAssembly().Location
    End Function

    Private Function GetAppVersion() As String
        'Return System.Windows.Forms.Application.ProductVersion & " (" & PROGRAM_DATE & ")"

        Return Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
        Dim strValidParameters = New List(Of String) From {"I", "X", "P", "Preview"}

        Try
            ' Make sure no invalid parameters are present
            If objParseCommandLine.InvalidParametersPresent(strValidParameters) Then
                Return False
            Else
                ' Query objParseCommandLine to see if various parameters are present
                If objParseCommandLine.NonSwitchParameterCount > 0 Then
                    mXMLSettingsFilePath = objParseCommandLine.RetrieveNonSwitchParameter(0)
                End If

                If objParseCommandLine.RetrieveValueForParameter("I", strValue) Then
                    mXMLSettingsFilePath = strValue
                End If

                If objParseCommandLine.IsParameterPresent("P") Then mPreviewMode = True
                If objParseCommandLine.IsParameterPresent("Preview") Then mPreviewMode = True
                If objParseCommandLine.IsParameterPresent("X") Then mShowExtendedXMLExample = True

                Return True
            End If

        Catch ex As Exception
            Console.WriteLine("Error parsing the command line parameters: " & ControlChars.NewLine & ex.Message)
            Return False
        End Try

    End Function

    Private Sub ShowProgramHelp()

        Try

            Console.WriteLine()
            Console.WriteLine("Syntax: " & Path.GetFileName(GetAppPath()) & " SettingsFileName.xml [/X] [/P] [/Preview]")
            Console.WriteLine()
            Console.WriteLine("This program uses the specified settings file to generate and e-mail reports.  Reports can be e-mailed daily or only on certain days. " &
                              "Shown below is an example settings file; to see an extended example, use the /X switch at the command line. " &
                              "To preview the reports that would be e-mailed, use the /P (or /Preview) switch.")

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
                Console.WriteLine("        <mail server=""emailgw.pnl.gov"" from=""dms@pnl.gov"" to=""matthew.monroe@pnnl.gov"" subject=""Gigasax Disk Space"" title=""Free space on Gigasax (GiB):"" />")
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

            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com")
            Console.WriteLine("Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/")

            ' Delay for 1500 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            Threading.Thread.Sleep(750)

        Catch ex As Exception
            Console.WriteLine("Error displaying the program syntax: " & ex.Message)
        End Try

    End Sub

End Module
