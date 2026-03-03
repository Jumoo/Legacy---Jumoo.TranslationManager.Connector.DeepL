using HtmlAgilityPack;

using Jumoo.TranslationManager.Core;
using Jumoo.TranslationManager.Core.Configuration;
using Jumoo.TranslationManager.Core.Models;
using Jumoo.TranslationManager.Core.Providers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jumoo.TranslationManager.Utilities;
using System.Globalization;
using DeepL;

#if NETCOREAPP
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Extensions;
#else
using Umbraco.Core;
using Jumoo.TranslationManager.Logging;
#endif

namespace Jumoo.TranslationManager.Connector.DeepL
{
    public class DeepLConnector : TranslateProviderBase, ITranslationProvider
    {
        private readonly TranslationConfigService _configService;

        public DeepLConnector(
            TranslationConfigService configService,
            ILogger<TranslateProviderBase> logger) 
            : base(logger)
        {
            _configService = configService;
            Reload();
        }

        public string Name => "DeepL Translation Connector v2 (Machine)";
        public string Alias => "deepl";
        public Guid Key => Guid.Parse("B5F17527-3CC6-4C49-A95E-E852541B4167");


        private string _apiKey;
        private bool _useFree;
        private int _throttle;
        private bool _split;
        private bool _asHtml;


        private DeepLTranslationService translatorService;

        public TranslationProviderViews Views => new TranslationProviderViews
        {
            Config = TranslateUriUtility.ToAbsolute("/App_Plugins/TranslationConnector.DeepL/config.html"),
            Pending = TranslateUriUtility.ToAbsolute("/App_Plugins/TranslationConnector.DeepL/pending.html")

        };

        public async Task<Attempt<TranslationJob>> Submit(TranslationJob job)
        {
            if (!Active())
                throw new Exception("No Api Key Set");

            // calculate what languages we can use to do the translation here....
            var supportedSource = (await translatorService.GetLanguagesAsync(LanguageType.Source)).ToList();
            var supportedTarget = (await translatorService.GetLanguagesAsync(LanguageType.Target)).ToList();

            var sourceLang = GetSupportedLanguageCode(job.SourceCulture, supportedSource);
            var targetLang = GetSupportedLanguageCode(job.TargetCulture, supportedTarget);

            foreach(var node in job.Nodes)
            {
                foreach(var group in node.Groups)
                {
                    foreach(var property in group.Properties)
                    {

                        var result = await GetTranslatedValue(property.Source, property.Target, sourceLang, targetLang);
                        if (result == null)
                            return Attempt<TranslationJob>.Fail(new Exception("No values translated"));

                        property.Target = result;
                    }

                    if (_throttle > 0)
                    {
                        _logger.LogDebug("Throttle: Waiting... {0}ms", _throttle);
                        await Task.Delay(_throttle);
                    }
                }
            }

            job.Status = JobStatus.Received;
            return Attempt<TranslationJob>.Succeed(job);
        }

        private async Task<TranslationValue> GetTranslatedValue(TranslationValue source, TranslationValue target, string sourceLang, string targetLang)
        {
            _logger.LogDebug("GetTranslatedValue: {0}", source.DisplayName);

            if (source.HasChildValues())
            {
                foreach (var innerValue in source.InnerValues)
                {
                    _logger.LogDebug("GetTranslatedValue: Child Value {0}", innerValue.Key);

                    var innerTarget = target.GetInnerValue(innerValue.Key);
                    if (innerTarget == null)
                    {
                        _logger.LogWarning("No inner target (bad setup)");
                        continue;
                    }

                    var translatedValue = await GetTranslatedValue(innerValue.Value, innerTarget, sourceLang, targetLang);
                    if (translatedValue != null)
                        innerTarget = translatedValue;
                }
            }

            if (!string.IsNullOrWhiteSpace(source.Value))
            {
                _logger.LogDebug("Translating [{0}]", source.Value);

                // has a value to translate 
                if (_split)
                {
                    _logger.LogDebug("Splitting");

                    // if it's html, we split it up. 
                    target.Value = await TranslateHtmlValue(source.Value, sourceLang, targetLang);
                    target.Translated = true;
                }
                else
                {
                    _logger.LogDebug("Not splitting, treating as single string");

                    target.Value = await TranslateStringValue(source.Value, sourceLang, targetLang);
                    target.Translated = true;
                }

            }

            return target;
        }

        /// <summary>
        ///  translate the text as if it's html, 
        ///  We split by top level node (so hopefully paragraphs)
        ///  and return that 
        /// </summary>
        private async Task<string> TranslateHtmlValue(string source, string sourceLang, string targetLang)
        {
            if (!IsHtml(source))
                return await TranslateStringValue(source, sourceLang, targetLang);

            var doc = new HtmlDocument();
            doc.LoadHtml(source);

            return await TranslateHtmlNodes(doc.DocumentNode.ChildNodes, sourceLang, targetLang);
        }

