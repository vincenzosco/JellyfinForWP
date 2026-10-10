Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    Public Module DeviceProfileBuilder
        Public Const DefaultBitrate As Integer = 8000000

        ' Direct play for MP4/MKV H.264+AAC/MP3, with a progressive HTTP MP4
        ' h264+aac transcode fallback. WP8.1 has no AdaptiveMediaSource (no HLS),
        ' but MediaElement plays progressive MP4 natively.
        Public Function DefaultProfile() As DeviceProfileDto
            Dim dp As New DeviceProfileDto()
            dp.MaxStreamingBitrate = DefaultBitrate
            dp.TranscodingProfiles = New List(Of TranscodingProfileDto) From {
                New TranscodingProfileDto With {.Container = "mp4", .Type = "Video", .VideoCodec = "h264",
                                                 .AudioCodec = "aac", .Protocol = "http", .Context = "Streaming",
                                                 .MaxAudioChannels = "2"}
            }
            dp.DirectPlayProfiles = New List(Of DirectPlayProfileDto) From {
                New DirectPlayProfileDto With {.Container = "mp4,mkv", .Type = "Video", .VideoCodec = "h264", .AudioCodec = "aac,mp3"}
            }
            dp.SubtitleProfiles = New List(Of SubtitleProfileDto) From {
                New SubtitleProfileDto With {.Format = "srt", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "ass", .Method = "Encode"}
            }
            Return dp
        End Function

        Public Function ToRequest(userId As String, startTicks As Long) As PlaybackInfoRequestDto
            Return New PlaybackInfoRequestDto With {
                .UserId = userId,
                .MaxStreamingBitrate = DefaultBitrate,
                .StartTimeTicks = startTicks,
                .AutoOpenLiveStream = True,
                .DeviceProfile = DefaultProfile()
            }
        End Function
    End Module
End Namespace
