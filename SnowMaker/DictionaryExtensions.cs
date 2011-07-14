using System;
using System.Collections.Generic;

namespace SnowMaker
{
    internal static class DictionaryExtensions
    {
        internal static TValue GetValue<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key,
            object dictionaryLock,
            Func<TValue> valueInitializer)
        {
            TValue value;
            var found = dictionary.TryGetValue(key, out value);
            if (found) return value;

            lock (dictionaryLock)
            {
                found = dictionary.TryGetValue(key, out value);
                if (found) return value;

                value = valueInitializer();

                dictionary.Add(key, value);
            }

            return value;
        }
    }
}