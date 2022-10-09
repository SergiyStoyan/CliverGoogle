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
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;
using Google.Apis.Services;

namespace Cliver
{
    public abstract partial class GoogleService<T> : IDisposable where T : Google.Apis.Services.BaseClientService, new()
    {
        ~GoogleService()
        {
            Dispose();
        }

        public void Dispose()
        {
            lock (this)
            {
                if (service != null)
                {
                    service.Dispose();
                    service = null;
                }
            }
        }

        protected GoogleService(string applicationName, IEnumerable<string> scopes, IDataStore dataStore, string clientSecretFile = null)
        {
            Credential = GoogleRoutines.GetCredential(applicationName, scopes, dataStore, clientSecretFile);
            initialize(applicationName);
        }

        void initialize(string applicationName)
        {
            BaseClientService.Initializer serviceInitializer = new BaseClientService.Initializer
            {
                HttpClientInitializer = Credential,
                ApplicationName = applicationName,
            };
            service = (T)Activator.CreateInstance(typeof(T), serviceInitializer);
            Timeout = new TimeSpan(0, 0, 0, 300);
        }

        protected GoogleService(string applicationName, IEnumerable<string> scopes, string credentialDir = null, string clientSecretFile = null)
        {
            if (credentialDir == null)
                credentialDir = Log.AppCompanyUserDataDir + "\\" + typeof(T).Name + "Credential";
            Credential = GoogleRoutines.GetCredential(applicationName, scopes, credentialDir, clientSecretFile);
            initialize(applicationName);
        }

        protected T service { get; private set; }

        public UserCredential Credential { get; private set; }

        public TimeSpan Timeout
        {
            get
            {
                return service.HttpClient.Timeout;
            }
            set
            {
                service.HttpClient.Timeout = value;
            }
        }

        public string GetCredentialDir()
        {
            IDataStore ds = Credential.Flow.DataStore;
            if (ds is FileDataStore)
                return ((FileDataStore)ds).FolderPath;
            if (ds is GoogleDataStoreUserSettings)
                return ((GoogleDataStoreUserSettings)ds).__StorageDir;
            if (ds is GoogleDataStore)
                return ((GoogleDataStore)ds).Settings.__StorageDir;
            return null;
        }
    }
}