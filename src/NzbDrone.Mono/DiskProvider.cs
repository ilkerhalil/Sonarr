﻿using System;
using System.IO;
using System.Linq;
using Mono.Unix;
using Mono.Unix.Native;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Mono
{
    public class DiskProvider : DiskProviderBase
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(DiskProvider));

        private readonly IProcMountProvider _procMountProvider;
        private readonly ISymbolicLinkResolver _symLinkResolver;

        public DiskProvider(IProcMountProvider procMountProvider, ISymbolicLinkResolver symLinkResolver)
        {
            _procMountProvider = procMountProvider;
            _symLinkResolver = symLinkResolver;
        }

        public override IMount GetMount(string path)
        {
            path = _symLinkResolver.GetCompleteRealPath(path);

            return base.GetMount(path);
        }

        public override long? GetAvailableSpace(string path)
        {
            Ensure.That(path, () => path).IsValidPath();

            try
            {
                var mount = GetMount(path);

                if (mount == null)
                {
                    Logger.Debug("Unable to get free space for '{0}', unable to find suitable drive", path);
                    return null;
                }

                return mount.AvailableFreeSpace;
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error(ex, "Couldn't get free space for " + path);
            }

            return null;
        }

        public override void InheritFolderPermissions(string filename)
        {
            Ensure.That(filename, () => filename).IsValidPath();

            try
            {
                var fs = File.GetAccessControl(filename);
                fs.SetAccessRuleProtection(false, false);
                File.SetAccessControl(filename, fs);
            }
            catch (NotImplementedException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }
        }

        public override void SetPermissions(string path, string mask, string user, string group)
        {
            Logger.Debug("Setting permissions: {0} on {1}", mask, path);

            var filePermissions = NativeConvert.FromOctalPermissionString(mask);

            if (Syscall.chmod(path, filePermissions) < 0)
            {
                var error = Stdlib.GetLastError();

                throw new LinuxPermissionsException("Error setting file permissions: " + error);
            }

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(group))
            {
                Logger.Debug("User or Group for chown not configured, skipping chown.");
                return;
            }

            uint userId;
            uint groupId;
            
            if (!uint.TryParse(user, out userId))
            {
                var u = Syscall.getpwnam(user);

                if (u == null)
                {
                    throw new LinuxPermissionsException("Unknown user: {0}", user);
                }

                userId = u.pw_uid;
            }

            if (!uint.TryParse(group, out groupId))
            {
                var g = Syscall.getgrnam(group);

                if (g == null)
                {
                    throw new LinuxPermissionsException("Unknown group: {0}", group);
                }

                groupId = g.gr_gid;
            }

            if (Syscall.chown(path, userId, groupId) < 0)
            {
                var error = Stdlib.GetLastError();

                throw new LinuxPermissionsException("Error setting file owner and/or group: " + error);
            }
        }

        public override System.Collections.Generic.List<IMount> GetMounts()
        {
            return base.GetMounts()
                       .Concat(_procMountProvider.GetMounts())
                       .DistinctBy(v => v.RootDirectory)
                       .ToList();
        }

        public override long? GetTotalSize(string path)
        {
            Ensure.That(path, () => path).IsValidPath();

            try
            {
                var mount = GetMount(path);

                if (mount == null) return null;

                return mount.TotalSize;
            }
            catch (InvalidOperationException e)
            {
                Logger.Error(e, "Couldn't get total space for " + path);
            }

            return null;
        }

        public override bool TryCreateHardLink(string source, string destination)
        {
            try
            {
                UnixFileSystemInfo.GetFileSystemEntry(source).CreateLink(destination);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, string.Format("Hardlink '{0}' to '{1}' failed.", source, destination));
                return false;
            }
        }
    }
}
