using FlowchartThreaderer.Models;
using FlowchartThreaderer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// Логіка взаємодії для TestWindow.xaml
    /// Реалізує автоматичне тестування з перебором усіх варіантів виконання (інтерлевінгів).
    /// </summary>
    public partial class TestWindow : Window
    {
        private readonly List<Canvas> _canvases;
        private readonly List<List<ConnectionControl>> _allConnections;
        private ObservableCollection<TestCaseViewModel> _testCases = new();
        private TestRunner _testRunner;
        private CancellationTokenSource? _cts;

        public TestWindow(List<Canvas> canvases, List<List<ConnectionControl>> allConnections)
        {
            InitializeComponent();

            // Перевірка наявності даних для тестування
            if (canvases == null || allConnections == null || canvases.Count == 0)
            {
                MessageBox.Show("Помилка: немає блок-схем для тестування.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            _canvases = canvases;
            _allConnections = allConnections;

            _testRunner = new TestRunner(_canvases, _allConnections);

            // Налаштування списку тестів
            lvTests.ItemsSource = _testCases;
        }

        private void AddTest_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInput.Text) && string.IsNullOrWhiteSpace(txtExpected.Text))
            {
                MessageBox.Show("Введіть вхідні дані або очікуваний вивід.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var test = new TestCase
            {
                Input = txtInput.Text.Trim(),
                ExpectedOutput = txtExpected.Text.Trim(),
                Description = $"Тест {_testCases.Count + 1}"
            };

            _testCases.Add(new TestCaseViewModel(test, _testCases.Count + 1));

            txtInput.Clear();
            txtExpected.Clear();
        }

        private async void RunAllTests_Click(object sender, RoutedEventArgs e)
        {
            if (_testCases.Count == 0) return;

            _cts = new CancellationTokenSource();
            txtStatus.Text = "Запуск повної перевірки...";
            pbProgress.Value = 0;

            foreach (var testVm in _testCases)
            {
                if (_cts.IsCancellationRequested) break;
                await RunTestLogicAsync(testVm);
            }

            txtStatus.Text = _cts.IsCancellationRequested ? "Тестування перервано користувачем." : "Всі тести оброблено.";
        }

        private async void RunCurrentTest_Click(object sender, RoutedEventArgs e)
        {
            if (lvTests.SelectedItem is not TestCaseViewModel selected)
            {
                MessageBox.Show("Оберіть тест зі списку.", "Повідомлення");
                return;
            }

            _cts = new CancellationTokenSource();
            await RunTestLogicAsync(selected);
        }

        private async Task RunTestLogicAsync(TestCaseViewModel testVm)
        {
            testVm.Status = "Виконання...";
            testVm.StatusColor = Brushes.Orange;
            lbUniqueOutputs.Items.Clear();
            pbProgress.Value = 0;

            try
            {
                // Отримання параметра K (макс. кількість операцій)
                int k = (int)sldK.Value;

                // Запуск симулятора
                var result = await _testRunner.RunTestAsync(testVm.TestCase, maxOperationsK: k, ct: _cts?.Token ?? default);

                // Оновлення прогресу та статистики
                pbProgress.Value = result.CheckedPercentage;
                txtPercentage.Text = $"{result.CheckedPercentage:F2}%";

                // Виведення унікальних результатів виконання для аналізу недетермінованості
                foreach (var output in result.AllOutputs)
                {
                    lbUniqueOutputs.Items.Add(string.IsNullOrWhiteSpace(output) ? "[Empty Output]" : output);
                }

                if (result.Passed)
                {
                    testVm.Status = "ПРОЙДЕНО";
                    testVm.StatusColor = Brushes.Green;
                }
                else
                {
                    // Якщо результат не знайдено, виводимо відсоток покриття дерева станів
                    testVm.Status = $"Не пройдено ({result.CheckedPercentage:F1}%)";
                    testVm.StatusColor = Brushes.Red;
                }

                txtStatus.Text = $"Перевірено {result.ExploredExecutions} унікальних шляхів до глибини K={k}.";
            }
            catch (OperationCanceledException)
            {
                testVm.Status = "СКАСОВАНО";
                testVm.StatusColor = Brushes.Gray;
            }
            catch (Exception ex)
            {
                testVm.Status = "ПОМИЛКА";
                testVm.StatusColor = Brushes.DarkRed;
                MessageBox.Show($"Помилка: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel(); // Дострокове переривання процесу перебору
        }

        private void ClearTests_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Видалити всі тести?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _testCases.Clear();
                lbUniqueOutputs.Items.Clear();
            }
        }

        private void LvTests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvTests.SelectedItem is TestCaseViewModel vm)
            {
                txtInput.Text = vm.TestCase.Input;
                txtExpected.Text = vm.TestCase.ExpectedOutput;
                // При зміні тесту очищуємо список старих результатів
                lbUniqueOutputs.Items.Clear();
            }
        }
    }

    /// <summary>
    /// ViewModel для прив'язки даних тесту до інтерфейсу ListView
    /// </summary>
    public class TestCaseViewModel : INotifyPropertyChanged
    {
        public TestCase TestCase { get; }
        public int Id { get; }

        private string _status = "Не виконано";
        private Brush _statusColor = Brushes.Gray;

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
        }

        public string InputPreview => TestCase.Input.Length > 40
            ? TestCase.Input.Substring(0, 37) + "..."
            : TestCase.Input;

        public string ExpectedPreview => TestCase.ExpectedOutput.Length > 40
            ? TestCase.ExpectedOutput.Substring(0, 37) + "..."
            : TestCase.ExpectedOutput;

        public TestCaseViewModel(TestCase testCase, int id)
        {
            TestCase = testCase;
            Id = id;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
