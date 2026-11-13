using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // Copies one video into isolated storage. Only the progressive MP4 the server
    // produces is downloaded: the original file may be a container this phone
    // cannot open, and a download the app cannot play would be dead weight.
    public static class DownloadService
    {
        // report is called from a background thread; the page marshals it to the
        // UI thread before touching a control.
        public static async Task<DownloadRecordDto> DownloadAsync(BaseItemDto item, Action<int> report)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return null;
            var session = AppSession.Current;
            string url = await TranscodeUrlAsync(item);
            if (string.IsNullOrEmpty(url)) throw new JellyfinException(Localized.Text("PlaybackNoSource"), 0);

            string fileName = Downloads.FileNameFor(item.Id);
            var store = DownloadStore.Current;
            long total = 0;
            long received = 0;
            try
            {
                using (var response = await GetResponseAsync(url, session.AccessToken))
                {
                    total = response.ContentLength;
                    // A download that cannot fit is better refused before it starts.
                    if (total > 0 && total > FreeSpace()) throw new JellyfinException(Localized.Text("DownloadNoSpace"), 0);
                    using (var input = response.GetResponseStream())
                    {
                        using (var output = store.CreateFile(fileName))
                        {
                            // Reading a network stream blocks; doing it here would
                            // freeze the page for the length of the film.
                            await Task.Factory.StartNew(() => Copy(input, output, total, report));
                            received = output.Length;
                        }
                    }
                }
            }
            catch
            {
                store.Discard(fileName);
                throw;
            }

            var record = new DownloadRecordDto
            {
                ItemId = item.Id,
                Name = item.Name,
                Subtitle = SubtitleFor(item),
                FileName = fileName,
                RunTimeTicks = item.RunTimeTicks,
                PositionTicks = 0L,
                SizeBytes = received > 0 ? received : total,
                DownloadedAtTicks = DateTime.UtcNow.Ticks
            };
            store.Save(record);
            if (report != null) report(100);
            return record;
        }

        // The same source selection the player uses, narrowed to transcodable
        // sources because only that URL is a plain MP4.
        private static async Task<string> TranscodeUrlAsync(BaseItemDto item)
        {
            var session = AppSession.Current;
            var info = await session.CreateClient().GetPlaybackInfoAsync(session.UserId, item.Id, 0, session.PreferredBitrate);
            if (info == null || info.MediaSources == null) return null;
            foreach (var source in info.MediaSources)
            {
                if (source != null && source.SupportsTranscoding)
                {
                    return PlaybackUrlResolver.TranscodeUrl(session.BaseUrl, session.AccessToken, item.Id, source, session.PreferredBitrate);
                }
            }
            return null;
        }

        private static void Copy(Stream input, Stream output, long total, Action<int> report)
        {
            var buffer = new byte[8192];
            long received = 0;
            int lastPercent = -1;
            int read = input.Read(buffer, 0, buffer.Length);
            while (read > 0)
            {
                output.Write(buffer, 0, read);
                received += read;
                int percent = Downloads.Percent(received, total);
                if (percent != lastPercent && report != null)
                {
                    lastPercent = percent;
                    report(percent);
                }
                read = input.Read(buffer, 0, buffer.Length);
            }
        }

        private static Task<HttpWebResponse> GetResponseAsync(string url, string token)
        {
            var completion = new TaskCompletionSource<HttpWebResponse>();
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                // A film is not a 15 second API call: no buffering, no timeout.
                request.AllowReadStreamBuffering = false;
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers["Authorization"] = JellyfinAuthHeader.WithToken(token, AppSession.Current.DeviceId);
                }
                request.BeginGetResponse(result =>
                {
                    try
                    {
                        completion.TrySetResult((HttpWebResponse)request.EndGetResponse(result));
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                }, null);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            return completion.Task;
        }

        private static long FreeSpace()
        {
            try
            {
                return System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication().AvailableFreeSpace;
            }
            catch
            {
                return long.MaxValue;
            }
        }

        private static string SubtitleFor(BaseItemDto item)
        {
            if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
            {
                string number = "";
                if (item.ParentIndexNumber.HasValue) number += "S" + item.ParentIndexNumber.Value.ToString("00");
                if (item.IndexNumber.HasValue) number += "E" + item.IndexNumber.Value.ToString("00");
                if (!string.IsNullOrEmpty(item.SeriesName)) return item.SeriesName + " " + number;
                return number;
            }
            if (item.ProductionYear.HasValue) return item.ProductionYear.Value.ToString();
            return "";
        }
    }
}
