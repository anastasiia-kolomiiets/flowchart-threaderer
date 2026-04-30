using FlowchartThreaderer.Models;
using FlowchartThreaderer.Services;
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

        // Зберігаємо список зв'язків для кожного потоку (блок-схеми на окремій вкладці)
        private Dictionary<Canvas, List<ConnectionControl>> tabConnections = new Dictionary<Canvas, List<ConnectionControl>>();

        private Canvas? currentCanvas;
        private List<ConnectionControl>? currentConnections;

        public MainWindow()
        {
            InitializeComponent();
            AddNewThread_Click(null, null); // Створюємо першу блок-схему при запуску
        }

        private void AddNewThread_Click(object sender, RoutedEventArgs? e)
        {
            if (FlowchartTabs.Items.Count >= 100) return; // Обмеження 100 потоків

            var newCanvas = new Canvas { Background = Brushes.White, Width = 2000, Height = 2000 };

            // Підключаємо події до нової канви
            newCanvas.MouseMove += Canvas_MouseMove;
            newCanvas.MouseDown += Canvas_MouseDown;
            newCanvas.MouseUp += Canvas_MouseUp;

            var newTab = new TabItem { Header = $"Потік {FlowchartTabs.Items.Count + 1}", Content = newCanvas };
            FlowchartTabs.Items.Add(newTab);
            tabConnections[newCanvas] = new List<ConnectionControl>();
            FlowchartTabs.SelectedItem = newTab;
        }

        private void FlowchartTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FlowchartTabs.SelectedItem is TabItem selectedTab && selectedTab.Content is Canvas canvas)
            {
                currentCanvas = canvas;
                currentConnections = tabConnections[canvas];
            }

            // Скидаємо виділення при переході між вкладками
            lastSelectedBlock?.Unselect();
            lastSelectedBlock = null;
            txtCommand.Tag = null;
            txtCommand.Text = "";
        }

        private void AddAction_Click(object sender, RoutedEventArgs e) => AddBlock(BlockType.Action);
        private void AddCondition_Click(object sender, RoutedEventArgs e) => AddBlock(BlockType.Condition);

        private void AddBlock(BlockType type)
        {
            if (currentCanvas == null) return;

            if (currentCanvas.Children.OfType<BlockControl>().Count() >= 100)
            {
                MessageBox.Show("Ліміт 100 блоків на одну схему вичерпано.");
                return;
            }

            var block = new BlockControl(type);
            Canvas.SetLeft(block, 50);
            Canvas.SetTop(block, 50);

            block.MouseDown += Block_MouseDown;
            block.MouseUp += Block_MouseUp; // Додаємо подію відпускання для стрілок
            currentCanvas.Children.Add(block);
        }

        private void Block_MouseDown(object sender, MouseButtonEventArgs e)
        {
                        if (currentCanvas == null) return;

            if (e.ChangedButton == MouseButton.Right) // Малювання стрілки
            {
                connectionSource = sender as BlockControl;
                if (connectionSource != null)
                {
                    isDrawingConnection = true;
                    tempConnection = new ConnectionControl();
                    currentCanvas.Children.Add(tempConnection);
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
            if (currentCanvas == null || currentConnections == null) return;

            if (e.ChangedButton == MouseButton.Right && isDrawingConnection)
            {
                Point mousePos = e.GetPosition(currentCanvas);
                IInputElement elementUnderMouse = currentCanvas.InputHitTest(mousePos);
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
                        bool hasConnection = currentConnections.Any(c => c.Source == connectionSource);

                        if (!hasConnection)
                        {
                            FinalizeConnection(connectionSource, target, "Normal");
                        }
                        else
                        {
                            MessageBox.Show("Цей блок уже має вихідний зв'язок!");
                            if (tempConnection != null) currentCanvas.Children.Remove(tempConnection);
                        }
                    }
                }
                else
                {
                    if (tempConnection != null) currentCanvas.Children.Remove(tempConnection);
                }

                CleanupDrawingState();
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (currentCanvas == null) return;
            Point mousePos = e.GetPosition(currentCanvas);

            if (isDrawingConnection && tempConnection != null && connectionSource != null)
            {
                tempConnection.Update(connectionSource.GetOutputPoint(), mousePos);
            }

            if (selectedElement != null && selectedElement.IsMouseCaptured)
            {
                Canvas.SetLeft(selectedElement, mousePos.X - offset.X);
                Canvas.SetTop(selectedElement, mousePos.Y - offset.Y);

                // Оновлюємо всі існуючі стрілки
                if (currentConnections != null)
                {
                    var attachedConnections = currentConnections.Where(c => c.Source == selectedElement || c.Target == selectedElement);
                    foreach (var conn in attachedConnections)
                    {
                        if (conn.Source != null && conn.Target != null)
                            conn.Update(conn.Source.GetOutputPoint(), conn.Target.GetInputPoint());
                    }
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

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Якщо клікнули по порожньому місцю канви
            lastSelectedBlock?.Unselect();
            lastSelectedBlock = null;
            txtCommand.Tag = null;
            txtCommand.Text = "";
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
            if (lastSelectedBlock != null && currentCanvas != null && currentConnections != null)
            {
                // Видаляємо стрілки, пов'язані з цим блоком
                var toRemove = currentConnections.FindAll(c => c.Source == lastSelectedBlock || c.Target == lastSelectedBlock);
                foreach (var conn in toRemove)
                {
                    currentCanvas.Children.Remove(conn);
                    currentConnections.Remove(conn);
                }

                // Видаляємо з канви
                currentCanvas.Children.Remove(lastSelectedBlock);

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
            if (currentConnections == null || currentCanvas == null) return;

            ContextMenu menu = new ContextMenu();

            // Перевіряємо, чи вже існують такі типи зв'язків для цього блоку
            bool hasTrue = currentConnections.Any(c => c.Source == source && c.LabelText.Text == "Так");
            bool hasFalse = currentConnections.Any(c => c.Source == source && c.LabelText.Text == "Ні");

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

            menu.PlacementTarget = currentCanvas;
            menu.IsOpen = true;
        }

        private void FinalizeConnection(BlockControl source, BlockControl target, string type)
        {
            if (currentConnections == null || currentCanvas == null) return;
            // Якщо ми створювали тимчасову стрілку в Block_MouseDown, використаємо її
            // або створимо нову, якщо меню затрималося
            var conn = new ConnectionControl { Source = source, Target = target };
            if (type != "Normal") conn.SetType(type);

            conn.Update(source.GetOutputPoint(), target.GetInputPoint());
            currentCanvas.Children.Add(conn);
            Panel.SetZIndex(conn, -1);
            currentConnections.Add(conn);
        }

        private void CleanupDrawingState()
        {
            if (tempConnection != null && currentCanvas != null) currentCanvas.Children.Remove(tempConnection);
            tempConnection = null;
            isDrawingConnection = false;
            connectionSource?.ReleaseMouseCapture();
            connectionSource = null;
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (currentCanvas == null || currentConnections == null) return;

            var project = new ProjectData();

            // Збираємо дані про всі блоки
            foreach (var child in currentCanvas.Children)
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
                }
            }

            // Збираємо дані про всі зв'язки
            foreach (var conn in currentConnections)
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
                string jsonString = File.ReadAllText(openFileDialog.FileName);
                var project = JsonSerializer.Deserialize<ProjectData>(jsonString);

                if (project == null) return;

                // Створюємо нову вкладку для завантаженого проекту
                AddNewThread_Click(null, null);
                if (currentCanvas == null || currentConnections == null) return;

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

                    currentCanvas.Children.Add(block);
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

                        currentCanvas.Children.Add(conn);
                        Panel.SetZIndex(conn, -1);
                        currentConnections.Add(conn);
                    }
                }
            }
        }

        private void GenerateCode_Click(object sender, RoutedEventArgs e)
        {
            if (currentCanvas == null || currentConnections == null) return;

            // Збираємо всі блоки з канви
            var allBlocks = currentCanvas.Children.OfType<BlockControl>().ToList();

            // Генеруємо код
            string generatedCode = CodeGenerator.GenerateCSharpCode(allBlocks, currentConnections);

            // Відкриваємо вікно з результатом
            var previewWindow = new CodePreviewWindow(generatedCode);
            previewWindow.Owner = this;
            previewWindow.ShowDialog();
        }

    }
}