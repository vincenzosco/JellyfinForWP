using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace JellyfinPlayer.Shared
{
    // Cross-target HTTP transport: compiles against desktop .NET 4.5 (for MSTest)
    // and the Windows Phone Silverlight 8.1 reference assemblies. HttpWebRequest
    // is used because Silverlight does not expose HttpClient; the request uses the
    // begin/end async pattern so the UI thread never blocks.
    public class HttpTransport : IHttpTransport
    {
        private const int RequestTimeoutMs = 15000;

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
            var completion = new TaskCompletionSource<string>();
            HttpWebRequest request = null;
            bool timedOut = false;

            try
            {
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers[header.Key] = header.Value;
                    }
                }

                // A manual 15 s bound: Silverlight's HttpWebRequest has no Timeout
                // property, and the timer must not block the calling thread.
                Task.Delay(RequestTimeoutMs).ContinueWith(t =>
                {
                    timedOut = true;
                    try
                    {
                        request.Abort();
                    }
                    catch
                    {
                    }
                });

                if (json == null)
                {
                    BeginGetResponse(request, completion, () => timedOut);
                }
                else
                {
                    request.ContentType = "application/json; charset=utf-8";
                    byte[] payload = Encoding.UTF8.GetBytes(json);
                    request.ContentLength = payload.Length;
                    request.BeginGetRequestStream(result =>
                    {
                        try
                        {
                            using (var requestStream = request.EndGetRequestStream(result))
                            {
                                requestStream.Write(payload, 0, payload.Length);
                            }
                            BeginGetResponse(request, completion, () => timedOut);
                        }
                        catch (Exception ex)
                        {
                            completion.TrySetException(MapException(ex, timedOut));
                        }
                    }, null);
                }
            }
            catch (Exception ex)
            {
                completion.TrySetException(MapException(ex, timedOut));
            }

            return completion.Task;
        }

        private static void BeginGetResponse(HttpWebRequest request, TaskCompletionSource<string> completion, Func<bool> isTimedOut)
        {
            try
            {
                request.BeginGetResponse(result =>
                {
                    try
                    {
                        using (var response = (HttpWebResponse)request.EndGetResponse(result))
                        {
                            using (var responseStream = response.GetResponseStream())
                            {
                                using (var reader = new StreamReader(responseStream))
                                {
                                    string body = reader.ReadToEnd();
                                    int code = (int)response.StatusCode;
                                    if (code < 200 || code >= 300)
                                    {
                                        throw new JellyfinException(ResponseFailures.DescribeHttpFailure(
                                            code, response.ContentType, body), code, JellyfinErrorKind.HttpFailure);
                                    }
                                    completion.TrySetResult(body);
                                }
                            }
                        }
                    }
                    catch (WebException ex)
                    {
                        var response = ex.Response as HttpWebResponse;
                        if (response == null)
                        {
                            completion.TrySetException(MapException(ex, isTimedOut()));
                        }
                        else
                        {
                            try
                            {
                                using (response)
                                {
                                    using (var responseStream = response.GetResponseStream())
                                    {
                                        using (var reader = new StreamReader(responseStream))
                                        {
                                            string body = reader.ReadToEnd();
                                            string mediaType = (response.ContentType ?? string.Empty).Split(';')[0];
                                            int code = (int)response.StatusCode;
                                            completion.TrySetException(new JellyfinException(
                                                ResponseFailures.DescribeHttpFailure(code, mediaType, body),
                                                code, JellyfinErrorKind.HttpFailure));
                                        }
                                    }
                                }
                            }
                            catch (Exception readException)
                            {
                                completion.TrySetException(MapException(readException, isTimedOut()));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(MapException(ex, isTimedOut()));
                    }
                }, null);
            }
            catch (Exception ex)
            {
                completion.TrySetException(MapException(ex, isTimedOut()));
            }
        }

        // Pure mapping so the failure messages can be unit-tested without a network.
        public static JellyfinException MapException(Exception ex)
        {
            return MapException(ex, false);
        }

        private static JellyfinException MapException(Exception ex, bool timedOut)
        {
            if (ex is JellyfinException) return (JellyfinException)ex;
            if (timedOut)
            {
                return new JellyfinException("The server did not respond (timeout).", 0, JellyfinErrorKind.Timeout);
            }
            if (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                return new JellyfinException("The server did not respond (timeout).", 0, JellyfinErrorKind.Timeout);
            }
            if (ex is WebException)
            {
                var webException = (WebException)ex;
                if (webException.Status == WebExceptionStatus.Timeout)
                {
                    return new JellyfinException("The server did not respond (timeout).", 0, JellyfinErrorKind.Timeout);
                }
                return new JellyfinException("Could not reach the server: " + webException.Message, 0, JellyfinErrorKind.Unreachable);
            }
            if (ex is IOException)
            {
                return new JellyfinException("Could not reach the server: " + ex.Message, 0, JellyfinErrorKind.Unreachable);
            }
            // HttpRequestException lives in System.Net.Http, which the Windows Phone
            // Silverlight reference set does not expose. Match it by name so the shared
            // mapping compiles on both targets while still classifying it as unreachable.
            if (ex.GetType().Name == "HttpRequestException")
            {
                return new JellyfinException("Could not reach the server: " + ex.Message, 0, JellyfinErrorKind.Unreachable);
            }
            return new JellyfinException(ex.Message, 0);
        }
    }
}
