using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public abstract class Cache
    {
        private const string FilenamePrefix = "mm-st-cached-heights-";
        private readonly Dictionary<string, KeyValuePair<string, object>> genericCache = new Dictionary<string, KeyValuePair<string, object>>();
        private readonly Dictionary<KeyValuePair<string, string>, string> diskCachedFiles = new Dictionary<KeyValuePair<string, string>, string>();

        [NonSerialized] protected long memoryCachedBytes = 0;
        [NonSerialized] protected long diskCachedBytes = 0;

        public long MemoryCachedMegaBytes
        {
            get { return memoryCachedBytes / 1000000; }
        }

        public long DiskCachedMegaBytes
        {
            get { return diskCachedBytes / 1000000; }
        }

        protected abstract void CleanupOrSpillOldCacheEntries();

        public void Reset(bool resetCache)
        {
            if (resetCache) ResetHeightCache();

            lock (genericCache) genericCache.Clear();
        }

        public void ResetHeightCache()
        {
            DoResetHeightCache();
            memoryCachedBytes = 0;
            diskCachedBytes = 0;
        }

        public abstract void DoResetHeightCache();

        public abstract bool IsCacheEmpty();

        protected void SaveToFile(string reference, string state, object value)
        {
            // skip if file already exists
            if (diskCachedFiles.ContainsKey(new KeyValuePair<string, string>(reference, state))) return;

            try
            {
                if (!Directory.Exists(SplineTools.Instance.CacheDirectory)) Directory.CreateDirectory(SplineTools.Instance.CacheDirectory);

                var cacheFilePath = CacheFilePath(reference, state);

                Log.Debug(this, () => "Saving heightMap to " + cacheFilePath);
                var writer = File.CreateText(cacheFilePath);

                var jsonString = JsonUtility.ToJson(value);
                writer.Write(jsonString);
                writer.Close();

                diskCachedFiles.Add(new KeyValuePair<string, string>(reference, state), cacheFilePath);
                diskCachedBytes += new FileInfo(cacheFilePath).Length;
            } catch (Exception e)
            {
                Log.Warn(this, () => "Error writing file: " + e);
            }
        }

        private string CacheFilePath(string reference, string state)
        {
            return SplineTools.Instance.CacheDirectory + Path.DirectorySeparatorChar + FilenamePrefix + reference + "-" + state + ".json";
        }

        protected object LoadFromFile<T>(string reference, string state)
        {
            var cacheKey = new KeyValuePair<string, string>(reference, state);
            if (!diskCachedFiles.ContainsKey(cacheKey)) return null;

            var cacheFilePath = CacheFilePath(reference, state);

            try
            {
                if (File.Exists(cacheFilePath))
                {
                    Log.Debug(this, () => "Loading heightMap from " + cacheFilePath);
                    var reader = File.OpenText(cacheFilePath);

                    var heightMap = JsonUtility.FromJson<T>(reader.ReadToEnd());

                    reader.Close();
                    return heightMap;
                }
            } catch (Exception e)
            {
                diskCachedFiles.Remove(cacheKey);
                Log.Warn(this, () => "Error reading cached heightmap: " + e);
            }

            return null;
        }

        protected void CleanupFiles()
        {
            diskCachedBytes = 0;
            diskCachedFiles.Clear();

            if (Directory.Exists(SplineTools.Instance.CacheDirectory))
            {
                Directory.GetFiles(SplineTools.Instance.CacheDirectory)
                    .Where(file => file.Contains(FilenamePrefix)).ToList()
                    .ForEach(fileName =>
                    {
                        Log.Debug(this, () => "Deleting " + fileName);
                        File.Delete(fileName);
                    });
            }
        }

        protected void DeleteOldestFile()
        {
            if (Directory.Exists(SplineTools.Instance.CacheDirectory))
            {
                var oldestFile = Directory.GetFiles(SplineTools.Instance.CacheDirectory)
                    .Where(file => file.Contains(FilenamePrefix)).ToList()
                    .OrderBy(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (oldestFile != null)
                {
                    Log.Debug(this, () => "Deleting " + oldestFile);
                    File.Delete(oldestFile);
                    var cachedKey = diskCachedFiles.Where(e => e.Value.Equals(oldestFile))
                        .Select(e => e.Key).FirstOrDefault();
                    if (!cachedKey.Equals(default(KeyValuePair<string, string>))) diskCachedFiles.Remove(cachedKey);
                    diskCachedBytes -= new FileInfo(oldestFile).Length;
                }
            }
        }

        public void InitCachedFiles()
        {
            diskCachedBytes = 0;

            if (Directory.Exists(SplineTools.Instance.CacheDirectory))
            {
                Directory.GetFiles(SplineTools.Instance.CacheDirectory)
                    .Where(file => file.Contains(FilenamePrefix)).ToList()
                    .ForEach(fileName =>
                    {
                        Log.Debug(this, () => "Found existing file " + fileName);
                        diskCachedBytes += new FileInfo(fileName).Length;
                    });
            }
        }

        public T GetOrCache<T>(string id, string state, Func<T> supplier)
        {
            lock (genericCache)
            {
                KeyValuePair<string, object> pair;
                if (genericCache.TryGetValue(id, out pair) && pair.Key.Equals(state)) return (T) pair.Value;

                var value = supplier.Invoke();
                genericCache[id] = new KeyValuePair<string, object>(state, value);
                return value;
            }
        }

        public T Get<T>(string id, string state)
        {
            lock (genericCache)
            {
                KeyValuePair<string, object> pair;
                if (genericCache.TryGetValue(id, out pair) && pair.Key.Equals(state)) return (T) pair.Value;

                return default(T);
            }
        }

        public T AddToCache<T>(string id, string state, T value)
        {
            lock (genericCache)
            {
                genericCache[id] = new KeyValuePair<string, object>(state, value);
                return value;
            }
        }
    }
}