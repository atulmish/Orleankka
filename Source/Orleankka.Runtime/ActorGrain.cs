﻿using System;
using System.Threading.Tasks;

using Orleans;
using Orleans.Runtime;
using Orleans.Internals;

using Microsoft.Extensions.DependencyInjection;

using Orleankka.Behaviors;
using Orleankka.Core;
using Orleankka.Services;
using Orleankka.Utility;

using IReminderService = Orleankka.Services.IReminderService;

namespace Orleankka
{
	using Cluster;

    public abstract class ActorGrain : Grain, IRemindable, IActor, IActorHost
    {
        const string StickyReminderName = "##sticky##";

        IActorInvoker invoker;

        ActorType actorType;
        ActorType ActorType => actorType ?? (actorType = ActorType.Of(GetType()));

        public Task Autorun()
        {
            KeepAlive();

            return Task.CompletedTask;
        }

        public Task<object> Receive(object message)
        {
            KeepAlive();

            return invoker.OnReceive(this, message);
        }

        public Task ReceiveVoid(object message) => Receive(message);

        public Task Notify(object message) => Receive(message);

        async Task IRemindable.ReceiveReminder(string name, TickStatus status)
        {
            KeepAlive();

            if (name == StickyReminderName)
                return;

            await invoker.OnReminder(this, name);
        }

        public override Task OnDeactivateAsync()
        {
            return invoker.OnDeactivate(this);
        }

        async Task HandleStickyness()
        {
            var period = TimeSpan.FromMinutes(1);
            await RegisterOrUpdateReminder(StickyReminderName, period, period);
        }

        void KeepAlive() => ActorType.KeepAlive(this);

        public override async Task OnActivateAsync()
        {
            if (ActorType.Sticky)
                await HandleStickyness();

            await Activate();
        }

        Task Activate()
        {
            var path = ActorPath.From(ActorType.Name, IdentityOf(this));

            var system = ServiceProvider.GetRequiredService<ClusterActorSystem>();
            var runtime = new ActorRuntime(system, this);
            Initialize(this, path, runtime, ActorType.dispatcher);

            invoker = ActorType.Invoker(system.Pipeline);
            return invoker.OnActivate(this);
        }

        static string IdentityOf(IGrain grain) => 
            (grain as IGrainWithStringKey).GetPrimaryKeyString();

        // ------ ACTOR HOST--------- //

        IGrainRuntime IActorHost.Runtime => this.Runtime();

        // ------ ACTOR --------- //

        ActorRef self;

        protected ActorGrain() => 
            Behavior = ActorBehavior.Null(this);

        /// <inheritdoc />
        protected ActorGrain(IActorRuntime runtime) 
            : this(null, runtime)
        {}
        
        /// <inheritdoc />
        protected ActorGrain(string id) 
            : this(id, null)
        {}

        /// <summary>
        /// Provided only for unit-testing purposes
        /// </summary>
        protected ActorGrain(string id = null, IActorRuntime runtime = null) : this()
        {
            Runtime = runtime;
            Dispatcher = ActorType.Dispatcher(GetType());
            Path = GetType().ToActorPath(id ?? Guid.NewGuid().ToString("N"));
        }

        internal void Initialize(IActorHost host, ActorPath path, IActorRuntime runtime, Dispatcher dispatcher)
        {
            Path = path;
            Runtime = runtime;
            Dispatcher = dispatcher;
            Host = host;
        }

        public string Id => Path.Id;
        internal IActorHost Host        {get; private set;}

        public ActorPath Path           {get; private set;}
        public IActorRuntime Runtime    {get; private set;}
        public ActorBehavior Behavior   {get; private set;}
        public Dispatcher Dispatcher    {get; private set;}
        
        public IActorSystem System           => Runtime.System;
        public IActivationService Activation => Runtime.Activation;
        public IReminderService Reminders    => Runtime.Reminders;
        public ITimerService Timers          => Runtime.Timers;

        public ActorRef Self => self ?? (self = System.ActorOf(Path));

        public virtual Task OnActivate() => Behavior.HandleActivate();
        public virtual Task OnDeactivate() => Behavior.HandleDeactivate();

        public virtual Task<object> OnReceive(object message) => Behavior.HandleReceive(message);
        public virtual Task OnReminder(string id) => Behavior.HandleReminder(id);

        public async Task<TResult> Dispatch<TResult>(object message, Func<object, Task<object>> fallback = null) => 
            (TResult)await Dispatch(message, fallback);

        public Task<object> Dispatch(object message, Func<object, Task<object>> fallback = null)
        {
            Requires.NotNull(message, nameof(message));
            return Dispatcher.Dispatch(this, message, fallback);
        }

        public virtual Task<object> OnUnhandledReceive(RequestOrigin origin, object message) =>
            throw new UnhandledMessageException(this, message);

        public virtual Task OnUnhandledReminder(string id) =>
            throw new UnhandledReminderException(this, id);

        public virtual Task OnTransitioning(Transition transition) => Task.CompletedTask;
        public virtual Task OnTransitioned(Transition transition) => Task.CompletedTask;
        public virtual Task OnTransitionFailure(Transition transition, Exception exception) => Task.CompletedTask;
    }
}