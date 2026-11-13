using System.Collections.Generic;
using System.Threading.Tasks;

namespace JellyfinPlayer.Shared
{
    // The seam that lets the API client be unit-tested without a network.
    public interface IHttpTransport
    {
        Task<string> GetStringAsync(string url, IDictionary<string, string> headers);
        Task<string> PostJsonAsync(string url, string json, IDictionary<string, string> headers);
        Task<string> SendAsync(string method, string url, string json, IDictionary<string, string> headers);
    }
}
