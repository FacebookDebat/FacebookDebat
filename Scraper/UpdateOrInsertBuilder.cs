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
        public List<T> Insert;
        public HashSet<T> Update;

        private Dictionary<string, T> AlreadyExists;
        private Dictionary<string, T> NewlyInserted;
        private Func<T, string> getKey;
        private Func<T, T, bool> Updater;

        public UpdateOrInsertBuilder(IEnumerable<T> AlreadyExists, Func<T, string> GetKey, Func<T, T, bool> Updater)
        {
            this.Insert = new List<T>();
            this.Update = new HashSet<T>();
            this.AlreadyExists = AlreadyExists.ToDictionary(x => GetKey(x), x => x);
            this.NewlyInserted = new Dictionary<string,T>();
            this.getKey = GetKey;
            this.Updater = Updater;
        }

        public void Process(string key, Func<T> itemCreator)
        {
            if (key == "765276356888011_765288790220101")
                Console.WriteLine("Here");

            var item = itemCreator();
            T found;
            if (AlreadyExists.TryGetValue(getKey(item), out found))
            {
                if (Updater(found, item))
                    Update.Add(found);
            }
            else if (!NewlyInserted.ContainsKey(getKey(item)))
            {
                Insert.Add(item);
                NewlyInserted.Add(getKey(item), item);
            }
        }


        public void SyncDatabase<Y>(int BulkSize, string table, string id, Func<T, Y> create)
        {
            DatabaseTools.Update(table, id, Update.Select(create));
            var insertedCount = 0;
            foreach (var list in Insert.Chunk(BulkSize))
            {
                DatabaseTools.BulkInsert(table, list.Select(create));
                insertedCount += list.Count();
                Console.WriteLine("Inserted " + insertedCount + "/" + Insert.Count);
            }
        }
    }
}
