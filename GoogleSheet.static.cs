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

namespace Cliver
{
    public partial class GoogleSheet
    {
        public static (string BookId, int SheetId) GetSheetIds(string bookIdOrLink)
        {
            if (!IsBookLink(bookIdOrLink))
                return (bookIdOrLink, -1);
            return ExtractSheetIdsFromWebLink(bookIdOrLink);
        }

        public static (string BookId, int SheetId) ExtractSheetIdsFromWebLink(string webLink)
        {
            Match m = Regex.Match(webLink.Trim(), @"/(?'BookId'[^/]+)(/(edit|view))?(\#gid\=(?'SheetId'\d+))?$", RegexOptions.IgnoreCase);
            if (!m.Success)
                throw new Exception("Could not parse the link: " + webLink);
            return (m.Groups["BookId"].Value, int.Parse(m.Groups["SheetId"].Value));
        }

        public static bool IsBookLink(string v)
        {
            return Regex.IsMatch(v.Trim(), @"^\s*https?\://docs\.google\.com/spreadsheets/", RegexOptions.IgnoreCase);
        }
    }
}