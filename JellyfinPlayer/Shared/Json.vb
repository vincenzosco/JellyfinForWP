Imports System.IO
Imports System.Runtime.Serialization.Json
Imports System.Text

Namespace Global.JellyfinPlayer.Shared
    Public Module Json
        Public Function Deserialize(Of T)(json As String) As T
            Dim serializer As New DataContractJsonSerializer(GetType(T))
            Using ms As New MemoryStream(Encoding.UTF8.GetBytes(json))
                Return DirectCast(serializer.ReadObject(ms), T)
            End Using
        End Function

        Public Function Serialize(value As Object) As String
            Dim serializer As New DataContractJsonSerializer(value.GetType())
            Using ms As New MemoryStream()
                serializer.WriteObject(ms, value)
                Return Encoding.UTF8.GetString(ms.ToArray(), 0, CInt(ms.Length))
            End Using
        End Function
    End Module
End Namespace
