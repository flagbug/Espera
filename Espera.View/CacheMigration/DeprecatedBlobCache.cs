﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using Akavache;
using Newtonsoft.Json;
using Splat;

namespace Espera.View.CacheMigration
{
    /// <summary>
    ///     The deprecated, file-system based BlobCache, used for migrating to the new BlobCache.
    /// </summary>
    public class DeprecatedBlobCache : IBlobCache, IEnableLogger
    {
        private const string BlobCacheIndexKey = "__THISISTHEINDEX__FFS_DONT_NAME_A_FILE_THIS™";
        private readonly Subject<Unit> actionTaken = new Subject<Unit>();
        protected readonly string CacheDirectory;
        private readonly IFilesystemProvider filesystem;
        private readonly IDisposable flushThreadSubscription;
        private readonly MemoizingMRUCache<string, AsyncSubject<byte[]>> memoizedRequests;
        private readonly AsyncSubject<Unit> shutdown = new AsyncSubject<Unit>();

        private ConcurrentDictionary<string, CacheIndexEntry> CacheIndex =
            new ConcurrentDictionary<string, CacheIndexEntry>();

        private bool disposed;

        public DeprecatedBlobCache(
            string cacheDirectory = null,
            IFilesystemProvider filesystemProvider = null,
            IScheduler scheduler = null,
            Action<AsyncSubject<byte[]>> invalidateCallback = null)
        {
            BlobCache.EnsureInitialized();

            filesystem = filesystemProvider ?? Locator.Current.GetService<IFilesystemProvider>();

            if (filesystem == null)
                throw new Exception(
                    "No IFilesystemProvider available. This should never happen, your DependencyResolver is broken");

            CacheDirectory = cacheDirectory ?? filesystem.GetDefaultRoamingCacheDirectory();
            Scheduler = scheduler ?? BlobCache.TaskpoolScheduler;

            // Here, we're not actually caching the requests directly (i.e. as byte[]s), but as the
            // "replayed result of the request", in the AsyncSubject - this makes the code
            // infinitely simpler because we don't have to keep a separate list of "in-flight reads"
            // vs "already completed and cached reads"
            memoizedRequests = new MemoizingMRUCache<string, AsyncSubject<byte[]>>(
                (x, c) => FetchOrWriteBlobFromDisk(x, c, false), 20, invalidateCallback);

            var cacheIndex = FetchOrWriteBlobFromDisk(BlobCacheIndexKey, null, true)
                .Catch(Observable.Return(new byte[0]))
                .Select(x => Encoding.UTF8.GetString(x, 0, x.Length).Split('\n')
                    .SelectMany(ParseCacheIndexEntry)
                    .ToDictionary(y => y.Key, y => y.Value))
                .Select(x => new ConcurrentDictionary<string, CacheIndexEntry>(x));

            cacheIndex.Subscribe(x => CacheIndex = x);

            flushThreadSubscription = Disposable.Empty;

            if (!ModeDetector.InUnitTestRunner())
                flushThreadSubscription = actionTaken
                    .Where(_ => CacheIndex != null)
                    .Throttle(TimeSpan.FromSeconds(30), Scheduler)
                    .SelectMany(_ => FlushCacheIndex(true))
                    .Subscribe(_ => this.Log().Debug("Flushing cache"));

            this.Log().Info("{0} entries in blob cache index", CacheIndex.Count);
        }

        public IScheduler Scheduler { get; protected set; }

        public IObservable<Unit> Shutdown => shutdown;

        public void Dispose()
        {
            if (disposed) return;
            lock (memoizedRequests)
            {
                if (disposed) return;
                disposed = true;

                actionTaken.OnCompleted();
                flushThreadSubscription.Dispose();

                var waitOnAllInflight = memoizedRequests.CachedValues()
                    .Select(x => x.Catch(Observable.Return(new byte[0])))
                    .Merge(8)
                    .Concat(Observable.Return(new byte[0]))
                    .Aggregate(Unit.Default, (acc, x) => acc);

                waitOnAllInflight
                    .SelectMany(FlushCacheIndex(true))
                    .Multicast(shutdown)
                    .PermaRef();
            }
        }

