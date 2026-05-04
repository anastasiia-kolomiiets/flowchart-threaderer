using FlowchartThreaderer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FlowchartThreaderer.Services
{
    public class TestRunner
    {
        private readonly List<Canvas> _canvases;
        private readonly List<List<ConnectionControl>> _allConnections;

        public TestRunner(List<Canvas> canvases, List<List<ConnectionControl>> connectionsPerTab)
        {
            _canvases = canvases;
            _allConnections = connectionsPerTab;
        }

        // Внутрішнє представлення графа для швидкого доступу без UI-потоку
        private class Node
        {
            public Guid Id { get; set; }
            public BlockType Type { get; set; }
            public string Command { get; set; } = "";
            public Guid? NextId { get; set; }
            public Guid? TrueTargetId { get; set; }
            public Guid? FalseTargetId { get; set; }
        }

        // Стан виконання віртуальної машини
        private class ExecutionState
        {
            public int[] V { get; set; } = new int[100];
            public Guid?[] PCs { get; set; } = Array.Empty<Guid?>(); // Вказівники інструкцій для кожного потоку
            public List<string> Output { get; set; } = new();
            public Queue<int> InputQueue { get; set; } = new();
            public int OperationsCount { get; set; } = 0;
            public double BranchWeight { get; set; } = 1.0; // Для підрахунку відсотка (корінь = 1.0 або 100%)

            public ExecutionState Clone()
            {
                return new ExecutionState
                {
                    V = (int[])this.V.Clone(),
                    PCs = (Guid?[])this.PCs.Clone(),
                    Output = new List<string>(this.Output),
                    InputQueue = new Queue<int>(this.InputQueue),
                    OperationsCount = this.OperationsCount,
                    BranchWeight = this.BranchWeight
                };
            }
        }

        public async Task<TestResult> RunTestAsync(TestCase testCase, int maxOperationsK, CancellationToken ct = default)
        {
            var result = new TestResult { TestCase = testCase };
            var threadStarts = new List<Guid?>();
            var graph = new Dictionary<Guid, Node>();

            // 1. ЕКСТРАКЦІЯ ГРАФА (виконується в UI-потоці)
            for (int i = 0; i < _canvases.Count; i++)
            {
                var blocks = _canvases[i].Children.OfType<BlockControl>().ToList();
                var connections = _allConnections[i];
                Guid? startBlockId = null;

                foreach (var block in blocks)
                {
                    var node = new Node { Id = block.Id, Type = block.Type, Command = block.Command };

                    if (block.Type == BlockType.Action)
                    {
                        node.NextId = connections.FirstOrDefault(c => c.Source == block)?.Target?.Id;
                    }
                    else
                    {
                        node.TrueTargetId = connections.FirstOrDefault(c => c.Source == block && c.ConnectionType == "True")?.Target?.Id;
                        node.FalseTargetId = connections.FirstOrDefault(c => c.Source == block && c.ConnectionType == "False")?.Target?.Id;
                    }
                    graph[node.Id] = node;

                    // Блок без вхідних зв'язків вважається початковим
                    if (!connections.Any(c => c.Target == block)) startBlockId = block.Id;
                }
                threadStarts.Add(startBlockId);
            }

            // 2. СИМУЛЯЦІЯ ВИКОНАННЯ (у фоновому потоці)
            return await Task.Run(() =>
            {
                double totalProgress = 0;
                int exploredCount = 0;
                bool foundValid = false;
                var uniqueOuts = new HashSet<string>();

                var initialState = new ExecutionState
                {
                    PCs = threadStarts.ToArray(),
                    BranchWeight = 1.0,
                    InputQueue = ParseInput(testCase.Input)
                };

                // Рекурсивний обхід дерева станів (DFS)
                void Explore(ExecutionState state)
                {
                    if (ct.IsCancellationRequested || foundValid) return;

                    bool allTerminated = state.PCs.All(pc => pc == null);

                    // Якщо досягли ліміту операцій (K) АБО всі потоки завершили роботу
                    if (state.OperationsCount >= maxOperationsK || allTerminated)
                    {
                        totalProgress += state.BranchWeight;
                        exploredCount++;

                        string outStr = string.Join("\n", state.Output).Trim();
                        uniqueOuts.Add(outStr);

                        if (NormalizeOutput(outStr) == NormalizeOutput(testCase.ExpectedOutput))
                        {
                            foundValid = true;
                        }
                        return;
                    }

                    // Знаходимо потоки, які ще не завершилися
                    var activeIndices = state.PCs
                        .Select((pc, idx) => new { pc, idx })
                        .Where(x => x.pc != null)
                        .Select(x => x.idx)
                        .ToList();

                    if (activeIndices.Count == 0) return;

                    // Розподіляємо «вагу» гілки між усіма можливими варіантами наступного кроку
                    double weightPerBranch = state.BranchWeight / activeIndices.Count;

                    foreach (var i in activeIndices)
                    {
                        if (ct.IsCancellationRequested || foundValid) break;

                        var nextState = state.Clone();
                        nextState.BranchWeight = weightPerBranch;

                        try
                        {
                            ExecuteStep(nextState, i, graph);
                            Explore(nextState);
                        }
                        catch (Exception)
                        {
                            // У разі помилки в команді, зараховуємо гілку як перевірену, але зламану
                            totalProgress += weightPerBranch;
                        }
                    }
                }

                Explore(initialState);

                result.Passed = foundValid;
                result.ExploredExecutions = exploredCount;
                result.CheckedPercentage = totalProgress * 100.0;
                result.AllOutputs = uniqueOuts.ToList();

                return result;
            });
        }

        // Обробка однієї команди для конкретного потоку
        private static void ExecuteStep(ExecutionState state, int threadIndex, Dictionary<Guid, Node> graph)
        {
            var nodeId = state.PCs[threadIndex].Value;
            var node = graph[nodeId];
            state.OperationsCount++;
            string cmd = node.Command.Trim();

            if (node.Type == BlockType.Action)
            {
                state.PCs[threadIndex] = node.NextId; // Рухаємо ПК далі
                if (string.IsNullOrEmpty(cmd)) return;

                if (Regex.IsMatch(cmd, @"^INPUT\s+V(\d+)$", RegexOptions.IgnoreCase))
                {
                    int vIdx = int.Parse(Regex.Match(cmd, @"\d+").Value);
                    state.V[vIdx] = state.InputQueue.Count > 0 ? state.InputQueue.Dequeue() : 0;
                }
                else if (Regex.IsMatch(cmd, @"^PRINT\s+V(\d+)$", RegexOptions.IgnoreCase))
                {
                    int vIdx = int.Parse(Regex.Match(cmd, @"\d+").Value);
                    state.Output.Add(state.V[vIdx].ToString());
                }
                else if (Regex.IsMatch(cmd, @"^V(\d+)\s*=\s*V(\d+)$"))
                {
                    var m = Regex.Match(cmd, @"^V(\d+)\s*=\s*V(\d+)$");
                    state.V[int.Parse(m.Groups[1].Value)] = state.V[int.Parse(m.Groups[2].Value)];
                }
                else if (Regex.IsMatch(cmd, @"^V(\d+)\s*=\s*(\d+)$"))
                {
                    var m = Regex.Match(cmd, @"^V(\d+)\s*=\s*(\d+)$");
                    state.V[int.Parse(m.Groups[1].Value)] = int.Parse(m.Groups[2].Value);
                }
            }
            else if (node.Type == BlockType.Condition)
            {
                bool conditionMet = true;
                if (!string.IsNullOrEmpty(cmd))
                {
                    var mEq = Regex.Match(cmd, @"^V(\d+)\s*==\s*(\d+)$");
                    var mLess = Regex.Match(cmd, @"^V(\d+)\s*<\s*(\d+)$");
                    var mMore = Regex.Match(cmd, @"^V(\d+)\s*>\s*(\d+)$");

                    if (mEq.Success)
                        conditionMet = state.V[int.Parse(mEq.Groups[1].Value)] == int.Parse(mEq.Groups[2].Value);
                    else if (mLess.Success)
                        conditionMet = state.V[int.Parse(mLess.Groups[1].Value)] < int.Parse(mLess.Groups[2].Value);
                    else if (mMore.Success)
                        conditionMet = state.V[int.Parse(mMore.Groups[1].Value)] > int.Parse(mMore.Groups[2].Value);
                }
                state.PCs[threadIndex] = conditionMet ? node.TrueTargetId : node.FalseTargetId;
            }
        }

        private static Queue<int> ParseInput(string input)
        {
            var q = new Queue<int>();
            var parts = input.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
                if (int.TryParse(p, out int val)) q.Enqueue(val);
            return q;
        }

        private static string NormalizeOutput(string output) => output.Replace("\r\n", "\n").Trim();
    }

    public class TestResult
    {
        public TestCase TestCase { get; set; } = new();
        public bool Passed { get; set; }
        public int ExploredExecutions { get; set; }
        public double CheckedPercentage { get; set; }
        public List<string> AllOutputs { get; set; } = new();
        public string Error { get; set; } = "";
    }
}