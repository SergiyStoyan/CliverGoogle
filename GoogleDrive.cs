﻿//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Cliver
{
    public partial class GoogleDrive : GoogleService<DriveService>
    {
        public GoogleDrive(GoogleSettings googleSettings) : base(googleSettings)
        {
        }

        //public GoogleDrive(string applicationName, IEnumerable<string> scopes, IDataStore dataStore, string clientSecretFile = null)
        //    : base(applicationName, scopes, dataStore, clientSecretFile)
        //{
        //}

        //public GoogleDrive(string applicationName, IEnumerable<string> scopes, string credentialDir = null, string clientSecretFile = null)
        //    : base(applicationName, scopes, credentialDir, clientSecretFile)
        //{
        //}

        const string folderMimeType = "application/vnd.google-apps.folder";

        public const string RootFolderId = "root";

        public class SearchFilter
        {
            public DateTime? ModifiedTimeMin = null;
            public string Name = null;
            public string ParentId = null;
            public bool? Trashed = false;
            public bool? IsFolder = null;
            public string Owner = null;
            public bool? IncludeItemsFromAllDrives = null;
            public bool? SupportsAllDrives = null;

            public const string OwnerMe = "me";

            internal string GetRequest()
            {
                List<string> qConditions = new List<string>();

                if (Name != null)
                    qConditions.Add("name='" + Regex.Replace(Name, @"'", @"\'") + "'");

                if (ParentId != null)
                    qConditions.Add("'" + ParentId + "' in parents");

                if (IsFolder == true)
                    qConditions.Add("mimeType='" + folderMimeType + "'");
                else if (IsFolder == false)
                    qConditions.Add("mimeType!='" + folderMimeType + "'");

                if (ModifiedTimeMin != null)
                    qConditions.Add("modifiedTime>'" + ModifiedTimeMin?.ToString("yyyy-MM-ddTHH:mm:ss") + "'");

                if (Trashed == true)
                    qConditions.Add("trashed=true");
                else if (Trashed == false)
                    qConditions.Add("trashed=false");

                if (Owner != null)
                    qConditions.Add("'" + Owner + "' in owners");

                return string.Join(" and ", qConditions);
            }
        }

        public IEnumerable<Google.Apis.Drive.v3.Data.File> FindObjects(SearchFilter searchFilter, string fields = "id, webViewLink", int pageSize = 1000/*, int maxCount = int.MaxValue*/)
        {
            string pageToken = null;
            string requestQ = searchFilter?.GetRequest();
            string requestFields = "nextPageToken, files(" + getProperFields(fields) + ")"; //"nextPageToken, files(" + (fields == null ? "id, webViewLink, parents, name, isAppAuthorized, ownedByMe" : fields) + ")";//"*";// 
            do
            {
                FilesResource.ListRequest request = Service.Files.List();
                request.Q = requestQ;
                request.IncludeItemsFromAllDrives = searchFilter.IncludeItemsFromAllDrives;
                request.SupportsAllDrives = searchFilter.SupportsAllDrives;
                //request.OrderBy = "modifiedTime desc";
                //request.Spaces = "drive";
                //request.Spaces = "appDataFolder";
                request.PageToken = pageToken;
                request.Fields = requestFields;
                //int size = maxCount - rfs.Count;
                //if (size < 1)
                //    break;
                //if (size > 1000)
                //    size = 1000;
                //request.PageSize = size;
                request.PageSize = pageSize;
                FileList fileList = request.Execute();
                foreach (Google.Apis.Drive.v3.Data.File f in fileList.Files)
                    yield return f;
                pageToken = fileList.NextPageToken;
            } while (pageToken != null);
        }

        public List<string> GetObjectPaths(Google.Apis.Drive.v3.Data.File @object)
        {
            List<string> paths = new List<string>();
            if (@object.Parents == null || @object.Name == null)
                @object = GetObject(@object.Id, "id, name, parents");
            buildPaths(paths, null, @object);
            return paths;
        }

        public Google.Apis.Drive.v3.Data.File GetObject(string objectIdOrLink, string fields = "id, webViewLink")
        {
            FilesResource.GetRequest getRequest = Service.Files.Get(GetObjectId(objectIdOrLink));
            getRequest.Fields = getProperFields(fields);
            try
            {
                return getRequest.Execute();
            }
            catch (Google.GoogleApiException e)
            {
                if (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)//when it comes to root 'My Drive', it throws 404 (it seems to depend on permissions)
                    return null;
                throw;
            }
        }

        public void DownloadFile(string fileIdOrLink, string localFile)
        {
            FilesResource.GetRequest request = Service.Files.Get(GetObjectId(fileIdOrLink));
            using (MemoryStream ms = new MemoryStream())
            {
                var progress = request.DownloadWithStatus(ms);
                using (FileStream fs = new FileStream(localFile, FileMode.Create, FileAccess.Write))
                {
                    ms.WriteTo(fs);
                    fs.Flush();
                }
                if (progress.Exception != null)
                    throw progress.Exception;
                if (progress.Status == Google.Apis.Download.DownloadStatus.Failed)
                    throw new Exception(Log.GetThisMethodName() + " got status " + progress.Status);
            }
        }

        public void DownloadFile(Uri file, string localFile)
        {
            Match m = Regex.Match(file.AbsoluteUri, @"https://.*?/file/d/(.*?)(/|$)");
            if (!m.Success)
                throw new Exception("Uri: " + file + " is not a google file link.");
            DownloadFile(m.Groups[1].Value, localFile);
        }

        /// <summary>
        /// (!) This method has a google internal limitation on size of the object. Use the other ExportDocument() instead.
        /// </summary>
        /// <param name="fileIdOrLink"></param>
        /// <param name="mimeType"></param>
        /// <param name="localFile"></param>
        /// <exception cref="Exception"></exception>
        public void ExportDocument(string fileIdOrLink, string mimeType, string localFile)
        {
            FilesResource.ExportRequest request = Service.Files.Export(GetObjectId(fileIdOrLink), mimeType);
            using (MemoryStream ms = new MemoryStream())
            {
                var progress = request.DownloadWithStatus(ms);
                using (FileStream fs = new FileStream(localFile, FileMode.Create, FileAccess.Write))
                {
                    ms.WriteTo(fs);
                    fs.Flush();
                }
                if (progress.Exception != null)
                    throw progress.Exception;
                if (progress.Status == Google.Apis.Download.DownloadStatus.Failed)
                    throw new Exception(Log.GetThisMethodName() + " got status " + progress.Status);
            }
        }

        public enum ExportType
        {
            Pdf,
            Odp,
            Pptx,
            Txt,
            Xlsx,
            Docx,
            Ods,
            Tsv,
            Csv,
            Zip
        }
        //public void ExportDocument(string fileIdOrLink, ExportType exportType, string localFile)
        ////!!!issues:
        //// - some exported xlsx cannot be read by POI;
        //// - ExportLinks always point to first sheet only when exporting to tsv/csv;
        //{
        //    Google.Apis.Drive.v3.Data.File file = GetObject(fileIdOrLink, "exportLinks");
        //    var el = file.ExportLinks.FirstOrDefault(a => Regex.IsMatch(a.Value, @"exportFormat=" + Regex.Escape(exportType.ToString()), RegexOptions.IgnoreCase));
        //    if (el.Value == null)
        //        throw new Exception("The document does not have an export link for the requested type " + exportType.ToString());
        //    using (Stream s = Service.HttpClient.GetStreamAsync(el.Value).Result)
        //    {
        //        using (FileStream fs = new FileStream(localFile, FileMode.Create, FileAccess.Write))
        //        {
        //            s.CopyTo(fs);
        //            fs.Flush();
        //        }
        //    }
        //}
        public void ExportDocument(string fileLink, ExportType exportType, string localFile)
            //!!!Export to tsv is done without quotations so multiline values brake rows
        {
            //string l = Regex.Replace(fileLink, @"/edit.*", "", RegexOptions.IgnoreCase) + "/gviz/tq?tqx=out:" + exportType + "&sheet=" + System.Web.HttpUtility.UrlEncode(sheetName);!!!produces js with exporting data
            //string l1 = Regex.Replace(fileLink, @"/edit.*", "", RegexOptions.IgnoreCase) + "/export?format=" + System.Web.HttpUtility.UrlEncode(exportType.ToString().ToLower()) + "&sheet=" + System.Web.HttpUtility.UrlEncode("Master");!!!gets only the first sheet
            Match m = Regex.Match(fileLink, @"^\s*(.*)/edit.*?(?:\#gid\=(\d+))?", RegexOptions.IgnoreCase);
            if (!m.Success)
                throw new Exception("Could not parse the link: " + fileLink);
            string l = m.Groups[1].Value + "/export?format=" + System.Web.HttpUtility.UrlEncode(exportType.ToString().ToLower()) + "&gid=" + m.Groups[2].Value;
            //https://docs.google.com/spreadsheets/d/e/{key}/pub?output=tsv&gid={gid}
            //https://docs.google.com/spreadsheets/d/KEY/export?format=csv&gid=SHEET_ID
            using (Stream s = Service.HttpClient.GetStreamAsync(l).Result)
            {
                using (FileStream fs = new FileStream(localFile, FileMode.Create, FileAccess.Write))
                {
                    s.CopyTo(fs);
                    fs.Flush();
                }
            }
        }

        public List<string> RemoveObjects(List<string> objectIds)
        {
            List<string> errors = new List<string>();
            BatchRequest batchRequest = new BatchRequest(Service);
            void callback(Google.Apis.Drive.v3.Data.File content, RequestError error, int index, HttpResponseMessage message)
            {
                if (error != null)
                    errors.Add("FileId=" + content.Id + ": " + error.Message);
            }
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Trashed = true
            };
            foreach (string oi in objectIds)
            {
                FilesResource.UpdateRequest updateRequest = Service.Files.Update(file, oi);
                batchRequest.Queue<Google.Apis.Drive.v3.Data.File>(updateRequest, callback);
            }
            batchRequest.ExecuteAsync().Wait();
            return errors;
        }

        string getProperFields(string fields)
        {
            return fields + (Regex.IsMatch(fields, @"(^|\s|,)id($|\s|,)", RegexOptions.IgnoreCase) ? "" : ", id");
        }

        public Google.Apis.Drive.v3.Data.File RenameObject(string objectId, string name2)
        {
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Name = name2
            };
            FilesResource.UpdateRequest updateRequest = Service.Files.Update(file, objectId);
            return updateRequest.Execute();
        }

        public const string MoveAll = "ALL";
        public Google.Apis.Drive.v3.Data.File MoveObject(string objectId, string newParentIds, string removingParentIds = MoveAll)
        {
            if (string.IsNullOrWhiteSpace(newParentIds))
                throw new Exception("newParentIds cannot be empty.");
            FilesResource.UpdateRequest updateRequest = Service.Files.Update(new Google.Apis.Drive.v3.Data.File(), objectId);
            updateRequest.AddParents = newParentIds;
            if (removingParentIds == MoveAll)
            {
                Google.Apis.Drive.v3.Data.File o = GetObject(objectId, "id, parents");
                updateRequest.RemoveParents = string.Join(",", o.Parents);
            }
            else if (!string.IsNullOrWhiteSpace(removingParentIds))
                updateRequest.RemoveParents = removingParentIds;
            return updateRequest.Execute();
        }

        public Google.Apis.Drive.v3.Data.File MoveObject(Google.Apis.Drive.v3.Data.File @object, string newParentIds, string removingParentIds = MoveAll)
        {
            if (removingParentIds == MoveAll)
            {
                if (@object.Parents == null)
                    throw new Exception("Object's Parents cannot be empty when removingParentIds == MoveAll.");
                removingParentIds = string.Join(",", @object.Parents);
            }
            return MoveObject(@object.Id, newParentIds, removingParentIds);
        }

        public Permission TransferOwnership(string objectId, string newOwnerEmail)
        {
            Permission permission = new Permission { Type = "user", Role = "owner", EmailAddress = newOwnerEmail };
            var createRequest = Service.Permissions.Create(permission, objectId);
            createRequest.TransferOwnership = true;
            return createRequest.Execute();
        }

        public Google.Apis.Drive.v3.Data.File SetReadonly(string objectId, bool @readonly)
        {
            FilesResource.UpdateRequest updateRequest = Service.Files.Update(
                new Google.Apis.Drive.v3.Data.File
                {
                    ContentRestrictions = new List<ContentRestriction>{
                        new ContentRestriction{ ReadOnly__= @readonly }
                    }
                },
                objectId
                );
            return updateRequest.Execute();
        }
    }
}