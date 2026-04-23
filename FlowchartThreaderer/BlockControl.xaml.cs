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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FlowchartThreaderer
{
    /// <summary>
    /// Interaction logic for BlockControl.xaml
    /// </summary>
    public partial class BlockControl : UserControl
    {
        public BlockType Type { get; set; }
        public string Command { get; set; } = "";

        public BlockControl(BlockType type)
        {
            InitializeComponent();
            this.Type = type;
            this.Loaded += (s, e) => { UpdateVisuals(); UpdateText(); };
        }

        public void UpdateVisuals()
        {
            RectShape.Visibility = (Type == BlockType.Action) ? Visibility.Visible : Visibility.Collapsed;
            RhombShape.Visibility = (Type == BlockType.Condition) ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateText()
        {
            TxtCommand.Text = string.IsNullOrWhiteSpace(Command) ? "Команда" : Command;
        }
    }
}
