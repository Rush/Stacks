﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public interface IActorContext : INotifyCompletion
    {
        bool IsCompleted { get; }

        void Post(Action action);

        IActorContext GetAwaiter();
        void GetResult();
    }
}
