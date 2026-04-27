using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Text.Json;
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

        // Змінні для зв'язків
        private ConnectionControl? tempConnection;
        private bool isDrawingConnection = false;
        private BlockControl? connectionSource;
        private List<ConnectionControl> connections = new List<ConnectionControl>();

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
            block.MouseUp += Block_MouseUp; // Додаємо подію відпускання для стрілок
            MainCanvas.Children.Add(block);
        }

        private void Block_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right) // Малювання стрілки
            {
                connectionSource = sender as BlockControl;
                if (connectionSource != null)
                {
                    isDrawingConnection = true;
                    tempConnection = new ConnectionControl();
                    MainCanvas.Children.Add(tempConnection);
                    Panel.SetZIndex(tempConnection, -1);
                    connectionSource.CaptureMouse();
                }
                e.Handled = true;
                return;
            }

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

        private void Block_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right && isDrawingConnection)
            {
                Point mousePos = e.GetPosition(MainCanvas);
                IInputElement elementUnderMouse = MainCanvas.InputHitTest(mousePos);
                BlockControl? target = FindParentBlock(elementUnderMouse as DependencyObject);

                if (target != null && target != connectionSource && connectionSource != null)
                {
                    if (connectionSource.Type == BlockType.Condition)
                    {
                        // Показуємо меню вибору True/False
                        ShowBranchMenu(connectionSource, target, mousePos);
                    }
                    else
                    {
                        // Перевіряємо, чи немає вже вихідного зв'язку для Action блоку
                        bool hasConnection = connections.Any(c => c.Source == connectionSource);

                        if (!hasConnection)
                        {
                            FinalizeConnection(connectionSource, target, "Normal");
                        }
                        else
                        {
                            MessageBox.Show("Цей блок уже має вихідний зв'язок!");
                            if (tempConnection != null) MainCanvas.Children.Remove(tempConnection);
                        }
                    }
                }
                else
                {
                    if (tempConnection != null) MainCanvas.Children.Remove(tempConnection);
                }

                CleanupDrawingState();
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(MainCanvas);

            if (isDrawingConnection && tempConnection != null && connectionSource != null)
            {
                tempConnection.Update(connectionSource.GetOutputPoint(), mousePos);
            }

            if (selectedElement != null && selectedElement.IsMouseCaptured)
            {
                Canvas.SetLeft(selectedElement, mousePos.X - offset.X);
                Canvas.SetTop(selectedElement, mousePos.Y - offset.Y);

                // Оновлюємо всі існуючі стрілки
                foreach (var conn in connections)
                {
                    if (conn.Source != null && conn.Target != null)
                        conn.Update(conn.Source.GetOutputPoint(), conn.Target.GetInputPoint());
                }
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                selectedElement?.ReleaseMouseCapture();
                selectedElement = null;
            }
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
                // Видаляємо стрілки, пов'язані з цим блоком
                var toRemove = connections.FindAll(c => c.Source == lastSelectedBlock || c.Target == lastSelectedBlock);
                foreach (var conn in toRemove)
                {
                    MainCanvas.Children.Remove(conn);
                    connections.Remove(conn);
                }

                // Видаляємо з канви
                MainCanvas.Children.Remove(lastSelectedBlock);

                // Очищаємо текстове поле, якщо видалили поточний блок
                if (txtCommand.Tag == lastSelectedBlock)
                {
                    txtCommand.Tag = null;
                    txtCommand.Text = "";
                }

                // Скидаємо посилання
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

        // Допоміжний метод для пошуку батьківського BlockControl
        private BlockControl? FindParentBlock(DependencyObject? child)
        {
            while (child != null && !(child is BlockControl))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as BlockControl;
        }

        private void ShowBranchMenu(BlockControl source, BlockControl target, Point position)
        {
            ContextMenu menu = new ContextMenu();

            // Перевіряємо, чи вже існують такі типи зв'язків для цього блоку
            bool hasTrue = connections.Any(c => c.Source == source && c.LabelText.Text == "Так");
            bool hasFalse = connections.Any(c => c.Source == source && c.LabelText.Text == "Ні");

            MenuItem trueItem = new MenuItem { Header = "Гілка ТАК (True)", Foreground = Brushes.Green, IsEnabled = !hasTrue };
            trueItem.Click += (s, e) => FinalizeConnection(source, target, "True");

            MenuItem falseItem = new MenuItem { Header = "Гілка НІ (False)", Foreground = Brushes.Red, IsEnabled = !hasFalse };
            falseItem.Click += (s, e) => FinalizeConnection(source, target, "False");

            menu.Items.Add(trueItem);
            menu.Items.Add(falseItem);

            if (hasTrue && hasFalse)
            {
                menu.Items.Clear();
                menu.Items.Add(new MenuItem { Header = "Усі виходи умови вже зайняті", IsEnabled = false });
            }

            menu.PlacementTarget = MainCanvas;
            menu.IsOpen = true;
        }

        private void FinalizeConnection(BlockControl source, BlockControl target, string type)
        {
            // Якщо ми створювали тимчасову стрілку в Block_MouseDown, використаємо її
            // або створимо нову, якщо меню затрималося
            var conn = new ConnectionControl { Source = source, Target = target };
            if (type != "Normal") conn.SetType(type);

            conn.Update(source.GetOutputPoint(), target.GetInputPoint());
            MainCanvas.Children.Add(conn);
            Panel.SetZIndex(conn, -1);
            connections.Add(conn);
        }

        private void CleanupDrawingState()
        {
            if (tempConnection != null) MainCanvas.Children.Remove(tempConnection);
            tempConnection = null;
            isDrawingConnection = false;
            connectionSource?.ReleaseMouseCapture();
            connectionSource = null;
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            var project = new ProjectData();

            // Збираємо дані про всі блоки
            var blockMap = new Dictionary<BlockControl, Guid>();
            foreach (var child in MainCanvas.Children)
            {
                if (child is BlockControl block)
                {
                    project.Blocks.Add(new BlockData
                    {
                        Id = block.Id,
                        X = Canvas.GetLeft(block),
                        Y = Canvas.GetTop(block),
                        Type = block.Type,
                        Command = block.Command
                    });
                    blockMap[block] = block.Id;
                }
            }

            // Збираємо дані про всі зв'язки
            foreach (var conn in connections)
            {
                if (conn.Source != null && conn.Target != null)
                {
                    project.Connections.Add(new ConnectionData
                    {
                        SourceId = conn.Source.Id,
                        TargetId = conn.Target.Id,
                        ConnectionType = conn.ConnectionType
                    });
                }
            }

            // Діалог збереження
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Flowchart Files (*.json)|*.json";
            if (saveFileDialog.ShowDialog() == true)
            {
                string jsonString = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(saveFileDialog.FileName, jsonString);
                MessageBox.Show("Проект збережено успішно!");
            }
        }

        private void LoadProject_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Flowchart Files (*.json)|*.json";

            if (openFileDialog.ShowDialog() == true)
            {
                // Очищаємо поточну канву
                MainCanvas.Children.Clear();
                connections.Clear();

                string jsonString = File.ReadAllText(openFileDialog.FileName);
                var project = JsonSerializer.Deserialize<ProjectData>(jsonString);

                if (project == null) return;

                // Словник для швидкого пошуку створених блоків за їх ID
                var idToBlockMap = new Dictionary<Guid, BlockControl>();

                // Відновлюємо блоки
                foreach (var bData in project.Blocks)
                {
                    var block = new BlockControl(bData.Type)
                    {
                        Id = bData.Id,
                        Command = bData.Command
                    };
                    Canvas.SetLeft(block, bData.X);
                    Canvas.SetTop(block, bData.Y);

                    // Підписуємо на події
                    block.MouseDown += Block_MouseDown;
                    block.MouseUp += Block_MouseUp;

                    MainCanvas.Children.Add(block);
                    idToBlockMap[bData.Id] = block;
                }

                // Відновлюємо зв'язки
                foreach (var cData in project.Connections)
                {
                    if (idToBlockMap.ContainsKey(cData.SourceId) && idToBlockMap.ContainsKey(cData.TargetId))
                    {
                        var source = idToBlockMap[cData.SourceId];
                        var target = idToBlockMap[cData.TargetId];

                        var conn = new ConnectionControl { Source = source, Target = target };
                        conn.SetType(cData.ConnectionType);
                        conn.Update(source.GetOutputPoint(), target.GetInputPoint());

                        MainCanvas.Children.Add(conn);
                        Panel.SetZIndex(conn, -1);
                        connections.Add(conn);
                    }
                }
            }
        }

    }
}