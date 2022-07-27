using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Akavache;
using Espera.Core;
using Espera.Core.Settings;
using Lager;
using Splat;

namespace Espera.View.CacheMigration
{
    public class AkavacheToSqlite3Migration : IEnableLogger
    {
        public static readonly string MigratedKey = "Sqlite3Migrated";

        private readonly IBlobCache newBlobCache;
        private readonly IBlobCache oldBlobCache;

        public AkavacheToSqlite3Migration(IBlobCache oldBlobCache, IBlobCache newBlobCache)
        {
            if (oldBlobCache == null)
                throw new ArgumentNullException("oldBlobCache");

            if (newBlobCache == null)
                throw new ArgumentNullException("newBlobCache");

            this.oldBlobCache = oldBlobCache;
            this.newBlobCache = newBlobCache;
        }

        public static bool NeedsMigration(IBlobCache blobCache)
        {
            return blobCache.GetCreatedAt(MigratedKey).Wait() == null;
        }

        public void Run()
        {
            this.Log().Info("Starting migration from deprecated BlobCache to new SqliteBlobCache");

            if (!oldBlobCache.GetAllKeys().Wait().Any())
            {
                newBlobCache.InsertObject(MigratedKey, true).Wait();

                this.Log().Info("Nothing to migrate, returning.");

                return;
            }

            try
            {
                MigrateArtworks();
                MigrateCoreSettings();
                MigrateViewSettings();
                MigrateChangelog();
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to migrate BlobCache", ex);
                return;
            }

            newBlobCache.InsertObject(MigratedKey, true).Wait();

            this.Log().Info("Finished BlobCache migration");
        }

        private void MigrateArtworks()
        {
            // Don't migrate online artwork lookup key, just let them expire
            foreach (var key in oldBlobCache.GetAllKeys().Wait().Where(x => x.StartsWith(BlobCacheKeys.Artwork)))
            {
                var oldData = oldBlobCache.Get(key).Wait();

                newBlobCache.Insert(key, oldData).Wait();
            }
        }

        private void MigrateChangelog()
        {
            try
            {
                var oldChangelog = oldBlobCache.GetObject<Changelog>(BlobCacheKeys.Changelog).Wait();

                newBlobCache.InsertObject(BlobCacheKeys.Changelog, oldChangelog).Wait();
            }

            catch (KeyNotFoundException)
            {
            }
        }

        private void MigrateCoreSettings()
        {
            var oldCoreSettings = new CoreSettings(oldBlobCache);
            var newCoreSettings = new CoreSettings(newBlobCache);

            MigrateSettingsStorage(oldCoreSettings, newCoreSettings);
        }

        private void MigrateSettingsStorage(SettingsStorage oldStorage, SettingsStorage newStorage)
        {
            foreach (var oldSetting in oldStorage.GetType().GetProperties())
                newStorage.GetType().GetProperty(oldSetting.Name).SetValue(newStorage, oldSetting.GetValue(oldStorage));
        }

        private void MigrateViewSettings()
        {
            var oldViewSettings = new ViewSettings(oldBlobCache);
            var newViewSettings = new ViewSettings(newBlobCache);

            MigrateSettingsStorage(oldViewSettings, newViewSettings);
        }
    }
}