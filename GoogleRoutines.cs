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
//using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Cliver
{
    public class GoogleRoutines
    {
        static public string GetDocumentId(string documentUrl)
        {
            Match m = Regex.Match(documentUrl, @"^.*?/d/(.+?)(/|$)");
            if (!m.Success)
                throw new Exception("Could not parse ID from documentUrl:" + documentUrl);
            return m.Groups[1].Value;
        }

        //public static Google.Apis.Oauth2.v2.Data.Userinfo GetUserInfo(UserCredential credential/*, string applicationName*/)
        //{
        //    var oauthSerivce = new Google.Apis.Oauth2.v2.Oauth2Service(
        //     new BaseClientService.Initializer()
        //     {
        //         HttpClientInitializer = credential,
        //         //ApplicationName = "OAuth 2.0 Sample",
        //     });

        //    return oauthSerivce.Userinfo.Get().Execute();
        //}

        public static string GetUserMainEmail(UserCredential credential)
        {
            JObject r = GetUserInfo(credential);
            //if (r["errors"]?.Any() == true)
            //    return null;
            if (r["email"] != null)
                return r["email"].ToString();
            return null;
        }

        public static JObject GetUserInfo(UserCredential credential)
        {
            //FormUrlEncodedContent c = new FormUrlEncodedContent(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("access_token", credential.Token.AccessToken) });
            Dictionary<string, string> names2value = new Dictionary<string, string> { { "access_token", credential.Token.AccessToken } };
            System.Net.HttpWebRequest userinfoRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://www.googleapis.com/oauth2/v3/userinfo?" + WebRoutines.GetUrlQuery(names2value));
            userinfoRequest.Method = "GET";
            System.Net.WebResponse userinfoResponse = userinfoRequest.GetResponse();
            using (StreamReader userinfoResponseReader = new StreamReader(userinfoResponse.GetResponseStream()))
                return JObject.Parse(userinfoResponseReader.ReadToEnd());
        }

        //public static JObject GetUserInfo2(UserCredential credential)
        //{
        //    System.Net.HttpWebRequest userinfoRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://www.googleapis.com/oauth2/v3/userinfo");
        //    userinfoRequest.Method = "GET";
        //    userinfoRequest.Headers.Add(string.Format("Authorization: Bearer {0}", credential.Token.AccessToken));
        //    userinfoRequest.ContentType = "application/x-www-form-urlencoded";
        //    //userinfoRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        //    System.Net.WebResponse userinfoResponse = userinfoRequest.GetResponse();
        //    using (StreamReader userinfoResponseReader = new StreamReader(userinfoResponse.GetResponseStream()))
        //        return JObject.Parse(userinfoResponseReader.ReadToEnd());
        //}

        public static UserCredential GetCredential(string applicationName, IEnumerable<string> scopes, IDataStore dataStore, string clientSecretFile)
        {
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
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        new GoogleAuthorizationCodeFlow.Initializer
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
}