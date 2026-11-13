using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Core.Tests
{
    // Records the last request and returns canned JSON (or a faulted task).
    public class FakeTransport : IHttpTransport
    {
        public Func<string, string> Handler;
        public Exception ErrorToThrow;

        public string LastUrl;
        public string LastMethod;
        public string LastJson;
        public IDictionary<string, string> LastHeaders;

        public Task<string> GetStringAsync(string url, IDictionary<string, string> headers)
        {
            return SendAsync("GET", url, null, headers);
        }

        public Task<string> PostJsonAsync(string url, string json, IDictionary<string, string> headers)
        {
            return SendAsync("POST", url, json, headers);
        }

        public Task<string> SendAsync(string method, string url, string json, IDictionary<string, string> headers)
        {
            LastUrl = url;
            LastMethod = method;
            LastJson = json;
            LastHeaders = headers;
            if (ErrorToThrow != null)
            {
                var tcs = new TaskCompletionSource<string>();
                tcs.SetException(ErrorToThrow);
                return tcs.Task;
            }
            return Task.FromResult(Handler != null ? Handler(url) : "{}");
        }
    }
}
