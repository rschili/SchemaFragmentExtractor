using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SchemaFragmentExtractor
{


    public class ViewModel : ViewModelBase
    {
        public BulkObservableCollection<SchemaFile> Schemas { get; } = new BulkObservableCollection<SchemaFile>();
        public async Task LoadFiles(params string[] files)
        {
            var schemaFiles = files.Select(f => new SchemaFile(f)).ToList();
            Schemas.AddRange(schemaFiles);
            var schemaFileLoadTasks = schemaFiles.Select(f => f.LoadAsync());
            await Task.WhenAll(schemaFileLoadTasks);
        }
    }
}
