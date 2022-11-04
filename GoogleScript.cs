//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Script.v1;
using Google.Apis.Script.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Cliver
{
    public partial class GoogleScript : GoogleService<ScriptService>
    {
        public GoogleScript(string applicationName, IEnumerable<string> scopes, string scriptId, GoogleUserSettings googleUserSettings, string clientSecretFile = null)
            : base(applicationName, scopes, googleUserSettings, clientSecretFile)
        {
            ScriptId = scriptId;
        }

        //public GoogleScript(string applicationName, IEnumerable<string> scopes, string scriptId, IDataStore dataStore, string clientSecretFile = null)
        //    : base(applicationName, scopes, dataStore, clientSecretFile)
        //{
        //    ScriptId = scriptId;
        //}

        //public GoogleScript(string applicationName, IEnumerable<string> scopes, string scriptId, string credentialDir = null, string clientSecretFile = null)
        //    : base(applicationName, scopes, credentialDir, clientSecretFile)
        //{
        //    ScriptId = scriptId;
        //}

        public readonly string ScriptId;

        public int TryMaxCount = 3;
        public int RetryDelaySecs = 20;

        public object Run(string function, params object[] parameters)
        {
            ExecutionRequest request = new ExecutionRequest
            {
                Function = function,
                Parameters = parameters,
#if DEBUG
                DevMode = true,
#else
                DevMode = false,
#endif
            };
            ScriptsResource.RunRequest runRequest = Service.Scripts.Run(request, ScriptId);
            Operation operation = null;
            for (int tryCount = 1; ; tryCount++)
                try
                {
                    operation = runRequest.Execute();
                    break;
                }
                catch (Google.GoogleApiException e)
                {
                    if (tryCount >= TryMaxCount)
                        throw;
                    if (e.Error?.Code != 500)
                        throw;
                    Log.Warning2("Retrying (" + tryCount + ")... Sleeping " + RetryDelaySecs + " secs", e);
                    System.Threading.Thread.Sleep(RetryDelaySecs * 1000);
                }
            if (operation.Error != null)
            {
                string message = "Server error: " + operation.Error.ToStringByJson();
                throw new Exception(message);
            }
            return operation.Response["result"];
        }

        public class Exception : System.Exception
        {
            public Exception(string message) : base(message) { }
        }

        ///// <summary>
        ///// Requires the following OAuth scope: https://www.googleapis.com/auth/script.projects
        ///// </summary>
        ///// <param name="scriptTitle"></param>
        ///// <param name="scriptFiles"></param>
        ///// <param name="parentId">The Drive ID of a parent file that the created script project is bound to. 
        ///// This is usually the ID of a Google Doc, Google Sheet, Google Form, or Google Slides file. 
        ///// If not set, a standalone script project is created.</param>
        ///// <returns></returns>
        //public object Deploy(string scriptTitle, List<string> scriptFiles, string parentId = null)
        //{
        //    CreateProjectRequest request = new CreateProjectRequest
        //    {
        //        Title = scriptTitle,
        //         ParentId=parentId
        //    };
        //  ProjectsResource.CreateRequest createRequest = scriptService.Projects.Create(request);
        //    Project project = createRequest.Execute();
        //    //if (project== null)
        //    //{
        //    //    string message = "Server error: " + operation.Error.ToStringByJson();
        //    //    throw new Exception2(message);
        //    //}
        //    Content content = new Content { Files = scriptFiles, ScriptId = project.ScriptId };
        //    ProjectsResource.UpdateContentRequest updateContentRequest = scriptService.Projects.UpdateContent((updateContentRequest)
        //    project.u
        //}
    }
}