Imports System.IO
Imports System.Collections.Specialized
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading


Namespace Logging

#Region "Logger Interface"
	Public Interface ILogger
		Enum logMsgType
			logNormal
			logError
			logWarning
			logDebug
			logNA
			logHealth
		End Enum

		Sub PostEntry(ByVal message As String, ByVal EntryType As logMsgType, ByVal localOnly As Boolean)
		Sub PostError(ByVal message As String, ByVal e As System.Exception, ByVal localOnly As Boolean)
	End Interface
#End Region

#Region "Base Logger Class"
	' Base class from which actual logging subclasses are derived.
	' It provides overridable member functions that 
	' implement the ILogger interface.  Subclasses will implement
	' the ILogger interface by overriding these members.  This
	' class also provides common resouces and functions that
	' all subclasses require.
	'=========================================================
	Public MustInherit Class clsBaseLogger
		Implements ILogger


		Protected Overridable Sub LogToFile(ByVal message As String, ByVal EntryType As ILogger.logMsgType)
		End Sub

		Public Overridable Sub PostEntry(ByVal message As String, ByVal EntryType As ILogger.logMsgType, ByVal localOnly As Boolean) Implements ILogger.PostEntry
		End Sub

		Public Overridable Sub PostError(ByVal message As String, ByVal e As System.Exception, ByVal localOnly As Boolean) Implements ILogger.PostError
		End Sub

		'Converts enumerated error type to string for logging output
		Protected Function TypeToString(ByVal MyErrType As ILogger.logMsgType) As String
			Select Case MyErrType
				Case ILogger.logMsgType.logNormal
					TypeToString = "Normal"
				Case ILogger.logMsgType.logError
					TypeToString = "Error"
				Case ILogger.logMsgType.logWarning
					TypeToString = "Warning"
				Case ILogger.logMsgType.logDebug
					TypeToString = "Debug"
				Case ILogger.logMsgType.logNA
					TypeToString = "na"
				Case ILogger.logMsgType.logHealth
					TypeToString = "Health"
				Case Else
					TypeToString = "??"
			End Select
		End Function

	End Class
#End Region

#Region "File Logger Class"
	' Provides logging to a local file
	'=========================================================
	Public Class clsFileLogger
		Inherits clsBaseLogger

		' log file path
		Private m_logFileName As String

		Public Sub New()
			MyBase.new()
		End Sub

		Public Sub New(ByVal filePath As String)
			MyBase.New()
			m_logFileName = filePath
		End Sub

		Public Property LogFilePath() As String
			Get
				Return m_logFileName
			End Get
			Set(ByVal Value As String)
				m_logFileName = Value
			End Set
		End Property

		Protected Overrides Sub LogToFile(ByVal message As String, ByVal EntryType As ILogger.logMsgType)
			Dim FileName As String
			Dim LogFile As StreamWriter

			' don't log to file if no file name given
			If m_logFileName = "" Then
				Exit Sub
			End If

			'Set up date values for file name
			FileName = "_" & Format(Now(), "MM-dd-yyyy") & ".txt"

			'Create log file name by appending specified file name and date
			FileName = m_logFileName & FileName

			Try
				If Not File.Exists(FileName) Then
					LogFile = File.CreateText(FileName)
				Else
					LogFile = File.AppendText(FileName)
				End If
				LogFile.Write(Now)
				LogFile.Write(", ")

				LogFile.Write(message)
				LogFile.Write(", ")

				LogFile.Write(TypeToString(EntryType))
				LogFile.Write(", ")

				LogFile.WriteLine()
				LogFile.Close()
			Catch e As System.Exception
				If Not LogFile Is Nothing Then
					LogFile.Close()
				End If
			End Try
		End Sub

		Public Overrides Sub PostEntry(ByVal message As String, ByVal EntryType As ILogger.logMsgType, ByVal localOnly As Boolean)
			LogToFile(message, EntryType)
		End Sub

		Public Overrides Sub PostError(ByVal message As String, ByVal e As System.Exception, ByVal localOnly As Boolean)
			LogToFile(message & ": " & e.Message, ILogger.logMsgType.logError)
		End Sub

	End Class
#End Region

