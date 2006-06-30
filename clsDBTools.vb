Imports System.Data.SqlClient
Imports System.Data
Imports DMS_EMail_Manager.Logging
Imports System.Collections.Specialized

Public Class clsDBTools

#Region "Member Variables"

	' access to the logger
	Protected m_logger As ILogger

	' DB access
	Protected m_connection_str As String
	Protected m_DBCn As SqlConnection
	Protected m_error_list As New StringCollection

#End Region

	' constructor
	Public Sub New(ByVal logger As ILogger, ByVal ConnectStr As String)
		m_logger = logger
		m_connection_str = ConnectStr
	End Sub

	Public Property ConnectStr() As String
		Get
			Return m_connection_str
		End Get
		Set(ByVal Value As String)
			m_connection_str = Value
		End Set
	End Property

	Protected Function OpenConnection() As Boolean
		Dim retryCount As Integer = 3
		While retryCount > 0
			Try
				m_DBCn = New SqlConnection(m_connection_str)
				AddHandler m_DBCn.InfoMessage, New SqlInfoMessageEventHandler(AddressOf OnInfoMessage)
				m_DBCn.Open()
				retryCount = 0
				Return True
			Catch e As SqlException
				retryCount -= 1
				m_DBCn.Close()
				m_logger.PostError("Connection problem: ", e, True)
				System.Threading.Thread.Sleep(300)
			End Try
		End While
		If retryCount < 1 Then
			m_logger.PostEntry("Unable to open connection after multiple tries", ILogger.logMsgType.logError, True)
			Return False
		End If
	End Function

	Protected Sub CLoseConnection()
		If Not m_DBCn Is Nothing Then
			m_DBCn.Close()
		End If
	End Sub

	Public Sub LogErrorEvents()
		If m_error_list.Count > 0 Then
			m_logger.PostEntry("Warning messages were posted to local log", Logging.ILogger.logMsgType.logWarning, True)
		End If
		Dim s As String
		For Each s In m_error_list
			m_logger.PostEntry(s, Logging.ILogger.logMsgType.logWarning, True)
		Next
	End Sub

	' event handler for InfoMessage event
	' errors and warnings sent from the SQL server are caught here
	'
	Private Sub OnInfoMessage(ByVal sender As Object, ByVal args As SqlInfoMessageEventArgs)
		Dim err As SqlError
		Dim s As String
		For Each err In args.Errors
			s = ""
			s &= "Message: " & err.Message
			s &= ", Source: " & err.Source
			s &= ", Class: " & err.Class
			s &= ", State: " & err.State
			s &= ", Number: " & err.Number
			s &= ", LineNumber: " & err.LineNumber
			s &= ", Procedure:" & err.Procedure
			s &= ", Server: " & err.Server
			m_error_list.Add(s)
		Next
	End Sub

	Public Function GetDiscDataSet(ByVal SQL As String, ByRef DS As DataSet, ByRef RowCount As Integer) As Boolean

		'Returns a disconnected dataset as specified by the SQL statement
		Dim Adapter As SqlDataAdapter

		'Verify database connection is open
		If Not OpenConnection() Then Return False

		Try
			'Get the dataset
			Adapter = New SqlDataAdapter(SQL, m_DBCn)
			DS = New DataSet
			RowCount = Adapter.Fill(DS)
			Return True
		Catch ex As Exception
			'If error happened, log it
			m_logger.PostError("Error reading database", ex, True)
			Return False
		Finally
			'Be sure connection is closed
			m_DBCn.Close()
		End Try

	End Function

	Public Function UpdateDatabase(ByVal SQL As String, ByRef AffectedRows As Integer) As Boolean

		'Updates a database table as specified in the SQL statement
		Dim Cmd As SqlCommand

		AffectedRows = 0

		'Verify database connection is open
		If Not OpenConnection() Then Return False

		Try
			Cmd = New SqlCommand(SQL, m_DBCn)
			AffectedRows = Cmd.ExecuteNonQuery()
			Return True
		Catch ex As Exception
			'If error happened, log it
			m_logger.PostError("Error updating database", ex, True)
			Return False
		Finally
			m_DBCn.Close()
		End Try

	End Function
End Class
