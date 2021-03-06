namespace Bonsai.Tests.Resolving.Generics
{
    using LifeStyles;
    using Machine.Specifications;
    using PowerAssert;
    using Registry;
    using TestModels;
    using TestModels.DataStore;

    [Ignore("not supported")]
    [Subject("Container")]
    public class When_resolving_a_generic_service 
    {
        Establish context = () =>
        {
            var builder = new ContainerBuilder();
            builder.SetupModules(new RegisterContracts());
            var container = builder.Create();
            _subject = container.CreateScope();
        };

        Because of = () => _service = _subject.Resolve<IDataStore<User>>();

        It should_provided_an_instance_of_the_service =
            () => PAssert.IsTrue(() => _service is DataStorePlain<User>);

        static IScope _subject;
        static IDataStore<User> _service;

        class RegisterContracts : IModule
        {
            public void Setup(ContainerBuilder builder)
            {
                builder.Register(typeof(DataStorePlain<>))
                    .As(typeof(IDataStore<>))
                    .As<IDataStore<User>>()
                    .Scoped<Transient>();
            }
        }
    }
}