        public IObservable<Unit> Flush()
        {
            return FlushCacheIndex(false);
        }

        public IObservable<byte[]> Get(string key)
        {
            if (disposed) return Observable.Throw<byte[]>(new ObjectDisposedException("PersistentBlobCache"));

            if (IsKeyStale(key))
            {
                Invalidate(key);
                return ExceptionHelper.ObservableThrowKeyNotFoundException<byte[]>(key);
            }

            AsyncSubject<byte[]> ret;
            lock (memoizedRequests)
            {
                // There are three scenarios here, and we handle all of them with aplomb and elegance:
                //
                // 1. The key is already in memory as a completed request - we return the
                //    AsyncSubject which will replay the result
                //
                // 2. The key is currently being fetched from disk - in this case, MemoizingMRUCache
                //    has an AsyncSubject for it (since FetchOrWriteBlobFromDisk completes
                // immediately) , and the client will get the result when the disk I/O completes
                //
                // 3. The key isn't in memory and isn't being fetched - in this case,
                //    FetchOrWriteBlobFromDisk will be called which will immediately return an
                //    AsyncSubject representing the queued disk read.
                ret = memoizedRequests.Get(key);
            }

            // If we fail trying to fetch/write the key on disk, we want to try again instead of
            // replaying the same failure
            ret.LogErrors("Get")
                .Subscribe(x => { }, ex => Invalidate(key));

            return ret;
        }

        public IObservable<IEnumerable<string>> GetAllKeys()
        {
            if (disposed) throw new ObjectDisposedException("PersistentBlobCache");
            return Observable.Return(CacheIndex.ToList()
                .Where(x => x.Value.ExpiresAt == null || x.Value.ExpiresAt >= BlobCache.TaskpoolScheduler.Now)
                .Select(x => x.Key).ToList());
        }

        public IObservable<DateTimeOffset?> GetCreatedAt(string key)
        {
            CacheIndexEntry value;
            if (!CacheIndex.TryGetValue(key, out value)) return Observable.Return<DateTimeOffset?>(null);

            return Observable.Return<DateTimeOffset?>(value.CreatedAt);
        }

        public IObservable<Unit> Insert(string key, byte[] data, DateTimeOffset? absoluteExpiration = null)
        {
            if (key == null || data == null) return Observable.Throw<Unit>(new ArgumentNullException());

            // NB: Since FetchOrWriteBlobFromDisk is guaranteed to not block, we never sit on this
            // lock for any real length of time
            AsyncSubject<byte[]> err;
            lock (memoizedRequests)
            {
                if (disposed) return Observable.Throw<Unit>(new ObjectDisposedException("PersistentBlobCache"));

                memoizedRequests.Invalidate(key);
                err = memoizedRequests.Get(key, data);
            }

            // If we fail trying to fetch/write the key on disk, we want to try again instead of
            // replaying the same failure
            err.LogErrors("Insert").Subscribe(
                x => CacheIndex[key] = new CacheIndexEntry(Scheduler.Now, absoluteExpiration),
                ex => Invalidate(key));

            return err.Select(_ => Unit.Default);
        }

        public IObservable<Unit> Invalidate(string key)
        {
            lock (memoizedRequests)
            {
                if (disposed) return Observable.Throw<Unit>(new ObjectDisposedException("PersistentBlobCache"));
                this.Log().Debug("Invalidating {0}", key);
                memoizedRequests.Invalidate(key);
            }

            CacheIndexEntry dontcare;
            CacheIndex.TryRemove(key, out dontcare);

            var path = GetPathForKey(key);
            var ret = Observable.Defer(() => filesystem.Delete(path))
                .Retry(2)
                .Do(_ => actionTaken.OnNext(Unit.Default));

            return ret.Multicast(new AsyncSubject<Unit>()).PermaRef();
        }

        public IObservable<Unit> InvalidateAll()
        {
            string[] keys;
            lock (memoizedRequests)
            {
                if (disposed) return Observable.Throw<Unit>(new ObjectDisposedException("PersistentBlobCache"));
                keys = CacheIndex.Keys.ToArray();
            }

            var ret = keys.ToObservable()
                .Select(x => Observable.Defer(() => Invalidate(x)))
                .Merge(8)
                .Aggregate(Unit.Default, (acc, x) => acc)
                .Multicast(new AsyncSubject<Unit>());

            ret.Connect();
            return ret;
        }

