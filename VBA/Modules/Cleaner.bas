Attribute VB_Name = "Cleaner"
Sub FolderKiller(ByVal folderDirectory As String)
    Dim fso As Object
    Set fso = CreateObject("Scripting.FileSystemObject")
    
    If fso.FolderExists(folderDirectory) Then
        fso.DeleteFolder folderDirectory, True
    Else
        Exit Sub
    End If
End Sub
