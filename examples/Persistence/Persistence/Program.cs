﻿// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Persistence;
using Proto.Persistence.Sqlite;
using Event = Proto.Persistence.Event;
using Snapshot = Proto.Persistence.Snapshot;

class Program
{
    static void Main(string[] args)
    {
        var provider = new SqliteProvider();

        var props = Actor.FromProducer(() => new MyPersistenceActor())
            .WithReceiveMiddleware(Persistence.Using(provider));

        var pid = Actor.Spawn(props);

        Console.ReadLine();
    }

    class MyPersistenceActor : IPersistentActor
    {
        private PID _loopActor;
        private State _state = new State();
        public Persistence Persistence { get; set; }

        public void UpdateState(object message)
        {
            switch (message)
            {
                case Event e:
                    Apply(e);
                    break;
                case Snapshot s:
                    Apply(s);
                    break;
            }
        }

        private void Apply(Event @event)
        {
            switch (@event)
            {
                case RecoverEvent msg:
                    Console.WriteLine("MyPersistenceActor - RecoverEvent = Event.Index = {0}, Event.Data = {1}", msg.Index, msg.Data);
                    break;
                case PersistedEvent msg:
                    Console.WriteLine("MyPersistenceActor - PersistedEvent = Event.Index = {0}, Event.Data = {1}", msg.Index, msg.Data);
                    break;
            }
        }

        private void Apply(Snapshot snapshot)
        {
            switch (snapshot)
            {
                case RecoverSnapshot msg:
                    if (msg.State is State ss)
                    {
                        _state = ss;
                        Console.WriteLine("MyPersistenceActor - RecoverSnapshot = Snapshot.Index = {0}, Snapshot.State = {1}", Persistence.Index, ss.Name);
                    }
                    break;
                case PersistedSnapshot msg:
                    Console.WriteLine("MyPersistenceActor - PersistedSnapshot = Snapshot.Index = {0}, Snapshot.State = {1}", msg.Index, msg.State);
                    break;
            }
        }

        private class StartLoopActor { }
        private class TimeToSnapshot { }

        private bool _timerStarted = false;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started msg:

                    Console.WriteLine("MyPersistenceActor - Started");

                    context.Self.Tell(new StartLoopActor());

                    break;
                case RecoveryStarted msg:

                    Console.WriteLine("MyPersistenceActor - RecoveryStarted");

                    break;
                case RecoveryCompleted msg:

                    Console.WriteLine("MyPersistenceActor - RecoveryCompleted");

                    context.Self.Tell(new StartLoopActor());

                    break;
                case RequestSnapshot msg:

                    await Handle(context, msg);

                    break;
                case TimeToSnapshot msg:

                    await Handle(context, msg);

                    break;
                case StartLoopActor msg:

                    await Handle(context, msg);

                    break;
                case RenameCommand msg:

                    await Handle(msg);

                    break;
            }
        }

        private async Task Handle(IContext context, RequestSnapshot message)
        {
            Console.WriteLine("MyPersistenceActor - RequestSnapshot");

            await Persistence.PersistSnapshotAsync(_state);

            context.Self.Tell(new TimeToSnapshot());
        }

        private Task Handle(IContext context, TimeToSnapshot message)
        {
            Console.WriteLine("MyPersistenceActor - TimeToSnapshot");

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));

                context.Self.Tell(new RequestSnapshot());
            });

            return Actor.Done;
        }

        private Task Handle(IContext context, StartLoopActor message)
        {
            if (_timerStarted) return Actor.Done;

            _timerStarted = true;

            Console.WriteLine("MyPersistenceActor - StartLoopActor");

            var props = Actor.FromProducer(() => new LoopActor());

            _loopActor = context.Spawn(props);

            context.Self.Tell(new TimeToSnapshot());
            
            return Actor.Done;
        }

        private async Task Handle(RenameCommand message)
        {
            Console.WriteLine("MyPersistenceActor - RenameCommand");

            _state.Name = message.Name;

            await Persistence.PersistEventAsync(new RenameEvent { Name = message.Name });
        }
    }

    class LoopActor : IActor
    {
        internal class LoopParentMessage { }

        public Task ReceiveAsync(IContext context)
        {
            switch(context.Message)
            {
                case Started _:

                    Console.WriteLine("LoopActor - Started");

                    context.Self.Tell(new LoopParentMessage());

                    break;
                case LoopParentMessage msg:

                    Task.Run(async () => {
                        
                        context.Parent.Tell(new RenameCommand { Name = "Daniel" });

                        await Task.Delay(TimeSpan.FromSeconds(2));

                        context.Self.Tell(new LoopParentMessage());
                    });

                    break;
            }

            return Actor.Done;
        }
    }
}