        public IObservable<Unit> Vacuum()
        {
            return Observable.Throw<Unit>(new NotImplementedException());
        }

        /// <summary>
        ///     This method is called immediately after reading any data to disk. Override this in
        ///     encrypting data stores in order to decrypt the data.
        /// </summary>
        /// <param name="data">The byte data that has just been read from disk.</param>
        /// <param name="scheduler">
        ///     The scheduler to use if an operation has to be deferred. If the operation can be done
        ///     immediately, use Observable.Return and ignore this parameter.
        /// </param>
        /// <returns>A Future result representing the decrypted data</returns>
        protected virtual IObservable<byte[]> AfterReadFromDiskFilter(byte[] data, IScheduler scheduler)
        {
            return Observable.Return(data);
        }

        /// <summary>
        ///     This method is called immediately before writing any data to disk. Override this in
        ///     encrypting data stores in order to encrypt the data.
        /// </summary>
        /// <param name="data">The byte data about to be written to disk.</param>
        /// <param name="scheduler">
        ///     The scheduler to use if an operation has to be deferred. If the operation can be done
        ///     immediately, use Observable.Return and ignore this parameter.
        /// </param>
        /// <returns>A Future result representing the encrypted data</returns>
        protected virtual IObservable<byte[]> BeforeWriteToDiskFilter(byte[] data, IScheduler scheduler)
        {
            return Observable.Return(data);
        }

        private AsyncSubject<byte[]> FetchOrWriteBlobFromDisk(string key, object byteData, bool synchronous)
        {
            // If this is secretly a write, dispatch to WriteBlobToDisk (we're kind of abusing the
            // 'context' variable from MemoizingMRUCache here a bit)
            if (byteData != null) return WriteBlobToDisk(key, (byte[])byteData, synchronous);

            var ret = new AsyncSubject<byte[]>();
            var ms = new MemoryStream();

            var scheduler = synchronous ? System.Reactive.Concurrency.Scheduler.Immediate : Scheduler;
            if (disposed)
            {
                Observable.Throw<byte[]>(new ObjectDisposedException("PersistentBlobCache"))
                    .Multicast(ret)
                    .PermaRef();
                return ret;
            }

            Func<IObservable<byte[]>> readResult = () =>
                Observable.Defer(() =>
                        filesystem.OpenFileForReadAsync(GetPathForKey(key), scheduler))
                    .Retry(1)
                    .SelectMany(x => x.CopyToAsync(ms, scheduler))
                    .SelectMany(x => AfterReadFromDiskFilter(ms.ToArray(), scheduler))
                    .Catch<byte[], Exception>(
                        ex => ExceptionHelper.ObservableThrowKeyNotFoundException<byte[]>(key, ex))
                    .Do(_ =>
                    {
                        if (!synchronous && key != BlobCacheIndexKey) actionTaken.OnNext(Unit.Default);
                    });

            readResult().Multicast(ret).PermaRef();
            return ret;
        }

        private IObservable<Unit> FlushCacheIndex(bool synchronous)
        {
            var index = CacheIndex.Select(x => JsonConvert.SerializeObject(x));

            return WriteBlobToDisk(BlobCacheIndexKey, Encoding.UTF8.GetBytes(string.Join("\n", index)), synchronous)
                .Select(_ => Unit.Default)
                .Catch<Unit, Exception>(ex =>
                {
                    this.Log().WarnException("Couldn't flush cache index", ex);
                    return Observable.Return(Unit.Default);
                });
        }

        private string GetPathForKey(string key)
        {
            return Path.Combine(CacheDirectory, Utility.GetMd5Hash(key));
        }

        private bool IsKeyStale(string key)
        {
            CacheIndexEntry value;
            return CacheIndex.TryGetValue(key, out value) && value.ExpiresAt != null && value.ExpiresAt < Scheduler.Now;
        }

