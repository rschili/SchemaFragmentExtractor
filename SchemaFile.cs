using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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

        public string? SchemaName { get; set; }

        public string? Version { get; set; }

        public List<ECClass> Classes { get; } = new List<ECClass>();

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
                    await Task.Run(() => LoadContents());
                    int? lineNumber = (Document.LastNode as IXmlLineInfo)?.LineNumber;
                    Label = $"{SchemaName} {Version} ({Classes.Count} classes, {lineNumber} Lines)";
                    PerformPropertyChanged(nameof(Label));
                }
            }
            catch (Exception e)
            {
                Label = $"'{FullPath}' failed to load: {e.Message}";
                PerformPropertyChanged(nameof(Label));
                Broken = true;
            }
        }

        private Regex _classCheckRegex = new Regex(@"^EC(\w*)Class$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal bool IsClassElement(XElement element)
        {
            if (element == null) return false;
            return _classCheckRegex.IsMatch(element.Name.LocalName);
        }

        internal void LoadContents()
        {
            if (Document == null) throw new InvalidOperationException("No Document available.");
            var root = Document.Root;
            SchemaName = root?.Attribute("schemaName")?.Value;
            Version = root?.Attribute("version")?.Value;
            if (SchemaName == null || Version == null || root == null)
                throw new InvalidDataException("Format unknown.");

            foreach (var child in root.Elements())
            {
                if (!IsClassElement(child))
                    continue;

                var typeName = child.Attribute("typeName")?.Value;
                if (typeName == null) continue; // Should never happen?
                Classes.Add(new ECClass(typeName, child));
            }
        }
    }

    public class ECClass
    {
        public string TypeName { get; init; }
        public XElement Element { get; init; }

        public ECClass(string typeName, XElement child)
        {
            TypeName = typeName;
            Element = child;
        }
    }
}
