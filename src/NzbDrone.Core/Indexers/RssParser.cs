﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers
{
    public class RssParser : IParseIndexerResponse
    {
        private static readonly Regex ReplaceEntities = new Regex("&[a-z]+;", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected readonly Logger _logger;

        // Use the 'guid' element content as InfoUrl.
        public bool UseGuidInfoUrl { get; set; }

        // Use the enclosure as download url and/or length.
        public bool UseEnclosureUrl { get; set; }
        public bool UseEnclosureLength { get; set; }

        // Parse "Size: 1.3 GB" or "1.3 GB" parts in the description element and use that as Size.
        public bool ParseSizeInDescription { get; set; }

        private IndexerResponse _indexerResponse;

        public RssParser()
        {
            _logger = NzbDroneLogger.GetLogger(this);
        }

        public virtual IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            _indexerResponse = indexerResponse;

            var releases = new List<ReleaseInfo>();

            if (!PreProcess(indexerResponse))
            {
                return releases;
            }

            var document = LoadXmlDocument(indexerResponse);
            var items = GetItems(document);

            foreach (var item in items)
            {
                try
                {
                    var reportInfo = ProcessItem(item);

                    releases.AddIfNotNull(reportInfo);
                }
                catch (Exception itemEx)
                {
                    itemEx.Data.Add("Item", item.Title());
                    _logger.Error(itemEx, "An error occurred while processing feed item from " + indexerResponse.Request.Url);
                }
            }

            return releases;
        }

        protected virtual XDocument LoadXmlDocument(IndexerResponse indexerResponse)
        {
            try
            {
                var content = indexerResponse.Content;
                content = ReplaceEntities.Replace(content, ReplaceEntity);

                using (var xmlTextReader = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreComments = true }))
                {
                    return XDocument.Load(xmlTextReader);
                }
            }
            catch (XmlException ex)
            {
                var contentSample = indexerResponse.Content.Substring(0, Math.Min(indexerResponse.Content.Length, 512));
                _logger.Debug("Truncated response content (originally {0} characters): {1}", indexerResponse.Content.Length, contentSample);

                ex.Data.Add("ContentLength", indexerResponse.Content.Length);
                ex.Data.Add("ContentSample", contentSample);

                throw;
            }
        }

        protected virtual string ReplaceEntity(Match match)
        {
            try
            {
                var character = WebUtility.HtmlDecode(match.Value);
                return string.Concat("&#", (int)character[0], ";");
            }
            catch
            {
                return match.Value;
            }
        }

        protected virtual ReleaseInfo CreateNewReleaseInfo()
        {
            return new ReleaseInfo();
        }

        protected virtual bool PreProcess(IndexerResponse indexerResponse)
        {
            if (indexerResponse.HttpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new IndexerException(indexerResponse, "Indexer API call resulted in an unexpected StatusCode [{0}]", indexerResponse.HttpResponse.StatusCode);
            }

            if (indexerResponse.HttpResponse.Headers.ContentType != null && indexerResponse.HttpResponse.Headers.ContentType.Contains("text/html") &&
                indexerResponse.HttpRequest.Headers.Accept != null && !indexerResponse.HttpRequest.Headers.Accept.Contains("text/html"))
            {
                throw new IndexerException(indexerResponse, "Indexer responded with html content. Site is likely blocked or unavailable.");
            }

            return true;
        }

        protected ReleaseInfo ProcessItem(XElement item)
        {
            var releaseInfo = CreateNewReleaseInfo();

            releaseInfo = ProcessItem(item, releaseInfo);

            _logger.Trace("Parsed: {0}", releaseInfo.Title);

            return PostProcess(item, releaseInfo);
        }

        protected virtual ReleaseInfo ProcessItem(XElement item, ReleaseInfo releaseInfo)
        {
            releaseInfo.Guid = GetGuid(item);
            releaseInfo.Title = GetTitle(item);
            releaseInfo.PublishDate = GetPublishDate(item);
            releaseInfo.DownloadUrl = GetDownloadUrl(item);
            releaseInfo.InfoUrl = GetInfoUrl(item);
            releaseInfo.CommentUrl = GetCommentUrl(item);

            try
            {
                releaseInfo.Size = GetSize(item);
            }
            catch (Exception)
            {
                throw new SizeParsingException("Unable to parse size from: {0}", releaseInfo.Title);
            }

            return releaseInfo;
        }

        protected virtual ReleaseInfo PostProcess(XElement item, ReleaseInfo releaseInfo)
        {
            return releaseInfo;
        }

        protected virtual string GetGuid(XElement item)
        {
            return item.TryGetValue("guid", Guid.NewGuid().ToString());
        }

        protected virtual string GetTitle(XElement item)
        {
            return item.TryGetValue("title", "Unknown");
        }

        protected virtual DateTime GetPublishDate(XElement item)
        {
            var dateString = item.TryGetValue("pubDate");

            if (dateString.IsNullOrWhiteSpace())
            {
                throw new UnsupportedFeedException("Rss feed must have a pubDate element with a valid publish date.");
            }

            return XElementExtensions.ParseDate(dateString);
        }

        protected virtual string GetDownloadUrl(XElement item)
        {
            if (UseEnclosureUrl)
            {
                return ParseUrl((string)item.Element("enclosure").Attribute("url"));
            }

            return ParseUrl((string)item.Element("link"));
        }

        protected virtual string GetInfoUrl(XElement item)
        {
            if (UseGuidInfoUrl)
            {
                return ParseUrl((string)item.Element("guid"));
            }

            return null;
        }

        protected virtual string GetCommentUrl(XElement item)
        {
            return ParseUrl((string)item.Element("comments"));
        }

        protected virtual long GetSize(XElement item)
        {
            if (UseEnclosureLength)
            {
                return GetEnclosureLength(item);
            }
            if (ParseSizeInDescription && item.Element("description") != null)
            {
                return ParseSize(item.Element("description").Value, true);
            }

            return 0;
        }

        protected virtual long GetEnclosureLength(XElement item)
        {
            var enclosure = item.Element("enclosure");

            if (enclosure != null)
            {
                return (long)enclosure.Attribute("length");
            }

            return 0;
        }

        protected IEnumerable<XElement> GetItems(XDocument document)
        {
            var root = document.Root;

            if (root == null)
            {
                return Enumerable.Empty<XElement>();
            }

            var channel = root.Element("channel");

            if (channel == null)
            {
                return Enumerable.Empty<XElement>();
            }

            return channel.Elements("item");
        }

        protected virtual string ParseUrl(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                var uri = new Uri(value, UriKind.RelativeOrAbsolute);

                if (!uri.IsAbsoluteUri)
                {
                    uri = new Uri(_indexerResponse.HttpRequest.Url, uri);
                }

                return uri.AbsoluteUri;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, string.Format("Failed to parse Uri {0}, ignoring.", value));
                return null;
            }
        }

        private static readonly Regex ParseSizeRegex = new Regex(@"(?<value>(?:\d+,)*\d+(?:\.\d{1,2})?)\W?(?<unit>[KMG]i?B)(?![\w/])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static long ParseSize(string sizeString, bool defaultToBinaryPrefix)
        {
            if (sizeString.All(char.IsDigit))
            {
                return long.Parse(sizeString);
            }

            var match = ParseSizeRegex.Matches(sizeString);

            if (match.Count != 0)
            {
                var value = decimal.Parse(Regex.Replace(match[0].Groups["value"].Value, "\\,", ""), CultureInfo.InvariantCulture);

                var unit = match[0].Groups["unit"].Value.ToLower();

                switch (unit)
                {
                    case "kb":
                        return ConvertToBytes(Convert.ToDouble(value), 1, defaultToBinaryPrefix);
                    case "mb":
                        return ConvertToBytes(Convert.ToDouble(value), 2, defaultToBinaryPrefix);
                    case "gb":
                        return ConvertToBytes(Convert.ToDouble(value), 3, defaultToBinaryPrefix);
                    case "kib":
                        return ConvertToBytes(Convert.ToDouble(value), 1, true);
                    case "mib":
                        return ConvertToBytes(Convert.ToDouble(value), 2, true);
                    case "gib":
                        return ConvertToBytes(Convert.ToDouble(value), 3, true);
                    default:
                        return (long)value;
                }
            }
            return 0;
        }

        private static long ConvertToBytes(double value, int power, bool binaryPrefix)
        {
            var prefix = binaryPrefix ? 1024 : 1000;
            var multiplier = Math.Pow(prefix, power);
            var result = value * multiplier;

            return Convert.ToInt64(result);
        }
    }
}
