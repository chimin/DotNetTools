using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConvertTable
{
    class GoogleSpreadsheetReader : ITableReader
    {
        private static string[] Scopes = { SheetsService.Scope.SpreadsheetsReadonly };
        private static string ApplicationName = "ConvertTable";
        private static char[] ColumnChars = Enumerable.Range(0, 26).Select(i => (char)('A' + i)).ToArray();

        private static string ColumnIndexToName(int index)
        {
            var result = new List<char>();
            for (; index > 0; index /= ColumnChars.Length)
            {
                result.Add(ColumnChars[index % ColumnChars.Length]);
            }
            if (result.Count == 0)
            {
                result.Add(ColumnChars[0]);
            }
            else
            {
                result.Reverse();
            }
            return new string(result.ToArray());
        }

        private static string RowIndexToName(int index)
        {
            return (index + 1).ToString();
        }

        public string Name { get; } = "google";

        public IEnumerable<IList<object>> Read(string input)
        {
            var items = input.Split(new[] { '@' }, 2);
            var spreadsheetId = items.ElementAt(0);
            var sheetTitle = items.ElementAtOrDefault(1);

            UserCredential credential;

            using (var stream = typeof(Program).Assembly.GetManifestResourceStream(typeof(Program), "client_secret.json"))
            {
                string credPath = ".GoogleCredential.json";

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
            }

            // Create Google Sheets API service.
            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Define request parameters.
            var spreadsheet = service.Spreadsheets.Get(spreadsheetId).Execute();
            var sheet = sheetTitle != null ?
                spreadsheet.Sheets.FirstOrDefault(i => i.Properties.Title == sheetTitle) :
                spreadsheet.Sheets[0];

            String range = sheet.Properties.Title + "!A1:" +
                ColumnIndexToName(sheet.Properties.GridProperties.ColumnCount.Value - 1) +
                RowIndexToName(sheet.Properties.GridProperties.RowCount.Value - 1);
            SpreadsheetsResource.ValuesResource.GetRequest request =
                    service.Spreadsheets.Values.Get(spreadsheetId, range);

            ValueRange response = request.Execute();
            IList<IList<Object>> values = response.Values;
            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    yield return row;
                }
            }
        }
    }
}
