//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
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

    abstract public class GoogleSettings : Settings, IDataStore
    {
        /// <summary>
        /// Application name which is passed to google.
        /// </summary>
        [JsonIgnore]
        public virtual string ApplicationName { get; } = Assembly.GetEntryAssembly().GetProduct();

        /// <summary>
        /// Application's google client secret file of the app created on https://console.cloud.google.com/
        /// </summary>
        [JsonIgnore]
        public virtual string ClientSecretFile { get; } = Log.AppDir + "\\" + "googleClientSecret.json";

        /// <summary>
        /// Permission scopes for the application.
        /// </summary>
        [JsonIgnore]
        public abstract string[] Scopes { get; }

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
        virtual protected StringEndec Endec { get; set; } = null;

        /// <summary>
        /// (!)When overriding, first invoke the base method.
        /// </summary>
        /// <exception cref="Exception"></exception>
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
                    if (GoogleCache is GoogleCacheType)//if Endec was set recently
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
                if (GoogleCache is GoogleCacheType)
                    googleCache = (GoogleCacheType)GoogleCache;
                else
                    throw new Exception("GoogleCache is an unexpected type: " + GoogleCache.GetType() + "\r\nConsider removing the config file: " + __Info.File);
            }
        }

        /// <summary>
        /// (!)When overriding, first invoke the base method.
        /// </summary>
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

        public GoogleCacheType GetGoogleCacheClone()
        {
            return googleCache?.CreateCloneByJson();
        }

        /// <summary>
        /// (!)Used by Google.Apis 
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
        /// (!)Used by Google.Apis 
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
        /// (!)Used by Google.Apis 
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
        /// (!)Used by Google.Apis 
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

        /// <summary>
        /// Creates a new instance of the given Settings field with cloned values.
        /// (!)The new instance shares the same __Info and Endec objects with the original instance.
        /// </summary>
        /// <typeparam name="S"></typeparam>
        /// <param name="jsonSerializerSettings">allows to customize cloning</param>
        /// <returns></returns>
        virtual public S Clone<S>(JsonSerializerSettings jsonSerializerSettings = null) where S : GoogleSettings, new()
        {
            S s = ((S)this).CreateClone(jsonSerializerSettings);
            s.Endec = Endec;
            s.Loaded();
            return s;
        }
    }
}