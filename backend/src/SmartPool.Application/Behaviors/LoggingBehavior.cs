using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SmartPool.Application.Behaviors
{
    /// <summary>
    /// Pipeline behavior: log thời gian xử lý mỗi Command/Query.
    /// Tự động áp dụng cho mọi Handler — không cần đăng ký thủ công.
    /// </summary>
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogInformation("[START] {Request}", requestName);

            var sw = Stopwatch.StartNew();
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[SLOW] {Request} took {Elapsed}ms", requestName, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[END] {Request} ({Elapsed}ms)", requestName, sw.ElapsedMilliseconds);

            return response;
        }
    }
}
