//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
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

        protected GoogleService(GoogleSettings googleSettings)
        {
            GoogleSettings = googleSettings;
        }
        public readonly GoogleSettings GoogleSettings;

        void initialize()
        {
            lock (this)
            {
                if (service != null)
                    return;

                if (GoogleSettings.GetAsync<object>(GoogleSettings.ApplicationName/*???check if credential.UserId is always ApplicationName*/).Result == null)
                    OnInteractiveAuthentication?.Invoke();

                credential = GoogleRoutines.GetCredential(GoogleSettings.ApplicationName, GoogleSettings.Scopes, GoogleSettings, GoogleSettings.ClientSecretFile);

                BaseClientService.Initializer serviceInitializer = new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = GoogleSettings.ApplicationName,
                };
                service = (T)Activator.CreateInstance(typeof(T), serviceInitializer);

                {//setting GoogleAccount info
                    Google.Apis.Auth.OAuth2.Responses.TokenResponse tokenResponse = credential.Flow.DataStore.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>(credential.UserId).Result;
                    if (tokenResponse.IssuedUtc.AddSeconds(10) > DateTime.UtcNow
                        || string.IsNullOrWhiteSpace(GoogleSettings.GoogleAccount)
                        )//account was set right now and its name needs to be updated
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
                        if (GoogleSettings.GoogleAccount != googleAccount)
                        {
                            GoogleSettings.GoogleAccount = googleAccount;
                            GoogleSettings.Save();
                        }
                    }
                    else
                        googleAccount = GoogleSettings.GoogleAccount;
                }
            }
        }

        public Action OnInteractiveAuthentication = null;

        public T Service
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