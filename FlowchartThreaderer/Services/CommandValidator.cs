using FlowchartThreaderer.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowchartThreaderer.Services
{
    public static class CommandValidator
    {
        // Регулярні вирази для різних типів команд
        private static readonly string Var = @"V\d+"; // Змінна вигляду V1, V2...
        private static readonly string Const = @"\d+"; // Числовий літерал

        public static bool IsValid(string command, BlockType type, out string errorMessage)
        {
            errorMessage = "";
            command = command.Trim();

            // Порожня команда вважається валідною до моменту запуску
            if (string.IsNullOrEmpty(command)) return true;

            bool isSyntaxValid = false;

            if (type == BlockType.Action)
            {
                // Перевірка форматів: V1=V2, INPUT V, PRINT V
                if (Regex.IsMatch(command, $@"^{Var}\s*=\s*{Var}$") ||
                    Regex.IsMatch(command, $@"^INPUT\s+{Var}$", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(command, $@"^PRINT\s+{Var}$", RegexOptions.IgnoreCase))
                {
                    isSyntaxValid = true;
                }
                // Перевірка формату V=C
                else
                {
                    var assignmentMatch = Regex.Match(command, $@"^({Var})\s*=\s*({Const})$");
                    if (assignmentMatch.Success)
                    {
                        isSyntaxValid = IsValidLiteral(assignmentMatch.Groups[2].Value, out errorMessage);
                    }
                    else
                    {
                        errorMessage = "Формат дії: V1=V2, V=C, INPUT V або PRINT V";
                    }
                }
            }
            else if (type == BlockType.Condition)
            {
                // Перевірка форматів: V==C, V<C, V>C
                var conditionMatch = Regex.Match(command, $@"^({Var})\s*(==|<|>)\s*({Const})$");
                if (conditionMatch.Success)
                {
                    isSyntaxValid = IsValidLiteral(conditionMatch.Groups[3].Value, out errorMessage);
                }
                else
                {
                    errorMessage = "Формат умови: V0==C, V0<C або V0>C";
                }
            }

            // Якщо синтаксис і літерали вірні, перевіряємо чи номери змінних у межах 100
            if (isSyntaxValid)
            {
                return IsVariableInRange(command, out errorMessage);
            }

            return false;
        }

        private static bool IsValidLiteral(string literal, out string error)
        {
            if (long.TryParse(literal, out long value))
            {
                if (value >= 0 && value <= 2147483647) // 2^31 - 1
                {
                    error = "";
                    return true;
                }
            }
            error = "Число має бути в діапазоні 0...2147483647";
            return false;
        }

        public static bool IsVariableInRange(string command, out string error)
        {
            error = "";
            var matches = Regex.Matches(command, @"V(\d+)");
            foreach (Match match in matches)
            {
                int index = int.Parse(match.Groups[1].Value);
                if (index < 0 || index > 99) // змінні з індексами від V0 до V99 (всього 100)
                {
                    error = "Дозволені змінні від V0 до V99";
                    return false;
                }
            }
            return true;
        }
    }
}