        private async Task<string> TranslateHtmlNodes(HtmlNodeCollection nodes, string sourceLang, string targetLang)
        {
            _logger.LogDebug("Treating as Html");

            var result = "";

            List<string> values = new List<string>();

            foreach (var node in nodes)
            {
                var value = node.OuterHtml;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (value.Length > 5000)
                    {
                        if (node.HasChildNodes)
                        {
                            // if we get here then the bulk values up and send process won't work.
                            // (because we need to know that the translations are wrapped in a html tag
                            // we haven't sent to translation)

                            // we have to send what we have already done to translation, 
                            result += await TranslateStringValues(values, sourceLang, targetLang, _asHtml);

                            // and then send this block to translation
                            var translatedResult = await TranslateHtmlNodes(node.ChildNodes, sourceLang, targetLang);
                            result += $"<{node.Name}>{translatedResult}<{node.Name}>";

                            // and then resume adding things to a now empty list, 
                            values.Clear();
                        }
                        else
                        {
                            _logger.LogWarning("Splitting single html element that spans more than 5000 charecters. " +
                                "This is larger than the request limit, splitting may result in some issues with translation.");

                            // we attempt to split the tag, we also wrap it in the nodeName, to make it fit
                            var innerValue = node.InnerHtml;

                            // take the tag name and the braces (< > < / > ) from the 5000 budget. 
                            var size = 4995 - (node.Name.Length * 2);
                            values.AddRange(Split(innerValue, size, node.Name));
                        }
                    }
                    else
                    {
                        values.Add(value);
                    }
                }
            }

            if (values.Count > 0)
            {
                result += await TranslateStringValues(values, sourceLang, targetLang, _asHtml);
            }

            return result;
        }

        /// <summary>
        ///  translates a string using the api, we assume the string isn't anything
        ///  fancy, and if it's super long, we just hard split it at 5000 chars
        /// </summary>
        private async Task<string> TranslateStringValue(string source, string sourceLang, string targetLang)
        {
            var values = Split(source, 5000);
            return await TranslateStringValues(values, sourceLang, targetLang, false);
        }

        private List<string> GetBlocks(IList<string> values, int start, out int end)
        {
            int pos = start;
            var block = new List<string>();
            var length = 0;
            while (pos < values.Count && block.Count < 25)
            {
                length += values[pos].Length;
                if (length < 5000)
                {
                    block.Add(values[pos]);
                }
                else
                {
                    break;
                }
                pos++;
            }

            end = pos;

            return block;
        }

        private async Task<string> TranslateStringValues(IEnumerable<string> values, string sourceLang, string targetLang, bool isHtml)
        {
            _logger.LogDebug("Translating: {count} values", values.Count());

            var valueList = values.ToList(); ;

            var translatedText = new StringBuilder();

            _logger.LogDebug("Splitting: {count} values", valueList.Count);
            int end = 0;
            while (end < valueList.Count)
            {
                var block = GetBlocks(valueList, end, out end).ToList();

                if (block.Count > 0)
                {
                    _logger.LogDebug("Blocks : {count}, {end}", block.Count, end);
                    _logger.LogDebug("Translating: {count} as one chunk", block.Count);
                    _logger.LogDebug("Chunks: {blocks}", string.Join("\r\n", block));

                    foreach (var b in block)
                    {
                        _logger.LogDebug("Chunk {length}", b.Length);
                    }

                    var translated = await translatorService.Translate(block, sourceLang, targetLang, isHtml);
                    var text = translated.Select(x => x.Text);

                    _logger.LogDebug("Returned: {count} translated values", text.Count());

                    translatedText.Append(string.Join("", text));
                }
                else
                {
                    _logger.LogDebug("Empty Block");
                    break;
                }
            }

            _logger.LogDebug("Translated: {translated}", translatedText.ToString());
            return translatedText.ToString();
        }


        private IEnumerable<string> Split(string str, int maxChunkSize, string surroundingTag = "")
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
            {
                var chunk = str.Substring(i, Math.Min(maxChunkSize, str.Length - i));

                if (!string.IsNullOrWhiteSpace(surroundingTag))
                {
                    yield return $"<{surroundingTag}>{chunk}</{surroundingTag}>";
                }
                else
                {
                    yield return chunk;
                }
            }
        }

        private bool IsHtml(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(text);
                return !doc.DocumentNode.ChildNodes.All(x => x.NodeType == HtmlNodeType.Text);
            }

            return false;
        }


               private static string GetSupportedLanguageCode(CultureInfoView culture, IList<DeepLLangInfo> supported)
        {
            if (supported.Any(x => x.Code.InvariantEquals(culture.Name)))
                return culture.Name;

            if (supported.Any(x => x.Code.InvariantEquals(culture.TwoLetterISOLanguageName)))
                return culture.TwoLetterISOLanguageName;

            throw new NotSupportedException($"Cannot use DeepL for {culture.Name} you might need to add a language mapping in the DeepL connector settings");
        }

        public bool CanTranslate(TranslationJob job)
        {
            var languages = translatorService.GetLanguagesAsync(LanguageType.Target).Result;
            return languages.Any(x => x.Name.InvariantEquals(job.TargetCulture.Name));
        }

        public IEnumerable<string> GetTargetLanguages(string sourceLanguage)
        {
            var languages = translatorService.GetLanguagesAsync(LanguageType.Target).Result;
            return languages.Select(x => x.Name);
        }

        public void Reload()
        {
            var config = _configService;

            _apiKey = config.GetProviderSetting(this.Alias, "apiKey", string.Empty);
            _useFree = config.GetProviderSetting(this.Alias, "useFree", true);

            _throttle = config.GetProviderSetting(this.Alias, "throttle", 0);
            _split = config.GetProviderSetting(this.Alias, "split", true);
            _asHtml = config.GetProviderSetting(this.Alias, "asHtml", true);

            translatorService = new DeepLTranslationService(_apiKey, _useFree);
        }

        public bool Active()
            => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<Attempt<TranslationJob>> Remove(TranslationJob job)
            => await Task.FromResult(Attempt<TranslationJob>.Succeed(job));


        public async Task<Attempt<TranslationJob>> Cancel(TranslationJob job)
            => await Task.FromResult(Attempt<TranslationJob>.Succeed(job));


        public async Task<Attempt<TranslationJob>> Check(TranslationJob job)
            => await Task.FromResult(Attempt<TranslationJob>.Succeed(job));
    }
}
