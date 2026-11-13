using System.Collections.Generic;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // A row of the downloads screens. Built once here so both the home section and
    // the manage page show the same title, subtitle and size.
    public class DownloadRow
    {
        public DownloadRecordDto Record { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Size { get; set; }
        // The delete button lives inside a data template, where a code-set label
        // cannot reach it; the row carries the localised word instead.
        public string DeleteLabel { get; set; }

        public static List<DownloadRow> Build(IList<DownloadRecordDto> records)
        {
            var rows = new List<DownloadRow>();
            if (records == null) return rows;
            foreach (var entry in records)
            {
                if (entry == null) continue;
                var row = new DownloadRow();
                row.Record = entry;
                row.Title = string.IsNullOrEmpty(entry.Name) ? entry.ItemId : entry.Name;
                row.Subtitle = entry.Subtitle;
                row.Size = Downloads.SizeLabel(entry.SizeBytes);
                row.DeleteLabel = Localized.Text("DeleteDownload");
                rows.Add(row);
            }
            return rows;
        }
    }
}
