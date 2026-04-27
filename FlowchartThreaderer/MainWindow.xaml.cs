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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BlockControl? selectedElement;
        private Point offset;
        private BlockControl? lastSelectedBlock; // Зберігаємо посилання на останній вибраний блок

        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddAction_Click(object sender, RoutedEventArgs e) => AddBlock(BlockType.Action);
        private void AddCondition_Click(object sender, RoutedEventArgs e) => AddBlock(BlockType.Condition);

        private void AddBlock(BlockType type)
        {
            var block = new BlockControl(type);
            Canvas.SetLeft(block, 50);
            Canvas.SetTop(block, 50);

            block.MouseDown += Block_MouseDown;
            MainCanvas.Children.Add(block);
        }

        private void Block_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Знімаємо виділення з попереднього блоку
            lastSelectedBlock?.Unselect();

            selectedElement = sender as BlockControl;
            if (selectedElement == null) return;

            // Виділяємо новий блок
            lastSelectedBlock = selectedElement;
            lastSelectedBlock.Select();

            offset = e.GetPosition(selectedElement);
            selectedElement.CaptureMouse();

            // Виводимо текст блоку в поле редагування
            txtCommand.Tag = null; // Тимчасово вимикаємо оновлення
            txtCommand.Text = selectedElement.Command;
            txtCommand.Tag = selectedElement;

            e.Handled = true;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (selectedElement != null && selectedElement.IsMouseCaptured)
            {
                var mousePos = e.GetPosition(MainCanvas);
                Canvas.SetLeft(selectedElement, mousePos.X - offset.X);
                Canvas.SetTop(selectedElement, mousePos.Y - offset.Y);
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            selectedElement?.ReleaseMouseCapture();
            selectedElement = null;
        }

        private void txtCommand_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtCommand.Tag is BlockControl block)
            {
                block.Command = txtCommand.Text;
                block.UpdateText();
            }
        }

        private void DeleteBlock_Click(object sender, RoutedEventArgs e)
        {
            if (lastSelectedBlock != null)
            {
                // 1. Видаляємо з канви
                MainCanvas.Children.Remove(lastSelectedBlock);

                // 2. Очищаємо текстове поле, якщо видалили поточний блок
                if (txtCommand.Tag == lastSelectedBlock)
                {
                    txtCommand.Tag = null;
                    txtCommand.Text = "";
                }

                // 3. Скидаємо посилання
                lastSelectedBlock = null;
            }
            else
            {
                MessageBox.Show("Спочатку виберіть блок на канві, клікнувши по ньому.");
            }
        }

        private void MainCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Якщо клікнули по порожньому місцю канви
            lastSelectedBlock?.Unselect();
            lastSelectedBlock = null;
            txtCommand.Tag = null;
            txtCommand.Text = "";
        }
    }
}