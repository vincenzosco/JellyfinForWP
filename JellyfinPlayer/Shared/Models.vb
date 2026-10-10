Imports System.Collections.Generic
Imports System.Runtime.Serialization

Namespace Global.JellyfinPlayer.Shared
    <DataContract>
    Public Class ServerInfoDto
        <DataMember> Public Property ServerName As String
        <DataMember> Public Property Version As String
        <DataMember> Public Property Id As String
    End Class

    <DataContract>
    Public Class PublicUserDto
        <DataMember> Public Property Id As String
        <DataMember> Public Property Name As String
        <DataMember> Public Property HasPassword As Boolean
    End Class

    <DataContract>
    Public Class UserDto
        <DataMember> Public Property Id As String
        <DataMember> Public Property Name As String
    End Class

    <DataContract>
    Public Class AuthenticateRequestDto
        <DataMember> Public Property Username As String
        <DataMember> Public Property Pw As String
    End Class

    <DataContract>
    Public Class AuthenticateResultDto
        <DataMember> Public Property User As UserDto
        <DataMember> Public Property AccessToken As String
        <DataMember> Public Property ServerId As String
    End Class

    <DataContract>
    Public Class QueryResultDto(Of T)
        <DataMember> Public Property Items As List(Of T)
        <DataMember> Public Property TotalRecordCount As Integer
    End Class

    ' Jellyfin returns ImageTags as a JSON object {"Primary":"...","Logo":"..."}.
    ' DataContractJsonSerializer cannot map a JSON object onto Dictionary(Of String,String),
    ' so only the keys this client reads are declared; unknown keys are ignored.
    <DataContract>
    Public Class ImageTagsDto
        <DataMember> Public Property Primary As String
    End Class

    <DataContract>
    Public Class UserDataDto
        <DataMember> Public Property PlaybackPositionTicks As Long?
        <DataMember> Public Property Played As Boolean
        <DataMember> Public Property IsFavorite As Boolean
    End Class

    <DataContract>
    Public Class MediaStreamDto
        <DataMember> Public Property Index As Integer
        <DataMember> Public Property Type As String
        <DataMember> Public Property Codec As String
        <DataMember> Public Property Language As String
        <DataMember> Public Property DisplayTitle As String
        <DataMember> Public Property IsDefault As Boolean
        <DataMember> Public Property IsExternal As Boolean
        <DataMember> Public Property Height As Integer?
        <DataMember> Public Property Width As Integer?
        <DataMember> Public Property Channels As Integer?
    End Class

    <DataContract>
    Public Class MediaSourceInfoDto
        <DataMember> Public Property Id As String
        <DataMember> Public Property Path As String
        <DataMember> Public Property Container As String
        <DataMember> Public Property Protocol As String
        <DataMember> Public Property SupportsDirectPlay As Boolean
        <DataMember> Public Property SupportsTranscoding As Boolean
        <DataMember> Public Property DirectStreamUrl As String
        <DataMember> Public Property TranscodingUrl As String
        <DataMember> Public Property TranscodingSubProtocol As String
        <DataMember> Public Property RunTimeTicks As Long?
        <DataMember> Public Property MediaStreams As List(Of MediaStreamDto)
    End Class

    <DataContract>
    Public Class BaseItemDto
        <DataMember> Public Property Id As String
        <DataMember> Public Property Name As String
        <DataMember> Public Property Type As String
        <DataMember> Public Property CollectionType As String
        <DataMember> Public Property Overview As String
        <DataMember> Public Property RunTimeTicks As Long?
        <DataMember> Public Property IndexNumber As Integer?
        <DataMember> Public Property ParentIndexNumber As Integer?
        <DataMember> Public Property SeriesName As String
        <DataMember> Public Property ProductionYear As Integer?
        <DataMember> Public Property UserData As UserDataDto
        <DataMember> Public Property ImageTags As ImageTagsDto
        <DataMember> Public Property BackdropImageTags As List(Of String)
        <DataMember> Public Property MediaSources As List(Of MediaSourceInfoDto)
        <DataMember> Public Property ChildCount As Integer?
    End Class

    <DataContract>
    Public Class PlaybackInfoResponseDto
        <DataMember> Public Property MediaSources As List(Of MediaSourceInfoDto)
        <DataMember> Public Property PlaySessionId As String
    End Class

    <DataContract>
    Public Class PlaybackProgressDto
        <DataMember> Public Property ItemId As String
        <DataMember> Public Property MediaSourceId As String
        <DataMember> Public Property PlaySessionId As String
        <DataMember> Public Property PositionTicks As Long?
        <DataMember> Public Property IsPaused As Boolean
        <DataMember> Public Property CanSeek As Boolean
        <DataMember> Public Property PlayMethod As String
    End Class

    <DataContract>
    Public Class TranscodingProfileDto
        <DataMember> Public Property Container As String
        <DataMember> Public Property Type As String
        <DataMember> Public Property VideoCodec As String
        <DataMember> Public Property AudioCodec As String
        <DataMember> Public Property Protocol As String
        <DataMember> Public Property Context As String
        <DataMember> Public Property MaxAudioChannels As String
    End Class

    <DataContract>
    Public Class DirectPlayProfileDto
        <DataMember> Public Property Container As String
        <DataMember> Public Property Type As String
        <DataMember> Public Property VideoCodec As String
        <DataMember> Public Property AudioCodec As String
    End Class

    <DataContract>
    Public Class SubtitleProfileDto
        <DataMember> Public Property Format As String
        <DataMember> Public Property Method As String
    End Class

    <DataContract>
    Public Class DeviceProfileDto
        <DataMember> Public Property MaxStreamingBitrate As Integer
        <DataMember> Public Property TranscodingProfiles As List(Of TranscodingProfileDto)
        <DataMember> Public Property DirectPlayProfiles As List(Of DirectPlayProfileDto)
        <DataMember> Public Property SubtitleProfiles As List(Of SubtitleProfileDto)
    End Class

    <DataContract>
    Public Class PlaybackInfoRequestDto
        <DataMember> Public Property UserId As String
        <DataMember> Public Property MaxStreamingBitrate As Integer
        <DataMember> Public Property StartTimeTicks As Long
        <DataMember> Public Property AudioStreamIndex As Integer?
        <DataMember> Public Property SubtitleStreamIndex As Integer?
        <DataMember> Public Property AutoOpenLiveStream As Boolean
        <DataMember> Public Property DeviceProfile As DeviceProfileDto
    End Class
End Namespace
