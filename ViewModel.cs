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
                ClassFilters = Regex.Matches(value, @"[\""].+?[\""]|[^ ]+")
                                .Select(m => m.Value.Trim('"'))
                                .ToList();
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
            var schemaFiles = files.Select(f => new SchemaFile(f)).ToList();
            Schemas.AddRange(schemaFiles);
            var schemaFileLoadTasks = schemaFiles.Select(f => f.LoadAsync());
            await Task.WhenAll(schemaFileLoadTasks);
            await Task.Run(() => RegenerateCache());
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

        private bool removeDisplayLabels = true;
        public bool RemoveDisplayLabels { get => removeDisplayLabels; set
            {
                removeDisplayLabels = value;
                PerformPropertyChanged(nameof(RemoveDisplayLabels));
                BuildResultSchema();
            }
        }
        private bool removeDescriptions = true;
        public bool RemoveDescriptions
        {
            get => removeDescriptions; set
            {
                removeDescriptions = value;
                PerformPropertyChanged(nameof(RemoveDescriptions));
                BuildResultSchema();
            }
        }
        internal void BuildResultSchema()
        {
            if (SelectedClasses.Count == 0)
            {
                Result = "No classes selected.";
                PerformPropertyChanged(nameof(Result));
                return;
            }

            var firstClass = SelectedClasses.First();
            var originalRoot = firstClass.Schema.Document?.Root;
            var rootName = originalRoot?.Name;
            if (rootName == null || originalRoot == null)
                return; //just to be defensive, should never happen

            var root = new XElement(rootName);
            root.ReplaceAttributes(originalRoot.Attributes());
            var xD = new XDocument(root);

            foreach (var c in SelectedClasses)
            {
                XElement deepCopy = new XElement(c.Element);
                root.Add(deepCopy);
            }

            if(RemoveDisplayLabels)
                RemoveAttributes(root, "displayLabel");

            if (RemoveDescriptions)
                RemoveAttributes(root, "description");

            Result = xD.ToString();
            PerformPropertyChanged(nameof(Result));
        }

        private static void RemoveAttributes(XElement root, XName attributeName)
        {
            root.Attribute(attributeName)?.Remove();
            if (root.HasElements)
            {
                foreach (var child in root.Elements())
                {
                    RemoveAttributes(child, attributeName);
                }
            }
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

        public IDictionary<string, string> References { get; } =  new Dictionary<string, string>();

        private void LoadReferences()
        {
            var references = Document?.Root?.Elements("ECSchemaReference");
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
    }

    public class ECClass
    {
        public string TypeName { get; init; }
        public string SchemaName => Schema?.SchemaName ?? "";
        public XElement Element { get; init; }

        public SchemaFile Schema { get; init; }

        public ECClass(string typeName, XElement child, SchemaFile schema)
        {
            TypeName = typeName;
            Element = child;
            Schema = schema;
        }
        internal List<ClassReference> GetClassDependencies()
        {
            List<ClassReference> result = new List<ClassReference>();
            foreach(var baseClassElement in Element.Elements("BaseClass"))
            {
                var baseClass = baseClassElement.Value.Split(':');
                if (baseClass.Length == 1)
                    result.Add(new ClassReference(SchemaName, baseClass[0]));
                else if (baseClass.Length == 2)
                {
                    var schemaName = Schema.References[baseClass[0]];
                    if(schemaName != null)
                        result.Add(new ClassReference(schemaName, baseClass[1]));
                }
                //Ignore more results, unknown format
            }

            return result;
        }
    }

    public record ClassReference(string SchemaName, string ClassName);
}
