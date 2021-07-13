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

        internal string BuildResultSchema(ICollection<ECClass> selectedClasses, List<string> attributeFilters, List<string>? customAttributeWhitelist)
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
                RemoveAliasFromBaseClasses(deepCopy);
                root.Add(deepCopy);
            }

            foreach(var filter in attributeFilters)
                RemoveAttributes(root, filter);

            if(customAttributeWhitelist != null)
            {
                RemoveCustomAttributes(xD, customAttributeWhitelist);
            }

            StripEmptyElements(xD);

            return xD.ToString();
        }

        private void StripEmptyElements(XDocument xD)
        {
            var emptyElements = xD.Descendants().Where(d => string.IsNullOrWhiteSpace(d.Value)).ToList();
            foreach (var ee in emptyElements)
                ee.RemoveNodes();
        }

        private void RemoveCustomAttributes(XDocument xD, List<string> customAttributeWhitelist)
        {
            XName customAttributeNodeName = XName.Get("ECCustomAttributes", xD.Root?.Name.Namespace.NamespaceName ?? "");
            var customAttributeElements = xD.Descendants(customAttributeNodeName).ToList();

            foreach(var caElement in customAttributeElements)
            {
                RemoveWithNextWhitespace(caElement);
            }
        }

        private void RemoveWithNextWhitespace(XElement element)
        {
            IEnumerable<XText> textNodes
                = element.NodesAfterSelf()
                         .TakeWhile(node => node is XText).Cast<XText>();
            if (element.ElementsAfterSelf().Any())
            {
                // Easy case, remove following text nodes.
                textNodes.ToList().ForEach(node => node.Remove());
            }
            else
            {
                // Remove trailing whitespace.
                textNodes.TakeWhile(text => !text.Value.Contains("\n"))
                         .ToList().ForEach(text => text.Remove());
                // Fetch text node containing newline, if any.
                XText? newLineTextNode
                    = element.NodesAfterSelf().OfType<XText>().FirstOrDefault();
                if (newLineTextNode != null)
                {
                    string value = newLineTextNode.Value;
                    if (value.Length > 1)
                    {
                        // Composite text node, trim until newline (inclusive).
                        newLineTextNode.AddAfterSelf(
                            new XText(value.Substring(value.IndexOf('\n') + 1)));
                    }
                    // Remove original node.
                    newLineTextNode.Remove();
                }
            }
            element.Remove();
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
            RemoveAliasFromBaseClasses(dependencyDeepCopy);
            root.Add(dependencyDeepCopy);
        }

        private static void RemoveAliasFromBaseClasses(XElement classElement)
        {
            foreach (var baseClassElement in classElement.Elements(classElement.Name.Namespace + "BaseClass"))
            {
                var separatorIndex = baseClassElement.Value.IndexOf(':');
                if(separatorIndex != -1)
                {
                    baseClassElement.Value = baseClassElement.Value.Substring(separatorIndex+1);
                }
            }
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
