﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser
{
    public class QualityParser
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Regex SourceRegex = new Regex(@"\b(?:
                                                                (?<bluray>BluRay|Blu-Ray|HDDVD|BD)|
                                                                (?<webdl>WEB[-_. ]DL|WEBDL|WebRip|iTunesHD|WebHD)|
                                                                (?<hdtv>HDTV)|
                                                                (?<bdrip>BDRip)|
                                                                (?<brrip>BRRip)|
                                                                (?<dvd>DVD|DVDRip|NTSC|PAL|xvidvd)|
                                                                (?<dsr>WS[-_. ]DSR|DSR)|
                                                                (?<pdtv>PDTV)|
                                                                (?<sdtv>SDTV)|
                                                                (?<tvrip>TVRip)
                                                                )\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex RawHDRegex = new Regex(@"\b(?<rawhd>TrollHD|RawHD|1080i[-_. ]HDTV|Raw[-_. ]HD|MPEG[-_. ]?2)\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ProperRegex = new Regex(@"\b(?<proper>proper|repack)\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex VersionRegex = new Regex(@"\dv(?<version>\d)\b|\[v(?<version>\d)\]",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RealRegex = new Regex(@"\b(?<real>REAL)\b",
                                                                RegexOptions.Compiled);

        private static readonly Regex ResolutionRegex = new Regex(@"\b(?:(?<_480p>480p|640x480|848x480)|(?<_576p>576p)|(?<_720p>720p|1280x720)|(?<_1080p>1080p|1920x1080)|(?<_2160p>2160p))\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CodecRegex = new Regex(@"\b(?:(?<x264>x264)|(?<h264>h264)|(?<xvidhd>XvidHD)|(?<xvid>Xvid)|(?<divx>divx))\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OtherSourceRegex = new Regex(@"(?<hdtv>HD[-_. ]TV)|(?<sdtv>SD[-_. ]TV)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AnimeBlurayRegex = new Regex(@"bd(?:720|1080)|(?<=[-_. (\[])bd(?=[-_. )\]])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HighDefPdtvRegex = new Regex(@"hr[-_. ]ws", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static QualityModel ParseQuality(string name)
        {
            Logger.Debug("Trying to parse quality for {0}", name);

            name = name.Trim();
            var normalizedName = name.Replace('_', ' ').Trim().ToLower();
            var result = ParseQualityModifiers(name, normalizedName);

            if (RawHDRegex.IsMatch(normalizedName))
            {
                result.Quality = Quality.RAWHD;
                return result;
            }
            
            var sourceMatch = SourceRegex.Match(normalizedName);
            var resolution = ParseResolution(normalizedName);
            var codecRegex = CodecRegex.Match(normalizedName);

            if (sourceMatch.Groups["bluray"].Success)
            {
                if (codecRegex.Groups["xvid"].Success || codecRegex.Groups["divx"].Success)
                {
                    result.Quality = Quality.DVD;
                    return result;
                }

                if (resolution == Resolution._2160p)
                {
                    result.Quality = Quality.Bluray2160p;
                    return result;
                }

                if (resolution == Resolution._1080p)
                {
                    result.Quality = Quality.Bluray1080p;
                    return result;
                }

                if (resolution == Resolution._480p || resolution == Resolution._576p)
                {
                    result.Quality = Quality.DVD;
                    return result;
                }

                result.Quality = Quality.Bluray720p;
                return result;
            }

            if (sourceMatch.Groups["webdl"].Success)
            {
                if (resolution == Resolution._2160p)
                {
                    result.Quality = Quality.WEBDL2160p;
                    return result;
                }

                if (resolution == Resolution._1080p)
                {
                    result.Quality = Quality.WEBDL1080p;
                    return result;
                }

                if (resolution == Resolution._720p)
                {
                    result.Quality = Quality.WEBDL720p;
                    return result;
                }

                if (name.Contains("[WEBDL]"))
                {
                    result.Quality = Quality.WEBDL720p;
                    return result;
                }

                result.Quality = Quality.WEBDL480p;
                return result;
            }

            if (sourceMatch.Groups["hdtv"].Success)
            {
                if (resolution == Resolution._2160p)
                {
                    result.Quality = Quality.HDTV2160p;
                    return result;
                }

                if (resolution == Resolution._1080p)
                {
                    result.Quality = Quality.HDTV1080p;
                    return result;
                }

                if (resolution == Resolution._720p)
                {
                    result.Quality = Quality.HDTV720p;
                    return result;
                }

                if (name.Contains("[HDTV]"))
                {
                    result.Quality = Quality.HDTV720p;
                    return result;
                }

                result.Quality = Quality.SDTV;
                return result;
            }

            if (sourceMatch.Groups["bdrip"].Success ||
                sourceMatch.Groups["brrip"].Success)
            {
                switch (resolution)
                {
                    case Resolution._720p:
                        result.Quality = Quality.Bluray720p;
                        return result;
                    case Resolution._1080p:
                        result.Quality = Quality.Bluray1080p;
                        return result;
                    default:
                        result.Quality = Quality.DVD;
                        return result;
                }
            }

            if (sourceMatch.Groups["dvd"].Success)
            {
                result.Quality = Quality.DVD;
                return result;
            }

            if (sourceMatch.Groups["pdtv"].Success ||
                sourceMatch.Groups["sdtv"].Success ||
                sourceMatch.Groups["dsr"].Success ||
                sourceMatch.Groups["tvrip"].Success)
            {
                if (HighDefPdtvRegex.IsMatch(normalizedName))
                {
                    result.Quality = Quality.HDTV720p;
                    return result;
                }

                result.Quality = Quality.SDTV;
                return result;
            }


            //Anime Bluray matching
            if (AnimeBlurayRegex.Match(normalizedName).Success)
            {
                if (resolution == Resolution._480p || resolution == Resolution._576p || normalizedName.Contains("480p"))
                {
                    result.Quality = Quality.DVD;
                    return result;
                }

                if (resolution == Resolution._1080p || normalizedName.Contains("1080p"))
                {
                    result.Quality = Quality.Bluray1080p;
                    return result;
                }

                result.Quality = Quality.Bluray720p;
                return result;
            }

            if (resolution == Resolution._2160p)
            {
                result.Quality = Quality.HDTV2160p;
                return result;
            }

            if (resolution == Resolution._1080p)
            {
                result.Quality = Quality.HDTV1080p;
                return result;
            }

            if (resolution == Resolution._720p)
            {
                result.Quality = Quality.HDTV720p;
                return result;
            }

            if (resolution == Resolution._480p)
            {
                result.Quality = Quality.SDTV;
                return result;
            }

            if (codecRegex.Groups["x264"].Success)
            {
                result.Quality = Quality.SDTV;
                return result;
            }

            if (normalizedName.Contains("848x480"))
            {
                if (normalizedName.Contains("dvd"))
                {
                    result.Quality = Quality.DVD;
                }

                result.Quality = Quality.SDTV;
            }

            if (normalizedName.Contains("1280x720"))
            {
                if (normalizedName.Contains("bluray"))
                {
                    result.Quality = Quality.Bluray720p;
                }

                result.Quality = Quality.HDTV720p;
            }

            if (normalizedName.Contains("1920x1080"))
            {
                if (normalizedName.Contains("bluray"))
                {
                    result.Quality = Quality.Bluray1080p;
                }

                result.Quality = Quality.HDTV1080p;
            }

            if (normalizedName.Contains("bluray720p"))
            {
                result.Quality = Quality.Bluray720p;
            }

            if (normalizedName.Contains("bluray1080p"))
            {
                result.Quality = Quality.Bluray1080p;
            }

            var otherSourceMatch = OtherSourceMatch(normalizedName);

            if (otherSourceMatch != Quality.Unknown)
            {
                result.Quality = otherSourceMatch;
            }

            //Based on extension
            if (result.Quality == Quality.Unknown && !name.ContainsInvalidPathChars())
            {
                try
                {
                    result.Quality = MediaFileExtensions.GetQualityForExtension(Path.GetExtension(name));
                    result.QualitySource = QualitySource.Extension;
                }
                catch (ArgumentException)
                {
                    //Swallow exception for cases where string contains illegal 
                    //path characters.
                }
            }

            return result;
        }

        private static Resolution ParseResolution(string name)
        {
            var match = ResolutionRegex.Match(name);

            if (!match.Success) return Resolution.Unknown;
            if (match.Groups["_480p"].Success) return Resolution._480p;
            if (match.Groups["_576p"].Success) return Resolution._576p;
            if (match.Groups["_720p"].Success) return Resolution._720p;
            if (match.Groups["_1080p"].Success) return Resolution._1080p;
            if (match.Groups["_2160p"].Success) return Resolution._2160p;

            return Resolution.Unknown;
        }

        private static Quality OtherSourceMatch(string name)
        {
            var match = OtherSourceRegex.Match(name);

            if (!match.Success) return Quality.Unknown;
            if (match.Groups["sdtv"].Success) return Quality.SDTV;
            if (match.Groups["hdtv"].Success) return Quality.HDTV720p;

            return Quality.Unknown;
        }

        private static QualityModel ParseQualityModifiers(string name, string normalizedName)
        {
            var result = new QualityModel { Quality = Quality.Unknown };

            if (ProperRegex.IsMatch(normalizedName))
            {
                result.Revision.Version = 2;
            }

            var versionRegexResult = VersionRegex.Match(normalizedName);

            if (versionRegexResult.Success)
            {
                result.Revision.Version = Convert.ToInt32(versionRegexResult.Groups["version"].Value);
            }

            //TODO: re-enable this when we have a reliable way to determine real
            //TODO: Only treat it as a real if it comes AFTER the season/epsiode number
            var realRegexResult = RealRegex.Matches(name);

            if (realRegexResult.Count > 0)
            {
                result.Revision.Real = realRegexResult.Count;
            }

            return result;
        }
    }

    public enum Resolution
    {
        _480p,
        _576p,
        _720p,
        _1080p,
        _2160p,
        Unknown
    }
}
