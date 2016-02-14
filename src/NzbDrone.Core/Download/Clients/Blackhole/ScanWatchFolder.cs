using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Crypto;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Organizer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.Download.Clients.Blackhole
{
    public interface IScanWatchFolder
    {
        IEnumerable<WatchFolderItem> GetItems(string watchFolder, TimeSpan gracePeriod);
    }

    public class ScanWatchFolder : IScanWatchFolder
    {
        private readonly Logger _logger;
        private readonly IDiskProvider _diskProvider;
        private readonly IDiskScanService _diskScanService;
        private readonly ICached<Dictionary<string, WatchFolderItem>>  _watchFolderItemCache;

        public ScanWatchFolder(ICacheManager cacheManager, IDiskScanService diskScanService, IDiskProvider diskProvider, Logger logger)
        {
            _logger = logger;
            _diskProvider = diskProvider;
            _diskScanService = diskScanService;
            _watchFolderItemCache = cacheManager.GetCache<Dictionary<string, WatchFolderItem>>(GetType());
        }

        public IEnumerable<WatchFolderItem> GetItems(string watchFolder, TimeSpan gracePeriod)
        {
            var newWatchItems = new Dictionary<string, WatchFolderItem>();
            var lastWatchItems = _watchFolderItemCache.Get(watchFolder, () => newWatchItems);

            var now = DateTime.UtcNow;

            foreach (var newWatchItem in GetDownloadItems(watchFolder).ToArray())
            {
                var oldWatchItem = lastWatchItems.GetValueOrDefault(newWatchItem.DownloadId);

                if (oldWatchItem != null && newWatchItem.Hash == oldWatchItem.Hash)
                {
                    newWatchItem.LastChanged = oldWatchItem.LastChanged;
                }
                else
                {
                    newWatchItem.LastChanged = now;
                }

                var remainingTime = gracePeriod - (now - newWatchItem.LastChanged);

                if (remainingTime > TimeSpan.Zero)
                {
                    newWatchItem.RemainingTime = remainingTime;
                    newWatchItem.Status = DownloadItemStatus.Downloading;
                }

                newWatchItems[newWatchItem.DownloadId] = newWatchItem;
            }

            _watchFolderItemCache.Set(watchFolder, newWatchItems, TimeSpan.FromMinutes(5));

            return newWatchItems.Values;
        }

        private IEnumerable<WatchFolderItem> GetDownloadItems(string watchFolder)
        {
            foreach (var folder in _diskProvider.GetDirectories(watchFolder))
            {
                var title = FileNameBuilder.CleanFileName(Path.GetFileName(folder));

                var files = _diskProvider.GetFiles(folder, SearchOption.AllDirectories);

                var historyItem = new WatchFolderItem
                {
                    DownloadId = Path.GetFileName(folder) + "_" + _diskProvider.FolderGetCreationTime(folder).Ticks,
                    Title = title,

                    TotalSize = files.Select(_diskProvider.GetFileSize).Sum(),

                    OutputPath = new OsPath(folder),
                    Hash = GetHash(folder, files)
                };

                if (files.Any(_diskProvider.IsFileLocked))
                {
                    historyItem.Status = DownloadItemStatus.Downloading;
                }
                else
                {
                    historyItem.Status = DownloadItemStatus.Completed;
                    historyItem.RemainingTime = TimeSpan.Zero;
                }
                
                yield return historyItem;
            }

            foreach (var videoFile in _diskScanService.GetVideoFiles(watchFolder, false))
            {
                var title = FileNameBuilder.CleanFileName(Path.GetFileName(videoFile));

                var historyItem = new WatchFolderItem
                {
                    DownloadId = Path.GetFileName(videoFile) + "_" + _diskProvider.FileGetLastWrite(videoFile).Ticks,
                    Title = title,

                    TotalSize = _diskProvider.GetFileSize(videoFile),

                    OutputPath = new OsPath(videoFile),
                    Hash = GetHash(videoFile)
                };

                if (_diskProvider.IsFileLocked(videoFile))
                {
                    historyItem.Status = DownloadItemStatus.Downloading;
                }
                else
                {
                    historyItem.Status = DownloadItemStatus.Completed;
                    historyItem.RemainingTime = TimeSpan.Zero;
                }

                yield return historyItem;
            }
        }

        private string GetHash(string folder, string[] files)
        {
            var data = new StringBuilder();

            data.Append(folder);
            try
            {
                data.Append(_diskProvider.FolderGetLastWrite(folder).ToBinary());
            }
            catch (Exception ex)
            {
                _logger.TraceException(string.Format("Ignored hashing error during scan for {0}", folder), ex);
            }

            foreach (var file in files.OrderBy(v => v))
            {
                data.Append(GetHash(file));
            }

            return HashConverter.GetHash(data.ToString()).ToHexString();
        }

        private string GetHash(string file)
        {
            var data = new StringBuilder();

            data.Append(file);
            try
            {
                data.Append(_diskProvider.FileGetLastWrite(file).ToBinary());
                data.Append(_diskProvider.GetFileSize(file));
            }
            catch (Exception ex)
            {
                _logger.TraceException(string.Format("Ignored hashing error during scan for {0}", file), ex);
            }

            return HashConverter.GetHash(data.ToString()).ToHexString();
        }
    }
}
