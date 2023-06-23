//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Cliver
{
    public partial class GoogleSheet : GoogleService<SheetsService>
    {
        public GoogleSheet(GoogleSettings googleSettings) : base(googleSettings)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bookIdOrLink"></param>
        /// <param name="range">example: "SheetName!A3:C9", where SheetName is optional and will be set automatically if bookIdOrLink contains the target sheet ID</param>
        /// <returns></returns>
        public IList<IList<object>> GetValues(string bookIdOrLink, string range = null)
        {
            var ids = GetIdsFromBookLink(bookIdOrLink);
            if (ids.SheetId >= 0
                && (range == null || !range.Contains("!") && range.Contains(":"))//no sheet is specified in the range
                )
            {
                var names = GetNames(bookIdOrLink);
                if (names.SheetName != null)
                {
                    if (!string.IsNullOrWhiteSpace(range))
                        range = "!" + range;
                    range = names.SheetName + range;
                }
            }
            SpreadsheetsResource.ValuesResource.GetRequest request = Service.Spreadsheets.Values.Get(ids.BookId, range);
            var response = request.Execute();
            return response.Values;
        }

        public (string BookName, string SheetName) GetNames(string bookIdOrLink)
        {
            var ids = GetIdsFromBookLink(bookIdOrLink);
            SpreadsheetsResource.GetRequest request = Service.Spreadsheets.Get(ids.BookId);
            var response = request.Execute();
            return (response.Properties.Title, response.Sheets.FirstOrDefault(a => a.Properties.SheetId == ids.SheetId)?.Properties.Title);
        }
    }
}