#Region "Database Logger Class"
	' Provides logging to a database and local file
	'=========================================================
	Public Class clsDBLogger
		Inherits clsFileLogger

		' connection string
		Private m_connection_str As String

		' module name
		Private m_moduleName As String = "Module"

		' db error list
		Private m_error_list As StringCollection


		Public Sub New()
			MyBase.New()
		End Sub

		Public Sub New(ByVal modName As String, ByVal connectionStr As String, ByVal filePath As String)
			MyBase.New(filePath)
			m_connection_str = modName
			m_moduleName = connectionStr
		End Sub

		Public Property ModuleName() As String
			Get
				Return m_moduleName
			End Get
			Set(ByVal Value As String)
				m_moduleName = Value
			End Set
		End Property

		Public Property ConnectionString() As String
			Get
				Return m_connection_str
			End Get
			Set(ByVal Value As String)
				m_connection_str = Value
			End Set
		End Property

		Protected Overridable Sub LogToDB(ByVal message As String, ByVal EntryType As ILogger.logMsgType)
			PostLogEntry(TypeToString(EntryType), message, m_moduleName)
		End Sub

		Public Overrides Sub PostEntry(ByVal message As String, ByVal EntryType As ILogger.logMsgType, ByVal localOnly As Boolean)
			MyBase.PostEntry(message, EntryType, localOnly)
			If Not localOnly Then
				LogToDB(message, EntryType)
			End If
		End Sub

		Public Overrides Sub PostError(ByVal message As String, ByVal e As System.Exception, ByVal localOnly As Boolean)
			MyBase.PostError(message, e, localOnly)
			If Not localOnly Then
				LogToDB(message & ": " & e.Message, ILogger.logMsgType.logError)
			End If
		End Sub

		Private Function PostLogEntry(ByVal type As String, ByVal message As String, ByVal postedBy As String) As Boolean
			Dim dbCn As SqlConnection
			Dim sc As SqlCommand
			Dim Outcome As Boolean = False

			Try
				m_error_list.Clear()
				' create the database connection
				'
				Dim cnStr As String = m_connection_str
				dbCn = New SqlConnection(cnStr)
				AddHandler dbCn.InfoMessage, New SqlInfoMessageEventHandler(AddressOf OnInfoMessage)
				dbCn.Open()

				' create the command object
				'
				sc = New SqlCommand("PostLogEntry", dbCn)
				sc.CommandType = CommandType.StoredProcedure

				' define parameters for command object
				'
				Dim myParm As SqlParameter
				'
				' define parameter for stored procedure's return value
				'
				myParm = sc.Parameters.Add("@Return", SqlDbType.Int)
				myParm.Direction = ParameterDirection.ReturnValue
				'
				' define parameters for the stored procedure's arguments
				'
				myParm = sc.Parameters.Add("@type", SqlDbType.VarChar, 50)
				myParm.Direction = ParameterDirection.Input
				myParm.Value = type

				myParm = sc.Parameters.Add("@message", SqlDbType.VarChar, 500)
				myParm.Direction = ParameterDirection.Input
				myParm.Value = message

				myParm = sc.Parameters.Add("@postedBy", SqlDbType.VarChar, 50)
				myParm.Direction = ParameterDirection.Input
				myParm.Value = postedBy

				' execute the stored procedure
				'
				sc.ExecuteNonQuery()

				' get return value
				'
				Dim ret As Object
				ret = sc.Parameters("@Return").Value

				' if we made it this far, we succeeded
				'
				Outcome = True

			Catch ex As System.Exception
				System.Console.WriteLine(ex.Message)
			Finally
				If Not dbCn Is System.DBNull.Value Then
					dbCn.Close()
					dbCn.Dispose()
				End If
			End Try

			Return Outcome

		End Function

		' event handler for InfoMessage event
		' errors and warnings sent from the SQL server are caught here
		'
		Private Sub OnInfoMessage(ByVal sender As Object, ByVal args As SqlInfoMessageEventArgs)
			Dim err As SqlError
			Dim s As String
			For Each err In args.Errors
                ''Console.WriteLine("The {0} has received a severity {1}, state {2} error number {3}\n" & _
                ''                  "on line {4} of procedure {5} on server {6}:\n{7}", _
                ''                  err.Source, err.Class, err.State, err.Number, err.LineNumber, _
                ''                  err.Procedure, err.Server, err.Message)
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

	End Class
#End Region

#Region "Queue Logger Class"
	' Wraps a queuing mechanism around any object that implements ILogger
	' interface. The posting member functions of this class put the log entry
	' onto the end of an internal queue and return very quickly to the caller.
	' A separate thread within the class is used to perform the actual output of
	' the log entries using the logging object that is specified
	' in the constructor for this class.
	'=========================================================
	Public Class clsQueLogger
		Implements ILogger

		' a class to hold a log entry in the internal queue
		Class clsLogEntry
			Public message As String
			Public entryType As ILogger.logMsgType
			Public localOnly As Boolean
		End Class

		' queue to hold entries to be output
		Protected m_queue As Queue

		' internal thread for outputting entries from queue
		Protected m_Thread As Thread
		Protected m_threadRunning As Boolean = False
		Protected m_ThreadStart As New ThreadStart(AddressOf Me.LogFromQueue)

		' logger object to use for outputting entries from queue
		Protected m_logger As ILogger

		Public Sub New(ByVal logger As ILogger)
			' remember my logging object
			m_logger = logger

			' create a thread safe queue for log entries
			Dim q As New Queue()
			m_queue = Queue.Synchronized(q)
		End Sub

		' start the log output thread if it isn't already running
		Protected Sub KickTheOutputThread()
			If Not m_threadRunning Then
				m_threadRunning = True
				m_Thread = New Thread(m_ThreadStart)
				m_Thread.Start()
			End If
		End Sub

		' pull all entries from the queue and output them to the log streams
		Protected Sub LogFromQueue()
			Dim le As clsLogEntry

			While True
				If m_queue.Count = 0 Then Exit While
				le = m_queue.Dequeue()
				m_logger.PostEntry(le.message, le.entryType, le.localOnly)
			End While
			m_threadRunning = False
		End Sub


		Public Sub PostEntry(ByVal message As String, ByVal EntryType As ILogger.logMsgType, ByVal localOnly As Boolean) Implements ILogger.PostEntry
			Dim le As New clsLogEntry()
			le.message = message
			le.entryType = EntryType
			le.localOnly = localOnly
			m_queue.Enqueue(le)
			KickTheOutputThread()
		End Sub

		Public Sub PostError(ByVal message As String, ByVal e As System.Exception, ByVal localOnly As Boolean) Implements ILogger.PostError
			Dim le As New clsLogEntry()
			le.message = message & ": " & e.Message
			le.entryType = ILogger.logMsgType.logError
			le.localOnly = localOnly
			m_queue.Enqueue(le)
			KickTheOutputThread()
		End Sub
	End Class
#End Region

End Namespace


