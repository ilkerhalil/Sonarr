using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Core.Parser
{
    public static class LanguageParser
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(LanguageParser));

        private static readonly Regex LanguageRegex = new Regex(@"(?:\W|_)(?<italian>\b(?:ita|italian)\b)|(?<german>german\b|videomann)|(?<flemish>flemish)|(?<greek>greek)|(?<french>(?:\W|_)(?:FR|VOSTFR)(?:\W|_))|(?<russian>\brus\b)|(?<dutch>nl\W?subs?)|(?<hungarian>\b(?:HUNDUB|HUN)\b)",
                                                                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SubtitleLanguageRegex = new Regex(".+?[-_. ](?<iso_code>[a-z]{2,3})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<IsoLanguage> IsoLanguages = new HashSet<IsoLanguage>
                                                                {
                                                                    new IsoLanguage("en", "eng", Language.English),
                                                                    new IsoLanguage("fr", "fra", Language.French),
                                                                    new IsoLanguage("es", "spa", Language.Spanish),
                                                                    new IsoLanguage("de", "deu", Language.German),
                                                                    new IsoLanguage("it", "ita", Language.Italian),
                                                                    new IsoLanguage("da", "dan", Language.Danish),
                                                                    new IsoLanguage("nl", "nld", Language.Dutch),
                                                                    new IsoLanguage("ja", "jpn", Language.Japanese),
//                                                                    new IsoLanguage("", "", Language.Cantonese),
//                                                                    new IsoLanguage("", "", Language.Mandarin),
                                                                    new IsoLanguage("ru", "rus", Language.Russian),
                                                                    new IsoLanguage("pl", "pol", Language.Polish),
                                                                    new IsoLanguage("vi", "vie", Language.Vietnamese),
                                                                    new IsoLanguage("sv", "swe", Language.Swedish),
                                                                    new IsoLanguage("no", "nor", Language.Norwegian),
                                                                    new IsoLanguage("fi", "fin", Language.Finnish),
                                                                    new IsoLanguage("tr", "tur", Language.Turkish),
                                                                    new IsoLanguage("pt", "por", Language.Portuguese),
//                                                                    new IsoLanguage("nl", "nld", Language.Flemish),
                                                                    new IsoLanguage("el", "ell", Language.Greek),
                                                                    new IsoLanguage("ko", "kor", Language.Korean),
                                                                    new IsoLanguage("hu", "hun", Language.Hungarian)
                                                                };

        public static Language ParseLanguage(string title)
        {
            var lowerTitle = title.ToLower();

            if (lowerTitle.Contains("english"))
                return Language.English;

            if (lowerTitle.Contains("french"))
                return Language.French;

            if (lowerTitle.Contains("spanish"))
                return Language.Spanish;

            if (lowerTitle.Contains("danish"))
                return Language.Danish;

            if (lowerTitle.Contains("dutch"))
                return Language.Dutch;

            if (lowerTitle.Contains("japanese"))
                return Language.Japanese;

            if (lowerTitle.Contains("cantonese"))
                return Language.Cantonese;

            if (lowerTitle.Contains("mandarin"))
                return Language.Mandarin;

            if (lowerTitle.Contains("korean"))
                return Language.Korean;

            if (lowerTitle.Contains("russian"))
                return Language.Russian;

            if (lowerTitle.Contains("polish"))
                return Language.Polish;

            if (lowerTitle.Contains("vietnamese"))
                return Language.Vietnamese;

            if (lowerTitle.Contains("swedish"))
                return Language.Swedish;

            if (lowerTitle.Contains("norwegian"))
                return Language.Norwegian;

            if (lowerTitle.Contains("nordic"))
                return Language.Norwegian;

            if (lowerTitle.Contains("finnish"))
                return Language.Finnish;

            if (lowerTitle.Contains("turkish"))
                return Language.Turkish;

            if (lowerTitle.Contains("portuguese"))
                return Language.Portuguese;

            if (lowerTitle.Contains("hungarian"))
                return Language.Hungarian;

            var match = LanguageRegex.Match(title);

            if (match.Groups["italian"].Captures.Cast<Capture>().Any())
                return Language.Italian;

            if (match.Groups["german"].Captures.Cast<Capture>().Any())
                return Language.German;

            if (match.Groups["flemish"].Captures.Cast<Capture>().Any())
                return Language.Flemish;

            if (match.Groups["greek"].Captures.Cast<Capture>().Any())
                return Language.Greek;

            if (match.Groups["french"].Success)
                return Language.French;

            if (match.Groups["russian"].Success)
                return Language.Russian;

            if (match.Groups["dutch"].Success)
                return Language.Dutch;

            if (match.Groups["hungarian"].Success)
                return Language.Hungarian;

            return Language.English;
        }

        public static Language ParseSubtitleLanguage(string fileName)
        {
            try
            {
                Logger.Debug("Parsing language from subtitlte file: {0}", fileName);

                var simpleFilename = Path.GetFileNameWithoutExtension(fileName);
                var languageMatch = SubtitleLanguageRegex.Match(simpleFilename);

                if (languageMatch.Success)
                {
                    var isoCode = languageMatch.Groups["iso_code"].Value;
                    IsoLanguage isoLanguage = null;

                    if (isoCode.Length == 2)
                    {
                        //Lookup ISO639-1 code
                        isoLanguage = IsoLanguages.SingleOrDefault(l => l.TwoLetterCode == isoCode);
                    }
                    else if (isoCode.Length == 3)
                    {
                        //Lookup ISO639-2T code
                        isoLanguage = IsoLanguages.SingleOrDefault(l => l.ThreeLetterCode == isoCode);
                    }

                    if (isoLanguage != null)
                    {
                        return isoLanguage.Language;
                    }
                }

                Logger.Debug("Unable to parse langauge from subtitle file: {0}", fileName);
            }
            catch (Exception ex)
            {
                Logger.Debug("Failed parsing langauge from subtitle file: {0}", fileName);
            }
            
            return Language.Unknown;
        }
    }
}
