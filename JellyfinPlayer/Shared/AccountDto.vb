Imports System.Collections.Generic
Imports System.Runtime.Serialization

Namespace Global.JellyfinPlayer.Shared
    ' One saved login. Json cannot map a JSON object onto a Dictionary, so the
    ' document is a class with [DataMember] properties (see ImageTagsDto).
    <DataContract>
    Public Class AccountDto
        <DataMember> Public Property Id As String
        <DataMember> Public Property BaseUrl As String
        <DataMember> Public Property UserId As String
        <DataMember> Public Property UserName As String
        <DataMember> Public Property ServerName As String
        <DataMember> Public Property AccessToken As String
        <DataMember> Public Property ServerId As String
    End Class

    ' The whole store as one document, so a save is a single atomic write.
    <DataContract>
    Public Class AccountDocumentDto
        <DataMember> Public Property Accounts As List(Of AccountDto)
        <DataMember> Public Property ActiveId As String
    End Class
End Namespace
