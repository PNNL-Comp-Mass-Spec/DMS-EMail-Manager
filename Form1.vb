Option Explicit On 
Option Strict On

Imports System.Web.Mail
Imports System.Text.Encoding
Imports System.IO
Imports System.Xml
Imports System.Data.SqlClient
Imports System.Management

Public Class Form1
    Inherits System.Windows.Forms.Form

    Private myLogger As Logging.ILogger

#Region " Windows Form Designer generated code "

    Public Sub New()
        MyBase.New()

        'This call is required by the Windows Form Designer.
        InitializeComponent()

        'Add any initialization after the InitializeComponent() call

    End Sub

    'Form overrides dispose to clean up the component list.
    Protected Overloads Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            If Not (components Is Nothing) Then
                components.Dispose()
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> Private Sub InitializeComponent()
        '
        'Form1
        '
        Me.AutoScaleBaseSize = New System.Drawing.Size(5, 13)
        Me.ClientSize = New System.Drawing.Size(520, 266)
        Me.Name = "Form1"
        Me.Text = "Form1"

    End Sub

#End Region

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

    Private Function FormatSQLReport(ByVal sqlReader As SqlDataReader) As String
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

    Private Function GetXMLAttribute(ByRef xn As XmlNode, ByRef xPath As String, ByRef attributeName As String, _
                Optional ByRef defaultValue As String = "") As String
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

    Private Function GetWMIReport(ByVal xn_report As XmlNode) As String
        Dim rowStr As String
        Dim reportRowCount As Integer
        Dim reportRows() As String
        Dim columnHeaderCount As Integer
        Dim columnHeaders() As String
        Dim wmiPath As String
        Dim queryStr As String
        Dim mo As ManagementObject
        Dim prop As PropertyData

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
        Dim oMs As New ManagementScope(wmiPath)
        queryStr = xn_report.SelectSingleNode("data").InnerText()
        Dim oQuery As New ObjectQuery(queryStr)
        Dim oSearcher As New ManagementObjectSearcher(oMs, oQuery)
        Dim oReturnCollection As ManagementObjectCollection = oSearcher.Get()

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

    Private Function GetSQLReport(ByVal xn_report As XmlNode, ByVal cmdType As CommandType) As String
        Dim connStr As String
        Dim ErrMsg As String
        Dim sql As String
        Dim dbConn As SqlConnection
        Dim sqlReader As SqlDataReader
        Dim sqlCMD As SqlCommand
        Dim report As String

        connStr = "Data Source=" & GetXMLAttribute(xn_report, "data", "source") & ";"
        connStr &= "Initial Catalog=" & GetXMLAttribute(xn_report, "data", "catalog") & ";"
        connStr &= "Integrated Security=SSPI;"
        connStr &= "Connection Timeout=120;"
        dbConn = New SqlConnection(connStr)
        sql = xn_report.SelectSingleNode("data").InnerText()
        sqlCMD = New SqlCommand(sql, dbConn)
        sqlCMD.CommandType = cmdType
        sqlCMD.CommandTimeout = 600
        Try
            dbConn.Open()
            sqlReader = sqlCMD.ExecuteReader()
            report = FormatSQLReport(sqlReader)
            sqlReader.Close()
            dbConn.Close()
        Catch ex As Exception
            ErrMsg = "Exception: " & ex.Message & vbCrLf & vbCrLf & "Getting report from database."
            myLogger.PostEntry(ErrMsg, Logging.ILogger.logMsgType.logError, True)
            report = ErrMsg
        End Try
        Return report
    End Function

    Private Sub LookupValueDivisors(ByVal xn_report As XmlNode, ByRef valueDivisor As Double, ByRef roundDigits As Integer, ByRef units As String)

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

    Private Sub ProcessReportSection(ByVal xn_report As XmlNode)
        Dim beginStr As String
        Dim titleStr As String
        Dim ErrMsg As String

        Dim strReportName As String
        Dim strReportType As String
        Dim strReportText As String = String.Empty

        Dim objClient As System.Net.Mail.SmtpClient
        Dim msg As System.Net.Mail.MailMessage
        Dim strFrom As String
        Dim strTo As String

        Dim reportFrequency As String
        Dim generateReport As Boolean
        Dim dayOfWeekList As String

        ' See if we should run this report today
        Try
            reportFrequency = GetXMLAttribute(xn_report, "frequency", "daily")
            If reportFrequency.ToLower = "false" Then
                ' Do not generate the report daily
                ' Compare the first three letters of today's day of the week with the list in dayOfWeekList
                ' Thus, dayOfWeekList can contain either abbreviated day names or full day names, and the separation character doesn't matter
                dayOfWeekList = GetXMLAttribute(xn_report, "frequency", "dayofweeklist")
                If dayOfWeekList.ToLower.IndexOf(Now.DayOfWeek.ToString.ToLower.Substring(0, 3)) >= 0 Then
                    generateReport = True
                Else
                    generateReport = False
                End If
            Else
                ' Generate the report daily
                generateReport = True
            End If
        Catch ex As Exception
            ErrMsg = "Exception: " & ex.Message & vbCrLf & vbCrLf & "reading frequency or dayofweeklist section from XML file."
            myLogger.PostEntry(ErrMsg, Logging.ILogger.logMsgType.logError, True)
            generateReport = True
        End Try
        If Not generateReport Then Exit Sub

        strReportName = xn_report.Attributes.GetNamedItem("name").Value

        strReportType = GetXMLAttribute(xn_report, "data", "type")
        Select Case strReportType.ToLower()
            Case "WMI".ToLower()
                strReportText = GetWMIReport(xn_report)
            Case "query".ToLower()
                strReportText = GetSQLReport(xn_report, CommandType.Text)
            Case "StoredProcedure".ToLower()
                strReportText = GetSQLReport(xn_report, CommandType.StoredProcedure)
            Case Else
                ' abort, retry, ignore?
                strReportText = "Unknown report type: " & strReportType
        End Select

        If strReportText.Length > 0 Then
            strFrom = GetXMLAttribute(xn_report, "mail", "from")
            strTo = GetXMLAttribute(xn_report, "mail", "to")
            msg = New System.Net.Mail.MailMessage(strFrom, strTo)

            msg.BodyEncoding = ASCII
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
                objClient = New System.Net.Mail.SmtpClient(GetXMLAttribute(xn_report, "mail", "server"))
                objClient.Send(msg)
            Catch Ex As Exception
                ErrMsg = "Exception: " & Ex.Message & vbCrLf & vbCrLf & "Sending mail message."
                myLogger.PostEntry(ErrMsg, Logging.ILogger.logMsgType.logError, True)
            End Try
        End If
       
    End Sub

    Private Sub ProcessXMLFile(ByVal fileName As String)
        Dim m_XmlDoc As New XmlDocument
        Dim xn_reports As XmlNode
        Dim i As Integer

        If File.Exists(fileName) Then
            Try
                m_XmlDoc.Load(fileName)
                xn_reports = m_XmlDoc.SelectSingleNode("/reports")
                If xn_reports.ChildNodes.Count > 0 Then
                    'For Each xn_report In xn_reports.ChildNodes
                    For i = 0 To xn_reports.ChildNodes.Count - 1
                        ProcessReportSection(xn_reports.ChildNodes.ItemOf(i))
                    Next i
                Else
                    myLogger.PostEntry("Configuration file contains no report sections to process.", Logging.ILogger.logMsgType.logError, True)
                End If
            Catch Ex As Exception
                Dim ErrMsg As String
                ErrMsg = "Exception: " & Ex.Message & vbCrLf & vbCrLf & "Reading XML file."
                myLogger.PostEntry(ErrMsg, Logging.ILogger.logMsgType.logError, True)
            End Try
        Else
            myLogger.PostEntry("Could not locate file: " & fileName, Logging.ILogger.logMsgType.logError, True)
        End If
    End Sub

    Private Sub Form1_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles MyBase.Load
        Dim command As String = Microsoft.VisualBasic.Command
        Dim logfile As String
        Dim XMLfile As String
        Dim programName As String
        Dim dirName As String

        programName = Path.GetFileNameWithoutExtension(Application.ExecutablePath)
        dirName = Path.GetDirectoryName(Application.ExecutablePath) & Path.DirectorySeparatorChar
        logfile = dirName & programName & ".log"
        XMLfile = dirName & programName & "_Settings.xml"

        Me.Text = programName & " V" & Application.ProductVersion
        myLogger = New Logging.clsFileLogger(logfile)
        If command = "/oneshot" Then
            ProcessXMLFile(XMLfile)
            Me.Close()
        ElseIf command = "/?" Then
            System.Windows.Forms.MessageBox.Show("Syntax: DMS_EMail_Manager.exe [/oneshot]")
            Me.Close()
        End If
    End Sub
End Class
