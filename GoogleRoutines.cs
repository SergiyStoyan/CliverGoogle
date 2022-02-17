//********************************************************************************************
//Author: Sergey Stoyan
//        sergey.stoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;

namespace Cliver
{
    public class GoogleRoutines
    {
        public static UserCredential GetCredential(string applicationName, IEnumerable<string> scopes, out string credentialDir, string clientSecretFile)
        {
            string cd = null;
            UserCredential uc = getCredential(applicationName, scopes, ref cd, clientSecretFile);
            credentialDir = cd;
            return uc;
        }

        public static UserCredential GetCredential(string applicationName, IEnumerable<string> scopes, string credentialDir, string clientSecretFile)
        {
            return getCredential(applicationName, scopes, ref credentialDir, clientSecretFile);
        }

        static UserCredential getCredential(string applicationName, IEnumerable<string> scopes, ref string credentialDir, string clientSecretFile)
        {
            if (credentialDir == null)
                credentialDir = Log.AppCompanyUserDataDir + "\\googleCredential";

            FileDataStore fileDataStore = new FileDataStore(credentialDir, true);
            return GetCredential(applicationName, scopes, fileDataStore, clientSecretFile);
        }

        public static UserCredential GetCredential(string applicationName, IEnumerable<string> scopes, IDataStore dataStore, string clientSecretFile)
        {
            if (clientSecretFile == null)
                clientSecretFile = Log.AppDir + "\\googleClientSecret.json";

            UserCredential credential;
            using (var stream = new FileStream(clientSecretFile, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecretsStream: stream,
                    scopes: scopes,
                    user: applicationName,
                    taskCancellationToken: System.Threading.CancellationToken.None,
                    dataStore: dataStore
                    ).Result;
            }
            List<string> s2s = scopes.Where(a => !Regex.IsMatch(credential.Token.Scope, Regex.Escape(a), RegexOptions.IgnoreCase)).ToList();
            if (s2s.Any())
            {
                using (var stream = new FileStream(clientSecretFile, FileMode.Open, FileAccess.Read))
                {
                    List<string> ss = credential.Token.Scope.Split(' ').ToList();
                    ss.AddRange(s2s);
                    //credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    //    clientSecretsStream: stream,
                    //    scopes: ss,
                    //    user: applicationName,
                    //    taskCancellationToken: System.Threading.CancellationToken.None,
                    //    dataStore: dataStore
                    //    ).Result;
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecretsStream = stream,
                        IncludeGrantedScopes = true
                    },
                        scopes: ss,
                        user: applicationName,
                        taskCancellationToken: System.Threading.CancellationToken.None,
                        dataStore: dataStore
                        ).Result;
                }
            }
            return credential;
        }
    }

    public class GoogleDataStoreUserSettings : UserSettings, IDataStore
    {
        public Dictionary<string, object> Keys2value = null;

        protected override void Loaded()
        {
            if (Keys2value == null)
                Keys2value = new Dictionary<string, object>();
        }

        public Task ClearAsync()
        {
            Task t = new Task(() =>
            {
                Keys2value.Clear();
                Save();
            });
            t.Start();
            return t;
        }

        public Task DeleteAsync<T>(string key)
        {
            Task t = new Task(() =>
            {
                Keys2value.Remove(key);
                Save();
            });
            t.Start();
            return t;
        }

        public Task<T> GetAsync<T>(string key)
        {
            Task<T> t = new Task<T>(() =>
            {
                if (Keys2value.TryGetValue(key, out object value))
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
                Keys2value[key] = value;
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
            this.settings = settings;
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
        readonly Settings settings;
        readonly FieldInfo fieldInfo;
        readonly Dictionary<string, object> keys2value = null;

        public Task ClearAsync()
        {
            Task t = new Task(() =>
            {
                keys2value.Clear();
                settings.Save();
            });
            t.Start();
            return t;
        }

        public Task DeleteAsync<T>(string key)
        {
            Task t = new Task(() =>
            {
                keys2value.Remove(key);
                settings.Save();
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
                settings.Save();
            });
            t.Start();
            return t;
        }
    }
}