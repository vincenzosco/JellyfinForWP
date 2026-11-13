Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    Public Module DeviceProfileBuilder
        Public Const DefaultBitrate As Integer = 8000000

        ' Direct play for MP4 H.264+AAC/MP3, with a progressive HTTP MP4
        ' h264+aac transcode fallback. The Windows Phone Silverlight MediaElement
        ' has no AdaptiveMediaSource (no HLS) and no confirmed MKV support, so the
        ' baseline claims only the container the platform is documented to play.
        Public Function DefaultProfile(Optional maxBitrate As Integer = DefaultBitrate) As DeviceProfileDto
            Dim dp As New DeviceProfileDto()
            dp.MaxStreamingBitrate = maxBitrate
            dp.TranscodingProfiles = New List(Of TranscodingProfileDto) From {
                New TranscodingProfileDto With {.Container = "mp4", .Type = "Video", .VideoCodec = "h264",
                                                 .AudioCodec = "aac", .Protocol = "http", .Context = "Streaming",
                                                 .MaxAudioChannels = "2"}
            }
            dp.DirectPlayProfiles = New List(Of DirectPlayProfileDto) From {
                New DirectPlayProfileDto With {.Container = "mp4", .Type = "Video", .VideoCodec = "h264", .AudioCodec = "aac,mp3"}
            }
            ' Encode burns the subtitle into the transcode. Text and image formats
            ' are both declared, so a PGS or VobSub track can be chosen and still
            ' reach the phone as a picture it already knows how to draw.
            dp.SubtitleProfiles = New List(Of SubtitleProfileDto) From {
                New SubtitleProfileDto With {.Format = "srt", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "subrip", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "ass", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "ssa", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "vtt", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "webvtt", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "mov_text", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "pgssub", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "dvdsub", .Method = "Encode"},
                New SubtitleProfileDto With {.Format = "dvbsub", .Method = "Encode"}
            }
            Return dp
        End Function

        Public Function ToRequest(userId As String, startTicks As Long, Optional maxBitrate As Integer = DefaultBitrate,
                                  Optional audioStreamIndex As Integer? = Nothing,
                                  Optional subtitleStreamIndex As Integer? = Nothing) As PlaybackInfoRequestDto
            Return New PlaybackInfoRequestDto With {
                .UserId = userId,
                .MaxStreamingBitrate = maxBitrate,
                .StartTimeTicks = startTicks,
                .AudioStreamIndex = audioStreamIndex,
                .SubtitleStreamIndex = subtitleStreamIndex,
                .AutoOpenLiveStream = True,
                .DeviceProfile = DefaultProfile(maxBitrate)
            }
        End Function
    End Module
End Namespace
