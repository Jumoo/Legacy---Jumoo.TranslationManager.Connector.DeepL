using DeepL;
using DeepL.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jumoo.TranslationManager.Connector.DeepL
{
    public class DeepLTranslationService
    {

        private readonly string _key = "";
        // private readonly string _host = "https://api-free.deepl.com/v2/";
        private readonly bool _useFree = true;

        private static AppInfo _appInfo = new AppInfo()
        {
            AppName = "Jumoo.TranslationManager.DeepL",
            AppVersion = typeof(DeepLTranslationService).Assembly.GetName().Version.ToString(3) ?? "9.0.0",
        };

        public DeepLTranslationService(string key, bool useFree)
        {
            _key = key;
            _useFree = useFree;
        }

        private async Task<DeepLClient> GetClientAsync()
        {
            if (string.IsNullOrEmpty(_key)) return null;

            return new DeepLClient(_key, new DeepLClientOptions
            {
                appInfo = _appInfo,
            });
        }

        public async Task<IEnumerable<DeepLLangInfo>> GetLanguagesAsync(LanguageType type)
        {
            using (var client = await GetClientAsync())
            {
                var languages = type == LanguageType.Target
                    ? (await client.GetTargetLanguagesAsync()).Select(x => new DeepLLangInfo
                    {
                        Code = x.Code,
                        Name = x.Name,
                    })
                    : (await client.GetSourceLanguagesAsync()).Select(x => new DeepLLangInfo
                    {
                        Code = x.Code,
                        Name = x.Name,
                    });

                return languages.Select(x => new DeepLLangInfo { Name = x.Name, Code = x.Code });
            }
        }

        public async Task<IEnumerable<TextResult>> Translate(IEnumerable<string> input, string sourceLanguage, string targetLanguage, bool isHtml)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));



            using (var client = await GetClientAsync())
            {
                return await client.TranslateTextAsync(input,
                    sourceLanguage,
                    targetLanguage);
            }

        }
    }

    public class DeepLLangInfo
    {
        public string Name { get; set; }
        public string Code { get; set; }
    }

    public enum LanguageType
    {
        Source,
        Target
    }

}
