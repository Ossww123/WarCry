#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Den.Tools.Matrices;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class MapMagicCache : Cache
    {
        private readonly Dictionary<string, Dictionary<string, KeyValuePair<Matrix, bool>>> heightMapResults =
            new Dictionary<string, Dictionary<string, KeyValuePair<Matrix, bool>>>();

        private readonly Dictionary<string, Dictionary<string, long>> lastCacheRequests = new Dictionary<string, Dictionary<string, long>>();
        private readonly Dictionary<string, HashSet<string>> heightMapResultsInProgress = new Dictionary<string, HashSet<string>>();

        public override void DoResetHeightCache()
        {
            lock (heightMapResults) heightMapResults.Clear();
            lock (heightMapResultsInProgress) heightMapResultsInProgress.Clear();
            CleanupFiles();
        }

        public override bool IsCacheEmpty()
        {
            lock (heightMapResults) return heightMapResults.Count == 0;
        }

        private void AddToCache(string reference, Den.Tools.CoordRect rect, Matrix matrix, bool persist = true)
        {
            lock (heightMapResults)
            {
                if (!heightMapResults.ContainsKey(reference)) heightMapResults.Add(reference, new Dictionary<string, KeyValuePair<Matrix, bool>>());

                memoryCachedBytes += EstimatedBytes(matrix);

                Den.Tools.Extensions.ForceAdd(heightMapResults[reference], ReferenceString(rect), new KeyValuePair<Matrix, bool>(matrix, persist));

                CleanupOrSpillOldCacheEntries();
            }
        }

        private void SpillToDisk(string reference, string state, Matrix matrix)
        {
            Log.Debug(this, () => "Spill to disk: " + reference + "-" + state);
            SaveToFile(reference, state, matrix);
        }

        private static int EstimatedBytes(Matrix matrix)
        {
            return matrix == null
                ? 0
                : matrix.count * sizeof(float);
        }

        protected override void CleanupOrSpillOldCacheEntries()
        {
            lock (heightMapResults)
            {
                // remove the entry with the oldest request time when limit is reached
                while (MemoryCachedMegaBytes > SplineTools.Instance.heightsCacheSizeInMemoryMegaBytes && lastCacheRequests.Count > 0)
                {
                    var toRemoveReference = lastCacheRequests.First().Key;
                    var toRemove = lastCacheRequests[toRemoveReference].First();

                    foreach (var reference in lastCacheRequests)
                    {
                        foreach (var request in reference.Value)
                        {
                            if (request.Value < toRemove.Value)
                            {
                                toRemove = request;
                                toRemoveReference = reference.Key;
                            }
                        }
                    }

//                    Log.Debug(this, () => "Remove from cache " + toRemoveReference + " -> " + toRemove.Key);

                    lastCacheRequests[toRemoveReference].Remove(toRemove.Key);
                    if (heightMapResults.ContainsKey(toRemoveReference) && heightMapResults[toRemoveReference].ContainsKey(toRemove.Key))
                    {
                        var entryToBeRemoved = heightMapResults[toRemoveReference][toRemove.Key];

                        if (entryToBeRemoved.Value && SplineTools.Instance.heightsCacheSizeOnDiskMegaBytes > 0)
                            SpillToDisk(toRemoveReference, toRemove.Key, entryToBeRemoved.Key);

                        heightMapResults[toRemoveReference].Remove(toRemove.Key);
                        memoryCachedBytes -= EstimatedBytes(entryToBeRemoved.Key);
                    }

                    if (lastCacheRequests.ContainsKey(toRemoveReference) && lastCacheRequests[toRemoveReference].Count == 0)
                        lastCacheRequests.Remove(toRemoveReference);
                    if (heightMapResults.ContainsKey(toRemoveReference) && heightMapResults[toRemoveReference].Count == 0)
                        heightMapResults.Remove(toRemoveReference);
                }

                while (DiskCachedMegaBytes > SplineTools.Instance.heightsCacheSizeOnDiskMegaBytes) DeleteOldestFile();
            }
        }

        private bool HeightMapIsMemoryCached(string reference, Den.Tools.CoordRect coordRect)
        {
            lock (heightMapResults)
            {
                return heightMapResults.ContainsKey(reference) && heightMapResults[reference].ContainsKey(ReferenceString(coordRect));
            }
        }

        public Matrix GetOrAddHeightMap(string reference, Den.Tools.CoordRect coordRect, Func<Matrix> resultsFunc, StopToken stop)
        {
            //check if result already exists
            var coordRectReference = ReferenceString(coordRect);

            lock (heightMapResults)
            {
                if (!lastCacheRequests.ContainsKey(reference)) lastCacheRequests.Add(reference, new Dictionary<string, long>());
                Den.Tools.Extensions.ForceAdd(lastCacheRequests[reference], coordRectReference, DateTime.UtcNow.Ticks);

                if (HeightMapIsMemoryCached(reference, coordRect)) return heightMapResults[reference][coordRectReference].Key;
            }

            //check if result is currently in computation
            var skip = false;

            lock (heightMapResultsInProgress)
            {
                if (heightMapResultsInProgress.ContainsKey(reference) && heightMapResultsInProgress[reference].Contains(coordRectReference))
                    skip = true;
                else
                {
                    if (!heightMapResultsInProgress.ContainsKey(reference)) heightMapResultsInProgress.Add(reference, new HashSet<string>());
                    heightMapResultsInProgress[reference].Add(coordRectReference);
                }
            }

            if (skip)
            {
                Log.Debug(typeof(Cache), () => "Skipping computation for " + reference + ": " + coordRect);
                return WaitForHeightMapResult(reference, coordRect, resultsFunc, stop);
            }

            try
            {
                // check if cached on disk
                var fromFile = (Matrix) LoadFromFile<Matrix>(reference, coordRectReference);
                if (fromFile != null)
                {
                    AddToCache(reference, coordRect, fromFile, false);
                    return fromFile;
                }

                // compute new results
                Log.Debug(typeof(Cache), () => "Computing results for " + reference + ": " + coordRect);
                var start = DateTime.UtcNow;

                var results = resultsFunc.Invoke();

                var duration = DateTime.UtcNow.Subtract(start).TotalMilliseconds;
                // don't persist results that can be generated fast
                var persist = duration > SplineTools.Instance.heightsCacheSpillToDiskThresholdMillis;

                if (results != null) AddToCache(reference, coordRect, results, persist);

                return results;
            } finally
            {
                lock (heightMapResultsInProgress) heightMapResultsInProgress[reference].Remove(coordRectReference);
            }
        }

        private Matrix WaitForHeightMapResult(string reference, Den.Tools.CoordRect coordRect, Func<Matrix> resultsFunc, StopToken stop)
        {
            while ((stop == null || !stop.stop) && CoordRectInProgress(reference, coordRect)) Thread.Sleep(100);

            lock (heightMapResults)
                if (heightMapResults.ContainsKey(reference))
                {
                    heightMapResults[reference].TryGetValue(ReferenceString(coordRect), out var results);
                    return results.Key;
                }

            // if the matrix was not found, try it again
            return GetOrAddHeightMap(reference, coordRect, resultsFunc, stop);
        }

        private bool CoordRectInProgress(string reference, Den.Tools.CoordRect coordRect)
        {
            lock (heightMapResultsInProgress)
            {
                return heightMapResultsInProgress.ContainsKey(reference) &&
                       heightMapResultsInProgress[reference].Contains(ReferenceString(coordRect));
            }
        }

        private string ReferenceString(Den.Tools.CoordRect coordRect)
        {
            return coordRect.offset.x + "-" + coordRect.offset.z + coordRect.size.x;
        }
    }
}

#endif
