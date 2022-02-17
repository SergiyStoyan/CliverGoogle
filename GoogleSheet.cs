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
    public class GoogleSheet : GoogleService<SheetsService>
    {
        public GoogleSheet(string applicationName, IEnumerable<string> scopes, IDataStore dataStore, string clientSecretFile = null)
        {
            Credential = GoogleRoutines.GetCredential(applicationName, scopes, dataStore, clientSecretFile);
            service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = Credential,
                ApplicationName = applicationName,
            });
        }

        public GoogleSheet(string applicationName, IEnumerable<string> scopes, string credentialDir = null, string clientSecretFile = null)
        {
            if (credentialDir == null)
                credentialDir = Log.AppCompanyUserDataDir + "\\googleSheetCredential";
            Credential = GoogleRoutines.GetCredential(applicationName, scopes, credentialDir, clientSecretFile);
            service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = Credential,
                ApplicationName = applicationName,
            });
        }

        public void test()
        {
            AddFilterViewRequest addFilterViewRequest = new AddFilterViewRequest { Filter = new FilterView { Criteria = new Dictionary<string, FilterCriteria> { { "1", new FilterCriteria { Condition = new BooleanCondition { } } } } } };
            SpreadsheetsResource.ValuesResource.GetRequest getRequest = service.Spreadsheets.Values.Get("1k-dLZFk4YmjX__3Yb9__A6JJojUPpNEE9CucZuyULSU", "Items!A1:C3");
            //            SpreadsheetsResource.GetRequest getRequest = service.Spreadsheets.Get("1k-dLZFk4YmjX__3Yb9__A6JJojUPpNEE9CucZuyULSU");
            var response = getRequest.Execute();
        }
    }
}