﻿namespace Bonsai
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// this is a scope, of which you can resolve objects at.
    /// </summary>
    public interface IScope : IContainer
    {
        /// <summary>
        /// resolves the service into an instance of the target type
        /// </summary>
        /// <param name="serviceName">the name of the service</param>
        /// <param name="service">the required service</param>
        /// <returns>instance of the service</returns>
        object Resolve(Type service, string serviceName = "default");

        /// <summary>
        /// resolves the service into an instance of the target type
        /// </summary>
        /// <typeparam name="TService">the required service</typeparam>
        /// <param name="serviceName">the name of the service</param>
        /// <returns>instance of the service</returns>
        TService Resolve<TService>(string serviceName = "default");

    }


    public static class ScopeExtensions
    {
        /// <summary>
        /// resolves the service into all instances of the target type
        /// </summary>
        /// <typeparam name="TService">the required service</typeparam>
        /// <returns>instance of the service</returns>
        public static IEnumerable<TService> ResolveAll<TService>(this IScope scope)
        {
            return scope.Resolve<IEnumerable<TService>>();
        }
    }  
}