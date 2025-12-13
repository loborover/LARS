Attribute VB_Name = "Z_Directory"
Private Const DvBOM As String = "BOM"
Private Const DvDP As String = "DailyPlan"
Private Const DvPL As String = "PartList"
Private Const DvFD As String = "Feeder"
Private Const DvMD As String = "MultiDocuments"
Private Const DvBackup As String = "Backup"
Private Const DvDev As String = "A_Develop"
Private SourceFileFolder_Directory As String

Private ws As Worksheet
Public isDirSetUp As Boolean

Public Sub SetUpDirectories()
    Set ws = ThisWorkbook.Worksheets("Setting")
    SourceFileFolder_Directory = ws.Columns(1).Find(What:="Source", lookAt:=xlWhole).Offset(0, 1).Value
    isDirSetUp = True
End Sub

Public Property Get BOM() As String
    BOM = ThisWorkbook.Path & DvBOM
End Property
Public Property Get DailyPlan() As String
    DailyPlan = ThisWorkbook.Path & DvDP
End Property
Public Property Get PartList() As String
    PartList = ThisWorkbook.Path & DvPL
End Property
Public Property Get Feeder() As String
    Feeder = ThisWorkbook.Path & DvFD
End Property
Public Property Get MultiDocuments() As String
    MultiDocuments = ThisWorkbook.Path & DvMD
End Property
Public Property Get Backup() As String
    Backup = ThisWorkbook.Path & DvBackup
End Property
Public Property Get Develop() As String
    Develop = ThisWorkbook.Path & DvDev
End Property
Public Property Get Source() As String
    Source = SourceFileFolder_Directory
End Property
