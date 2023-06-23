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
        public static (string BookId, int SheetId) GetIdsFromBookLink(string bookIdOrLink)
        {
            if (!IsBookLink(bookIdOrLink))
                return (bookIdOrLink, -1);
            return ExtractIdsFromBookLink(bookIdOrLink);
        }

        /// <summary>
        /// bookLink must contain book ID and can have sheet ID.
        /// </summary>
        /// <param name="bookLink"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static (string BookId, int SheetId) ExtractIdsFromBookLink(string bookLink)
        {
            Match m = Regex.Match(bookLink.Trim(), @"/(?'BookId'[^/]+)(/$|/(edit|view)\#gid\=(?'SheetId'\d+)$)", RegexOptions.IgnoreCase);
            if (!m.Success)
                throw new Exception("Could not parse the link: " + bookLink);
            if (!int.TryParse(m.Groups["SheetId"].Value, out int sheetId))
                throw new Exception("Could not parse SheetId in the link: " + bookLink);
            return (m.Groups["BookId"].Value, sheetId);
        }

        public static bool IsBookLink(string v)
        {
            return Regex.IsMatch(v.Trim(), @"^\s*https?\://docs\.google\.com/spreadsheets/", RegexOptions.IgnoreCase);
        }
    }
}