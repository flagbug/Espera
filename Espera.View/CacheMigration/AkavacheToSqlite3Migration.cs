using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
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

        public void Run()
        {
            if (this.AlreadyMigrated())
            {
                return;
            }

            this.Log().Info("Starting migration from deprecated BlobCache to new SqliteBlobCache");

            try
            {
                this.MigrateArtworks();
                this.MigrateCoreSettings();
                this.MigrateViewSettings();
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to migrate BlobCache", ex);
                return;
            }

            this.newBlobCache.InsertObject(MigratedKey, true).Wait();

            this.oldBlobCache.InvalidateAll().Wait();

            this.Log().Info("Finished BlobCache migration");
        }

        private bool AlreadyMigrated()
        {
            return this.newBlobCache.GetCreatedAt(MigratedKey).Wait() != null;
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