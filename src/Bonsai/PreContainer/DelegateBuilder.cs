namespace Bonsai.PreContainer
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Contracts;
    using FastExpressionCompiler;
    using Internal;
    using Registry;

    public class DelegateBuilder
    {
        public IEnumerable<Contract> Create(IEnumerable<RegistrationContext> contexts)
        {
            var contracts = new List<Contract>();
            foreach (var context in contexts)
            {
                var contract = new Contract()
                {
                    Id = context.Id,
                    LifeSpan = context.Registration.ScopedTo,
                    ServiceKeys = context.Keys,
                    CreateInstance = context.Registration.CreateInstance,
                    Instance = context.Registration.Instance
                };

                if (typeof(IDisposable).IsAssignableFrom(context.ImplementedType))
                {
                    contract.DisposeInstance = Disposal;
                }
                else
                {
                    contract.DisposeInstance = NoOpDisposal;
                }

                contracts.Add( contract);
            }


            foreach (var context in contexts)
            {
                var contract = contracts.First(x => x.Id == context.Id);
                contract.CreateInstance = Create(context, contracts);
            }

            return contracts;
        }

        void NoOpDisposal(object instance)
        {
        }

        void Disposal(object instance)
        {
            ((IDisposable) instance).Dispose();
        }


        CreateInstance Create(RegistrationContext context, IEnumerable<Contract> contracts)
        {
            if (context.Registration.Instance != null) return null;
            if (context.Registration.CreateInstance != null) return context.Registration.CreateInstance;

            var ctor = context.InjectOnMethods.First(x => x.InjectOn == InjectOn.Constructor);
            //var parentContract = contracts.First(x => x.ServiceKeys.Contains(context.Keys.First()));

            List<Expression> createParams = new List<Expression>();

            var parameters = ctor.Parameters;

            var scopeParam = Expression.Parameter(typeof(IAdvancedScope));
            //var parentContractParam = Expression.Constant(parentContract); 
            var parentContractParam = Expression.Parameter(typeof(Contract));
            
            var resolve = typeof(IAdvancedScope).GetMethod("Resolve", new Type[]
            {
                typeof(Contract),
                typeof(Contract)
            });
            foreach (var param in parameters)
            {
                var p = param;


                if (p.Value != null)
                {
                    var provided = Expression.Constant(p.Value);
                    var cast = Expression.Convert(provided, p.Value.GetType());
                    createParams.Add(cast);
                }


                if (p.CreateInstance != null)
                {
                    
                    Expression c = Expression.Constant(null);
                    var provided = Expression.Constant(p.CreateInstance);
                    var suppliedDelegate = Expression.Invoke(provided,
                        scopeParam,
                        Expression.Convert(c, typeof(Contract)),
                        parentContractParam);
                    var con = Expression.Convert(suppliedDelegate, p.ProvidedType);
                
                    createParams.Add(con);
                    continue;
                }
                
                var contract = contracts.First(x => x.ServiceKeys.Contains(p.ServiceKey));
                Expression constantExpr = Expression.Constant(contract);
                MethodCallExpression resolveParam = Expression.Call(
                    scopeParam,
                    resolve,
                    constantExpr,
                    parentContractParam
                );

                var convert = Expression.Convert(resolveParam, p.ServiceKey.Service);                
                createParams.Add(convert);
            }
            
            var method = (ConstructorInfo) ctor.Method;
            var newExpression =
                Expression.New(method, createParams);
            
            var compiledCtor = Expression.Lambda<Func<IAdvancedScope, Contract, object>>(
                newExpression, 
                scopeParam, 
                parentContractParam)
                .CompileFast();

            object ParameterLessCtor(IAdvancedScope scope, Contract ct, Contract pct) => compiledCtor(scope, ct);
            return ParameterLessCtor;
        }
        
    }
}