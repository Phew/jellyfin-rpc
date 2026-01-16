Set WshShell = CreateObject("WScript.Shell")
' Get the directory of the current script
Set fso = CreateObject("Scripting.FileSystemObject")
currentDir = fso.GetParentFolderName(WScript.ScriptFullName)

' Path to the python script
scriptPath = dblQuote(currentDir & "\cli-app\main.py")

' Run python in the background (0 = hide window)
' We assume 'python' is in the PATH. If not, this might fail or require full path to python.exe
WshShell.Run "python " & scriptPath, 0, False

Function dblQuote(str)
    dblQuote = Chr(34) & str & Chr(34)
End Function
