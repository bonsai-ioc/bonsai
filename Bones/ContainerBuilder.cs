﻿namespace Bones
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class ContainerBuilder
    {
        private readonly List<Registration> _registrations = new List<Registration>();

        public void SetupModules(params IModule[] modules)
        {
            foreach (var module in modules)
            {
                module.Setup(this);
            }
        }

        public IContainer Create()
        {
            //find known dependencies
            //create contracts for all contracts
            //allow for any modifications
            //build delegates
            //build contract registry
            //build initial scope and return as container.

            var registrationRegistry = new RegistrationRegistry(_registrations);
            new InjectionPlanner(registrationRegistry).Plan();
            var registrationContexts = new RegistrationScanner(registrationRegistry).Scan();
            var contexts = new DelegateBuilder().Create(registrationContexts);
            var contractRegistry = new ContractRegistry(contexts);

            return new Scope(contractRegistry, null,"singleton");
        }

        public void RegisterContract(Registration registration)
        {
            _registrations.Add(registration);
        }
    }


    public class Contract
    {
        public CreateInstance CreateInstance { get; set; }

        public HashSet<ServiceKey> ServiceKeys { get; set; }

        public ILifeSpan LifeSpan { get; set; }
    }


    public class DelegateBuilder
    {
        public IEnumerable<Contract> Create(IEnumerable<RegistrationContext> contexts)
        {
            foreach (var context in contexts)
            {
                yield return new Contract()
                {
                    LifeSpan = context.Registration.ScopedTo,
                    ServiceKeys = context.Registration.Types,
                    CreateInstance = Create(context)
                };
            }
        }


        CreateInstance Create(RegistrationContext context)
        {
            var ctor = context.InjectOnMethods.First(x => x.InjectOn == InjectOn.Constructor);

            List<Func<Scope, object>> createParams = new List<Func<Scope, object>>();

            var parameters = ctor.Parameters;

            foreach (var param in parameters)
            {
                var p = param;
                object CreateParam(Scope scope) => scope.Resolve(p.ServiceKey);
                createParams.Add(CreateParam);
            }

            var method = (ConstructorInfo) ctor.Method;
            
            object ParameterLessCtor(Scope scope) => method.Invoke(createParams.Select(x => x(scope)).ToArray());
            return ParameterLessCtor;
        }
    }

    public class RegistrationContext
    {
        public List<MethodInformation> InjectOnMethods { get; set; } = new List<MethodInformation>();
        public Registration Registration { get; set; }

        public ServiceKey ImplementedKey { get; set; }

        public Type ImplementedType { get; set; }

        public HashSet<ServiceKey> Keys { get; set; } = new HashSet<ServiceKey>();
    }

    public class MethodInformation
    {
        public InjectOn InjectOn { get; set; }

        public string Name { get; set; }

        public List<ParameterInformation> Parameters { get; set; } = new List<ParameterInformation>();

        /// <summary>
        /// reference to the method (setter method)
        /// </summary>
        public MethodBase Method { get; set; }
    }

    public class ParameterInformation
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public ServiceKey ServiceKey { get; set; }
        public ServiceKey GenericServiceKey { get; set; }
    }


    /// <summary>
    /// these are the different way to inject into an object
    /// </summary>
    public enum InjectOn
    {
        Constructor,
        Property,
        Method
    }


    public delegate object CreateInstance(Scope currentScope);

    public class InjectionPlanner
    {
        private readonly RegistrationRegistry _registrations;
        private ConstructorPlanner _constructorPlanner;

        public InjectionPlanner(RegistrationRegistry registrations)
        {
            _registrations = registrations;
            _constructorPlanner = new ConstructorPlanner(registrations);
        }

        public void Plan()
        {
            //var r = registrations.ToList();
            foreach (var registration in _registrations)
            {
                _constructorPlanner.Plan(registration);
            }
        }

        public class ConstructorPlanner
        {
            private readonly RegistrationRegistry _registrations;

            public ConstructorPlanner(RegistrationRegistry registrations)
            {
                _registrations = registrations;
            }

            public void Plan(Registration registration)
            {
                if (registration.Constructor != null || registration.Instance != null)
                {
                    return;
                }

                var constructors =
                    registration.ImplementedType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                registration.Constructor = constructors
                    .Select(x => new {Score = Score(x, registration), Constructor = x})
                    .Where(x => x.Score >= 0)
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Constructor)
                    .FirstOrDefault();
            }

            int Score(MethodBase method, Registration registration)
            {
                var parameters = method.GetParameters();

                var plannedMethodParameters =
                    registration
                        .Dependencies
                        .Where(dependency => dependency.Named == method.Name)
                        .ToList();

                int extraPoints = 0;

                var count = parameters.Count(parameter =>
                {
                    //see if it is a dependency which the user has provided
                    var planned = plannedMethodParameters.FirstOrDefault(x => x.ParameterName == parameter.Name);
                    Func<bool> containsPlannedType = () =>
                    {
                        if (planned == null) return false;
                        var name = planned.Named ?? "default";
                        var type = planned.RequiredType;
                        var key = new ServiceKey(type, name);

                        return _registrations.BySupportingType(key) != null;
                    };


                    if (planned?.Value != null || containsPlannedType())
                    {
                        extraPoints += 5;
                        return true;
                    }

                    return _registrations.Contains(new ServiceKey(parameter.ParameterType));
                });

                if (count != parameters.Count())
                {
                    //not supported.
                    return -1;
                }

                return count + extraPoints;
            }
        }
    }
    
    
    
    public class RegistrationScanner
    {
        private readonly RegistrationRegistry _registrations;
        private readonly Dictionary<string, RegistrationContext> _contexts = new Dictionary<string, RegistrationContext>();

        public RegistrationScanner(RegistrationRegistry registrations)
        {
            _registrations = registrations;
        }

        public IEnumerable<RegistrationContext> Scan()
        {
            foreach (var registration in _registrations)
            {
                GetServiceKeys(registration);
            }

            return _contexts.Values;
        }


        IEnumerable<ServiceKey> GetAllServiceKeys(Registration registration)
        {
            var isGenericDescription = registration.ImplementedType.IsGenericType
                                       && registration.ImplementedType.GenericTypeArguments.Any();
            var hasAnInstance = registration.Instance != null;

            if (isGenericDescription || hasAnInstance)
            {
                yield break;
            }


            //only interested in non open generics and non generics.
            Func<Type, bool> filter = type => !type.IsGenericType || type.GenericTypeArguments.Any();

            foreach (var registrationType in registration.Types.Where(x => filter(x.Service)))
            {
                yield return registrationType;
            }


            // foreach (var serviceKey in GetServiceKeys(registration)) yield return serviceKey;
        }

        private void GetServiceKeys(
            Registration registration,
            Type registrationType = null)
        {
            string hash;
            hash = registrationType?.IsGenericType == true
                ? $"{registration.Id} {registration.ImplementedType.MakeGenericType(registrationType.GenericTypeArguments)}"
                : $"{registration.Id} {registration.ImplementedType.FullName}";

//            var gotContextForRegistrationType = registrationType != null
//                                                && registrationType.IsGenericType
//                                                && _contexts.ContainsKey(hash);
//
//            var gotContextForRegistration = !registration.ImplementedType.IsGenericType
//                                            && _contexts.ContainsKey(hash);

            //already processed
            var haveRegistration = _contexts.ContainsKey(hash);
            if (haveRegistration)
            {
                return;
            }
            
            RegistrationContext context = new RegistrationContext();
            var constructor = new MethodInformation()
            {
                InjectOn = InjectOn.Constructor,
                Name = "ctor"
            };

            context.Registration = registration;
            context.InjectOnMethods.Add(constructor);

            _contexts.Add(hash, context);
            //ConstructorInfo ctorInfo;

            //get the actual constructor
            if (registrationType != null && registrationType.IsGenericType)
            {
                throw new NotImplementedException();
                //constructor.Method = (ConstructorInfo) registration.Constructor;
            }
            else
            {
                constructor.Method = (ConstructorInfo) registration.Constructor;
                //ctorInfo = ;
            }


            var parameters = constructor.Method.GetParameters();
            foreach (var parameter in parameters)
            {
                var dependency =
                    registration.Dependencies.FirstOrDefault(x =>
                        x.ParameterName == parameter.Name
                        && x.InjectOn == InjectOn.Constructor);

                if (dependency?.Value != null)
                {
                    constructor.Parameters.Add(new ParameterInformation()
                    {
                        Name = parameter.Name,
                        Value = dependency.Value
                    });
                    continue;
                }

                var type = dependency?.RequiredType ?? parameter.ParameterType;
                var name = dependency?.Named ?? "default";

                var dependencyKey = new ServiceKey(type, name);

                constructor.Parameters.Add(new ParameterInformation()
                {
                    Name = parameter.Name,
                    ServiceKey = dependencyKey
                });

                //recurive search
                var dependencyRegistration = _registrations.BySupportingType(dependencyKey);
                GetServiceKeys(dependencyRegistration, type);
            }
        }
    }


    public class RegistrationRegistry : IEnumerable<Registration>
    {
        private List<Registration> _registrations;


        public RegistrationRegistry(IEnumerable<Registration> registrations)
        {
            _registrations = registrations.ToList();
        }


        public bool Contains(ServiceKey key)
        {
            if (key == null) return false;
            if (_registrations.Any(x => x.Types.Contains(key)))
            {
                return true;
            }

            if (!key.Service.IsGenericType)
            {
                return false;
            }

            var t = key.Service.GetGenericTypeDefinition();
            return _registrations.Any(x =>
                x.Types.Any(supported => supported.Service == t && supported.ServiceName == key.ServiceName));
        }


        public Registration BySupportingType(ServiceKey exposedType)
        {
            return _registrations.FirstOrDefault(x =>
            {
                if (x.Types.Contains(exposedType))
                {
                    return true;
                }


                if (!exposedType.Service.IsGenericType)
                {
                    return false;
                }

                return x.Types.Any(possibleType =>
                    possibleType.Service.GetGenericTypeDefinition() == exposedType.Service.GetGenericTypeDefinition());
            });
        }


        public IEnumerator<Registration> GetEnumerator()
        {
            return _registrations.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    public class ContractRegistry
    {
        private IDictionary<ServiceKey, Contract> _contracts;
        
        public ContractRegistry(IEnumerable<Contract> contracts)
        {
            _contracts = new Dictionary<ServiceKey, Contract>();
            foreach (var contract in contracts)
            {
                foreach (var key in contract.ServiceKeys)
                {
                    _contracts.Add(key, contract);
                }
            }   
        }

        public Contract GetContract(ServiceKey serviceKey)
        {
            return _contracts.TryGetValue(serviceKey, out var entry)
                ? entry
                : null;
        }
    }
}