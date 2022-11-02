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
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Cliver
{
    public class GoogleDrive : GoogleService<DriveService>
    {
        public GoogleDrive(string applicationName, IEnumerable<string> scopes, GoogleUserSettings googleUserSettings, string clientSecretFile = null)
            : base(applicationName, scopes, googleUserSettings, clientSecretFile)
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

        void buildPaths(List<string> paths, string currentPath, Google.Apis.Drive.v3.Data.File currentObject)
        {
            if (currentObject == null || currentObject.Parents == null || currentObject.Parents.Count < 1)//it is root 'My Drive'
            {
                paths.Add(currentPath);
                return;
            }
            currentPath = currentObject.Name + (string.IsNullOrWhiteSpace(currentPath) ? "" : "\\" + currentPath);
            foreach (string parentId in currentObject.Parents)
                buildPaths(paths, currentPath, GetObject(parentId, "id, name, parents"));
        }

        public Google.Apis.Drive.v3.Data.File GetObject(string objectId, string fields = "id, webViewLink")
        {
            FilesResource.GetRequest getRequest = Service.Files.Get(objectId);
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

        public enum GettingMode
        {
            AlwaysCreateNew,
            GetLatestExistingOrCreate,
            GetLatestExistingOnly,
        }
        public Google.Apis.Drive.v3.Data.File GetFolder(string parentFolderId, string folderName, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            if (parentFolderId == null && string.IsNullOrEmpty(folderName))//root folder
                return GetObject(RootFolderId, fields);
            if (gettingMode != GettingMode.AlwaysCreateNew)
            {
                SearchFilter sf = new SearchFilter { IsFolder = true, ParentId = parentFolderId, Name = folderName };
                IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                Google.Apis.Drive.v3.Data.File ff = fs.FirstOrDefault();
                if (ff != null)
                    return ff;
                if (gettingMode == GettingMode.GetLatestExistingOnly)
                    return null;
            }
            Google.Apis.Drive.v3.Data.File f = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = folderMimeType,
                Parents = parentFolderId != null ? new List<string> { parentFolderId } : null
            };
            var request = Service.Files.Create(f);
            request.Fields = getProperFields(fields);
            return request.Execute();
        }
        //replace GetFolder(string parentFolderId, string folderName, GettingMode gettingMode, string fields = "id, webViewLink")
        //public Google.Apis.Drive.v3.Data.File GetFolder(string baseFolderId, string relativeFolderPath, GettingMode gettingMode, string fields = "id, webViewLink"){ }

        public Google.Apis.Drive.v3.Data.File GetFolder(string folderPath, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            if (gettingMode == GettingMode.AlwaysCreateNew
                || !paths2object.TryGetValue(folderPath, out Google.Apis.Drive.v3.Data.File _object)
                || _object == null && gettingMode == GettingMode.GetLatestExistingOrCreate
                )
            {
                Match m = Regex.Match(folderPath, @"^(.*)\\+([^\\]+)$");
                string folderName;
                string parentFolderId;
                if (m.Success)
                {
                    folderName = m.Groups[2].Value;
                    Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(m.Groups[1].Value, gettingMode == GettingMode.AlwaysCreateNew ? GettingMode.GetLatestExistingOrCreate : gettingMode, fields);
                    if (parentFolder == null)
                        return null;
                    parentFolderId = parentFolder.Id;
                }
                else
                {
                    folderName = folderPath;
                    parentFolderId = RootFolderId;
                }
                _object = GetFolder(parentFolderId, folderName, gettingMode, fields);
                paths2object[folderPath] = _object;
            }
            return _object;
        }
        Dictionary<string, Google.Apis.Drive.v3.Data.File> paths2object = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();

        public Google.Apis.Drive.v3.Data.File GetFile(string filePath, string fields = "id, webViewLink")
        {
            if (!paths2object.TryGetValue(filePath, out Google.Apis.Drive.v3.Data.File @object))
            {
                string parentFolderPath = Regex.Replace(filePath, @"\\[^\\]*$", "");
                Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(parentFolderPath, GettingMode.GetLatestExistingOnly);
                if (parentFolder == null)
                    return null;

                string fileName = Regex.Replace(filePath, @".*\\", "");
                SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = parentFolder.Id, Name = fileName };
                IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                @object = fs.FirstOrDefault();
                if (@object != null)
                    paths2object[filePath] = @object;
            }
            return @object;
        }
        //replace GetFile(string filePath, string fields = "id, webViewLink")
        //public Google.Apis.Drive.v3.Data.File GetFile(string baseFolderId, string relativeFilePath, string fields = "id, webViewLink"){ }

        public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, string remoteFilePath, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        {
            Google.Apis.Drive.v3.Data.File folder = GetFolder(PathRoutines.GetFileDir(remoteFilePath), GettingMode.GetLatestExistingOrCreate, fields);
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Name = PathRoutines.GetFileName(remoteFilePath),
                //MimeType = getMimeType(localFile), 
                //Description=,
            };
            using (FileStream fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read))
            {
                if (updateExisting)
                {
                    SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = folder.Id, Name = file.Name };
                    IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                    Google.Apis.Drive.v3.Data.File f = fs.FirstOrDefault();
                    if (f != null)
                    {
                        FilesResource.UpdateMediaUpload updateMediaUpload = Service.Files.Update(file, f.Id, fileStream, contentType != null ? contentType : getMimeType(localFile));
                        updateMediaUpload.Fields = getProperFields(fields);
                        Google.Apis.Upload.IUploadProgress uploadProgress = updateMediaUpload.Upload();
                        if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
                            throw new Exception("Uploading file failed.", uploadProgress.Exception);
                        if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
                            throw new Exception("Uploading file has not been completed.");
                        return updateMediaUpload.ResponseBody;
                    }
                }
                {
                    file.Parents = new List<string>
                    {
                        folder.Id
                    };
                    FilesResource.CreateMediaUpload createMediaUpload = Service.Files.Create(file, fileStream, contentType != null ? contentType : getMimeType(localFile));
                    createMediaUpload.Fields = getProperFields(fields);
                    Google.Apis.Upload.IUploadProgress uploadProgress = createMediaUpload.Upload();
                    if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
                        throw new Exception("Uploading file failed.", uploadProgress.Exception);
                    if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
                        throw new Exception("Uploading file has not been completed.");
                    return createMediaUpload.ResponseBody;
                }
            }
        }
        static string getMimeType(string fileName)
        {
            string mimeType = "application/unknown";
            string ext = Path.GetExtension(fileName).ToLower();
            Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            if (regKey != null && regKey.GetValue("Content Type") != null)
                mimeType = regKey.GetValue("Content Type").ToString();
            return mimeType;
        }
        //replace UploadFile(string localFile, string remoteFilePath, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        //public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, string remoteBaseFolderId, string remoteRelativeFilePath, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink"){ }

        public void DownloadFile(string fileId, string localFile)
        {
            FilesResource.GetRequest request = Service.Files.Get(fileId);
            using (MemoryStream ms = new MemoryStream())
            {
                request.Download(ms);
                using (FileStream fs = new FileStream(localFile, FileMode.Create, FileAccess.Write))
                {
                    ms.WriteTo(fs);
                    fs.Flush();
                }
            }
        }

        public void DownloadFile(Uri file, string localFile)
        {
            Match m = Regex.Match(file.AbsoluteUri, @"https://.*?/file/d/(.*?)(/|$)");
            if (!m.Success)
                throw new Exception("Uri: " + file + " is not a google file link.");
            DownloadFile(m.Groups[1].Value, localFile);
        }

        public Google.Apis.Drive.v3.Data.File DownloadFileByPath(string remoteFilePath, string localFile)
        {
            Google.Apis.Drive.v3.Data.File file = GetFile(remoteFilePath);
            if (file == null)
                return null;
            DownloadFile(file.Id, localFile);
            return file;
        }
        //replace public Google.Apis.Drive.v3.Data.File DownloadFileByPath(string remoteFilePath, string localFile)
        //public Google.Apis.Drive.v3.Data.File DownloadFileByPath(string remoteBaseFolderId, string remoteRelativeFilePath, string localFile){}

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

        public static string ExtractObjectIdFromWebLink(string webLink)
        {
            webLink = Regex.Replace(webLink, @"/(edit|view)[^/]*\s*$", "", RegexOptions.IgnoreCase);
            Match m = Regex.Match(webLink, @"/([^/]*)$", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}