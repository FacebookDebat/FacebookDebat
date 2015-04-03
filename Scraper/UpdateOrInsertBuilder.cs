using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Data;
using Common;

namespace Scraper
{
    public class UpdateOrInsertBuilder<T>
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
    (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public List<T> Insert;
        public HashSet<T> Update;

        private Dictionary<string, T> AlreadyExists;
        private Dictionary<string, T> NewlyInserted;
        private Func<T, string> GetKey;
        private Func<T, T, bool> Updater;

        public UpdateOrInsertBuilder(IEnumerable<T> AlreadyExists, Func<T, string> GetKey, Func<T, T, bool> Updater)
        {
            this.Insert = new List<T>();
            this.Update = new HashSet<T>();

            var duplicates = AlreadyExists.GroupBy(x => GetKey(x)).Where(x => x.Count() > 1).ToList();

            this.AlreadyExists = AlreadyExists.ToDictionary(x => GetKey(x), x => x);
            this.NewlyInserted = new Dictionary<string,T>();
            this.GetKey = GetKey;
            this.Updater = Updater;
        }

        public void Process(string key, Func<T> itemCreator)
        {
            var item = itemCreator();
            T found;
            if (AlreadyExists.TryGetValue(GetKey(item), out found))
            {
                if (Updater(found, item))
                    Update.Add(found);
            }
            else if (!NewlyInserted.ContainsKey(GetKey(item)))
            {
                Insert.Add(item);
                NewlyInserted.Add(GetKey(item), item);
            }
        }


        public void SyncDatabase<Y>(int BulkSize, string table, string idColumn, Func<T, Y> create)
        {
            log.Info("Updating " + table);
            DatabaseTools.Update(table, idColumn, Update.Select(create));
            var insertedCount = 0;
            log.Info("Inserting " + table);
            foreach (var list in Insert.Chunk(BulkSize))
            {
                DatabaseTools.BulkInsert(table, list.Select(create));
                insertedCount += list.Count();
                log.Info("Inserted " + insertedCount + "/" + Insert.Count);
            }
        }
    }
}
