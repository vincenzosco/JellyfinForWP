using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JellyfinPlayer.Shared
{
    // One saved login. Json cannot map a JSON object onto a Dictionary, so the
    // document is a class with [DataMember] properties (see ImageTagsDto).
    [DataContract]
    public class AccountDto
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string BaseUrl { get; set; }
        [DataMember] public string UserId { get; set; }
        [DataMember] public string UserName { get; set; }
        [DataMember] public string ServerName { get; set; }
        [DataMember] public string AccessToken { get; set; }
        [DataMember] public string ServerId { get; set; }
        // What /System/Info/Public reported at login; picks the auth spellings.
        [DataMember] public string ServerVersion { get; set; }
    }

    // The whole store as one document, so a save is a single atomic write.
    [DataContract]
    public class AccountDocumentDto
    {
        [DataMember] public List<AccountDto> Accounts { get; set; }
        [DataMember] public string ActiveId { get; set; }
    }
}
