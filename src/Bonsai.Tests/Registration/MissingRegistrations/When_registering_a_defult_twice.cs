namespace Bonsai.Tests.Registration.MissingRegistrations
{
    using System;
    using Exceptions;
    using LifeStyles;
    using Machine.Specifications;
    using PowerAssert;
    using Registry;
    using TestModels;
    using TestModels.Service1;

    [Ignore("wip")]
    [Subject("Registration")]
    public class When_registering_a_defult_twice 
    {
        Establish context = () => {
            _subject = new ContainerBuilder();
            _subject.SetupModules(new RegisterContracts());
        };

        Because of = () => _exception = Catch.Exception(()=> _subject.Create());

        It should_throw_an_exception = () => PAssert.IsTrue(() => _exception is DuplicateNamedContractException);
        
        static ContainerBuilder _subject;
        static Exception _exception;
        
        class RegisterContracts : IModule
        {
            public void Setup(ContainerBuilder builder)
            {
                builder.Register<ClassMonitor>().As<ClassMonitor>().Scoped<Transient>();
                builder.Register<ServiceWithCtor>().As<IService>("default").Scoped<Singleton>();
                builder.Register<ServiceWithCtorAndDisposable>().As<IService>("default").Scoped<Singleton>();
            }
        }
    }
}