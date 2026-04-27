using System;
using System.Collections.Generic;
using System.Text;

namespace FlowchartThreaderer
{
    // Опис одного блоку для файлу
    public class BlockData
    {
        public Guid Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public BlockType Type { get; set; }
        public string Command { get; set; } = "";
    }

    // Опис одного зв'язку для файлу
    public class ConnectionData
    {
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
        public string ConnectionType { get; set; } = "Normal";
    }

    // Загальна структура проекту
    public class ProjectData
    {
        public List<BlockData> Blocks { get; set; } = new List<BlockData>();
        public List<ConnectionData> Connections { get; set; } = new List<ConnectionData>();
    }
}
