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
            if (clientSecretFile == null)
                clientSecretFile = Log.AppDir + "\\" + DefaultClientSecretFile;
            Credential = GoogleRoutines.GetCredential(applicationName, scopes, dataStore, clientSecretFile);

            //!!!consider moving all the long actions out of the constructor!
            initialize(applicationName);

            {//setting GoogleAccount info
                Google.Apis.Auth.OAuth2.Responses.TokenResponse tokenResponse = Credential.Flow.DataStore.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>(Credential.UserId).Result;
                if (tokenResponse.IssuedUtc.AddMinutes(1) > DateTime.UtcNow)
                {
                    string googleAccount = null;
                    try
                    {
                        Gmail gmail = this as Gmail;
                        if (gmail != null)//it requires less permission scopes
                        {
                            var userProfile = gmail.GetUserProfile();
                            googleAccount = userProfile.EmailAddress;
                        }
                        else//it needs these scopes: "https://www.googleapis.com/auth/userinfo.profile", "https://www.googleapis.com/auth/userinfo.email" 
                        {
                            googleAccount = GoogleRoutines.GetUserMainEmail(Credential);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Could not get UserInfo. Make sure that the respective permission scopes are provided.", e);
                    }
                    if (string.IsNullOrWhiteSpace(googleAccount))
                        googleAccount = "<not available>";
                    GoogleDataStoreUserSettings settings = dataStore as GoogleDataStoreUserSettings;
                    if (settings != null)
                    {
                        settings.GoogleAccount = googleAccount;
                        settings.Save();
                    }
                    else
                    {
                        dataStore.StoreAsync("_GoogleAccount", googleAccount).Wait();
                    }
                    GoogleAccount = googleAccount;
                }
            }
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

        public const string DefaultClientSecretFile = "googleClientSecret.json";

        protected GoogleService(string applicationName, IEnumerable<string> scopes, string credentialDir = null, string clientSecretFile = null)
        {
            if (credentialDir == null)
                credentialDir = Log.AppCompanyUserDataDir + "\\" + typeof(T).Name + "Credential";
            if (clientSecretFile == null)
                clientSecretFile = Log.AppDir + "\\" + DefaultClientSecretFile;

            //!!!consider moving all the long actions out of the constructor!
            Credential = GoogleRoutines.GetCredential(applicationName, scopes, credentialDir, clientSecretFile);
            initialize(applicationName);
        }

        protected T service { get; private set; }

        public UserCredential Credential { get; private set; }

        public string GoogleAccount { get; private set; }

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