//********************************************************************************************
//Author: Sergiy Stoyan
//        systoyan@gmail.com
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
    /*
     TBD:
    - reliably hook OnInteractiveAuthentication() before interactively authorizing;
     
     */
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

        protected GoogleService(string applicationName, IEnumerable<string> scopes, /*IDataStore dataStore*/GoogleUserSettings googleUserSettings, string clientSecretFile = null)
        {
            if (clientSecretFile == null)
                clientSecretFile = Log.AppDir + "\\" + "googleClientSecret.json";
            ApplicationName = applicationName;
            Scopes = scopes;
            //DataStore = dataStore;
            GoogleUserSettings = googleUserSettings;
            ClientSecretFile = clientSecretFile;
        }
        public readonly string ApplicationName;
        public readonly IEnumerable<string> Scopes;
        //public readonly IDataStore DataStore;
        public readonly GoogleUserSettings GoogleUserSettings;
        public readonly string ClientSecretFile;

        //protected GoogleService(string applicationName, IEnumerable<string> scopes, string credentialDir = null, string clientSecretFile = null)
        //    : this(applicationName, scopes, createFileDataStore(credentialDir), clientSecretFile)
        //{ }
        //static FileDataStore createFileDataStore(string credentialDir)
        //{
        //    if (credentialDir == null)
        //        credentialDir = Log.AppCompanyUserDataDir + "\\" + typeof(T).Name + "Credential";
        //    return new FileDataStore(credentialDir, true);
        //}

        void initialize()
        {
            lock (this)
            {
                if (/*DataStore*/GoogleUserSettings.GetAsync<object>(ApplicationName/*???check if credential.UserId is always ApplicationName*/).Result == null)
                    OnInteractiveAuthentication?.Invoke();

                credential = GoogleRoutines.GetCredential(ApplicationName, Scopes, /*DataStore*/GoogleUserSettings, ClientSecretFile);

                BaseClientService.Initializer serviceInitializer = new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                };
                service = (T)Activator.CreateInstance(typeof(T), serviceInitializer);

                {//setting GoogleAccount info
                    Google.Apis.Auth.OAuth2.Responses.TokenResponse tokenResponse = credential.Flow.DataStore.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>(credential.UserId).Result;
                    if (tokenResponse.IssuedUtc.AddSeconds(30) > DateTime.UtcNow)
                    {
                        try
                        {
                            Gmail gmail = this as Gmail;
                            if (gmail != null)//requires less permission scopes than the other services
                            {
                                var userProfile = gmail.GetUserProfile();
                                googleAccount = userProfile.EmailAddress;
                            }
                            else//requires these scopes: "https://www.googleapis.com/auth/userinfo.profile", "https://www.googleapis.com/auth/userinfo.email" 
                            {
                                googleAccount = GoogleRoutines.GetUserMainEmail(credential);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error("Could not get the google account info. Make sure that the respective permission scopes are provided.", e);
                            googleAccount = "<not available>";
                        }
                        GoogleUserSettings settings = credential.Flow.DataStore as GoogleUserSettings;
                        if (settings != null)
                        {
                            settings.GoogleAccount = googleAccount;
                            settings.Save();
                        }
                        else
                        {
                            credential.Flow.DataStore.StoreAsync("_GoogleAccount", googleAccount).Wait();
                        }
                    }
                }
            }
        }

        public Action OnInteractiveAuthentication = null;

        protected T Service
        {
            get
            {
                if (service == null)
                    initialize();
                return service;
            }
        }
        T service;

        public UserCredential Credential
        {
            get
            {
                if (credential == null)
                    initialize();
                return credential;
            }
        }
        UserCredential credential;

        public string GoogleAccount
        {
            get
            {
                if (googleAccount == null)
                    initialize();
                return googleAccount;
            }
        }
        string googleAccount;

        public TimeSpan Timeout
        {
            get
            {
                return Service.HttpClient.Timeout;
            }
            set
            {
                Service.HttpClient.Timeout = value;
            }
        }
    }
}