using ornek.Controllers;

public class DailyMeasurementCheckService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DailyMeasurementCheckService> _logger;

    public DailyMeasurementCheckService(IServiceProvider services, ILogger<DailyMeasurementCheckService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).AddMinutes(-1); // Run at 23:59
            var delay = nextRun - now;

            if (delay.TotalMilliseconds > 0)
            {
                await Task.Delay(delay, stoppingToken);
            }

            using (var scope = _services.CreateScope())
            {
                var controller = scope.ServiceProvider.GetRequiredService<PatientController>();
                controller.GetType().GetMethod("CheckDailyMeasurements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(controller, null);
            }
        }
    }
}