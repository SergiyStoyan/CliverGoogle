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
                throw new Exception(nameof(file.MimeType) + " is not set.");
            return file.MimeType == GoogleDrive.FolderMimeType;
        }

        //static public bool IsLocked(this Google.Apis.Drive.v3.Data.File file) !!!dangerous because:
        //- file.ContentRestrictions == null when unlocked;
        //- file can be aged;
        //{
        //    if (file.ContentRestrictions == null)
        //        throw new Exception(nameof(file.ContentRestrictions) + " is not set.");
        //    return file.ContentRestrictions.Any(a => a.ReadOnly__ == true);
        //}
    }
}