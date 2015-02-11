using Akavache;
using Espera.Core;
using Espera.Core.Settings;
using Lager;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;

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

            if (!this.oldBlobCache.GetAllKeys().Wait().Any())
            {
                this.newBlobCache.InsertObject(MigratedKey, true).Wait();

                this.Log().Info("Nothing to migrate, returning.");

                return;
            }

            try
            {
                this.MigrateArtworks();
                this.MigrateCoreSettings();
                this.MigrateViewSettings();
                this.MigrateChangelog();
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to migrate BlobCache", ex);
                return;
            }

            this.newBlobCache.InsertObject(MigratedKey, true).Wait();

            this.Log().Info("Finished BlobCache migration");
        }

        private void MigrateArtworks()
        {
            // Don't migrate online artwork lookup key, just let them expire
            foreach (string key in this.oldBlobCache.GetAllKeys().Wait().Where(x => x.StartsWith(BlobCacheKeys.Artwork)))
            {
                byte[] oldData = this.oldBlobCache.Get(key).Wait();

                this.newBlobCache.Insert(key, oldData).Wait();
            }
        }

        private void MigrateChangelog()
        {
            try
            {
                var oldChangelog = this.oldBlobCache.GetObject<Changelog>(BlobCacheKeys.Changelog).Wait();

                this.newBlobCache.InsertObject(BlobCacheKeys.Changelog, oldChangelog).Wait();
            }

            catch (KeyNotFoundException)
            { }
        }

        private void MigrateCoreSettings()
        {
            var oldCoreSettings = new CoreSettings(this.oldBlobCache);
            var newCoreSettings = new CoreSettings(this.newBlobCache);

            this.MigrateSettingsStorage(oldCoreSettings, newCoreSettings);
        }

        private void MigrateSettingsStorage(SettingsStorage oldStorage, SettingsStorage newStorage)
        {
            foreach (PropertyInfo oldSetting in oldStorage.GetType().GetProperties())
            {
                newStorage.GetType().GetProperty(oldSetting.Name).SetValue(newStorage, oldSetting.GetValue(oldStorage));
            }
        }

        private void MigrateViewSettings()
        {
            var oldViewSettings = new ViewSettings(this.oldBlobCache);
            var newViewSettings = new ViewSettings(this.newBlobCache);

            this.MigrateSettingsStorage(oldViewSettings, newViewSettings);
        }
    }
}