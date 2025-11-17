VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "ModelInfo"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Public Enum ModelinfoFeild
    mif_WorkOrder = 901
    mif_FullName = 902
    mif_Number = 903
    mif_SpecNumber = 904
    mif_Spec = 905
    mif_Type = 906
    mif_Species = 907
    mif_TySpec = 908
    mif_Color = 909
    mif_Suffix = 910
End Enum

Private vWorkOrder As String
Private vFullName As String ' LSGL6335F.A
Private vNumber As String ' LSGL6335F
Private vSpecNumber As String ' LSGL6335
Private vSpec As String ' 6335
Private vType As String ' LSGL
Private vSpecies As String ' LS63
Private vTnS As String ' LS6335
Private vColor As String ' F
Private vSuffix As String ' A
Private vPivot(0 To 1) As Long

'초기화 이벤트 메서드
'Private Sub Class_Initialize()
'    ReDim Preserve vBooleans(1 To VarCount) As Boolean
'End Sub
'소멸 이벤트 메서드
'Private Sub Class_Terminate()
    
'End Sub

Public Property Get WorkOrder() As String
    WorkOrder = vWorkOrder
End Property
Public Property Let WorkOrder(value As String)
    vWorkOrder = value
End Property
Public Property Get FullName() As String
    FullName = vFullName
End Property
Public Property Let FullName(value As String)
    If vFullName <> value Then ParseModelinfo value
End Property
Public Property Get Suffix() As String
    Suffix = vSuffix
End Property
Public Property Get Number() As String
    Number = vNumber
End Property
Public Property Get Spec() As String
    Spec = vSpec
End Property
Public Property Get TheType() As String
    TheType = vType
End Property
Public Property Get Species() As String
    Species = vSpecies
End Property
Public Property Get SpecNumber() As String
    SpecNumber = vSpecNumber
End Property
Public Property Get TySpec() As String
    TySpec = vTnS
End Property
Public Property Get Color() As String
    Color = vColor
End Property
Public Property Get Row() As Long
    Row = vPivot(0)
End Property
Public Property Get col() As Long
    col = vPivot(1)
End Property
Friend Sub Set_Pivot(Optional ByRef Target As Range = Nothing, _
                             Optional ByVal Row As Long = -255, Optional ByVal Column As Long = -255)  ' Pivot 설정
    Select Case Target Is Nothing
    Case False
        vPivot(0) = Target.Row
        vPivot(1) = Target.Column
    Case True
        If Row > 0 Then vPivot(0) = Row
        If Column > 0 Then vPivot(1) = Column
    End Select
End Sub
Private Sub ParseModelinfo(ByRef WOFN As String) ' 모델명을 분리, 구분하는 서브루틴
    
    Dim Dot As Long
    
    vFullName = WOFN
    Dot = InStr(vFullName, ".")
    vNumber = Left(vFullName, Dot - 1)
    vSpec = mid(vNumber, 5, 4)
    vType = Left(vNumber, 4)
    vSpecies = Left(vType, 2) & Left(vSpec, 2)
    vSpecNumber = vType & vSpec
    vSuffix = mid(vFullName, Dot + 1)
    vColor = mid(vNumber, 9)
    vTnS = Left(vType, 2) & vSpec
    
End Sub
Public Function Copy() As ModelInfo ' Copy Function
    Dim cLS As New ModelInfo
    With cLS
        .FullName = Me.FullName
        .WorkOrder = Me.WorkOrder
        .Set_Pivot Row:=Me.Row, Column:=Me.col
    End With
    Set Copy = cLS
End Function
