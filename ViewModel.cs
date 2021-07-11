using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Xml;
using System.Xml.Linq;

namespace SchemaFragmentExtractor
{


    public class ViewModel : ViewModelBase
    {
        public BulkObservableCollection<SchemaFile> Schemas { get; } = new BulkObservableCollection<SchemaFile>();

        public BulkObservableCollection<ECClass> AllClasses { get; } = new BulkObservableCollection<ECClass>();

        public List<ECClass> SelectedClasses { get; set; } = new List<ECClass>();

        private string _classFilter = "";

        private List<string> ClassFilters = new List<string>();

        public string ClassFilter
        {
            get { return _classFilter; }
            set
            {
                _classFilter = value;
                ClassFilters = StringUtils.SplitFilter(value);
                //PerformPropertyChanged(nameof(ClassFilter));
                CollectionViewSource.GetDefaultView(AllClasses).Refresh();
            }
        }

        public string Result { get; set; } = "";

        public ViewModel()
        {
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(AllClasses);
            view.Filter = FilterClasses;
            BuildResultSchema(); //initialize to dummy message
        }

        private bool FilterClasses(object item)
        {
            if (ClassFilters.Count == 0) return true;

            var ecClass = item as ECClass;
            if (ecClass == null) return true;
            foreach (var filter in ClassFilters)
            {
                if (!ecClass.TypeName.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !ecClass.SchemaName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        public async Task LoadFiles(params string[] files)
        {
            var schemaFiles = files
                .Where(f => f.EndsWith(".ecschema.xml", StringComparison.OrdinalIgnoreCase))
                .Select(f => new SchemaFile(f)).ToList();

            await LoadSchemaFilesWithReferences(schemaFiles);
            await Task.Run(() => RegenerateCache());
        }

        private async Task LoadSchemaFilesWithReferences(List<SchemaFile> schemaFiles)
        {
            Schemas.AddRange(schemaFiles);
            var schemaFileLoadTasks = schemaFiles.Select(f => f.LoadAsync());
            await Task.WhenAll(schemaFileLoadTasks);

            var referencedSchemaNamesToLoad = schemaFiles
                .SelectMany(sf => sf.References.Values)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(schemaName => !Schemas.Any(s => schemaName.Equals(s.SchemaName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var schemaDirectories = Schemas
                .Select(s => Path.GetDirectoryName(s.FullPath))
                .Where(sf => sf != null).Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var subSchemaFiles = FindSchemaFiles(schemaDirectories, referencedSchemaNamesToLoad);
            if (subSchemaFiles.Count > 0)
                await LoadSchemaFilesWithReferences(subSchemaFiles);
        }

        private List<SchemaFile> FindSchemaFiles(List<string> directories, List<string> schemaNames)
        {
            var schemaFiles = schemaNames.Select(sn => FindSchemaFile(directories, sn))
                .Where(sf => sf != null).Cast<string>()
                .Select(sf => new SchemaFile(sf)).ToList();
            return schemaFiles;
        }

        private string? FindSchemaFile(List<string> directories, string schemaName)
        {
            foreach(var directory in directories)
            {
                var result = Directory.GetFiles(directory, $"{schemaName}*ecschema.xml", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (result != null)
                    return result;
            }

            return null;
        }

        public void RegenerateCache()
        {
            List<ECClass> classes = new List<ECClass>();
            foreach (var schema in Schemas)
                classes.AddRange(schema.Classes);

            App.Current.Dispatcher.Invoke(() =>
            {
                AllClasses.Clear();
                AllClasses.AddRange(classes);
            });
        }
        internal void SelectClasses(List<ECClass> classes)
        {
            SelectedClasses = classes;
            BuildResultSchema();
        }

        private bool filterAttributes = true;
        public bool FilterAttributes
        {
            get { return filterAttributes; }
            set
            {
                filterAttributes = value;
                BuildResultSchema();
            }
        }
        public string AttributeFilter { get; set; } = "displayLabel description";


        private bool filterCustomAttributes = true;
        public bool FilterCustomAttributes
        {
            get { return filterCustomAttributes; }
            set
            {
                filterCustomAttributes = value;
                BuildResultSchema();
            }
        }
        public string CustomAttributeFilter { get; set; } = "ECDbMap.*";


        private bool shortenAttributes = false;
        public bool ShortenAttributes
        {
            get { return shortenAttributes; }
            set
            {
                shortenAttributes = value;
                BuildResultSchema();
            }
        }
        public string AttributesToShorten { get; set; } = "typeName propertyName";

        public void BuildResultSchema()
        {
            var generator = new SchemaGenerator(Schemas);
            var attributeFilter = StringUtils.SplitFilter(AttributeFilter);
            Result = generator.BuildResultSchema(SelectedClasses, FilterAttributes ? attributeFilter : new List<string>());
            PerformPropertyChanged(nameof(Result));
        }

    }
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

        public string? SchemaFullName { get; set; }

        public string? Version { get; set; }

        public List<ECClass> Classes { get; } = new List<ECClass>();

        /// <summary>
        /// Key = alias, Value = Reference Schema Name
        /// </summary>
        public IDictionary<string, string> References { get; } = new Dictionary<string, string>();

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
                    Label = $"{SchemaFullName} ({Classes.Count} classes, {lineNumber} Lines)";
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

        private static Regex _classCheckRegex = new Regex(@"^EC(\w*)Class$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
            SchemaFullName = $"{SchemaName}.{Version}";
            if (SchemaName == null || Version == null || root == null)
                throw new InvalidDataException("Format unknown.");

            LoadReferences();

            foreach (var child in root.Elements())
            {
                if (!IsClassElement(child))
                    continue;

                var typeName = child.Attribute("typeName")?.Value;
                if (typeName == null) continue; // Should never happen?
                Classes.Add(new ECClass(typeName, child, this));
            }
        }
        private void LoadReferences()
        {
            var root = Document?.Root;
            if (root == null)
                return;

            var references = root.Elements(root.Name.Namespace + "ECSchemaReference");
            if (references == null)
                return;

            foreach (var reference in references)
            {
                var alias = reference.Attribute("alias")?.Value;
                var name = reference.Attribute("name")?.Value;
                if (alias != null && name != null)
                    References.Add(alias, name);
            }
        }
    }

    public class ECClass
    {
        public string TypeName { get; init; }

        public string FullName => $"{SchemaName}:{TypeName}";
        public string SchemaName => Schema?.SchemaName ?? "";
        public XElement Element { get; init; }

        public SchemaFile Schema { get; init; }

        public ECClass(string typeName, XElement child, SchemaFile schema)
        {
            TypeName = typeName;
            Element = child;
            Schema = schema;
        }
    }
}
