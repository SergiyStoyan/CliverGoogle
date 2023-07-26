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
using System.Security.Cryptography;

namespace Cliver
{
    public static class Extensions
    {
        static public bool IsFolder(this Google.Apis.Drive.v3.Data.File file)
        {
            if (file.MimeType == null) 
                throw new Exception("File.MimeType is not set.");
            return file.MimeType == GoogleDrive.FolderMimeType;
        }
    }
}