        private IEnumerable<KeyValuePair<string, CacheIndexEntry>> ParseCacheIndexEntry(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Enumerable.Empty<KeyValuePair<string, CacheIndexEntry>>();

            try
            {
                return new[] { JsonConvert.DeserializeObject<KeyValuePair<string, CacheIndexEntry>>(s) };
            }
            catch (Exception ex)
            {
                this.Log().Warn("Invalid cache index entry", ex);
                return Enumerable.Empty<KeyValuePair<string, CacheIndexEntry>>();
            }
        }

        private AsyncSubject<byte[]> WriteBlobToDisk(string key, byte[] byteData, bool synchronous)
        {
            var ret = new AsyncSubject<byte[]>();
            var scheduler = synchronous ? System.Reactive.Concurrency.Scheduler.Immediate : Scheduler;

            var path = GetPathForKey(key);

            // NB: The fact that our writing AsyncSubject waits until the write actually completes
            // means that an Insert immediately followed by a Get will take longer to process -
            // however, this also means that failed writes will disappear from the cache, which is A
            // Good Thing.

            Func<IObservable<byte[]>> writeResult = () => BeforeWriteToDiskFilter(byteData, scheduler)
                .Select(x => new MemoryStream(x))
                .Zip(Observable.Defer(() =>
                            filesystem.OpenFileForWriteAsync(path, scheduler))
                        .Retry(1),
                    (from, to) => new { from, to })
                .SelectMany(x => x.from.CopyToAsync(x.to, scheduler))
                .Select(_ => byteData)
                .Do(_ =>
                {
                    if (!synchronous && key != BlobCacheIndexKey) actionTaken.OnNext(Unit.Default);
                }, ex => LogHost.Default.WarnException("Failed to write out file: " + path, ex));

            writeResult().Multicast(ret).Connect();
            return ret;
        }

        private class CacheIndexEntry
        {
            public CacheIndexEntry(DateTimeOffset createdAt, DateTimeOffset? expiresAt)
            {
                CreatedAt = createdAt;
                ExpiresAt = expiresAt;
            }

            public DateTimeOffset CreatedAt { get; }

            public DateTimeOffset? ExpiresAt { get; }
        }
    }

    internal static class Utility
    {
        public static IObservable<Unit> CopyToAsync(this Stream This, Stream destination, IScheduler scheduler = null)
        {
            return Observable.Start(() =>
            {
                try
                {
                    This.CopyTo(destination);
                }
                catch (Exception ex)
                {
                    LogHost.Default.WarnException("CopyToAsync failed", ex);
                }
                finally
                {
                    This.Dispose();
                    destination.Dispose();
                }
            }, scheduler ?? BlobCache.TaskpoolScheduler);
        }

        public static string GetMd5Hash(string input)
        {
            using (var md5Hasher = MD5.Create())
            {
                // Convert the input string to a byte array and compute the hash.
                var data = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sBuilder = new StringBuilder();
                foreach (var item in data) sBuilder.Append(item.ToString("x2"));
                return sBuilder.ToString();
            }
        }

        public static IObservable<T> LogErrors<T>(this IObservable<T> This, string message = null)
        {
            return Observable.Create<T>(subj =>
            {
                return This.Subscribe(subj.OnNext,
                    ex =>
                    {
                        var msg = message ?? "0x" + This.GetHashCode().ToString("x");
                        LogHost.Default.Info("{0} failed with {1}:\n{2}", msg, ex.Message, ex.ToString());
                        subj.OnError(ex);
                    }, subj.OnCompleted);
            });
        }

        internal static IObservable<T> PermaRef<T>(this IConnectableObservable<T> This)
        {
            This.Connect();
            return This;
        }
    }

    internal class ExceptionHelper
    {
        public static IObservable<T> ObservableThrowKeyNotFoundException<T>(string key, Exception innerException = null)
        {
            return Observable.Throw<T>(
                new KeyNotFoundException(string.Format(CultureInfo.InvariantCulture,
                    "The given key '{0}' was not present in the cache.", key), innerException));
        }

        public static IObservable<T> ObservableThrowObjectDisposedException<T>(string obj,
            Exception innerException = null)
        {
            return Observable.Throw<T>(
                new ObjectDisposedException(string.Format(CultureInfo.InvariantCulture,
                    "The cache '{0}' was disposed.", obj), innerException));
        }
    }
}