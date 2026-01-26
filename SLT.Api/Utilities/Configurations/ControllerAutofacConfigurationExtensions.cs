using Autofac;
using static Utilities.Constants.RegisterMode;
using System.Reflection;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._User;
using Utilities.MongoDatabase.Contracts;


namespace SLT.Api.Utilities.Configurations
{
    public static class ControllerAutofacConfigurationExtensions
    {
        public static void AddControllerServices(this ContainerBuilder containerBuilder)
        {
            var assembliesToRegister = new Assembly[]
            {
                typeof(IUserRepository).Assembly,
                typeof(IUserService).Assembly,
            };

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
                .AssignableTo<IScopedDependency>()
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
                .AssignableTo<ITransientDependency>()
                .AsImplementedInterfaces()
                .InstancePerDependency();

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
                .AssignableTo<ISingletonDependency>()
                .AsImplementedInterfaces()
                .SingleInstance();

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
               .AssignableTo<ISelfSingletonDependency>()
               .AsSelf()
               .SingleInstance();

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
              .AssignableTo<IHostedDependency>()
              .As<IHostedService>()
              .SingleInstance();
        }

    }
}
