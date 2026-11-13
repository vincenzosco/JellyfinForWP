using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JellyfinPlayer.Shared
{
    [DataContract]
    public class ServerInfoDto
    {
        [DataMember] public string ServerName { get; set; }
        [DataMember] public string Version { get; set; }
        [DataMember] public string Id { get; set; }
    }

    // Reply from a Jellyfin server to the UDP discovery probe on port 7359.
    [DataContract]
    public class DiscoveredServerDto
    {
        [DataMember] public string Address { get; set; }
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
    }

    [DataContract]
    public class PublicUserDto
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public bool HasPassword { get; set; }
    }

    [DataContract]
    public class UserDto
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
    }

    // Jellyfin 10.9+ reads "Pw"; 10.8 and earlier read "Password". Both are sent
    // so login works against either generation of server.
    [DataContract]
    public class AuthenticateRequestDto
    {
        [DataMember] public string Username { get; set; }
        [DataMember] public string Pw { get; set; }
        [DataMember] public string Password { get; set; }
    }

    [DataContract]
    public class AuthenticateResultDto
    {
        [DataMember] public UserDto User { get; set; }
        [DataMember] public string AccessToken { get; set; }
        [DataMember] public string ServerId { get; set; }
    }

    [DataContract]
    public class QueryResultDto<T>
    {
        [DataMember] public List<T> Items { get; set; }
        [DataMember] public int TotalRecordCount { get; set; }
    }

    // Jellyfin returns ImageTags as a JSON object {"Primary":"...","Logo":"..."}.
    // DataContractJsonSerializer cannot map a JSON object onto Dictionary(string,string),
    // so only the keys this client reads are declared; unknown keys are ignored.
    [DataContract]
    public class ImageTagsDto
    {
        [DataMember] public string Primary { get; set; }
    }

    [DataContract]
    public class UserDataDto
    {
        [DataMember] public long? PlaybackPositionTicks { get; set; }
        [DataMember] public bool Played { get; set; }
        [DataMember] public bool IsFavorite { get; set; }
    }

    [DataContract]
    public class MediaStreamDto
    {
        [DataMember] public int Index { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public string Codec { get; set; }
        [DataMember] public string Language { get; set; }
        [DataMember] public string DisplayTitle { get; set; }
        [DataMember] public bool IsDefault { get; set; }
        [DataMember] public bool IsExternal { get; set; }
        [DataMember] public int? Height { get; set; }
        [DataMember] public int? Width { get; set; }
        [DataMember] public int? Channels { get; set; }
    }

    [DataContract]
    public class MediaSourceInfoDto
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Path { get; set; }
        [DataMember] public string Container { get; set; }
        [DataMember] public string Protocol { get; set; }
        [DataMember] public bool SupportsDirectPlay { get; set; }
        [DataMember] public bool SupportsDirectStream { get; set; }
        [DataMember] public bool SupportsTranscoding { get; set; }
        [DataMember] public string DirectStreamUrl { get; set; }
        [DataMember] public string TranscodingUrl { get; set; }
        [DataMember] public string TranscodingSubProtocol { get; set; }
        [DataMember] public long? RunTimeTicks { get; set; }
        [DataMember] public List<MediaStreamDto> MediaStreams { get; set; }
    }

    [DataContract]
    public class BaseItemDto
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public string CollectionType { get; set; }
        [DataMember] public string Overview { get; set; }
        [DataMember] public long? RunTimeTicks { get; set; }
        [DataMember] public int? IndexNumber { get; set; }
        [DataMember] public int? ParentIndexNumber { get; set; }
        [DataMember] public string SeriesName { get; set; }
        [DataMember] public string SeriesId { get; set; }
        [DataMember] public string SeasonId { get; set; }
        [DataMember] public string ParentId { get; set; }
        [DataMember] public int? ProductionYear { get; set; }
        [DataMember] public UserDataDto UserData { get; set; }
        [DataMember] public ImageTagsDto ImageTags { get; set; }
        [DataMember] public List<string> BackdropImageTags { get; set; }
        [DataMember] public List<MediaSourceInfoDto> MediaSources { get; set; }
        [DataMember] public int? ChildCount { get; set; }
    }

    [DataContract]
    public class PlaybackInfoResponseDto
    {
        [DataMember] public List<MediaSourceInfoDto> MediaSources { get; set; }
        [DataMember] public string PlaySessionId { get; set; }
    }

    [DataContract]
    public class PlaybackProgressDto
    {
        [DataMember] public string ItemId { get; set; }
        [DataMember] public string MediaSourceId { get; set; }
        [DataMember] public string PlaySessionId { get; set; }
        [DataMember] public long? PositionTicks { get; set; }
        [DataMember] public bool IsPaused { get; set; }
        [DataMember] public bool CanSeek { get; set; }
        [DataMember] public string PlayMethod { get; set; }
    }

    [DataContract]
    public class TranscodingProfileDto
    {
        [DataMember] public string Container { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public string VideoCodec { get; set; }
        [DataMember] public string AudioCodec { get; set; }
        [DataMember] public string Protocol { get; set; }
        [DataMember] public string Context { get; set; }
        [DataMember] public string MaxAudioChannels { get; set; }
    }

    [DataContract]
    public class DirectPlayProfileDto
    {
        [DataMember] public string Container { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public string VideoCodec { get; set; }
        [DataMember] public string AudioCodec { get; set; }
    }

    [DataContract]
    public class SubtitleProfileDto
    {
        [DataMember] public string Format { get; set; }
        [DataMember] public string Method { get; set; }
    }

    [DataContract]
    public class DeviceProfileDto
    {
        [DataMember] public int MaxStreamingBitrate { get; set; }
        [DataMember] public List<TranscodingProfileDto> TranscodingProfiles { get; set; }
        [DataMember] public List<DirectPlayProfileDto> DirectPlayProfiles { get; set; }
        [DataMember] public List<SubtitleProfileDto> SubtitleProfiles { get; set; }
    }

    [DataContract]
    public class PlaybackInfoRequestDto
    {
        [DataMember] public string UserId { get; set; }
        [DataMember] public int MaxStreamingBitrate { get; set; }
        [DataMember] public long StartTimeTicks { get; set; }
        [DataMember] public int? AudioStreamIndex { get; set; }
        [DataMember] public int? SubtitleStreamIndex { get; set; }
        [DataMember] public bool AutoOpenLiveStream { get; set; }
        [DataMember] public DeviceProfileDto DeviceProfile { get; set; }
    }
}
