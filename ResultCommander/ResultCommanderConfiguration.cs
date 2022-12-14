using Autofac;
using Autofac.Features.Decorators;
using Microsoft.Extensions.DependencyInjection;
using MikyM.Utilities.Extensions;
using ServiceLifetime = AttributeBasedRegistration.ServiceLifetime;

namespace ResultCommander;

/// <summary>
/// Command handler options.
/// </summary>
[PublicAPI]
public sealed class ResultCommanderConfiguration
{
    internal bool UsingAutofac => Builder is not null;
    
    internal ResultCommanderConfiguration(ContainerBuilder builder)
    {
        Builder = builder;
    }
    
    internal ResultCommanderConfiguration(IServiceCollection serviceCollection)
    {
        ServiceCollection = serviceCollection;
    }

    internal ContainerBuilder? Builder { get; set; }
    internal IServiceCollection? ServiceCollection { get; set; }

    /// <summary>
    /// Gets or sets the default lifetime of command handlers.
    /// </summary>
    public ServiceLifetime DefaultHandlerLifetime { get; set; } = ServiceLifetime.InstancePerLifetimeScope;
    
    /// <summary>
    /// Gets or sets the default lifetime of <see cref="ICommandHandlerResolver"/>.
    /// </summary>
    public ServiceLifetime DefaultHandlerResolverLifetime { get; set; } = ServiceLifetime.InstancePerLifetimeScope;

    /// <summary>
    /// Registers a decorator for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="condition">Condition to decide whether the decorator should be applied.</param>
    /// <returns>Current <see cref="ResultCommanderConfiguration"/> instance.</returns>
    public ResultCommanderConfiguration AddDecorator<TDecorator>(Func<IDecoratorContext, bool>? condition = null) where TDecorator : ICommandHandlerBase
    {
        if (Builder is null)
            throw new NotSupportedException(
                "Decorators aren't supported when using base Microsoft DI, you must use Autofac instead.");
        
        if (typeof(TDecorator).IsGenericType && typeof(TDecorator).IsGenericTypeDefinition)
            throw new NotSupportedException("Given decorator type is an open generic type, use AddGenericDecorator method instead");
        
        if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)))
            Builder.RegisterDecorator(typeof(TDecorator), typeof(IAsyncCommandHandler<>), condition);
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)))
            Builder.RegisterDecorator(typeof(TDecorator), typeof(IAsyncCommandHandler<,>), condition);
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)))
            Builder.RegisterDecorator(typeof(TDecorator), typeof(ISyncCommandHandler<>), condition);
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)))
            Builder.RegisterDecorator(typeof(TDecorator), typeof(ISyncCommandHandler<,>), condition);
        else
            throw new NotSupportedException("Given decorator type can't decorate any command handler");

        return this;
    }
    
    /// <summary>
    /// Registers a decorator for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="condition">Condition to decide whether the decorator should be applied.</param>
    /// <returns>Current <see cref="ResultCommanderConfiguration"/> instance.</returns>
    public ResultCommanderConfiguration AddDecorator<TDecorator, TDecoratedHandler>(Func<IDecoratorContext, bool>? condition = null) where TDecorator : TDecoratedHandler where TDecoratedHandler : ICommandHandlerBase
    {
        if (Builder is null)
            throw new NotSupportedException(
                "Decorators aren't supported when using base Microsoft DI, you must use Autofac instead.");
        
        if (typeof(TDecorator).IsGenericType && typeof(TDecorator).IsGenericTypeDefinition)
            throw new NotSupportedException("Given decorator type is an open generic type, use AddGenericDecorator method instead");
        if (typeof(TDecoratedHandler).IsGenericType && typeof(TDecoratedHandler).IsGenericTypeDefinition)
            throw new NotSupportedException("You can't use open generics with this method, use AddGenericDecorator instead");
        
        Builder.RegisterDecorator<TDecorator, TDecoratedHandler>(condition);

        return this;
    }

    /// <summary>
    /// Registers an open generic decorator for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="decoratorType">Decorator type.</param>
    /// <param name="condition">Condition to decide whether the decorator should be applied.</param>
    /// <returns>Current <see cref="ResultCommanderConfiguration"/> instance</returns>
    public ResultCommanderConfiguration AddGenericDecorator(Type decoratorType, Func<IDecoratorContext, bool>? condition = null)
    {
        if (Builder is null)
            throw new NotSupportedException(
                "Decorators aren't supported when using base Microsoft DI, you must use Autofac instead.");
        
        if (!decoratorType.IsGenericType || !decoratorType.IsGenericTypeDefinition)
            throw new NotSupportedException("Given decorator type is not a generic type");
        
        if (decoratorType.IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)))
            Builder.RegisterGenericDecorator(decoratorType, typeof(IAsyncCommandHandler<>), condition);
        else if (decoratorType.IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)))
            Builder.RegisterGenericDecorator(decoratorType, typeof(IAsyncCommandHandler<,>), condition);
        else if (decoratorType.IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)))
            Builder.RegisterGenericDecorator(decoratorType, typeof(ISyncCommandHandler<>), condition);
        else if (decoratorType.IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)))
            Builder.RegisterGenericDecorator(decoratorType, typeof(ISyncCommandHandler<,>), condition);
        else
            throw new NotSupportedException("Given decorator type can't decorate any command handler");

        return this;
    }
    
    /// <summary>
    /// Registers an open generic decorator for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="decoratorType">Decorator type.</param>
    /// <param name="condition">Condition to decide whether the decorator should be applied.</param>
    /// <returns>Current <see cref="ResultCommanderConfiguration"/> instance.</returns>
    public ResultCommanderConfiguration AddGenericDecorator<TDecoratedHandler>(Type decoratorType, Func<IDecoratorContext, bool>? condition = null) where TDecoratedHandler : ICommandHandlerBase
    {
        if (Builder is null)
            throw new NotSupportedException(
                "Decorators aren't supported when using base Microsoft DI, you must use Autofac instead.");
        
        if (!decoratorType.IsGenericType || !decoratorType.IsGenericTypeDefinition)
            throw new NotSupportedException("Given decorator type is not a generic type");

        if (decoratorType.IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)) &&
            typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            Builder.RegisterGenericDecorator(decoratorType, typeof(IAsyncCommandHandler<>), predicate);
        }
        else if (decoratorType.IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)) &&
                typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            Builder.RegisterGenericDecorator(decoratorType, typeof(IAsyncCommandHandler<,>), predicate);
        }
        else if (decoratorType.IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)) &&
            typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            Builder.RegisterGenericDecorator(decoratorType, typeof(ISyncCommandHandler<>), predicate);
        }
        else if (decoratorType.IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)) &&
                 typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            Builder.RegisterGenericDecorator(decoratorType, typeof(ISyncCommandHandler<,>), predicate);
        }
        else
            throw new NotSupportedException("Given decorator type can't decorate given command handler");

        return this;
    }

    /// <summary>
    /// Registers an adapter for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="adapter">Func that used to adapt service to another.</param>
    /// <returns>Current <see cref="ResultCommanderConfiguration"/> instance.</returns>
    public ResultCommanderConfiguration AddAdapter<TAdapter, THandler>(Func<THandler, TAdapter> adapter)
        where THandler : class, ICommandHandlerBase where TAdapter : notnull
    {
        if (Builder is null)
            throw new NotSupportedException(
                "Adapters aren't supported when using base Microsoft DI, you must use Autofac instead.");
        
        Builder.RegisterAdapter(adapter);
        return this;
    }
}
