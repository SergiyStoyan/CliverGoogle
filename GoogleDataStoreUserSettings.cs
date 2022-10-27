//********************************************************************************************
//Author: Sergey Stoyan
//        sergey.stoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using Google.Apis.Util.Store;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;

namespace Cliver
{
    public class GoogleDataStoreUserSettings : UserSettings, IDataStore
    {
        /// <summary>
        /// The user's google account chosen latest.
        /// </summary>
        [JsonProperty]
        public string GoogleAccount { get; internal set; }

        /// <summary>
        /// (!)This object is a cache storage by Google and must not be accessed from outside.
        /// </summary>
        [JsonProperty]
        protected Dictionary<string, object> GoogleCache = new Dictionary<string, object>();

        public void ClearGoogleAccount()
        {
            ClearAsync().Wait();
            GoogleAccount = null;
        }

        /// <summary>
        /// Used by Google.Apis 
        /// </summary>
        /// <returns></returns>
        public Task ClearAsync()
        {
            Task t = new Task(() =>
            {
                GoogleCache.Clear();
                Save();
            });
            t.Start();
            return t;
        }

        /// <summary>
        /// Used by Google.Apis 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public Task DeleteAsync<T>(string key)
        {
            Task t = new Task(() =>
            {
                GoogleCache.Remove(key);
                Save();
            });
            t.Start();
            return t;
        }

        /// <summary>
        /// Used by Google.Apis 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public Task<T> GetAsync<T>(string key)
        {
            Task<T> t = new Task<T>(() =>
            {
                if (GoogleCache.TryGetValue(key, out object value))
                    return (T)value;
                return default(T);
            });
            t.Start();
            return t;
        }

        /// <summary>
        /// Used by Google.Apis 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public Task StoreAsync<T>(string key, T value)
        {
            Task t = new Task(() =>
            {
                GoogleCache[key] = value;
                Save();
            });
            t.Start();
            return t;
        }
    }

    public class GoogleDataStore : IDataStore
    {
        public GoogleDataStore(Settings settings, string fieldName)
        {
            Settings = settings;
            fieldInfo = settings.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo == null)
                throw new Exception("Field '" + fieldName + " could not be found.");
            Type fieldType = typeof(Dictionary<string, object>);
            if (fieldInfo.FieldType != fieldType)
                throw new Exception("Field '" + fieldName + "' is not type " + fieldType);
            keys2value = (Dictionary<string, object>)fieldInfo.GetValue(settings);
            //Convert.ChangeType(keys2value, fieldType);
            if (keys2value == null)
            {
                keys2value = new Dictionary<string, object>();
                fieldInfo.SetValue(settings, keys2value);
            }
        }
        public readonly Settings Settings;
        readonly FieldInfo fieldInfo;
        readonly Dictionary<string, object> keys2value = null;

        public Task ClearAsync()
        {
            Task t = new Task(() =>
            {
                keys2value.Clear();
                Settings.Save();
            });
            t.Start();
            return t;
        }

        public Task DeleteAsync<T>(string key)
        {
            Task t = new Task(() =>
            {
                keys2value.Remove(key);
                Settings.Save();
            });
            t.Start();
            return t;
        }

        public Task<T> GetAsync<T>(string key)
        {
            Task<T> t = new Task<T>(() =>
            {
                if (keys2value.TryGetValue(key, out object value))
                    return (T)value;
                return default(T);
            });
            t.Start();
            return t;
        }

        public Task StoreAsync<T>(string key, T value)
        {
            Task t = new Task(() =>
            {
                keys2value[key] = value;
                Settings.Save();
            });
            t.Start();
            return t;
        }
    }
}