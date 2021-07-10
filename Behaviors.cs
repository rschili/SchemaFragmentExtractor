using ICSharpCode.AvalonEdit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SchemaFragmentExtractor
{
    public class Behaviors
    {
        public static readonly DependencyProperty AvalonEditTextProperty = DependencyProperty.RegisterAttached("AvalonEditText", typeof(string),
            typeof(Behaviors), new UIPropertyMetadata(AvalonEditTextChanged));

        public static void SetAvalonEditText(DependencyObject dp, DependencyProperty value)
        {
            dp.SetValue(AvalonEditTextProperty, value);
        }

        public static DependencyProperty GetAvalonEditText(DependencyObject dp)
        {
            return (DependencyProperty)dp.GetValue(AvalonEditTextProperty);
        }


        private static void AvalonEditTextChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            var textEditor = target as TextEditor;
            if (textEditor != null)
            {
                if (e.NewValue != null)
                {
                    textEditor.Text = e.NewValue.ToString();
                }
            }
        }
    }
}
