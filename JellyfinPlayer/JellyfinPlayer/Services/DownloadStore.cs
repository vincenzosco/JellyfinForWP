using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // Where the offline videos live: one folder, one file per item, one JSON
    // document listing them. Silverlight has no database and no background agent
    // here, so this is the whole store.
    public class DownloadStore
    {
        private const string Folder = "downloads";
        private const string IndexFile = "downloads.json";

        private static DownloadStore _current;
        public static DownloadStore Current
        {
            get
            {
                if (_current == null) _current = new DownloadStore();
                return _current;
            }
        }

        private readonly List<DownloadRecordDto> _records = new List<DownloadRecordDto>();
        private bool _loaded;

        public List<DownloadRecordDto> Records()
        {
            EnsureLoaded();
            return _records;
        }

        public DownloadRecordDto Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            EnsureLoaded();
            foreach (var record in _records)
            {
                if (record != null && record.ItemId == itemId) return record;
            }
            return null;
        }

        public bool Has(string itemId)
        {
            return Find(itemId) != null;
        }

        public string FilePath(DownloadRecordDto record)
        {
            return Folder + "\\" + record.FileName;
        }

        // A fresh file for a new download; a re-download replaces the old one.
        public IsolatedStorageFileStream CreateFile(string fileName)
        {
            var files = Storage();
            EnsureFolder(files);
            return files.CreateFile(Folder + "\\" + fileName);
        }

        public IsolatedStorageFileStream OpenFile(DownloadRecordDto record)
        {
            var files = Storage();
            EnsureFolder(files);
            return files.OpenFile(Folder + "\\" + record.FileName, FileMode.Open, FileAccess.Read);
        }

        public void Save(DownloadRecordDto record)
        {
            if (record == null || string.IsNullOrEmpty(record.ItemId)) return;
            EnsureLoaded();
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].ItemId == record.ItemId)
                {
                    // The old file may have a different name; do not leave it behind.
                    if (_records[i].FileName != record.FileName) DeleteFile(_records[i].FileName);
                    _records[i] = record;
                    WriteIndex();
                    return;
                }
            }
            _records.Add(record);
            WriteIndex();
        }

        public void Remove(string itemId)
        {
            EnsureLoaded();
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].ItemId == itemId)
                {
                    DeleteFile(_records[i].FileName);
                    _records.RemoveAt(i);
                }
            }
            WriteIndex();
        }

        // A download that failed half way leaves no partial file behind.
        public void Discard(string fileName)
        {
            DeleteFile(fileName);
        }

        // The resume position is written when playback stops, not every second.
        public void UpdatePosition(string itemId, long ticks)
        {
            var record = Find(itemId);
            if (record == null) return;
            record.PositionTicks = ticks;
            WriteIndex();
        }

        public long TotalBytes()
        {
            long total = 0;
            foreach (var record in Records())
            {
                total += record.SizeBytes;
            }
            return total;
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _records.Clear();
            try
            {
                var files = Storage();
                EnsureFolder(files);
                if (!files.FileExists(IndexFile)) return;
                using (var stream = files.OpenFile(IndexFile, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        string text = reader.ReadToEnd();
                        if (string.IsNullOrEmpty(text)) return;
                        var loaded = Json.Deserialize<List<DownloadRecordDto>>(text);
                        if (loaded != null) _records.AddRange(loaded);
                    }
                }
            }
            catch
            {
                // A damaged index must not stop the app: the files that remain are
                // unlisted, and a new download writes a clean document.
            }
        }

        private void WriteIndex()
        {
            try
            {
                var files = Storage();
                EnsureFolder(files);
                using (var stream = files.CreateFile(IndexFile))
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(Json.Serialize(Records()));
                    }
                }
            }
            catch
            {
                // Storage full or locked: the video itself is still there.
            }
        }

        private void DeleteFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                var files = Storage();
                if (files.FileExists(Folder + "\\" + fileName)) files.DeleteFile(Folder + "\\" + fileName);
            }
            catch
            {
            }
        }

        private static IsolatedStorageFile Storage()
        {
            return IsolatedStorageFile.GetUserStoreForApplication();
        }

        private static void EnsureFolder(IsolatedStorageFile files)
        {
            if (!files.DirectoryExists(Folder)) files.CreateDirectory(Folder);
        }
    }
}
