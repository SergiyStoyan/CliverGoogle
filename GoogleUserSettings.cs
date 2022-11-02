//********************************************************************************************
//Author: Sergiy Stoyan
//        systoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using Google.Apis.Util.Store;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cliver
{
    using GoogleCacheType = Dictionary<string, object>;

    public class GoogleUserSettings : UserSettings, IDataStore
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
        object GoogleCache;

        GoogleCacheType googleCache;

        /// <summary>
        /// Set this object in the child class if the cache must be stored encrypted.
        /// </summary>
        virtual protected StringEndec Endec { get; } = null;

        protected override void Loaded()
        {
            if (GoogleCache == null)
            {
                googleCache = new GoogleCacheType();
                return;
            }

            if (Endec != null)
            {
                if (GoogleCache is string)
                    googleCache = Endec.Decrypt<GoogleCacheType>((string)GoogleCache);
                else
                {
                    if (GoogleCache is JObject)//if Endec was set recently
                    {
                        googleCache = ((JObject)GoogleCache).ToObject<GoogleCacheType>();
                        Save();
                    }
                    else if (GoogleCache is GoogleCacheType)//if Endec was set recently
                    {
                        googleCache = (GoogleCacheType)GoogleCache;
                        Save();
                    }
                    else
                        throw new Exception("GoogleCache is an unexpected type: " + GoogleCache.GetType());
                }
            }
            else
            {
                if (GoogleCache is JObject)
                    googleCache = ((JObject)GoogleCache).ToObject<GoogleCacheType>();
                else if (GoogleCache is GoogleCacheType)
                    googleCache = (GoogleCacheType)GoogleCache;
                else
                    throw new Exception("GoogleCache is an unexpected type: " + GoogleCache.GetType());
            }
        }

        protected override void Saving()
        {
            if (Endec != null)
                GoogleCache = Endec.Encrypt(googleCache);
            else
                GoogleCache = googleCache;
        }

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
                googleCache.Clear();
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
                googleCache.Remove(key);
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
                if (googleCache.TryGetValue(key, out object value))
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
                googleCache[key] = value;
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