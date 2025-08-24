using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CaptureTool.Common.Sync;

/// <summary>
/// Provides helper methods for synchronizing and setting property values 
/// across collections of objects.
/// </summary>
/// <remarks>
/// <para>
/// These methods use expression trees to generate strongly-typed getters and setters
/// for the specified property. This allows properties to be referenced in a safe,
/// refactor-friendly way without relying on reflection or string names.
/// </para>
/// <para>
/// Both <see cref="SyncProperty{T,TProp}(T, IEnumerable{T}, Expression{Func{T,TProp}})"/> 
/// and <see cref="SetProperty{T,TProp}(T, IEnumerable{T}, Expression{Func{T,TProp}}, TProp)"/> 
/// will skip updating the source object.
/// </para>
/// </remarks>
/// <example>
/// Example usage:
/// <code>
/// // Copy property from source to all others
/// SyncHelper.SyncProperty(source, items, x => x.Mode);
///
/// // Force set property on all others
/// SyncHelper.SetProperty(source, items, x => x.Mode, CaptureMode.Region);
/// </code>
/// </example>
public static class SyncHelper
{
    /// <summary>
    /// Synchronizes the value of a specified property from the source object
    /// to all other items in the target collection.
    /// </summary>
    /// <typeparam name="T">The type of objects in the collection.</typeparam>
    /// <typeparam name="TProp">The type of the property being synchronized.</typeparam>
    /// <param name="source">The object providing the property value to copy.</param>
    /// <param name="targets">The collection of objects to update.</param>
    /// <param name="propertySelector">
    /// An expression selecting the property to synchronize. 
    /// Must be a simple property access (e.g., <c>x => x.Property</c>).
    /// </param>
    /// <remarks>
    /// The <paramref name="source"/> object itself is skipped during updates.
    /// </remarks>
    public static void SyncProperty<T, TProp>(T source, IEnumerable<T> targets, Expression<Func<T, TProp>> propertySelector)
    {
        if (propertySelector.Body is not MemberExpression memberExpr)
        {
            throw new ArgumentException("Property selector must be a member expression.", nameof(propertySelector));
        }

        var getter = propertySelector.Compile();
        var paramTarget = Expression.Parameter(typeof(T), "target");
        var paramValue = Expression.Parameter(typeof(TProp), "value");
        var assignment = Expression.Lambda<Action<T, TProp>>(
            Expression.Assign(
                Expression.MakeMemberAccess(paramTarget, memberExpr.Member),
                paramValue),
            paramTarget,
            paramValue);
        var setter = assignment.Compile();
        
        foreach (var target in targets)
        {
            if (ReferenceEquals(target, source))
                continue;

            setter(target, getter(source));
        }
    }

    ///<summary>
    /// Sets the value of a specified property on all items in the target collection,
    /// excluding the provided source object.
    /// </summary>
    /// <typeparam name="T">The type of objects in the collection.</typeparam>
    /// <typeparam name="TProp">The type of the property being set.</typeparam>
    /// <param name="source">The object to exclude from updates.</param>
    /// <param name="targets">The collection of objects to update.</param>
    /// <param name="propertySelector">
    /// An expression selecting the property to set. 
    /// Must be a simple property access (e.g., <c>x => x.Property</c>).
    /// </param>
    /// <param name="value">The value to assign to the property.</param>
    public static void SetProperty<T, TProp>(T source, IEnumerable<T> targets, Expression<Func<T, TProp>> propertySelector, TProp value)
    {
        if (propertySelector.Body is not MemberExpression memberExpr)
        {
            throw new ArgumentException("Property selector must be a member expression.", nameof(propertySelector));
        }

        var paramTarget = Expression.Parameter(typeof(T), "target");
        var paramValue = Expression.Parameter(typeof(TProp), "value");
        var assignment = Expression.Lambda<Action<T, TProp>>(
            Expression.Assign(
                Expression.MakeMemberAccess(paramTarget, memberExpr.Member),
                paramValue),
            paramTarget,
            paramValue);
        var setter = assignment.Compile();

        foreach (var target in targets)
        {
            if (ReferenceEquals(target, source))
                continue;

            setter(target, value);
        }
    }
}