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
    /// Interaction logic for ConnectionControl.xaml
    /// </summary>
    public partial class ConnectionControl : UserControl
    {
        public BlockControl? Source { get; set; }
        public BlockControl? Target { get; set; }

        public ConnectionControl()
        {
            InitializeComponent();
        }

        public void Update(Point start, Point end)
        {
            ConnectorLine.X1 = start.X;
            ConnectorLine.Y1 = start.Y;
            ConnectorLine.X2 = end.X;
            ConnectorLine.Y2 = end.Y;

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X) * 180 / Math.PI;
            Canvas.SetLeft(ArrowHead, end.X - 5);
            Canvas.SetTop(ArrowHead, end.Y - 5);
            ArrowHead.RenderTransform = new RotateTransform(angle, 5, 5);

            // Розміщуємо текст по центру лінії
            Canvas.SetLeft(LabelText, (start.X + end.X) / 2 - 10);
            Canvas.SetTop(LabelText, (start.Y + end.Y) / 2 - 10);
        }

        public void SetType(string type)
        {
            if (type == "True")
            {
                ConnectorLine.Stroke = ArrowHead.Fill = Brushes.Green;
                LabelText.Text = "Так";
                LabelText.Foreground = Brushes.Green;
            }
            else if (type == "False")
            {
                ConnectorLine.Stroke = ArrowHead.Fill = Brushes.Red;
                LabelText.Text = "Ні";
                LabelText.Foreground = Brushes.Red;
            }
        }
    }
}
