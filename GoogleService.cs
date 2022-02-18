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

namespace Cliver
{
    public partial class GoogleService<T> : IDisposable where T : class, IDisposable
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

        protected T service;

        public UserCredential Credential { get; protected set; }

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