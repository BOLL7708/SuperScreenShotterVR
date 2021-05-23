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
using System.Windows.Shapes;

namespace BOLL7708.EasyCSUtils
{
    /// <summary>
    /// Interaction logic for InputDialog.xaml
    /// </summary>
    public partial class InputDialog : Window
    {
        public string value;

        public InputDialog(Window window, string title, string label, string value="", int windowWidth=200)
        {
            this.value = value;
            this.Owner = window;
            InitializeComponent();
            Title = title;
            labelValue.Content = label+':';
            textBoxValue.Text = value;
            textBoxValue.Focus();
            textBoxValue.SelectAll();
            Width = windowWidth;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            value = textBoxValue.Text;
            DialogResult = true;
        }
    }
}
