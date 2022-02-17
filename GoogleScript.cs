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
using Google.Apis.Script.v1;
using Google.Apis.Script.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Cliver
{
    partial class GoogleScript : IDisposable
    {
        ~GoogleScript()
        {
            Dispose();
        }

        public void Dispose()
        {
            lock (this)
            {
                if (scriptService != null)
                {
                    scriptService.Dispose();
                    scriptService = null;
                }
            }
        }

        public GoogleScript(string applicationName, IEnumerable<string> scopes, string scriptId, IDataStore dataStore, string clientSecretFile = null)
        {
            UserCredential credential = GoogleRoutines.GetCredential(applicationName, scopes, dataStore, clientSecretFile);
            scriptService = new ScriptService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName,
            });
            ScriptId = scriptId;
        }

        public GoogleScript(string applicationName, IEnumerable<string> scopes, string scriptId, string credentialDir = null, string clientSecretFile = null)
        {
            CredentialDir = credentialDir != null ? credentialDir : Log.AppCompanyUserDataDir + "\\googleScriptCredential";
            UserCredential credential = GoogleRoutines.GetCredential(applicationName, scopes, CredentialDir, clientSecretFile);
            scriptService = new ScriptService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName,
            });
            ScriptId = scriptId;
        }
        ScriptService scriptService;

        public readonly string CredentialDir;
        public readonly string ScriptId;

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
            ScriptsResource.RunRequest runRequest = scriptService.Scripts.Run(request, "AKfycbwONtX66jqAhHFTkVTyQ1xZr9iDzoPfuqGfQWjnJNf6-0OBN_MzdjvFpGNaqQ6ivJdWLw");
            Operation operation = runRequest.Execute();
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