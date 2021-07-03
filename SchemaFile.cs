using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SchemaFragmentExtractor
{
    public class SchemaFile : ViewModelBase
    {
        public SchemaFile(string fullPath)
        {
            FullPath = fullPath;
            Label = $"{fullPath} - Loading...";
        }

        public string FullPath { get; init; }
        public bool Broken { get; set; } = false; //Indicate if schema is broken (Label should contain details)
        public XDocument? Document { get; private set; }
        public string Label { get; set; }

        internal async Task LoadAsync()
        {
            try
            {
                using (var reader = new StreamReader(FullPath))
                {
                    var doc = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo, CancellationToken.None);
                    Document = doc;
                    Label = $"{FullPath} - Loading Contents...";
                    PerformPropertyChanged(nameof(Label));
                }
            }
            catch(Exception e)
            {
                Label = $"'{FullPath}' failed to load: {e.Message}";
                PerformPropertyChanged(nameof(Label));
                Broken = true;
            }

            await Task.Delay(2000);
            Label = $"{FullPath} - Ready.";
            PerformPropertyChanged(nameof(Label));
        }
    }
}
