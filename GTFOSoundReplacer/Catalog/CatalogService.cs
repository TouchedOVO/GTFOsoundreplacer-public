using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GTFOSoundReplacer.UI;

namespace GTFOSoundReplacer.Catalog
{
    public static class CatalogService
    {
        private static volatile SoundCatalog _current = new SoundCatalog();
        private static volatile string _status = "音频目录尚未初始化";
        private static int _started;
        public static SoundCatalog Current => _current;
        public static string Status => _status;

        public static void Start(string root, string cacheFolder, string translationUrl, Action<string> info, Action<string> warn)
        {
            if (Interlocked.Exchange(ref _started, 1) != 0) return;
            _status = "正在建立音频目录…";
            _ = Task.Run(async () =>
            {
                try { await Initialize(root, cacheFolder, translationUrl, info, warn).ConfigureAwait(false); }
                catch (Exception e) { _status = "音频目录初始化失败；已有替换仍可使用"; warn("Catalog initialization: " + e); }
            });
        }

        internal static async Task Initialize(string root, string folder, string url, Action<string> info, Action<string> warn, Func<string, Task<byte[]>> fetch = null)
        {
            long start = Stopwatch.GetTimestamp();
            bool writable = true;
            try { Directory.CreateDirectory(folder); }
            catch (Exception e) { writable = false; warn("Catalog cache directory unavailable: " + e.Message); }
            string cachePath = Path.Combine(folder, "events.cache.json");
            string translationPath = Path.Combine(folder, "zh-CN.json");
            root = Path.GetFullPath(root);
            CatalogDocument previous = null;
            try { if (File.Exists(cachePath)) previous = CatalogFiles.LoadCache(cachePath, root); }
            catch (Exception e) { warn("Ignoring invalid catalog cache: " + e.Message); }
            CatalogDocument doc;
            bool reused = false, stale = false;
            try
            {
                doc = CatalogFiles.Scan(root, previous, warn, out reused);
                if (!reused && writable)
                    try { CatalogFiles.WriteCache(cachePath, doc); }
                    catch (Exception e) { warn("Catalog cache write failed; using memory result: " + e.Message); }
            }
            catch (Exception e)
            {
                warn("Catalog scan failed: " + e.Message);
                doc = previous;
                stale = doc != null;
            }
            if (doc == null)
            {
                _status = "无法读取音频目录；已有替换仍可使用";
                return;
            }
            Dictionary<uint, Translation> translations = null;
            try { if (File.Exists(translationPath)) translations = CatalogFiles.ParseTranslations(CatalogFiles.ReadBounded(translationPath)); }
            catch (Exception e) { warn("Ignoring invalid local translation: " + e.Message); }
            _current = new SoundCatalog(doc, translations);
            string baseStatus = (stale ? "音频目录使用旧缓存（本轮读取失败）" : "音频目录已就绪") + " · " + doc.Events.Count + " 项";
            _status = baseStatus + (translations == null ? " · 中文映射未就绪" : " · 已读取本地中文映射");
            long end = Stopwatch.GetTimestamp();
            info("[CATALOG] startTick=" + start + " endTick=" + end + " elapsedTicks=" + (end - start) +
                 " frequency=" + Stopwatch.Frequency + " ms=" + ((end - start) * 1000.0 / Stopwatch.Frequency).ToString("F3") +
                 " banks=" + doc.Banks.Count + " uniqueEvents=" + doc.Events.Count + " cacheReused=" + reused + " stale=" + stale);
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                byte[] bytes = await (fetch == null ? Download(url) : fetch(url)).ConfigureAwait(false);
                var downloaded = CatalogFiles.ParseTranslations(bytes);
                var next = new SoundCatalog(doc, downloaded);
                if (writable)
                    try { CatalogFiles.AtomicWrite(translationPath, bytes); }
                    catch (Exception e) { warn("Translation cache write failed; using memory result: " + e.Message); }
                _current = next;
                _status = baseStatus + " · 中文映射已更新";
                info("[CATALOG] Translation updated, entries=" + downloaded.Count);
            }
            catch (Exception e)
            {
                warn("Translation download failed; keeping local names/translations: " + e.Message);
                _status += " · 下载失败，已保留本地数据";
            }
        }

        public static async Task<byte[]> Download(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidDataException("Translation URL must be HTTPS without credentials");
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancel.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > CatalogFiles.MaxJsonBytes) throw new InvalidDataException("Translation download too large");
            using var input = await response.Content.ReadAsStreamAsync(cancel.Token).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[16384];
            int count;
            while ((count = await input.ReadAsync(buffer.AsMemory(), cancel.Token).ConfigureAwait(false)) != 0)
            {
                if (output.Length + count > CatalogFiles.MaxJsonBytes) throw new InvalidDataException("Translation download too large");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
    }
}
