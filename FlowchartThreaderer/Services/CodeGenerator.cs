using FlowchartThreaderer.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowchartThreaderer.Services
{
    public class CodeGenerator
    {
        public static string GenerateCSharpCode(List<BlockControl> blocks, List<ConnectionControl> connections)
        {
            if (blocks.Count == 0) return "// Схема порожня. Додайте блоки.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace GeneratedApp");
            sb.AppendLine("{");
            sb.AppendLine("    public class FlowchartProgram");
            sb.AppendLine("    {");
            // Спільні змінні для всіх потоків
            sb.AppendLine("        public static int[] V = new int[100];");
            sb.AppendLine();
            sb.AppendLine("        public void Execute()");
            sb.AppendLine("        {");

            // 1. Знаходимо стартовий блок (той, у якого немає вхідних стрілок)
            var startBlock = blocks.FirstOrDefault(b => !connections.Any(c => c.Target == b));
            if (startBlock == null) startBlock = blocks.FirstOrDefault(); // Запасний варіант, якщо все в циклі

            if (startBlock != null)
            {
                sb.AppendLine("            // --- Точка входу ---");
                sb.AppendLine($"            goto Block_{startBlock.Id.ToString("N")};");
                sb.AppendLine();
            }

            // 2. Проходимося по кожному блоку і генеруємо для нього мітку та логіку
            foreach (var block in blocks)
            {
                // Формуємо унікальну мітку з Guid (формат "N" прибирає дефіси)
                string blockLabel = $"Block_{block.Id.ToString("N")}";
                sb.AppendLine($"        {blockLabel}:");

                // Перекладаємо команду
                string csharpCode = TranslateToCSharp(block.Command, block.Type);

                if (block.Type == BlockType.Action)
                {
                    sb.AppendLine($"            {csharpCode}");

                    // Шукаємо, куди йде єдина стрілка
                    var nextConn = connections.FirstOrDefault(c => c.Source == block);
                    if (nextConn != null && nextConn.Target != null)
                    {
                        sb.AppendLine($"            goto Block_{nextConn.Target.Id.ToString("N")};");
                    }
                    else
                    {
                        sb.AppendLine($"            return; // Кінець алгоритму");
                    }
                }
                else if (block.Type == BlockType.Condition)
                {
                    var trueConn = connections.FirstOrDefault(c => c.Source == block && c.ConnectionType == "True");
                    var falseConn = connections.FirstOrDefault(c => c.Source == block && c.ConnectionType == "False");

                    sb.AppendLine($"            if ({csharpCode})");
                    sb.AppendLine($"            {{");
                    if (trueConn != null && trueConn.Target != null)
                        sb.AppendLine($"                goto Block_{trueConn.Target.Id.ToString("N")};");
                    else
                        sb.AppendLine($"                return;");

                    sb.AppendLine($"            }}");
                    sb.AppendLine($"            else");
                    sb.AppendLine($"            {{");
                    if (falseConn != null && falseConn.Target != null)
                        sb.AppendLine($"                goto Block_{falseConn.Target.Id.ToString("N")};");
                    else
                        sb.AppendLine($"                return;");
                    sb.AppendLine($"            }}");
                }
                sb.AppendLine(); // Порожній рядок між блоками для краси
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string TranslateToCSharp(string command, BlockType type)
        {
            if (string.IsNullOrWhiteSpace(command)) return type == BlockType.Action ? ";" : "true";

            // 1. Замінюємо V1, V2... на V[1], V[2]... за допомогою Regex
            string translated = Regex.Replace(command, @"V(\d+)", "V[$1]");

            // 2. Обробка специфічних команд
            if (type == BlockType.Action)
            {
                if (translated.ToUpper().StartsWith("INPUT"))
                {
                    // INPUT V[1] -> V[1] = int.Parse(Console.ReadLine());
                    return translated.ToUpper().Replace("INPUT ", "") + " = int.Parse(Console.ReadLine() ?? \"0\");";
                }
                if (translated.ToUpper().StartsWith("PRINT"))
                {
                    // PRINT V[1] -> Console.WriteLine(V[1]);
                    return "Console.WriteLine(" + translated.ToUpper().Replace("PRINT ", "") + ");";
                }

                // Для V[1]=V[2] або V[1]=100 просто додаємо крапку з комою
                return translated + ";";
            }

            // Для умов (V[1]<10) просто повертаємо як є, C# це зрозуміє
            return translated;
        }

    }
}
