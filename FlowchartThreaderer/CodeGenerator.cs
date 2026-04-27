using System;
using System.Collections.Generic;
using System.Text;

namespace FlowchartThreaderer
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

                if (block.Type == BlockType.Action)
                {
                    // Додаємо крапку з комою, якщо користувач забув її написати
                    string cmd = block.Command.Trim();
                    if (!string.IsNullOrEmpty(cmd) && !cmd.EndsWith(";")) cmd += ";";

                    sb.AppendLine($"            {cmd}");

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
                    string condition = block.Command.Trim();
                    var trueConn = connections.FirstOrDefault(c => c.Source == block && c.ConnectionType == "True");
                    var falseConn = connections.FirstOrDefault(c => c.Source == block && c.ConnectionType == "False");

                    sb.AppendLine($"            if ({condition})");
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
    }
}
