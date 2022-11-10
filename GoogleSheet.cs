//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
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
        public GoogleSheet(string applicationName, IEnumerable<string> scopes, GoogleUserSettings googleUserSettings, string clientSecretFile = null)
            : base(applicationName, scopes, googleUserSettings, clientSecretFile)
        {
        }

        //public GoogleSheet(string applicationName, IEnumerable<string> scopes, IDataStore dataStore, string clientSecretFile = null)
        //    : base(applicationName, scopes, dataStore, clientSecretFile)
        //{
        //}

        //public GoogleSheet(string applicationName, IEnumerable<string> scopes, string credentialDir = null, string clientSecretFile = null)
        //    : base(applicationName, scopes, credentialDir, clientSecretFile)
        //{
        //}

        public void test()
        {
            AddFilterViewRequest addFilterViewRequest = new AddFilterViewRequest { Filter = new FilterView { Criteria = new Dictionary<string, FilterCriteria> { { "1", new FilterCriteria { Condition = new BooleanCondition { } } } } } };
            SpreadsheetsResource.ValuesResource.GetRequest getRequest = Service.Spreadsheets.Values.Get("1k-dLZFk4YmjX__3Yb9__A6JJojUPpNEE9CucZuyULSU", "Items!A1:C3");
            //            SpreadsheetsResource.GetRequest getRequest = service.Spreadsheets.Get("1k-dLZFk4YmjX__3Yb9__A6JJojUPpNEE9CucZuyULSU");
            var response = getRequest.Execute();
        }
    }
}