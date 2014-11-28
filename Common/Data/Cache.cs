using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Data
{
    public class Cache<D, T, V>
        where D : DbContext, new()
        where T : class
    {
        private object iLock = new object();

        private ConcurrentDictionary<string, V> dict;

        D database;
        Func<D, DbSet<T>> getTable;
        Func<T, string> getStr;
        Func<T, V> getValue;
        bool autoSave;

        public Cache(D db, Func<D, DbSet<T>> getTable, Func<T, string> getStr, Func<T, V> getValue, bool autoSave)
        {
            this.database = db;
            this.autoSave = autoSave;
            this.getTable = getTable;
            this.getStr = getStr;
            this.getValue = getValue;

            var currentlyInDB = new ConcurrentDictionary<string, V>();
            foreach (var item in getTable(database))
            {
                if (!currentlyInDB.TryAdd(getStr(item), getValue(item)))
                    throw new Exception("Duplicate key??");
            }

            dict = currentlyInDB;
        }

        public V GetOrAdd(string id, Func<T> insertparams)
        {
            // Get the value from the cache
            V value;
            if (dict.TryGetValue(id, out value))
                return value;

            // Value not found
            lock (iLock)
            {
                if (dict.TryGetValue(id, out value))
                    return value;

                // Noboddy created since the lock was obtained. We'll do it.
                var table = getTable(database);

                var obj = insertparams();

                if (autoSave)
                {
                    table.Add(obj);
                    database.SaveChanges();
                }

                var newValue = getValue(obj);
                dict.TryAdd(id, newValue);
                return newValue;
            }
        }
    }
}
