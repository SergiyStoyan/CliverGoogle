//********************************************************************************************
//Author: Sergey Stoyan
//        sergey.stoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Cliver
{
    public class GoogleSheet : IDisposable
    {
        ~GoogleSheet()
        {
            Dispose();
        }

        public void Dispose()
        {
            lock (this)
            {
                if (sheetsService != null)
                {
                    sheetsService.Dispose();
                    sheetsService = null;
                }
            }
        }

        public GoogleSheet(string applicationName, IEnumerable<string> scopes)
        {
            UserCredential credential;
            using (var stream = new FileStream(Log.AppDir + "\\googleCredentials.json", FileMode.Open, FileAccess.Read))
            {
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets: GoogleClientSecrets.FromStream(stream).Secrets,
                    scopes: scopes,
                    user: applicationName,
                    taskCancellationToken: System.Threading.CancellationToken.None,
                    dataStore: new FileDataStore(CredentialsTokenDir, true)
                    ).Result;
            }
            sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName,
            });
        }

        public GoogleSheet(string applicationName, IEnumerable<string> scopes, IDataStore dataStore, string clientSecretFile = null)
        {
            UserCredential credential = GoogleRoutines.GetCredential(applicationName, scopes, dataStore, clientSecretFile);
            sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName,
            });
        }

        public GoogleSheet(string applicationName, IEnumerable<string> scopes, string credentialDir = null, string clientSecretFile = null)
        {
            CredentialDir = credentialDir != null ? credentialDir : Log.AppCompanyUserDataDir + "\\gmailCredential";
            UserCredential credential = GoogleRoutines.GetCredential(applicationName, scopes, CredentialDir, clientSecretFile);
            sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName,
            });
        }
        SheetsService sheetsService;

        public readonly string CredentialDir;

        public static readonly string CredentialsTokenDir = Log.AppCompanyUserDataDir + "\\googleCredentialsToken";

        public void test()
        {
            AddFilterViewRequest addFilterViewRequest = new AddFilterViewRequest { Filter = new FilterView { Criteria = new Dictionary<string, FilterCriteria> { { "1", new FilterCriteria { Condition = new BooleanCondition { } } } } } };
            SpreadsheetsResource.ValuesResource.GetRequest getRequest = sheetsService.Spreadsheets.Values.Get("1k-dLZFk4YmjX__3Yb9__A6JJojUPpNEE9CucZuyULSU", "Items!A1:C3");
            //            SpreadsheetsResource.GetRequest getRequest = sheetsService.Spreadsheets.Get("1k-dLZFk4YmjX__3Yb9__A6JJojUPpNEE9CucZuyULSU");
            var response = getRequest.Execute();
        }
    }
}