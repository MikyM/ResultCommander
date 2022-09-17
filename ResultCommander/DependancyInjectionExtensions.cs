﻿using System.Reflection;
using AttributeBasedRegistration;
using AttributeBasedRegistration.Attributes;
using Autofac;
using Autofac.Extras.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ResultCommander;

/// <summary>
/// DI extensions for <see cref="ContainerBuilder"/>.
/// </summary>
[PublicAPI]
public static class DependancyInjectionExtensions
{
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, IEnumerable<Type> assembliesContainingTypesToScan, Action<ResultCommanderConfiguration>? options = null)
        => AddResultCommander(builder, assembliesContainingTypesToScan.Select(x => x.Assembly).Distinct(), options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesToScan">Assemblies to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, IEnumerable<Assembly> assembliesToScan, Action<ResultCommanderConfiguration>? options = null)
    {
        var config = new ResultCommanderConfiguration(builder);
        options?.Invoke(config);

        var iopt = Options.Create(config);

        builder.RegisterInstance(iopt).As<IOptions<ResultCommanderConfiguration>>().SingleInstance();
        builder.Register(x => x.Resolve<IOptions<ResultCommanderConfiguration>>().Value).As<ResultCommanderConfiguration>().SingleInstance();

        foreach (var assembly in assembliesToScan)
        {
            var commandSet = assembly.GetTypes()
                .Where(x =>
                    x.GetInterfaces().Any(y =>
                        y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IAsyncCommandHandler<>)) &&
                    x.IsClass && !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var commandResultSet = assembly.GetTypes()
                .Where(x =>
                    x.GetInterfaces().Any(y =>
                        y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IAsyncCommandHandler<,>)) &&
                    x.IsClass && !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var commandSubSet = commandSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var commandResultSubSet = commandResultSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var syncCommandSet = assembly.GetTypes()
                .Where(x =>
                    x.GetInterfaces().Any(y =>
                        y.IsGenericType && y.GetGenericTypeDefinition() == typeof(ISyncCommandHandler<>)) &&
                    x.IsClass && !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var syncCommandResultSet = assembly.GetTypes()
                .Where(x =>
                    x.GetInterfaces().Any(y =>
                        y.IsGenericType && y.GetGenericTypeDefinition() == typeof(ISyncCommandHandler<,>)) &&
                    x.IsClass && !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var syncCommandSubSet = commandSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var syncCommandResultSubSet = commandResultSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            foreach (var type in commandSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>)).AsImplementedInterfaces();

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        registrationBuilder = registrationBuilder.SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        registrationBuilder = registrationBuilder.InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        registrationBuilder = registrationBuilder.InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerDependency:
                        registrationBuilder = registrationBuilder.InstancePerDependency();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        registrationBuilder =
                            registrationBuilder.InstancePerMatchingLifetimeScope(lifeAttr?.Tags.ToArray() ?? throw new InvalidOperationException());
                        break;
                    case Lifetime.InstancePerOwned:
                        if (lifeAttr?.Owned is null) throw new InvalidOperationException("Owned type was null");

                        registrationBuilder = registrationBuilder.InstancePerOwned(lifeAttr.Owned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
                if (intrAttr is null) 
                    continue;

                var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

                foreach (var attr in intrAttrs)
                {
                    registrationBuilder = registrationBuilder.EnableInterfaceInterceptors();
                    registrationBuilder = attr.IsAsync
                        ? registrationBuilder.InterceptedBy(
                            typeof(AsyncInterceptorAdapter<>).MakeGenericType(attr.Interceptor))
                        : registrationBuilder.InterceptedBy(attr.Interceptor);
                }
            }

            foreach (var type in commandResultSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>)).AsImplementedInterfaces();

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        registrationBuilder = registrationBuilder.SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        registrationBuilder = registrationBuilder.InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        registrationBuilder = registrationBuilder.InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerDependency:
                        registrationBuilder = registrationBuilder.InstancePerDependency();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        registrationBuilder =
                            registrationBuilder.InstancePerMatchingLifetimeScope(lifeAttr?.Tags.ToArray() ?? throw new InvalidOperationException());
                        break;
                    case Lifetime.InstancePerOwned:
                        if (lifeAttr?.Owned is null) throw new InvalidOperationException("Owned type was null");

                        registrationBuilder = registrationBuilder.InstancePerOwned(lifeAttr.Owned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
                if (intrAttr is null) 
                    continue;

                if (intrAttr.Intercept is not (Intercept.Interface or Intercept.InterfaceAndClass))
                    throw new NotSupportedException("Only interface interception is supported for command handlers");
                
                var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

                foreach (var attr in intrAttrs)
                {
                    registrationBuilder = registrationBuilder.EnableInterfaceInterceptors();
                    registrationBuilder = attr.IsAsync
                        ? registrationBuilder.InterceptedBy(
                            typeof(AsyncInterceptorAdapter<>).MakeGenericType(attr.Interceptor))
                        : registrationBuilder.InterceptedBy(attr.Interceptor);
                }
            }

            commandSet.RemoveAll(x => commandSubSet.Any(y => y == x));
            commandResultSet.RemoveAll(x => commandResultSubSet.Any(y => y == x));

            if (commandSet.Any())
            {
                switch (config.DefaultHandlerLifetime)
                {
                    case Lifetime.SingleInstance:
                        builder.RegisterTypes(commandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>)).AsImplementedInterfaces()
                            .SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        builder.RegisterTypes(commandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>)).AsImplementedInterfaces()
                            .InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        builder.RegisterTypes(commandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>)).AsImplementedInterfaces()
                            .InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException();
                    case Lifetime.InstancePerDependency:
                        builder.RegisterTypes(commandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>)).AsImplementedInterfaces()
                            .InstancePerDependency();
                        break;
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            if (commandResultSet.Any())
            {
                switch (config.DefaultHandlerLifetime)
                {
                    case Lifetime.SingleInstance:
                        builder.RegisterTypes(commandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>)).AsImplementedInterfaces()
                            .SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        builder.RegisterTypes(commandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>)).AsImplementedInterfaces()
                            .InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        builder.RegisterTypes(commandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>)).AsImplementedInterfaces()
                            .InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException();
                    case Lifetime.InstancePerDependency:
                        builder.RegisterTypes(commandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>)).AsImplementedInterfaces()
                            .InstancePerDependency();
                        break;
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            foreach (var type in syncCommandSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(typeof(ISyncCommandHandler<>)).AsImplementedInterfaces();

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        registrationBuilder = registrationBuilder.SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        registrationBuilder = registrationBuilder.InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        registrationBuilder = registrationBuilder.InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerDependency:
                        registrationBuilder = registrationBuilder.InstancePerDependency();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        registrationBuilder =
                            registrationBuilder.InstancePerMatchingLifetimeScope(lifeAttr?.Tags.ToArray() ?? throw new InvalidOperationException());
                        break;
                    case Lifetime.InstancePerOwned:
                        if (lifeAttr?.Owned is null) throw new InvalidOperationException("Owned type was null");

                        registrationBuilder = registrationBuilder.InstancePerOwned(lifeAttr.Owned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
                if (intrAttr is null) 
                    continue;

                var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

                foreach (var attr in intrAttrs)
                {
                    registrationBuilder = registrationBuilder.EnableInterfaceInterceptors();
                    registrationBuilder = attr.IsAsync
                        ? registrationBuilder.InterceptedBy(
                            typeof(AsyncInterceptorAdapter<>).MakeGenericType(attr.Interceptor))
                        : registrationBuilder.InterceptedBy(attr.Interceptor);
                }
            }

            foreach (var type in syncCommandResultSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>)).AsImplementedInterfaces();

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        registrationBuilder = registrationBuilder.SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        registrationBuilder = registrationBuilder.InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        registrationBuilder = registrationBuilder.InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerDependency:
                        registrationBuilder = registrationBuilder.InstancePerDependency();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        registrationBuilder =
                            registrationBuilder.InstancePerMatchingLifetimeScope(lifeAttr?.Tags.ToArray() ?? throw new InvalidOperationException());
                        break;
                    case Lifetime.InstancePerOwned:
                        if (lifeAttr?.Owned is null) throw new InvalidOperationException("Owned type was null");

                        registrationBuilder = registrationBuilder.InstancePerOwned(lifeAttr.Owned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
                if (intrAttr is null) 
                    continue;

                if (intrAttr.Intercept is not (Intercept.Interface or Intercept.InterfaceAndClass))
                    throw new NotSupportedException("Only interface interception is supported for command handlers");
                
                var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

                foreach (var attr in intrAttrs)
                {
                    registrationBuilder = registrationBuilder.EnableInterfaceInterceptors();
                    registrationBuilder = attr.IsAsync
                        ? registrationBuilder.InterceptedBy(
                            typeof(AsyncInterceptorAdapter<>).MakeGenericType(attr.Interceptor))
                        : registrationBuilder.InterceptedBy(attr.Interceptor);
                }
            }

            syncCommandSet.RemoveAll(x => syncCommandSubSet.Any(y => y == x));
            syncCommandResultSet.RemoveAll(x => syncCommandResultSubSet.Any(y => y == x));

            if (syncCommandSet.Any())
            {
                switch (config.DefaultHandlerLifetime)
                {
                    case Lifetime.SingleInstance:
                        builder.RegisterTypes(syncCommandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<>)).AsImplementedInterfaces()
                            .SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        builder.RegisterTypes(syncCommandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<>)).AsImplementedInterfaces()
                            .InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        builder.RegisterTypes(syncCommandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<>)).AsImplementedInterfaces()
                            .InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException();
                    case Lifetime.InstancePerDependency:
                        builder.RegisterTypes(syncCommandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<>)).AsImplementedInterfaces()
                            .InstancePerDependency();
                        break;
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            if (syncCommandResultSet.Any())
            {
                switch (config.DefaultHandlerLifetime)
                {
                    case Lifetime.SingleInstance:
                        builder.RegisterTypes(syncCommandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>)).AsImplementedInterfaces()
                            .SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        builder.RegisterTypes(syncCommandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>)).AsImplementedInterfaces()
                            .InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        builder.RegisterTypes(syncCommandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>)).AsImplementedInterfaces()
                            .InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException();
                    case Lifetime.InstancePerDependency:
                        builder.RegisterTypes(syncCommandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>)).AsImplementedInterfaces()
                            .InstancePerDependency();
                        break;
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        switch (config.DefaultHandlerFactoryLifetime)
        {
            case Lifetime.SingleInstance:
                builder.RegisterType<CommandHandlerFactory>().As<ICommandHandlerFactory>().SingleInstance();
                break;
            case Lifetime.InstancePerRequest:
                builder.RegisterType<CommandHandlerFactory>().As<ICommandHandlerFactory>().InstancePerRequest();
                break;
            case Lifetime.InstancePerLifetimeScope:
                builder.RegisterType<CommandHandlerFactory>().As<ICommandHandlerFactory>().InstancePerLifetimeScope();
                break;
            case Lifetime.InstancePerMatchingLifetimeScope:
                throw new NotSupportedException();
            case Lifetime.InstancePerDependency:
                builder.RegisterType<CommandHandlerFactory>().As<ICommandHandlerFactory>().InstancePerDependency();
                break;
            case Lifetime.InstancePerOwned:
                throw new NotSupportedException();
            default:
                throw new ArgumentOutOfRangeException();
        }

        return builder;
    }

    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="serviceCollection">Current instance of <see cref="IServiceCollection"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection,
        IEnumerable<Type> assembliesContainingTypesToScan, Action<ResultCommanderConfiguration>? options = null)
        => AddResultCommander(serviceCollection, assembliesContainingTypesToScan.Select(x => x.Assembly).Distinct(), options);
        
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="serviceCollection">Current instance of <see cref="IServiceCollection"/>.</param>
    /// <param name="assembliesToScaAn">Assemblies to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, IEnumerable<Assembly> assembliesToScaAn, Action<ResultCommanderConfiguration>? options = null)
    {
        var config = new ResultCommanderConfiguration(serviceCollection);
        options?.Invoke(config);

        var iopt = Options.Create(config);
        serviceCollection.AddSingleton(iopt);
        serviceCollection.AddSingleton(x =>
            x.GetRequiredService<IOptions<ResultCommanderConfiguration>>().Value);

        foreach (var assembly in assembliesToScaAn)
        {
            var commandSet = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(y =>
                                y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IAsyncCommandHandler<>)) &&
                            x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var commandResultSet = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(y =>
                                y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IAsyncCommandHandler<,>)) &&
                            x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var commandSubSet = commandSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var commandResultSubSet = commandResultSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var syncCommandSet = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(y =>
                                y.IsGenericType && y.GetGenericTypeDefinition() == typeof(ISyncCommandHandler<>)) &&
                            x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var syncCommandResultSet = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(y =>
                                y.IsGenericType && y.GetGenericTypeDefinition() == typeof(ISyncCommandHandler<,>)) &&
                            x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var syncCommandSubSet = commandSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            var syncCommandResultSubSet = commandResultSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract && x.GetCustomAttribute<SkipHandlerRegistrationAttribute>() is null)
                .ToList();

            foreach (var type in commandSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var closedGenericTypes = type.GetInterfaces().ToList();

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, type));
                        break;
                    case Lifetime.InstancePerRequest:
                        closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));;
                        break;
                    case Lifetime.InstancePerDependency:
                        closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, type));
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException("Supported only when using Autofac.");
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException("Supported only when using Autofac.");
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            foreach (var type in commandResultSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                var closedGenericTypes = type.GetInterfaces().ToList();

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, type));
                        break;
                    case Lifetime.InstancePerRequest:
                        closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                        break;
                    case Lifetime.InstancePerDependency:
                        closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, type));
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException("Supported only when using Autofac.");
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException("Supported only when using Autofac.");
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            commandSet.RemoveAll(x => commandSubSet.Any(y => y == x));
            commandResultSet.RemoveAll(x => commandResultSubSet.Any(y => y == x));

            if (commandSet.Any())
            {
                foreach (var command in commandSet)
                {
                    var closedGenericTypes = command.GetInterfaces().ToList();

                    switch (config.DefaultHandlerLifetime)
                    {
                        case Lifetime.SingleInstance:
                            closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, command));
                            break;
                        case Lifetime.InstancePerRequest:
                            closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, command));
                            break;
                        case Lifetime.InstancePerLifetimeScope:
                            closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, command));
                            break;
                        case Lifetime.InstancePerMatchingLifetimeScope:
                            throw new NotSupportedException("Supported only when using Autofac.");
                        case Lifetime.InstancePerOwned:
                            throw new NotSupportedException("Supported only when using Autofac.");
                        case Lifetime.InstancePerDependency:
                            closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, command));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            if (commandResultSet.Any())
            {
                foreach (var command in commandResultSet)
                {
                    var closedGenericTypes = command.GetInterfaces().ToList();

                    switch (config.DefaultHandlerLifetime)
                    {
                        case Lifetime.SingleInstance:
                            closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, command));
                            break;
                        case Lifetime.InstancePerRequest:
                            closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, command));
                            break;
                        case Lifetime.InstancePerLifetimeScope:
                            closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, command));
                            break;
                        case Lifetime.InstancePerMatchingLifetimeScope:
                            throw new NotSupportedException("Supported only when using Autofac.");
                        case Lifetime.InstancePerOwned:
                            throw new NotSupportedException("Supported only when using Autofac.");
                        case Lifetime.InstancePerDependency:
                            closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, command));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            foreach (var type in syncCommandSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var closedGenericTypes = type.GetInterfaces().ToList();

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, type));
                        break;
                    case Lifetime.InstancePerRequest:
                        closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                        break;
                    case Lifetime.InstancePerDependency:
                        closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, type));
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException("Supported only when using Autofac.");
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException("Supported only when using Autofac.");
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            foreach (var type in syncCommandResultSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                var closedGenericTypes = type.GetInterfaces().ToList();

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, type));
                        break;
                    case Lifetime.InstancePerRequest:
                        closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                        break;
                    case Lifetime.InstancePerDependency:
                        closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, type));
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException("Supported only when using Autofac.");
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException("Supported only when using Autofac.");
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            commandSet.RemoveAll(x => commandSubSet.Any(y => y == x));
            commandResultSet.RemoveAll(x => commandResultSubSet.Any(y => y == x));

            if (syncCommandSet.Any())
            {
                foreach (var command in syncCommandSet)
                {
                    var closedGenericTypes = command.GetInterfaces().ToList();

                    switch (config.DefaultHandlerLifetime)
                    {
                        case Lifetime.SingleInstance:
                            closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, command));
                            break;
                        case Lifetime.InstancePerRequest:
                            closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, command));
                            break;
                        case Lifetime.InstancePerLifetimeScope:
                            closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, command));
                            break;
                        case Lifetime.InstancePerMatchingLifetimeScope:
                            throw new NotSupportedException("Supported only when using Autofac.");
                        case Lifetime.InstancePerOwned:
                            throw new NotSupportedException("Supported only when using Autofac.");
                        case Lifetime.InstancePerDependency:
                            closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, command));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            if (syncCommandResultSet.Any())
            {
                foreach (var command in syncCommandResultSet)
                {
                    var closedGenericTypes = command.GetInterfaces().ToList();

                    switch (config.DefaultHandlerLifetime)
                    {
                        case Lifetime.SingleInstance:
                            closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, command));
                            break;
                        case Lifetime.InstancePerRequest:
                            closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, command));
                            break;
                        case Lifetime.InstancePerLifetimeScope:
                            closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, command));
                            break;
                        case Lifetime.InstancePerMatchingLifetimeScope:
                            throw new NotSupportedException("Supported only when using Autofac.");
                        case Lifetime.InstancePerOwned:
                            throw new NotSupportedException("Supported only when using Autofac.");
                        case Lifetime.InstancePerDependency:
                            closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, command));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
        
        return serviceCollection;
    }
}
