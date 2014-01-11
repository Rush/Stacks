﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IRawByteClient
    {
        event Action<ArraySegment<byte>> Received;

        void Send(byte[] buffer);
        void Send(ArraySegment<byte> buffer);
    }
}
