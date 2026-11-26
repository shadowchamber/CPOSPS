using System;
using System.Collections.Generic;

namespace IIGCore.HardwareStation.PosTerminalWeb.Engine
{
    public interface IOposBus : IDisposable
    {
        void Write(string fileName, params string[] lines);
    }

    public sealed class MemoryOposBus : IOposBus
    {
        public List<Tuple<string, string[]>> Writes { get; } = new List<Tuple<string, string[]>>();

        public void Write(string fileName, params string[] lines)
        {
            Writes.Add(Tuple.Create(fileName, lines));
        }

        public void Dispose()
        {
        }
    }
}
