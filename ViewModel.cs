﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SchemaFragmentExtractor
{


    public class ViewModel : ViewModelBase
    {
        public BulkObservableCollection<SchemaFile> Schemas { get; } = new BulkObservableCollection<SchemaFile>();

        public BulkObservableCollection<ECClass> AllClasses { get; } = new BulkObservableCollection<ECClass>();

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

        public ViewModel()
        {
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(AllClasses);
            view.Filter = FilterClasses;
        }

        private bool FilterClasses(object item)
        {
            if (ClassFilters.Count == 0) return true;

            var ecClass = item as ECClass;
            if (ecClass == null) return true;
            foreach(var filter in ClassFilters)
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
    }
}