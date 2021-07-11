# SchemaFragmentExtractor

Quick and Dirty Helper tool which extracts portions of ECSchemas to better understand their composition, and to help writing tests.
ECSchema is a concept used in [Bentley iTwin services](https://www.itwinjs.org/bis/ec/ec-schema/)

Built using C#/WPF with .NET 5.0.

The UI has 3 Tabs
* The first tab lets you drop *.ecschema.xml files into a list to build your pool of data to choose from
* The second tab allows you to select one or many classes from these schemas
* The third tab gives the selection extracted and put into a schema with all of its dependencies. This also provides some options to modify the output XML.