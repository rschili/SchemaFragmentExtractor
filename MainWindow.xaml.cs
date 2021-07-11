using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SchemaFragmentExtractor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public ViewModel VM { get; }

        public MainWindow()
        {
            InitializeComponent();
            VM = new ViewModel();
            this.DataContext = VM;
        }

        private async void InputDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;

                await VM.LoadFiles(files);
            }
        }

        private void InputDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        /*
         *             if (!SelectedClasses.Contains(c))
                SelectedClasses.Add(c);
            BuildResultSchema();
         * */

        private void Regenerate_Click(object sender, RoutedEventArgs e)
        {
            VM?.BuildResultSchema();
        }

        private void ECClassListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = ((ListViewItem)sender).Content as ECClass;
            if (item == null)
                return;

            if (!VM.SelectedClasses.Contains(item))
            {
                VM.SelectedClasses.Add(item);
                VM.BuildResultSchema();
            }

            e.Handled = true;
        }

        private void SelectClass_Click(object sender, RoutedEventArgs e)
        {
            var item = FilteredClassesView.SelectedItem as ECClass;
            if (item == null)
                return;

            if (!VM.SelectedClasses.Contains(item))
            {
                VM.SelectedClasses.Add(item);
                VM.BuildResultSchema();
            }

            e.Handled = true;
        }
    }
}
