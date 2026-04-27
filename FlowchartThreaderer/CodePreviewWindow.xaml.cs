using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FlowchartThreaderer
{
    /// <summary>
    /// Interaction logic for CodePreviewWindow.xaml
    /// </summary>
    public partial class CodePreviewWindow : Window
    {
        public CodePreviewWindow(string code)
        {
            InitializeComponent();
            TxtCode.Text = code;
        }
    }
}
