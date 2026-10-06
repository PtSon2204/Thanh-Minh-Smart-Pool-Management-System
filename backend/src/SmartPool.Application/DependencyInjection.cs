using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartPool.Application.Behaviors;
using System.Reflection;

namespace SmartPool.Application
{
    /// <summary>
    /// Extension method đăng ký toàn bộ services của tầng Application.
    /// Mỗi thành viên thêm service của mình vào đây, KHÔNG sửa Program.cs.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var assembly = Assembly.GetExecutingAssembly();

            services.AddSingleton<TimeProvider>(TimeProvider.System);

            // MediatR — tự scan toàn bộ Handlers trong Assembly này
            // Mỗi thành viên chỉ cần tạo Handler class, không cần đăng ký thủ công
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(assembly);

                // Pipeline behaviors — chạy theo thứ tự cho mọi Command/Query
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            });

            // FluentValidation — tự scan toàn bộ Validators trong Assembly này
            services.AddValidatorsFromAssembly(assembly);

            // AutoMapper — scan Profiles trong Assembly này
            // Mỗi thành viên chỉ cần tạo class kế thừa Profile
            services.AddAutoMapper(cfg => { /* global config nếu cần */ }, assembly);

            // Register IMemoryCache
            services.AddMemoryCache();

            return services;
        }
    }
}
