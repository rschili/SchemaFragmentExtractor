using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SchemaFragmentExtractor
{
    public class SchemaGenerator
    {
        public Collection<SchemaFile> Schemas { get; }

        public SchemaGenerator(Collection<SchemaFile> schemas)
        {
            Schemas = schemas;
        }

        internal string BuildResultSchema(ICollection<ECClass> selectedClasses, List<string> attributeFilters)
        {
            if (selectedClasses.Count == 0)
            {
                return "No classes selected.";
            }

            var firstClass = selectedClasses.First();
            var originalRoot = firstClass.Schema.Document?.Root;
            var rootName = originalRoot?.Name;
            if (rootName == null || originalRoot == null)
                return "No root node found in schema"; //just to be defensive, should never happen

            var root = new XElement(rootName);
            root.ReplaceAttributes(originalRoot.Attributes());
            var xD = new XDocument(root);

            var insertedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var selectedClass in selectedClasses)
            {
                if (!insertedClasses.Add(selectedClass.FullName))
                    continue;

                var dependencies = GetClassDependencies(selectedClass);
                foreach (var dependency in dependencies)
                {
                    InsertReferencedElement(root, selectedClass, dependency, insertedClasses);
                }
                XElement deepCopy = new XElement(selectedClass.Element);
                root.Add(deepCopy);
            }

            foreach(var filter in attributeFilters)
                RemoveAttributes(root, filter);

            return xD.ToString();
        }

        private void InsertReferencedElement(XElement root, ECClass selectedClass, SchemaItemReference dependency, HashSet<string> insertedClasses)
        {
            ECClass? dependencyClass = null;
            if (dependency.SchemaName.Equals(selectedClass.SchemaName))
            {
                dependencyClass = selectedClass.Schema.Classes.FirstOrDefault(c => c.TypeName.Equals(dependency.TypeName));
            }
            else
            {
                var dependencySchema = Schemas.FirstOrDefault(s => dependency.SchemaName.Equals(s.SchemaName));
                if (dependencySchema != null)
                    dependencyClass = dependencySchema.Classes.FirstOrDefault(c => c.TypeName.Equals(dependency.TypeName));
            }

            if (dependencyClass == null) //TODO: build dependencyClass as dummy
                return;

            if (!insertedClasses.Add(dependencyClass.FullName))
                return;

            foreach (var subDependency in GetClassDependencies(dependencyClass))
            {
                InsertReferencedElement(root, dependencyClass, subDependency, insertedClasses);
            }
            XElement dependencyDeepCopy = new XElement(dependencyClass.Element);
            root.Add(dependencyDeepCopy);
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
        internal List<SchemaItemReference> GetClassDependencies(ECClass c)
        {
            List<SchemaItemReference> result = new List<SchemaItemReference>();
            foreach (var baseClassElement in c.Element.Elements(c.Element.Name.Namespace + "BaseClass"))
            {
                var baseClass = baseClassElement.Value.Split(':');
                if (baseClass.Length == 1)
                    result.Add(new SchemaItemReference(c.SchemaName, baseClass[0]));
                else if (baseClass.Length == 2)
                {
                    string? schemaName;
                    if (c.Schema.References.TryGetValue(baseClass[0], out schemaName) && schemaName != null)
                        result.Add(new SchemaItemReference(schemaName, baseClass[1]));
                }
                //Ignore more results, unknown format
            }

            return result;
        }
    }

    public record SchemaItemReference(string SchemaName, string TypeName);
}
