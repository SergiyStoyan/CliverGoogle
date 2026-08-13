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
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;
//using Google = global::Google;

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

        public const string FolderMimeType = "application/vnd.google-apps.folder";

        public const string RootFolderId = "root";

        public class SearchFilter
        {
            public DateTime? ModifiedTimeMin = null;
            //public class NameFilter
            //{
            //    public string Value;
            //    public enum Operators
            //    {
            //        Contains,
            //        IsEqual,
            //        IsNotEqual
            //    }
            //    Operators Operator;
            //}
            //public NameFilter Name = null;
            public string Name = null;
            public string ParentLinkOrId = null;
            public bool? Trashed = false;
            public bool? IsFolder = null;
            public string Owner = null;
            public bool? IncludeItemsFromAllDrives = null;
            public bool? SupportsAllDrives = null;

            public string OrderBy = null;// "modifiedTime desc";

            public const string OwnerMe = "me";

            internal string GetRequest()
            {
                List<string> qConditions = new List<string>();

                if (Name != null)
                    qConditions.Add("name='" + Regex.Replace(Name, @"'", @"\'") + "'");

                if (ParentLinkOrId != null)
                    qConditions.Add("'" + GetObjectId(ParentLinkOrId) + "' in parents");

                if (IsFolder == true)
                    qConditions.Add("mimeType='" + FolderMimeType + "'");
                else if (IsFolder == false)
                    qConditions.Add("mimeType!='" + FolderMimeType + "'");

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
            string requestFields = "nextPageToken, files(" + GetNormalizedRequestFields(fields) + ")"; //"nextPageToken, files(" + (fields == null ? "id, webViewLink, parents, name, isAppAuthorized, ownedByMe" : fields) + ")";//"*";// 
            do
            {
                FilesResource.ListRequest request = Service.Files.List();
                request.Q = requestQ;
                request.IncludeItemsFromAllDrives = searchFilter.IncludeItemsFromAllDrives;
                request.SupportsAllDrives = searchFilter.SupportsAllDrives;
                request.OrderBy = searchFilter.OrderBy;
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

        public List<Path> GetObjectPaths(Google.Apis.Drive.v3.Data.File @object)
        {
            List<Path> paths = new List<Path>();
            if (@object.Parents == null || @object.Name == null)
                @object = GetObject(@object.Id, "id, name, parents");
            buildPaths(paths, null, @object);
            return paths;
        }

        public Google.Apis.Drive.v3.Data.File GetObject(string objectIdOrLink, string fields = "id, webViewLink")
        {
            FilesResource.GetRequest getRequest = Service.Files.Get(GetObjectId(objectIdOrLink));
            getRequest.Fields = GetNormalizedRequestFields(fields);
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

        public void DownloadFile(string fileIdOrLink, string localFile, bool updateExisting = true)
        {
            string tempLocalFile;
            if (System.IO.File.Exists(localFile))
                if (updateExisting)
                    tempLocalFile = PathRoutines.InsertSuffixBeforeFileExtension(localFile, "_downloading_" + DateTime.Now.Ticks, false);
                else
                    throw new Exception2(nameof(updateExisting) + " = FALSE. Cannot update the file: " + localFile);
            else
                tempLocalFile = localFile;

            FilesResource.GetRequest request = Service.Files.Get(GetObjectId(fileIdOrLink));
            using (MemoryStream ms = new MemoryStream())
            {
                var progress = request.DownloadWithStatus(ms);
                using (FileStream fs = new FileStream(tempLocalFile, FileMode.Create, FileAccess.Write))
                {
                    ms.WriteTo(fs);
                    fs.Flush();
                }
                if (progress.Exception != null)
                    throw progress.Exception;
                if (progress.Status == Google.Apis.Download.DownloadStatus.Failed)
                    throw new Exception(Log.GetThisMethodName() + " got status " + progress.Status);
            }

            if (tempLocalFile != localFile)
                FileSystemRoutines.MoveFile(tempLocalFile, localFile);
        }

        public string DownloadFile2Folder(string fileIdOrLink, string localFolder, string localFileName = null, bool updateExisting = true)
        {
            if (localFileName == null)
            {
                FilesResource.GetRequest request = Service.Files.Get(GetObjectId(fileIdOrLink));
                request.Fields = "id, name";
                var f = request.Execute();
                if (f == null)
                    throw new Exception("File does not exist: " + fileIdOrLink);
                localFileName = PathRoutines.GetLegalizedFileName(f.Name);
            }
            string localFile = localFolder + System.IO.Path.DirectorySeparatorChar + localFileName;
            string tempLocalFile;
            if (System.IO.File.Exists(localFile))
                if (updateExisting)
                    tempLocalFile = PathRoutines.InsertSuffixBeforeFileExtension(localFile, "_downloading_" + DateTime.Now.Ticks, false);
                else
                    throw new Exception2(nameof(updateExisting) + " = FALSE. Cannot update the file: " + localFile);
            else
                tempLocalFile = localFile;
            DownloadFile(fileIdOrLink, tempLocalFile);
            if (tempLocalFile != localFile)
                FileSystemRoutines.MoveFile(tempLocalFile, localFile);
            return localFile;
        }

        public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, string remoteFolderIdOrLink, string remoteFileName = null, bool updateExisting = true, string fields = "id, webViewLink")
        {
            if (string.IsNullOrWhiteSpace(remoteFileName))
                remoteFileName = PathRoutines.GetFileName(localFile);
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Name = remoteFileName,
                //MimeType = getMimeType(localFile), 
            };
            string remoteFolderId = GetObjectId(remoteFolderIdOrLink);
            using (FileStream fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read))
            {
                if (updateExisting)
                {
                    SearchFilter sf = new SearchFilter { IsFolder = false, ParentLinkOrId = remoteFolderId, Name = file.Name, OrderBy = "createdTime desc" };
                    IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields, 5);
                    Google.Apis.Drive.v3.Data.File f = fs.FirstOrDefault();
                    if (f != null)
                    {
                        FilesResource.UpdateMediaUpload updateMediaUpload = Service.Files.Update(file, f.Id, fileStream, getMimeType(localFile));
                        updateMediaUpload.Fields = GetNormalizedRequestFields(fields);
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
                        remoteFolderId
                    };
                    FilesResource.CreateMediaUpload createMediaUpload = Service.Files.Create(file, fileStream, getMimeType(localFile));
                    createMediaUpload.Fields = GetNormalizedRequestFields(fields);
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
            string ext = System.IO.Path.GetExtension(fileName).ToLower();
            Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            return regKey?.GetValue("Content Type")?.ToString();
        }

        public Google.Apis.Drive.v3.Data.File UploadFile2Folder(string localFile, string remoteFolderLinkOrId, bool updateExisting = true, string fields = "id, webViewLink")
        {
            return UploadFile(localFile, remoteFolderLinkOrId, null, updateExisting, fields);
        }

        public Google.Apis.Drive.v3.Data.File UpdateFile(string localFile, string remoteFileIdOrLink, string fileName2 = null, string fields = "id, webViewLink")
        {
            using (FileStream fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read))
            {
                string contentType2 = getMimeType(localFile);
                Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName2,// != null ? fileName2 : PathRoutines.GetFileName(localFile),
                    //MimeType = contentType2,
                };
                FilesResource.UpdateMediaUpload updateMediaUpload = Service.Files.Update(file, GetObjectId(remoteFileIdOrLink), fileStream, contentType2);
                updateMediaUpload.Fields = GetNormalizedRequestFields(fields);
                Google.Apis.Upload.IUploadProgress uploadProgress = updateMediaUpload.Upload();
                if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
                    throw new Exception("Uploading file failed.", uploadProgress.Exception);
                if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw new Exception("Uploading file has not been completed.");
                return updateMediaUpload.ResponseBody;
            }
        }

        /// <summary>
        /// (!) It works by downloading and uploading file content. That's it loads network and hence is slow.
        /// </summary>
        /// <param name="fileIdOrLink"></param>
        /// <param name="folderIdOrLink"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        public Google.Apis.Drive.v3.Data.File UpdateFile2(string fileIdOrLink1, string fileIdOrLink2, string fileName2 = null, string fields = "id, webViewLink")
        {
            using (var memoryStream = new MemoryStream())
            {
                var f1 = GetObject(fileIdOrLink1);
                if (f1 == null)
                    throw new Exception("Google file does not exist: " + fileIdOrLink1);

                Service.Files.Get(f1.Id).Download(memoryStream);
                memoryStream.Position = 0;

                Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName2,// != null ? fileName2 : PathRoutines.GetFileName(f1.Name),
                    MimeType = f1.MimeType,
                    //Description=,
                };
                FilesResource.UpdateMediaUpload updateMediaUpload = Service.Files.Update(file, GetObjectId(fileIdOrLink2), memoryStream, f1.MimeType);
                updateMediaUpload.Fields = GetNormalizedRequestFields(fields);
                Google.Apis.Upload.IUploadProgress uploadProgress = updateMediaUpload.Upload();
                if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
                    throw new Exception("Uploading file failed.", uploadProgress.Exception);
                if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw new Exception("Uploading file has not been completed.");
                return updateMediaUpload.ResponseBody;
            }
        }

        public bool IsLocked(string fileIdOrLink)
        {
            var f = GetObject(fileIdOrLink, "id, webViewLink, contentRestrictions");
            if (f.ContentRestrictions == null)
                return false;
            return f.ContentRestrictions.Any(a => a.ReadOnly__ == true);
        }

        public bool LockFile(string fileIdOrLink, string reason, bool? ownerRestricted, bool exceptionOnFail = true)
        {
            var contentRestriction = new ContentRestriction { ReadOnly__ = true, Reason = reason, OwnerRestricted = ownerRestricted };
            var f = SetContentRestriction(fileIdOrLink, contentRestriction);
            if (f.ContentRestrictions == null)
            {
                if (exceptionOnFail)
                    throw new Exception("Remote object do not support ContentRestrictions: " + fileIdOrLink);
                return false;
            }
            var cr = f.ContentRestrictions.Any(a => a.ReadOnly__ == true);
            if (!cr)
                if (exceptionOnFail)
                    throw new Exception("Could not set ContentRestrictions.ReadOnly = " + contentRestriction.ReadOnly__ + " for: " + fileIdOrLink);
                else
                    return false;
            return true;
        }

        public bool UnlockFile(string fileIdOrLink, bool exceptionOnFail = true)
        {
            var contentRestriction = new ContentRestriction { ReadOnly__ = false };
            var f = SetContentRestriction(fileIdOrLink, contentRestriction);
            if (f.ContentRestrictions == null)
                return true;
            var cr = f.ContentRestrictions.Any(a => a.ReadOnly__ == true);
            if (cr)
                if (exceptionOnFail)
                    throw new Exception("Could not set ContentRestrictions.ReadOnly = " + contentRestriction.ReadOnly__ + " for: " + fileIdOrLink);
                else
                    return false;
            return true;
        }

        public Google.Apis.Drive.v3.Data.File SetContentRestriction(string fileIdOrLink, ContentRestriction contentRestriction)
        {
            if (contentRestriction.ReadOnly__ == null)
                throw new Exception(nameof(contentRestriction.ReadOnly__) + " must be specified.");

            Google.Apis.Drive.v3.Data.File f = new Google.Apis.Drive.v3.Data.File();
            f.ContentRestrictions = new List<ContentRestriction> { contentRestriction };
            FilesResource.UpdateRequest request = Service.Files.Update(f, GetObjectId(fileIdOrLink));
            request.Fields = "id, webViewLink, contentRestrictions";
            f = request.Execute();
            if (f == null)
                throw new Exception("Remote object does not exist: " + fileIdOrLink);
            return f;
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
        {
            //string l = Regex.Replace(fileLink, @"/edit.*", "", RegexOptions.IgnoreCase) + "/gviz/tq?tqx=out:" + exportType + "&sheet=" + System.Web.HttpUtility.UrlEncode(sheetName);!!!produces js with exporting data

            var ids = GoogleSheet.GetIdsFromBookLink(fileLink);

            //https://docs.google.com/spreadsheets/d/e/{key}/pub?output=tsv&gid={gid} - !!!404 (probably because not public)
            //string l = "https://docs.google.com/spreadsheets/d/e/" + ids.BookId + "/pub?output=" + System.Web.HttpUtility.UrlEncode(exportType.ToString().ToLower()) + "&gid=" + ids.SheetId;

            //https://docs.google.com/spreadsheets/d/KEY/export?format=csv&gid=SHEET_ID - !!!Export to tsv is done without quotations so multiline values brake rows
            //string l = m.Groups[1].Value + "/export?format=" + System.Web.HttpUtility.UrlEncode(exportType.ToString().ToLower()) + "&gid=" + m.Groups[2].Value;

            //https://docs.google.com/feeds/download/spreadsheets/Export?key<FILE_ID>&exportFormat=csv&gid=<>   - multiline values are converted into singleline joined with space
            string l = "https://docs.google.com/feeds/download/spreadsheets/Export?key=" + ids.BookId + "&exportFormat=" + System.Web.HttpUtility.UrlEncode(exportType.ToString().ToLower()) + "&gid=" + ids.SheetId;
            using (Stream s = Service.HttpClient.GetStreamAsync(l).Result)
            {
                using (FileStream fs = new FileStream(localFile, FileMode.Create, FileAccess.Write))
                {
                    s.CopyTo(fs);
                    fs.Flush();
                }
            }
        }

        public List<string> RemoveObjects(IEnumerable<string> objectIdOrLinks, bool deletePermanently = false)
        {
            List<string> errors = new List<string>();
            BatchRequest batchRequest = new BatchRequest(Service);
            void callback(Google.Apis.Drive.v3.Data.File content, RequestError error, int index, HttpResponseMessage message)
            {
                if (error != null)
                    errors.Add("FileId=" + content?.Id + ", index=" + index + " : " + error.ToStringByJson());
            }
            IClientServiceRequest request = null;
            if (deletePermanently)
            {
                foreach (string objectIdOrLink in objectIdOrLinks)
                {
                    request = Service.Files.Delete(GetObjectId(objectIdOrLink));
                }
            }
            else
            {
                Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
                {
                    Trashed = true
                };
                foreach (string objectIdOrLink in objectIdOrLinks)
                {
                    request = Service.Files.Update(file, GetObjectId(objectIdOrLink));
                }
            }
            batchRequest.Queue<Google.Apis.Drive.v3.Data.File>(request, callback);
            batchRequest.ExecuteAsync().Wait();
            return errors;
        }

        public List<string> TrashObjects(params string[] objectIdOrLinks)
        {
            return RemoveObjects(objectIdOrLinks, false);
        }

        public List<string> TrashObjects(IEnumerable<string> objectIdOrLinks)
        {
            return RemoveObjects(objectIdOrLinks, false);
        }

        public List<string> DeleteObjects(params string[] objectIdOrLinks)
        {
            return RemoveObjects(objectIdOrLinks, true);
        }

        public List<string> DeleteObjects(IEnumerable<string> objectIdOrLinks)
        {
            return RemoveObjects(objectIdOrLinks, true);
        }

        public IEnumerable<Google.Apis.Drive.v3.Data.File> GetChildren(string folderIdOrLink, string fields = "id, webViewLink, mimeType", string orderBy = "name", int pageSize = 1000)
        {
            foreach (var f in FindObjects(new SearchFilter { ParentLinkOrId = folderIdOrLink, OrderBy = orderBy }, fields, pageSize))
                yield return f;
        }

        public Google.Apis.Drive.v3.Data.File RenameObject(string objectIdOrLink, string name2)
        {
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Name = name2
            };
            FilesResource.UpdateRequest updateRequest = Service.Files.Update(file, GetObjectId(objectIdOrLink));
            return updateRequest.Execute();
        }

        public Google.Apis.Drive.v3.Data.File MoveObject(string objectIdOrLink1, string folderIdOrLink2, string fileName2 = null, bool updateExisting = true, string fields = "id, webViewLink")
        {
            if (string.IsNullOrWhiteSpace(folderIdOrLink2))
                throw new Exception(nameof(folderIdOrLink2) + " cannot be empty.");

            if (updateExisting)
            {
                var file2 = FindObjects(new SearchFilter { Name = fileName2, ParentLinkOrId = folderIdOrLink2, OrderBy = "createdTime desc" }, null, 5).FirstOrDefault();
                if (file2 != null)
                    return UpdateFile2(objectIdOrLink1, file2.Id, fileName2, fields);
            }

            Google.Apis.Drive.v3.Data.File f = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName2
            };
            //if (removeParentIds == MoveObject_removeAllOldParents)
            //    f.Parents = addParentIds.Split(',');
            var request = Service.Files.Update(f, GetObjectId(objectIdOrLink1));
            //if (removeParentIds != MoveObject_removeAllOldParents)
            //{
            request.AddParents = GetObjectId(folderIdOrLink2);
            Google.Apis.Drive.v3.Data.File o = GetObject(objectIdOrLink1, fields);
            if (f == null)
                throw new Exception("Google file does not exist: " + objectIdOrLink1);
            request.RemoveParents = string.Join(",", o.Parents);
            //}
            request.Fields = GetNormalizedRequestFields(fields);
            return request.Execute();
        }

        public Permission TransferOwnership(string objectIdOrLink, string newOwnerEmail)
        {
            Permission permission = new Permission { Type = "user", Role = "owner", EmailAddress = newOwnerEmail };
            var createRequest = Service.Permissions.Create(permission, GetObjectId(objectIdOrLink));
            createRequest.TransferOwnership = true;
            return createRequest.Execute();
        }
    }
}